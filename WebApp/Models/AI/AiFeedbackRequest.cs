// Carries bounded user feedback for one Intelligence response.
namespace WebApp.Models.AI;

public sealed class AiFeedbackRequest
{
    public Guid ResponseId { get; set; }
    public string Rating { get; set; } = string.Empty;
    public string? Comment { get; set; }
}
