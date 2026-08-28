using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApp.Models.DocumentSigning;
using WebApp.Models.Integration;
using WebApp.Repositories.DocumentSigning;
using WebApp.ViewModels.DocumentSigning;

namespace WebApp.Services.DocumentSigning;

// Coordinates document signing workflows between portal storage and the Oneflow client.
public class DocumentSigningService : IDocumentSigningService
{
    private readonly OneflowDocumentSigningClient _oneflowClient;
    private readonly IDocumentSigningRepository _repository;
    private readonly DocumentSigningStatusSyncJobScheduler _statusSyncJobScheduler;
    private readonly IOptions<DocumentSigningOptions> _options;
    private readonly ILogger<DocumentSigningService> _logger;

    public DocumentSigningService(
        OneflowDocumentSigningClient oneflowClient,
        IDocumentSigningRepository repository,
        DocumentSigningStatusSyncJobScheduler statusSyncJobScheduler,
        IOptions<DocumentSigningOptions> options,
        ILogger<DocumentSigningService> logger)
    {
        _oneflowClient = oneflowClient;
        _repository = repository;
        _statusSyncJobScheduler = statusSyncJobScheduler;
        _options = options;
        _logger = logger;
    }

    public bool IsEnabledForCompany(Guid companyId)
    {
        return ResolveProviderOptions(companyId)?.IsConfigured() == true;
    }

    public bool CanPingForCompany(Guid companyId)
    {
        return ResolveProviderOptions(companyId)?.CanPing() == true;
    }

    public async Task<IReadOnlyList<DocumentSigningListItem>> ListForOrderAsync(Guid companyId, int? jeevesCompanyCode, long orderNo, CancellationToken cancellationToken = default)
    {
        var signings = await _repository.ListByOrderAsync(companyId, jeevesCompanyCode, orderNo, cancellationToken);
        EnsureBackgroundSyncScheduled(signings);
        return signings.Select(MapDocumentSigningListItem).ToList();
    }

    public async Task<IReadOnlyList<DocumentSigningListItem>> ListRecentAsync(Guid companyId, int? jeevesCompanyCode, int take = 20, CancellationToken cancellationToken = default)
    {
        var signings = await _repository.ListRecentAsync(companyId, jeevesCompanyCode, take, cancellationToken);
        EnsureBackgroundSyncScheduled(signings);
        return signings.Select(MapDocumentSigningListItem).ToList();
    }

    public async Task<DocumentSigningCreateResult> CreateAndStartAsync(DocumentSigningCreateRequest request, CancellationToken cancellationToken = default)
    {
        var companyOptions = ResolveProviderOptions(request.CompanyId);
        if (companyOptions?.IsConfigured() != true)
            throw new InvalidOperationException("Dokumentsignering är inte konfigurerad för det valda bolaget.");

        var participants = ResolveCreateParticipants(request);
        var primaryParticipant = participants[0];
        var correlationKey = ResolveCorrelationKey(request, participants);
        var existing = await _repository.GetByCorrelationKeyAsync(request.CompanyId, correlationKey, cancellationToken);
        if (existing != null)
        {
            _logger.LogInformation(
                "Reused existing document signing {SigningId} for correlation key {CorrelationKey}",
                existing.Id,
                correlationKey);
            return MapCreateResult(existing);
        }

        var signerName = $"{request.SignerFirstName} {request.SignerLastName}".Trim();
        var invitationMessage = string.IsNullOrWhiteSpace(request.InvitationMessage)
            ? companyOptions.DefaultInvitationMessage?.Trim()
            : request.InvitationMessage.Trim();
        var signing = new DocumentSigningRecord
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            JeevesCompanyCode = request.JeevesCompanyCode,
            OrderNo = request.OrderNo,
            OrderCustomerName = Trim(request.OrderCustomerName, 256) ?? string.Empty,
            DocumentTitle = Trim(request.DocumentTitle, 256) ?? string.Empty,
            DocumentId = $"pending-{Guid.NewGuid():N}",
            CorrelationKey = correlationKey,
            SignerName = Trim(primaryParticipant.Name, 256) ?? string.Empty,
            SignerEmail = Trim(primaryParticipant.Email, 256) ?? string.Empty,
            SignerMobile = Trim(primaryParticipant.PhoneNumber, 64),
            MainFileName = Trim(request.MainFile?.FileName, 256) ?? string.Empty,
            AttachmentCount = request.Attachments.Count,
            PublicToken = Guid.NewGuid().ToString("N"),
            CreatedByUserId = Trim(request.CreatedByUserId, 450) ?? string.Empty,
            CreatedByEmail = Trim(request.CreatedByEmail, 256) ?? string.Empty,
            InvitationMessage = Trim(invitationMessage, 4000),
            PortalStatus = "creating",
            ProviderStatus = "draft",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        JsonDocument? latestContract = null;
        IReadOnlyList<DocumentSigningParticipantRecord> latestParticipants = Array.Empty<DocumentSigningParticipantRecord>();

        try
        {
            await _repository.AddAsync(signing, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            var concurrent = await _repository.GetByCorrelationKeyAsync(request.CompanyId, correlationKey, cancellationToken);
            if (concurrent != null)
                return MapCreateResult(concurrent);

            throw;
        }

        try
        {
            request.Participants = participants;
            latestContract = await _oneflowClient.CreateContractFromTemplateAsync(companyOptions, request, cancellationToken);
            latestParticipants = ApplyOneflowContract(signing, latestContract);
            signing.PortalStatus = "preparing";
            await _repository.UpdateAsync(signing, latestParticipants, cancellationToken);

            EnsureExpandedPdfAllowed(latestContract.RootElement, request.MainFile is not null);
            EnsureAttachmentsAllowed(latestContract.RootElement, request.Attachments.Count);

            if (request.MainFile is not null)
            {
                latestContract.Dispose();
                await _oneflowClient.UploadMainFileAsync(companyOptions, signing.DocumentId, request.MainFile, "expanded_pdf", cancellationToken);
                latestContract = await _oneflowClient.GetContractAsync(companyOptions, signing.DocumentId, cancellationToken);
                latestParticipants = ApplyOneflowContract(signing, latestContract);
                await _repository.UpdateAsync(signing, latestParticipants, cancellationToken);
            }

            foreach (var attachment in request.Attachments)
            {
                latestContract.Dispose();
                await _oneflowClient.UploadAttachmentAsync(companyOptions, signing.DocumentId, attachment, cancellationToken);
                latestContract = await _oneflowClient.GetContractAsync(companyOptions, signing.DocumentId, cancellationToken);
                latestParticipants = ApplyOneflowContract(signing, latestContract);
                await _repository.UpdateAsync(signing, latestParticipants, cancellationToken);
            }

            latestContract.Dispose();
            latestContract = await _oneflowClient.PublishContractAsync(
                companyOptions,
                signing.DocumentId,
                request.DocumentTitle,
                invitationMessage,
                cancellationToken);
            latestParticipants = ApplyOneflowContract(signing, latestContract);
            signing.PortalStatus = "sent";
            signing.StartedAtUtc = DateTime.UtcNow;
            signing.UpdatedAtUtc = DateTime.UtcNow;
            await _repository.UpdateAsync(signing, latestParticipants, cancellationToken);
            EnqueueBackgroundSync(signing);

            return MapCreateResult(signing);
        }
        catch (Exception ex)
        {
            signing.PortalStatus = "failed";
            signing.LatestError = Trim(ex.Message, 4000);
            signing.UpdatedAtUtc = DateTime.UtcNow;

            try
            {
                await _repository.UpdateAsync(signing, latestParticipants, cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogWarning(updateEx, "Failed to persist Oneflow document signing failure state for order {OrderNo}", request.OrderNo);
            }

            throw;
        }
        finally
        {
            latestContract?.Dispose();
        }
    }

    public async Task<DocumentSigningListItem?> SyncAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default)
    {
        var signing = await _repository.GetByIdWithParticipantsAsync(companyId, signingId, cancellationToken);
        if (signing == null)
            return null;

        var companyOptions = ResolveProviderOptions(companyId);
        if (companyOptions?.IsConfigured() != true)
            throw new InvalidOperationException("Dokumentsignering är inte konfigurerad för det valda bolaget.");

        await RefreshSigningFromProviderAsync(signing, companyOptions, cancellationToken);
        return MapDocumentSigningListItem(signing);
    }

    public async Task<DocumentSigningLaunchResult?> LaunchAsync(Guid companyId, Guid signingId, CancellationToken cancellationToken = default)
    {
        var signing = await _repository.GetByIdWithParticipantsAsync(companyId, signingId, cancellationToken);
        if (signing == null)
            return null;

        var companyOptions = ResolveProviderOptions(companyId);
        if (companyOptions?.IsConfigured() != true)
            throw new InvalidOperationException("Dokumentsignering är inte konfigurerad för det valda bolaget.");

        await RefreshSigningFromProviderAsync(signing, companyOptions, cancellationToken);

        var participant = ResolveLaunchParticipant(signing);
        if (participant == null)
            throw new InvalidOperationException("Kunde inte hitta en unik extern signerare att skapa access link för.");

        var accessLinkUrl = await _oneflowClient.CreateParticipantAccessLinkAsync(
            companyOptions,
            signing.DocumentId,
            participant.OneflowParticipantId,
            cancellationToken);

        return new DocumentSigningLaunchResult
        {
            SigningId = signing.Id,
            ParticipantId = participant.OneflowParticipantId,
            AccessLinkUrl = accessLinkUrl
        };
    }

    public async Task PingAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var companyOptions = ResolveProviderOptions(companyId);
        if (companyOptions?.CanPing() != true)
            throw new InvalidOperationException("Oneflow-token saknas för det valda bolaget.");

        await _oneflowClient.PingAsync(companyOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentSigningOneflowWorkspaceViewModel>> ListWorkspacesAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var companyOptions = ResolveProviderOptions(companyId);
        if (companyOptions?.CanPing() != true)
            throw new InvalidOperationException("Oneflow-token saknas för det valda bolaget.");

        return await _oneflowClient.ListWorkspacesAsync(companyOptions, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentSigningOneflowTemplateViewModel>> ListTemplatesAsync(Guid companyId, int? workspaceId = null, CancellationToken cancellationToken = default)
    {
        var companyOptions = ResolveProviderOptions(companyId);
        if (companyOptions?.CanPing() != true)
            throw new InvalidOperationException("Oneflow-token saknas för det valda bolaget.");

        return await _oneflowClient.ListTemplatesAsync(companyOptions, workspaceId, cancellationToken);
    }

    public async Task<DocumentSigningPublicResultViewModel?> GetPublicResultAsync(Guid publicToken, CancellationToken cancellationToken = default)
    {
        var signing = await _repository.GetByPublicTokenAsync(publicToken.ToString("N"), cancellationToken);
        if (signing == null)
            return null;

        return new DocumentSigningPublicResultViewModel
        {
            DocumentTitle = signing.DocumentTitle,
            PortalStatus = signing.PortalStatus,
            ProviderStatus = signing.ProviderStatus,
            SignerName = signing.SignerName,
            MainFileName = signing.MainFileName,
            CreatedAtUtc = signing.CreatedAtUtc,
            CompletedAtUtc = signing.CompletedAtUtc,
            SignedAndSealed = signing.SignedAndSealed
        };
    }

    private DocumentSigningProviderOptions? ResolveProviderOptions(Guid companyId)
    {
        return _options.Value.Companies.Values.FirstOrDefault(x => x.CompanyId == companyId && x.Enabled);
    }

    private async Task RefreshSigningFromProviderAsync(
        DocumentSigningRecord signing,
        DocumentSigningProviderOptions companyOptions,
        CancellationToken cancellationToken)
    {
        using var contract = await _oneflowClient.GetContractAsync(companyOptions, signing.DocumentId, cancellationToken);
        var participants = ApplyOneflowContract(signing, contract);
        signing.LastSyncedAtUtc = DateTime.UtcNow;
        signing.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(signing, participants, cancellationToken);
    }

    private void EnsureBackgroundSyncScheduled(IEnumerable<DocumentSigningRecord> signings)
    {
        foreach (var signing in signings)
        {
            if (!ShouldScheduleBackgroundSync(signing.PortalStatus))
                continue;

            EnqueueBackgroundSync(signing);
        }
    }

    private void EnqueueBackgroundSync(DocumentSigningRecord signing)
    {
        try
        {
            _statusSyncJobScheduler.EnqueueIfMissing(signing, TimeSpan.FromMinutes(1));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to schedule background document signing sync for signing {SigningId}", signing.Id);
        }
    }

    private static IReadOnlyList<DocumentSigningParticipantRecord> ApplyOneflowContract(DocumentSigningRecord signing, JsonDocument contract)
    {
        var root = contract.RootElement;
        signing.LatestError = null;
        var recipientSigned = false;
        var internalSignatoryPending = false;
        string? externalSignatoryId = null;
        string? externalSignatoryName = null;
        string? externalSignatoryEmail = null;
        string? fallbackSignatoryId = null;
        string? fallbackSignatoryName = null;
        string? fallbackSignatoryEmail = null;
        var participants = new List<DocumentSigningParticipantRecord>();

        if (root.TryGetProperty("id", out var contractIdElement))
        {
            signing.DocumentId = contractIdElement.ValueKind == JsonValueKind.String
                ? (contractIdElement.GetString() ?? signing.DocumentId)
                : contractIdElement.ToString();
        }

        if (root.TryGetProperty("title", out var titleElement))
            signing.DocumentTitle = titleElement.GetString() ?? signing.DocumentTitle;

        if (root.TryGetProperty("state", out var stateElement))
            signing.ProviderStatus = (stateElement.GetString() ?? signing.ProviderStatus).ToLowerInvariant();

        if (root.TryGetProperty("parties", out var partiesElement) && partiesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var party in partiesElement.EnumerateArray())
            {
                var partyId = TryReadString(party, "id");
                if (!party.TryGetProperty("participants", out var participantsElement) || participantsElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var participant in participantsElement.EnumerateArray())
                {
                    var isSignatory = participant.TryGetProperty("signatory", out var signatoryElement)
                        && signatoryElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                        && signatoryElement.GetBoolean();
                    var isMyParticipant = participant.TryGetProperty("my_participant", out var myParticipantElement)
                        && myParticipantElement.ValueKind is JsonValueKind.True or JsonValueKind.False
                        && myParticipantElement.GetBoolean();
                    var signState = TryReadString(participant, "sign_state") ?? string.Empty;
                    var participantId = TryReadString(participant, "id");
                    var participantName = TryReadString(participant, "name") ?? string.Empty;
                    var participantEmail = TryReadString(participant, "email") ?? string.Empty;
                    var participantPhone = TryReadString(participant, "phone_number");
                    var role = TryReadString(participant, "role") ?? (isSignatory ? "signatory" : "participant");
                    var deliveryStatus = TryReadString(participant, "delivery_status")
                        ?? TryReadString(participant, "delivery_state")
                        ?? string.Empty;
                    var signingOrder = TryReadInt(participant, "signing_order");

                    participants.Add(new DocumentSigningParticipantRecord
                    {
                        Id = Guid.NewGuid(),
                        SigningId = signing.Id,
                        OneflowParticipantId = participantId ?? $"unknown-{Guid.NewGuid():N}",
                        OneflowPartyId = partyId,
                        Name = Trim(participantName, 256) ?? string.Empty,
                        NormalizedName = NormalizeName(participantName),
                        Email = Trim(participantEmail, 256) ?? string.Empty,
                        NormalizedEmail = NormalizeEmail(participantEmail),
                        PhoneNumber = Trim(participantPhone, 64),
                        NormalizedPhoneNumber = NormalizePhoneNumber(participantPhone),
                        Role = Trim(role, 64) ?? (isSignatory ? "signatory" : "participant"),
                        SignState = Trim(signState, 64) ?? string.Empty,
                        DeliveryStatus = Trim(deliveryStatus, 64) ?? string.Empty,
                        IsSignatory = isSignatory,
                        IsMyParticipant = isMyParticipant,
                        SigningOrder = signingOrder,
                        SignedAtUtc = TryReadDateTimeOffset(participant, "signed_at"),
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow
                    });

                    if (!isSignatory)
                        continue;

                    if (!isMyParticipant && string.Equals(signState, "signed", StringComparison.OrdinalIgnoreCase))
                        recipientSigned = true;

                    if (isMyParticipant && !string.Equals(signState, "signed", StringComparison.OrdinalIgnoreCase))
                        internalSignatoryPending = true;

                    if (fallbackSignatoryId == null)
                    {
                        fallbackSignatoryId = participantId;
                        fallbackSignatoryName = participantName;
                        fallbackSignatoryEmail = participantEmail;
                    }

                    if (!isMyParticipant)
                    {
                        externalSignatoryId = participantId;
                        externalSignatoryName = participantName;
                        externalSignatoryEmail = participantEmail;
                    }
                }
            }
        }

        signing.SignatoryId = externalSignatoryId ?? fallbackSignatoryId ?? signing.SignatoryId;
        signing.SignerName = externalSignatoryName ?? fallbackSignatoryName ?? signing.SignerName;
        signing.SignerEmail = externalSignatoryEmail ?? fallbackSignatoryEmail ?? signing.SignerEmail;
        signing.Participants = participants;

        signing.SignedAndSealed = string.Equals(signing.ProviderStatus, "signed", StringComparison.OrdinalIgnoreCase);
        signing.PortalStatus = MapPortalStatus(signing.ProviderStatus, signing.SignedAndSealed, signing.LatestError, recipientSigned, internalSignatoryPending);

        if (IsTerminalStatus(signing.ProviderStatus) && signing.CompletedAtUtc == null)
            signing.CompletedAtUtc = DateTime.UtcNow;

        return participants;
    }

    private static string MapPortalStatus(string providerStatus, bool signedAndSealed, string? latestError, bool recipientSigned, bool internalSignatoryPending)
    {
        if (!string.IsNullOrWhiteSpace(latestError))
            return "failed";

        return providerStatus switch
        {
            "pending" when recipientSigned && internalSignatoryPending => "waitinginternal",
            "draft" => "preparing",
            "pending" => "sent",
            "signed" => signedAndSealed ? "signed" : "completed",
            "declined" => "rejected",
            "overdue" => "timedout",
            _ => providerStatus
        };
    }

    private static DocumentSigningListItem MapDocumentSigningListItem(DocumentSigningRecord signing)
    {
        return new DocumentSigningListItem
        {
            Id = signing.Id,
            OrderNo = signing.OrderNo,
            DocumentTitle = signing.DocumentTitle,
            DocumentId = signing.DocumentId,
            PortalStatus = signing.PortalStatus,
            ProviderStatus = signing.ProviderStatus,
            SignerName = signing.SignerName,
            SignerEmail = signing.SignerEmail,
            MainFileName = signing.MainFileName,
            AttachmentCount = signing.AttachmentCount,
            CreatedAtUtc = signing.CreatedAtUtc,
            StartedAtUtc = signing.StartedAtUtc,
            CompletedAtUtc = signing.CompletedAtUtc,
            LastSyncedAtUtc = signing.LastSyncedAtUtc,
            SignedAndSealed = signing.SignedAndSealed,
            IsTerminal = IsTerminalStatus(signing.ProviderStatus)
        };
    }

    private static bool IsTerminalStatus(string? providerStatus)
    {
        return string.Equals(providerStatus, "signed", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerStatus, "declined", StringComparison.OrdinalIgnoreCase)
            || string.Equals(providerStatus, "overdue", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldScheduleBackgroundSync(string? portalStatus)
    {
        return string.Equals(portalStatus, "sent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(portalStatus, "waitinginternal", StringComparison.OrdinalIgnoreCase)
            || string.Equals(portalStatus, "preparing", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureExpandedPdfAllowed(JsonElement contractRoot, bool hasMainFile)
    {
        if (!hasMainFile)
            return;

        var availableOptions = GetAvailableOptions(contractRoot);
        if (availableOptions.TryGetValue("can_receive_expanded_pdf", out var canReceiveExpandedPdf) && canReceiveExpandedPdf)
            return;

        throw new InvalidOperationException("Oneflow-templaten tillåter inte uppladdad huvud-PDF via API. Templaten måste ha stöd för expanded PDF.");
    }

    private static void EnsureAttachmentsAllowed(JsonElement contractRoot, int attachmentCount)
    {
        if (attachmentCount <= 0)
            return;

        var availableOptions = GetAvailableOptions(contractRoot);
        if (availableOptions.TryGetValue("can_receive_attachments", out var canReceiveAttachments) && canReceiveAttachments)
            return;

        throw new InvalidOperationException("Oneflow-templaten tillåter inte bilagor via API. Använd en template med attachment-sektion eller skicka utan bilagor.");
    }

    private static Dictionary<string, bool> GetAvailableOptions(JsonElement contractRoot)
    {
        var result = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        if (!contractRoot.TryGetProperty("_available_options", out var availableOptions)
            || availableOptions.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var option in availableOptions.EnumerateObject())
        {
            if (option.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                result[option.Name] = option.Value.GetBoolean();
        }

        return result;
    }

    private static DocumentSigningCreateResult MapCreateResult(DocumentSigningRecord signing)
    {
        return new DocumentSigningCreateResult
        {
            SigningId = signing.Id,
            DocumentId = signing.DocumentId,
            PortalStatus = signing.PortalStatus
        };
    }

    private static IReadOnlyList<DocumentSigningParticipantInput> ResolveCreateParticipants(DocumentSigningCreateRequest request)
    {
        var explicitParticipants = request.Participants
            .Where(participant => !string.IsNullOrWhiteSpace(participant.Email))
            .Select(participant => new DocumentSigningParticipantInput
            {
                Name = participant.Name.Trim(),
                Email = participant.Email.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(participant.PhoneNumber) ? null : participant.PhoneNumber.Trim(),
                Role = string.IsNullOrWhiteSpace(participant.Role) ? "signatory" : participant.Role.Trim(),
                IsSignatory = participant.IsSignatory,
                CanUpdateContract = participant.CanUpdateContract,
                SigningOrder = participant.SigningOrder
            })
            .ToList();

        if (explicitParticipants.Count > 0)
            return explicitParticipants;

        var signerName = $"{request.SignerFirstName} {request.SignerLastName}".Trim();
        if (string.IsNullOrWhiteSpace(signerName) || string.IsNullOrWhiteSpace(request.SignerEmail))
            throw new InvalidOperationException("Minst en signerare med namn och e-post måste anges.");

        return new[]
        {
            new DocumentSigningParticipantInput
            {
                Name = signerName,
                Email = request.SignerEmail.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(request.SignerMobile) ? null : request.SignerMobile.Trim(),
                Role = "signatory",
                IsSignatory = true,
                CanUpdateContract = true
            }
        };
    }

    private static string ResolveCorrelationKey(
        DocumentSigningCreateRequest request,
        IReadOnlyList<DocumentSigningParticipantInput> participants)
    {
        if (!string.IsNullOrWhiteSpace(request.CorrelationKey))
            return request.CorrelationKey.Trim();

        var builder = new StringBuilder();
        builder.Append(request.CompanyId.ToString("D")).Append('|')
            .Append(request.JeevesCompanyCode?.ToString() ?? string.Empty).Append('|')
            .Append(request.OrderNo).Append('|')
            .Append((request.DocumentTitle ?? string.Empty).Trim()).Append('|')
            .Append((request.OrderCustomerName ?? string.Empty).Trim()).Append('|')
            .Append((request.InvitationMessage ?? string.Empty).Trim());

        if (request.MainFile != null)
        {
            builder.Append("|main:")
                .Append(request.MainFile.FileName)
                .Append(':')
                .Append(ComputeSha256(request.MainFile.Content));
        }

        foreach (var attachment in request.Attachments
            .OrderBy(attachment => attachment.FileName, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("|attachment:")
                .Append(attachment.FileName)
                .Append(':')
                .Append(ComputeSha256(attachment.Content));
        }

        foreach (var participant in participants
            .OrderBy(participant => NormalizeEmail(participant.Email), StringComparer.OrdinalIgnoreCase)
            .ThenBy(participant => NormalizeName(participant.Name), StringComparer.OrdinalIgnoreCase))
        {
            builder.Append("|participant:")
                .Append(NormalizeName(participant.Name))
                .Append(':')
                .Append(NormalizeEmail(participant.Email))
                .Append(':')
                .Append(NormalizePhoneNumber(participant.PhoneNumber))
                .Append(':')
                .Append(participant.Role)
                .Append(':')
                .Append(participant.SigningOrder?.ToString() ?? string.Empty);
        }

        return ComputeSha256(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string ComputeSha256(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        while (ex != null)
        {
            if (ex is Microsoft.EntityFrameworkCore.DbUpdateException)
                return true;

            ex = ex.InnerException!;
        }

        return false;
    }

    private static DocumentSigningParticipantRecord? ResolveLaunchParticipant(DocumentSigningRecord signing)
    {
        var participants = signing.Participants
            .Where(participant => participant.IsSignatory && !participant.IsMyParticipant && !string.IsNullOrWhiteSpace(participant.OneflowParticipantId))
            .ToList();

        if (participants.Count == 0)
            return null;

        var normalizedEmail = NormalizeEmail(signing.SignerEmail);
        var normalizedName = NormalizeName(signing.SignerName);
        var normalizedPhone = NormalizePhoneNumber(signing.SignerMobile);

        var candidates = participants
            .Where(participant => string.Equals(participant.OneflowParticipantId, signing.SignatoryId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 1)
            return candidates[0];

        candidates = participants
            .Where(participant => string.Equals(participant.NormalizedEmail, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (candidates.Count == 1)
            return candidates[0];

        candidates = candidates
            .Where(participant =>
                string.Equals(participant.NormalizedName, normalizedName, StringComparison.OrdinalIgnoreCase)
                || (!string.IsNullOrWhiteSpace(normalizedPhone)
                    && string.Equals(participant.NormalizedPhoneNumber, normalizedPhone, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (candidates.Count == 1)
            return candidates[0];

        if (participants.Count == 1)
            return participants[0];

        return null;
    }

    private static string? NormalizeEmail(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
    }

    private static string? NormalizeName(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : string.Join(' ', value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    private static string? NormalizePhoneNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (char.IsDigit(character))
                builder.Append(character);
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    private static string? TryReadString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null
        };
    }

    private static int? TryReadInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var number))
            return number;

        if (property.ValueKind == JsonValueKind.String && int.TryParse(property.GetString(), out number))
            return number;

        return null;
    }

    private static DateTime? TryReadDateTimeOffset(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            return null;

        return DateTimeOffset.TryParse(property.GetString(), out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
