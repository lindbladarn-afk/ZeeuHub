namespace WebApp.Services.Integration.CustomerSync.Jeeves;

// Describes the result of a Jeeves customer create or update operation.
public sealed class JeevesCustomerWriteResult
{
    public string? CustomerNumber { get; set; }
    public bool Created { get; set; }
    public bool Changed { get; set; }
}
