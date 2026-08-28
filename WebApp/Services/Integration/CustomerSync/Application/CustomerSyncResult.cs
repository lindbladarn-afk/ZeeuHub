using System.Text.Json;

namespace WebApp.Services.Integration.CustomerSync.Application;

// Summarizes one customer sync job in a durable, background-job friendly format.
public sealed class CustomerSyncResult
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public bool Succeeded { get; set; }
    public string Summary { get; set; } = string.Empty;
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }

    public string ToJson()
        => JsonSerializer.Serialize(this, JsonOptions);

    public static CustomerSyncResult FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new CustomerSyncResult();

        return JsonSerializer.Deserialize<CustomerSyncResult>(json, JsonOptions) ?? new CustomerSyncResult();
    }
}
