using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using WebApp.Models.DocumentSigning;
using WebApp.Models.Integration;
using WebApp.Services.Integration;

namespace WebApp.Services.DocumentSigning;

public sealed class OneflowDocumentSigningClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OneflowDocumentSigningClient> _logger;

    public OneflowDocumentSigningClient(
        IHttpClientFactory httpClientFactory,
        ILogger<OneflowDocumentSigningClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<JsonDocument> CreateContractFromTemplateAsync(
        DocumentSigningProviderOptions options,
        DocumentSigningCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var participants = ResolveParticipantInputs(request);
        var primaryParticipant = participants.FirstOrDefault();
        var partyName = string.IsNullOrWhiteSpace(request.OrderCustomerName)
            ? primaryParticipant?.Name ?? string.Empty
            : request.OrderCustomerName.Trim();

        var party = new Dictionary<string, object?>
        {
            ["type"] = "company",
            ["name"] = partyName,
            ["country_code"] = string.IsNullOrWhiteSpace(options.OneflowCounterpartyCountryCode)
                ? "SE"
                : options.OneflowCounterpartyCountryCode.Trim().ToUpperInvariant(),
            ["participants"] = participants.Select(CreateParticipantPayload).ToArray()
        };

        var payload = new Dictionary<string, object?>
        {
            ["workspace_id"] = options.OneflowWorkspaceId!.Value,
            ["template_id"] = options.OneflowTemplateId!.Value,
            ["parties"] = new object[] { party }
        };

        if (!string.IsNullOrWhiteSpace(request.DocumentTitle))
            payload["name"] = request.DocumentTitle.Trim();

        return SendAsync(options, HttpMethod.Post, "v1/contracts/create", CreateJsonContent(payload), cancellationToken);
    }

    public async Task UploadMainFileAsync(
        DocumentSigningProviderOptions options,
        string contractId,
        DocumentSigningUploadFile file,
        string uploadAs,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(CreatePdfContent(file), "file", file.FileName);
        content.Add(new StringContent(uploadAs), "upload_as");
        using var _ = await SendAsync(options, HttpMethod.Post, $"v1/contracts/{Uri.EscapeDataString(contractId)}/files", content, cancellationToken);
    }

    public async Task UploadAttachmentAsync(
        DocumentSigningProviderOptions options,
        string contractId,
        DocumentSigningUploadFile file,
        CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(CreatePdfContent(file), "file", file.FileName);
        content.Add(new StringContent("attachment"), "upload_as");
        using var _ = await SendAsync(options, HttpMethod.Post, $"v1/contracts/{Uri.EscapeDataString(contractId)}/files", content, cancellationToken);
    }

    public Task<JsonDocument> PublishContractAsync(
        DocumentSigningProviderOptions options,
        string contractId,
        string subject,
        string? message,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            subject,
            message = message ?? string.Empty
        };

        return SendAsync(options, HttpMethod.Post, $"v1/contracts/{Uri.EscapeDataString(contractId)}/publish", CreateJsonContent(payload), cancellationToken);
    }

    public Task<JsonDocument> GetContractAsync(
        DocumentSigningProviderOptions options,
        string contractId,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(options, HttpMethod.Get, $"v1/contracts/{Uri.EscapeDataString(contractId)}", null, cancellationToken);
    }

    public async Task<string> CreateParticipantAccessLinkAsync(
        DocumentSigningProviderOptions options,
        string contractId,
        string participantId,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            options,
            HttpMethod.Post,
            $"v1/contracts/{Uri.EscapeDataString(contractId)}/participants/{Uri.EscapeDataString(participantId)}/access_link",
            null,
            cancellationToken);

        return ExtractAccessLinkUrl(response.RootElement);
    }

    public async Task PingAsync(
        DocumentSigningProviderOptions options,
        CancellationToken cancellationToken = default)
    {
        using var _ = await SendAsync(options, HttpMethod.Get, "v1/ping", null, cancellationToken, includeUserEmail: false);
    }

    public async Task<IReadOnlyList<DocumentSigningOneflowWorkspaceViewModel>> ListWorkspacesAsync(
        DocumentSigningProviderOptions options,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(options, HttpMethod.Get, "v1/workspaces?sort=name", null, cancellationToken);
        return EnumerateCollection(response.RootElement, "workspaces", "data", "items")
            .Select(item => new DocumentSigningOneflowWorkspaceViewModel
            {
                Id = TryReadInt(item, "id") ?? 0,
                Name = TryReadString(item, "name") ?? $"Workspace {TryReadInt(item, "id") ?? 0}"
            })
            .Where(item => item.Id > 0)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<DocumentSigningOneflowTemplateViewModel>> ListTemplatesAsync(
        DocumentSigningProviderOptions options,
        int? workspaceId = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(options, HttpMethod.Get, "v1/templates?sort=name", null, cancellationToken);
        var templates = EnumerateCollection(response.RootElement, "templates", "data", "items")
            .Select(item =>
            {
                var workspaceIds = ExtractWorkspaceIds(item);
                return new DocumentSigningOneflowTemplateViewModel
                {
                    Id = TryReadInt(item, "id") ?? 0,
                    Name = TryReadString(item, "name") ?? $"Template {TryReadInt(item, "id") ?? 0}",
                    WorkspaceIds = workspaceIds
                };
            })
            .Where(item => item.Id > 0)
            .Where(item => !workspaceId.HasValue || item.WorkspaceIds.Contains(workspaceId.Value))
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return templates;
    }

    private async Task<JsonDocument> SendAsync(
        DocumentSigningProviderOptions options,
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken,
        bool includeUserEmail = true)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("x-oneflow-api-token", options.ApiToken);
        if (includeUserEmail && !string.IsNullOrWhiteSpace(options.OneflowUserEmail))
            request.Headers.TryAddWithoutValidation("x-oneflow-user-email", options.OneflowUserEmail);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (content is not null)
            request.Content = content;

        var client = _httpClientFactory.CreateClient("Integration.DocumentSigning");
        client.BaseAddress = new Uri(NormalizeBaseUrl(options.BaseUrl));

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Oneflow document signing API call failed. Path: {Path}, Status: {StatusCode}, Diagnostic: {Diagnostic}",
                path,
                (int)response.StatusCode,
                IntegrationLogSanitizer.Diagnostic(body));
            throw new InvalidOperationException(BuildErrorMessage(body, response.StatusCode));
        }

        if (string.IsNullOrWhiteSpace(body))
            throw new InvalidOperationException("Oneflow returnerade ett tomt svar.");

        return JsonDocument.Parse(body);
    }

    private static string NormalizeBaseUrl(string? baseUrl)
    {
        var normalized = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.oneflow.com/" : baseUrl.Trim();
        if (!normalized.EndsWith('/'))
            normalized += "/";
        return normalized;
    }

    private static StringContent CreateJsonContent(object payload)
        => new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    private static IReadOnlyList<DocumentSigningParticipantInput> ResolveParticipantInputs(DocumentSigningCreateRequest request)
    {
        var participants = request.Participants.Count > 0
            ? request.Participants
            : new[]
            {
                new DocumentSigningParticipantInput
                {
                    Name = $"{request.SignerFirstName} {request.SignerLastName}".Trim(),
                    Email = request.SignerEmail,
                    PhoneNumber = request.SignerMobile,
                    IsSignatory = true,
                    CanUpdateContract = true,
                    Role = "signatory"
                }
            };

        return participants
            .Where(participant => !string.IsNullOrWhiteSpace(participant.Email))
            .ToArray();
    }

    private static Dictionary<string, object?> CreateParticipantPayload(DocumentSigningParticipantInput participant)
    {
        var payload = new Dictionary<string, object?>
        {
            ["name"] = participant.Name.Trim(),
            ["email"] = participant.Email.Trim(),
            ["signatory"] = participant.IsSignatory,
            ["delivery_channel"] = "email"
        };

        if (!string.IsNullOrWhiteSpace(participant.PhoneNumber))
            payload["phone_number"] = participant.PhoneNumber.Trim();

        if (participant.SigningOrder.HasValue)
            payload["signing_order"] = participant.SigningOrder.Value;

        if (participant.CanUpdateContract)
        {
            payload["_permissions"] = new Dictionary<string, bool>
            {
                ["contract:update"] = true
            };
        }

        return payload;
    }

    private static IEnumerable<JsonElement> EnumerateCollection(JsonElement root, params string[] candidateProperties)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray();

        foreach (var property in candidateProperties)
        {
            if (root.TryGetProperty(property, out var child) && child.ValueKind == JsonValueKind.Array)
                return child.EnumerateArray();
        }

        return Array.Empty<JsonElement>();
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

    private static IReadOnlyList<int> ExtractWorkspaceIds(JsonElement template)
    {
        var result = new List<int>();
        if (!template.TryGetProperty("workspaces", out var workspaces) || workspaces.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var workspace in workspaces.EnumerateArray())
        {
            if (workspace.ValueKind == JsonValueKind.Number && workspace.TryGetInt32(out var id))
            {
                result.Add(id);
                continue;
            }

            if (workspace.ValueKind == JsonValueKind.Object)
            {
                var objectId = TryReadInt(workspace, "id");
                if (objectId.HasValue)
                    result.Add(objectId.Value);
            }
        }

        return result.Distinct().ToList();
    }

    private static ByteArrayContent CreatePdfContent(DocumentSigningUploadFile file)
    {
        var content = new ByteArrayContent(file.Content);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return content;
    }

    private static string ExtractAccessLinkUrl(JsonElement root)
    {
        foreach (var propertyName in new[] { "url", "access_link", "link", "href" })
        {
            if (root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var url = value.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                    return url;
            }
        }

        if (root.TryGetProperty("data", out var dataElement) && dataElement.ValueKind == JsonValueKind.Object)
            return ExtractAccessLinkUrl(dataElement);

        throw new InvalidOperationException("Oneflow returnerade ingen access link-URL för vald signerare.");
    }

    private static string BuildErrorMessage(string body, System.Net.HttpStatusCode statusCode)
    {
        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                var message =
                    root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() :
                    root.TryGetProperty("error", out var errorElement) ? errorElement.GetString() :
                    root.TryGetProperty("detail", out var detailElement) ? detailElement.GetString() :
                    null;

                if (root.TryGetProperty("parameter_problems", out var parameterProblems)
                    && parameterProblems.ValueKind == JsonValueKind.Object)
                {
                    var details = new List<string>();
                    foreach (var problem in parameterProblems.EnumerateObject())
                    {
                        if (problem.Value.ValueKind != JsonValueKind.Array)
                            continue;

                        var values = problem.Value
                            .EnumerateArray()
                            .Where(item => item.ValueKind == JsonValueKind.String)
                            .Select(item => item.GetString())
                            .Where(value => !string.IsNullOrWhiteSpace(value))
                            .ToList();

                        if (values.Count > 0)
                            details.Add($"{problem.Name}: {string.Join(", ", values!)}");
                    }

                    if (details.Count > 0 && !string.IsNullOrWhiteSpace(message))
                        message = $"{message} ({string.Join(" | ", details)})";
                }

                if (!string.IsNullOrWhiteSpace(message))
                    return $"Oneflow API-fel för dokumentsignering ({(int)statusCode}): {message}";
            }
            catch
            {
                // Fall through to generic message.
            }
        }

        return $"Oneflow API-fel för dokumentsignering ({(int)statusCode}).";
    }
}
