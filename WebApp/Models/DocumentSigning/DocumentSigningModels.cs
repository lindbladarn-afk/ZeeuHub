using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebApp.Models.DocumentSigning;

public class DocumentSigningRecord
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    public int? JeevesCompanyCode { get; set; }

    public long OrderNo { get; set; }

    [MaxLength(256)]
    public string OrderCustomerName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string DocumentTitle { get; set; } = string.Empty;

    [MaxLength(64)]
    public string DocumentId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? SignatoryId { get; set; }

    [MaxLength(128)]
    public string CorrelationKey { get; set; } = string.Empty;

    [MaxLength(64)]
    public string PortalStatus { get; set; } = "created";

    [MaxLength(64)]
    public string ProviderStatus { get; set; } = "draft";

    [MaxLength(256)]
    public string SignerName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string SignerEmail { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? SignerMobile { get; set; }

    [MaxLength(256)]
    public string MainFileName { get; set; } = string.Empty;

    public int AttachmentCount { get; set; }

    [MaxLength(64)]
    public string PublicToken { get; set; } = string.Empty;

    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [MaxLength(256)]
    public string CreatedByEmail { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public bool SignedAndSealed { get; set; }
    public int? ProviderObjectVersion { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? InvitationMessage { get; set; }

    [Column(TypeName = "nvarchar(max)")]
    public string? LatestError { get; set; }

    [NotMapped]
    public IReadOnlyList<DocumentSigningParticipantRecord> Participants { get; set; } = Array.Empty<DocumentSigningParticipantRecord>();
}

public sealed class DocumentSigningParticipantRecord
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid SigningId { get; set; }

    [MaxLength(128)]
    public string OneflowParticipantId { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? OneflowPartyId { get; set; }

    [MaxLength(256)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? NormalizedName { get; set; }

    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? NormalizedEmail { get; set; }

    [MaxLength(64)]
    public string? PhoneNumber { get; set; }

    [MaxLength(64)]
    public string? NormalizedPhoneNumber { get; set; }

    [MaxLength(64)]
    public string Role { get; set; } = "signatory";

    [MaxLength(64)]
    public string SignState { get; set; } = string.Empty;

    [MaxLength(64)]
    public string DeliveryStatus { get; set; } = string.Empty;

    public bool IsSignatory { get; set; }
    public bool IsMyParticipant { get; set; }
    public int? SigningOrder { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class DocumentSigningListItem
{
    public Guid Id { get; set; }
    public long OrderNo { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string DocumentId { get; set; } = string.Empty;
    public string PortalStatus { get; set; } = string.Empty;
    public string ProviderStatus { get; set; } = string.Empty;
    public string SignerName { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public string MainFileName { get; set; } = string.Empty;
    public int AttachmentCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public bool SignedAndSealed { get; set; }
    public bool IsTerminal { get; set; }
}

public sealed class DocumentSigningCreateRequest
{
    public Guid CompanyId { get; set; }
    public int? JeevesCompanyCode { get; set; }
    public long OrderNo { get; set; }
    public string OrderCustomerName { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByEmail { get; set; } = string.Empty;
    public string? CorrelationKey { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public string SignerFirstName { get; set; } = string.Empty;
    public string SignerLastName { get; set; } = string.Empty;
    public string SignerEmail { get; set; } = string.Empty;
    public string? SignerMobile { get; set; }
    public string? InvitationMessage { get; set; }
    public DocumentSigningUploadFile? MainFile { get; set; }
    public IReadOnlyList<DocumentSigningUploadFile> Attachments { get; set; } = Array.Empty<DocumentSigningUploadFile>();
    public IReadOnlyList<DocumentSigningParticipantInput> Participants { get; set; } = Array.Empty<DocumentSigningParticipantInput>();
}

public sealed class DocumentSigningCreateResult
{
    public Guid SigningId { get; set; }
    public string DocumentId { get; set; } = string.Empty;
    public string PortalStatus { get; set; } = string.Empty;
}

public sealed class DocumentSigningParticipantInput
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "signatory";
    public bool IsSignatory { get; set; } = true;
    public bool CanUpdateContract { get; set; } = true;
    public int? SigningOrder { get; set; }
}

public sealed class DocumentSigningLaunchResult
{
    public Guid SigningId { get; set; }
    public string ParticipantId { get; set; } = string.Empty;
    public string AccessLinkUrl { get; set; } = string.Empty;
}
