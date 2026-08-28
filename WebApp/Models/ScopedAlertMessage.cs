namespace WebApp.Models;

public sealed class ScopedAlertMessage
{
    public string Level { get; set; } = Alert.INFORMATION;
    public string Message { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
}
