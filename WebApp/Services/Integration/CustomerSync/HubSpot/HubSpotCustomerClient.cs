using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using WebApp.Observability;

namespace WebApp.Services.Integration.CustomerSync.HubSpot;

// Reads HubSpot company data needed by the first CustomerSync import step.
public sealed class HubSpotCustomerClient : IHubSpotCustomerClient
{
    private const string ClientName = "Integration.HubSpot";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] CompanyProperties =
    {
        "name",
        "domain",
        "phone",
        "email",
        "hs_lastmodifieddate",
        "organizationnumber",
        "organisationnumber",
        "orgnr",
        "vat_number"
    };

    private readonly IHttpClientFactory _httpClientFactory;

    public HubSpotCustomerClient(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<HubSpotCustomerDto>> ListCompaniesAsync(
        CustomerSyncHubSpotConnection connection,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (string.IsNullOrWhiteSpace(connection.Token))
            throw new InvalidOperationException("HubSpot token is missing for CustomerSync.");

        var pageSize = Math.Clamp(limit, 1, 100);
        var client = CreateClient(connection);
        var results = new List<HubSpotCustomerDto>();
        string? after = null;

        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BuildCompaniesUri(pageSize, after));
            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"HubSpot companies could not be loaded. Status {(int)response.StatusCode}. {IntegrationLogSanitizer.Diagnostic(body)}");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var page = await JsonSerializer.DeserializeAsync<HubSpotCompaniesResponse>(stream, JsonOptions, cancellationToken)
                ?? new HubSpotCompaniesResponse();

            results.AddRange(page.Results.Select(MapCompany));
            after = page.Paging?.Next?.After;

            if (string.IsNullOrWhiteSpace(after) || page.Results.Count == 0)
                break;
        }

        return results;
    }

    public Task<HubSpotCustomerDto?> GetCompanyAsync(
        Guid companyId,
        string hubSpotCompanyId,
        CancellationToken cancellationToken)
        => throw new NotImplementedException("Single-company HubSpot read is not connected in CustomerSync step 1.");

    public Task<HubSpotCustomerWriteResult> UpsertCompanyAsync(
        Guid companyId,
        HubSpotCustomerDto customer,
        CancellationToken cancellationToken)
        => throw new NotImplementedException("HubSpot write is intentionally out of scope for CustomerSync step 1.");

    private HttpClient CreateClient(CustomerSyncHubSpotConnection connection)
    {
        var client = _httpClientFactory.CreateClient(ClientName);
        client.BaseAddress = ResolveBaseAddress(connection.BaseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", connection.Token);
        return client;
    }

    private static Uri ResolveBaseAddress(string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return new Uri("https://api.hubapi.com/");

        var trimmed = baseUrl.Trim();
        return new Uri(trimmed.EndsWith("/", StringComparison.Ordinal) ? trimmed : $"{trimmed}/");
    }

    private static string BuildCompaniesUri(int limit, string? after)
    {
        var properties = Uri.EscapeDataString(string.Join(",", CompanyProperties));
        var uri = $"crm/v3/objects/companies?limit={limit.ToString(CultureInfo.InvariantCulture)}&properties={properties}";
        return string.IsNullOrWhiteSpace(after) ? uri : $"{uri}&after={Uri.EscapeDataString(after)}";
    }

    private static HubSpotCustomerDto MapCompany(HubSpotCompanyRecord record)
    {
        record.Properties.TryGetValue("hs_lastmodifieddate", out var updatedAtRaw);

        return new HubSpotCustomerDto
        {
            CompanyId = record.Id,
            OrganizationNumber = FirstNonEmpty(record.Properties, "organizationnumber", "organisationnumber", "orgnr", "vat_number"),
            Name = FirstNonEmpty(record.Properties, "name"),
            Domain = FirstNonEmpty(record.Properties, "domain"),
            Email = FirstNonEmpty(record.Properties, "email"),
            Phone = FirstNonEmpty(record.Properties, "phone"),
            UpdatedAtUtc = TryParseUtc(updatedAtRaw) ?? record.UpdatedAt
        };
    }

    private static string? FirstNonEmpty(IReadOnlyDictionary<string, string?> properties, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return null;
    }

    private static DateTime? TryParseUtc(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.UtcDateTime
            : null;
    }

    private sealed class HubSpotCompaniesResponse
    {
        public List<HubSpotCompanyRecord> Results { get; set; } = new();
        public HubSpotPaging? Paging { get; set; }
    }

    private sealed class HubSpotCompanyRecord
    {
        public string? Id { get; set; }
        public Dictionary<string, string?> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public DateTime? UpdatedAt { get; set; }
    }

    private sealed class HubSpotPaging
    {
        public HubSpotPagingNext? Next { get; set; }
    }

    private sealed class HubSpotPagingNext
    {
        public string? After { get; set; }
    }
}
