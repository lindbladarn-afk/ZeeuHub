using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.Sources
{
    public class CentraOrderSource : IOrderSourceClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<IntegrationOptions> _options;
        private readonly ILogger<CentraOrderSource> _logger;

        public CentraOrderSource(
            IHttpClientFactory httpClientFactory,
            IOptions<IntegrationOptions> options,
            ILogger<CentraOrderSource> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        public IntegrationSource Source => IntegrationSource.Centra;

        public Task<IReadOnlyList<IntegrationOrder>> FetchOrdersAsync(IntegrationFetchRequest request, CancellationToken ct = default)
        {
            return string.IsNullOrWhiteSpace(request.ExternalOrderId)
                ? FetchLatestAsync(request, ct)
                : FetchByIdAsync(request, ct);
        }

        private async Task<IReadOnlyList<IntegrationOrder>> FetchByIdAsync(IntegrationFetchRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.ExternalOrderId))
                return new List<IntegrationOrder>();

            var companyConfig = _options.Value.Companies.Find(c => c.CompanyId == request.CompanyId);
            var sourceConfig = companyConfig?.GetSource(IntegrationSource.Centra);
            if (sourceConfig is null || string.IsNullOrWhiteSpace(sourceConfig.BaseUrl))
                return new List<IntegrationOrder>();

            var client = _httpClientFactory.CreateClient("Integration.Centra");
            client.BaseAddress = new System.Uri(sourceConfig.BaseUrl);

            if (!string.IsNullOrWhiteSpace(sourceConfig.Token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sourceConfig.Token);
            }

            var query = @"
query Order($id: String!) {
  order(id: $id) {
    status
    id
    number
    ... on DirectToConsumerOrder {
      shippingAddress { firstName lastName }
      billingAddress { firstName lastName }
    }
    store { id }
    createdAt
    lines {
      lineValue { value currency { code } }
    }
  }
}";

            var payload = new
            {
                query,
                variables = new { id = request.ExternalOrderId }
            };

            using var response = await client.PostAsJsonAsync(string.Empty, payload, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var bodyError = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Centra GraphQL failed: {Status} {Diagnostic}", response.StatusCode, IntegrationLogSanitizer.Diagnostic(bodyError));
                throw new IntegrationSourceException(IntegrationSource.Centra, (int)response.StatusCode, BuildHttpMessage(response.StatusCode, bodyError));
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            if (TryGetGraphQlErrorMessage(body, out var graphqlError))
            {
                _logger.LogWarning("Centra order lookup GraphQL error: {Error}", graphqlError);
                throw new IntegrationSourceException(IntegrationSource.Centra, (int)response.StatusCode, "GraphQL: " + graphqlError);
            }

            var parsed = JsonSerializer.Deserialize<CentraOrderResponse>(body, JsonOptions);
            if (parsed?.Data?.Order == null)
            {
                _logger.LogWarning("Centra order not found for ExternalOrderId {ExternalOrderId}.", request.ExternalOrderId);
                return new List<IntegrationOrder>();
            }

            var order = parsed.Data.Order;
            var total = SumLineValue(order);
            var currency = GetLineCurrency(order);
            var customerName = BuildCustomerName(order);
            return new List<IntegrationOrder>
            {
                new IntegrationOrder
                {
                    ExternalId = order.Id ?? string.Empty,
                    OrderNo = order.Number?.ToString(),
                    OrderDate = order.CreatedAt,
                    CustomerName = customerName,
                    Status = order.Status,
                    TotalAmount = total,
                    Currency = currency,
                    RawJson = body
                }
            };
        }

        private async Task<IReadOnlyList<IntegrationOrder>> FetchLatestAsync(IntegrationFetchRequest request, CancellationToken ct)
        {
            var companyConfig = _options.Value.Companies.Find(c => c.CompanyId == request.CompanyId);
            var sourceConfig = companyConfig?.GetSource(IntegrationSource.Centra);
            if (sourceConfig is null || string.IsNullOrWhiteSpace(sourceConfig.BaseUrl))
                return new List<IntegrationOrder>();

            var client = _httpClientFactory.CreateClient("Integration.Centra");
            client.BaseAddress = new System.Uri(sourceConfig.BaseUrl);

            if (!string.IsNullOrWhiteSpace(sourceConfig.Token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", sourceConfig.Token);
            }

            var useDateFilter = request.FromUtc is not null || request.ToUtc is not null;
            var query = useDateFilter
                ? @"
query($limit: Int!, $page: Int!, $sort: [OrderSort!], $createdFrom: DateTimeTz, $createdTo: DateTimeTz) {
  orders(limit: $limit, page: $page, sort: $sort, where: { createdAt: { from: $createdFrom, to: $createdTo } }) {
    id
    number
    status
    orderDate
    createdAt
    ... on DirectToConsumerOrder {
      shippingAddress { firstName lastName }
      billingAddress { firstName lastName }
    }
    lines {
      lineValue { value currency { code } }
    }
  }
}"
                : @"
query($limit: Int!, $page: Int!, $sort: [OrderSort!]) {
  orders(limit: $limit, page: $page, sort: $sort) {
    id
    number
    status
    orderDate
    createdAt
    ... on DirectToConsumerOrder {
      shippingAddress { firstName lastName }
      billingAddress { firstName lastName }
    }
    lines {
      lineValue { value currency { code } }
    }
  }
}";

            object payload = useDateFilter
                ? new
                {
                    query,
                    variables = new
                    {
                        limit = 50,
                        page = 1,
                        sort = new[] { "orderDate_DESC" },
                        createdFrom = request.FromUtc?.ToString("o"),
                        createdTo = request.ToUtc?.ToString("o")
                    }
                }
                : new
                {
                    query,
                    variables = new
                    {
                        limit = 50,
                        page = 1,
                        sort = new[] { "orderDate_DESC" }
                    }
                };

            using var response = await client
                .PostAsJsonAsync(string.Empty, payload, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var bodyError = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Centra GraphQL list failed: {Status} {Diagnostic}", response.StatusCode, IntegrationLogSanitizer.Diagnostic(bodyError));
                throw new IntegrationSourceException(IntegrationSource.Centra, (int)response.StatusCode, BuildHttpMessage(response.StatusCode, bodyError));
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            if (TryGetGraphQlErrorMessage(body, out var graphqlListError))
            {
                _logger.LogWarning("Centra order list GraphQL error: {Error}", graphqlListError);
                if (useDateFilter)
                    return await FetchLatestWithoutDateFilterAsync(client, ct);
                throw new IntegrationSourceException(IntegrationSource.Centra, (int)response.StatusCode, "GraphQL: " + graphqlListError);
            }

            var parsed = JsonSerializer.Deserialize<CentraOrdersResponse>(body, JsonOptions);
            var orders = parsed?.Data?.Orders ?? new List<CentraOrder>();
            if (orders.Count == 0)
                return new List<IntegrationOrder>();

            var results = new List<IntegrationOrder>();
            foreach (var order in orders)
            {
                var total = SumLineValue(order);
                var currency = GetLineCurrency(order);
                var customerName = BuildCustomerName(order);
                results.Add(new IntegrationOrder
                {
                    ExternalId = order.Id ?? string.Empty,
                    OrderNo = order.Number?.ToString(),
                    OrderDate = order.CreatedAt ?? order.OrderDate,
                    CustomerName = customerName,
                    Status = order.Status,
                    TotalAmount = total,
                    Currency = currency,
                    RawJson = body
                });
            }

            return results;
        }

        private async Task<IReadOnlyList<IntegrationOrder>> FetchLatestWithoutDateFilterAsync(
            HttpClient client,
            CancellationToken ct)
        {
            var query = @"
query($limit: Int!, $page: Int!, $sort: [OrderSort!]) {
  orders(limit: $limit, page: $page, sort: $sort) {
    id
    number
    status
    orderDate
    createdAt
  }
}";

            var payload = new
            {
                query,
                variables = new
                {
                    limit = 50,
                    page = 1,
                    sort = new[] { "orderDate_DESC" }
                }
            };

            using var response = await client.PostAsJsonAsync(string.Empty, payload, JsonOptions, ct);
            if (!response.IsSuccessStatusCode)
            {
                var bodyError = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Centra fallback list failed: {Status} {Diagnostic}", response.StatusCode, IntegrationLogSanitizer.Diagnostic(bodyError));
                throw new IntegrationSourceException(IntegrationSource.Centra, (int)response.StatusCode, BuildHttpMessage(response.StatusCode, bodyError));
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            if (TryGetGraphQlErrorMessage(body, out var graphqlFallbackError))
            {
                _logger.LogWarning("Centra fallback list GraphQL error: {Error}", graphqlFallbackError);
                throw new IntegrationSourceException(IntegrationSource.Centra, (int)response.StatusCode, "GraphQL: " + graphqlFallbackError);
            }

            var parsed = JsonSerializer.Deserialize<CentraOrdersResponse>(body, JsonOptions);
            var orders = parsed?.Data?.Orders ?? new List<CentraOrder>();
            if (orders.Count == 0)
                return new List<IntegrationOrder>();

            var results = new List<IntegrationOrder>();
            foreach (var order in orders)
            {
                var total = SumLineValue(order);
                var currency = GetLineCurrency(order);
                var customerName = BuildCustomerName(order);
                results.Add(new IntegrationOrder
                {
                    ExternalId = order.Id ?? string.Empty,
                    OrderNo = order.Number?.ToString(),
                    OrderDate = order.CreatedAt ?? order.OrderDate,
                    CustomerName = customerName,
                    Status = order.Status,
                    TotalAmount = total,
                    Currency = currency,
                    RawJson = body
                });
            }

            return results;
        }

        private sealed class CentraOrderResponse
        {
            [JsonPropertyName("data")]
            public CentraOrderData? Data { get; set; }
        }

        private sealed class CentraOrdersResponse
        {
            [JsonPropertyName("data")]
            public CentraOrdersData? Data { get; set; }
        }

        private sealed class CentraOrderData
        {
            [JsonPropertyName("order")]
            public CentraOrder? Order { get; set; }
        }

        private sealed class CentraOrdersData
        {
            [JsonPropertyName("orders")]
            public List<CentraOrder>? Orders { get; set; }
        }

        private sealed class CentraOrder
        {
            [JsonPropertyName("id")]
            public string? Id { get; set; }

            [JsonPropertyName("number")]
            public int? Number { get; set; }

            [JsonPropertyName("status")]
            public string? Status { get; set; }

            [JsonPropertyName("orderDate")]
            public System.DateTime? OrderDate { get; set; }

            [JsonPropertyName("createdAt")]
            public System.DateTime? CreatedAt { get; set; }

            [JsonPropertyName("shippingAddress")]
            public CentraAddress? ShippingAddress { get; set; }

            [JsonPropertyName("billingAddress")]
            public CentraAddress? BillingAddress { get; set; }

            [JsonPropertyName("lines")]
            public List<CentraLine>? Lines { get; set; }
        }

        private sealed class CentraAddress
        {
            [JsonPropertyName("firstName")]
            public string? FirstName { get; set; }

            [JsonPropertyName("lastName")]
            public string? LastName { get; set; }
        }

        private sealed class CentraLine
        {
            [JsonPropertyName("lineValue")]
            public CentraMoney? LineValue { get; set; }
        }

        private sealed class CentraMoney
        {
            [JsonPropertyName("value")]
            public decimal Value { get; set; }

            [JsonPropertyName("currency")]
            public CentraCurrency? Currency { get; set; }
        }

        private sealed class CentraCurrency
        {
            [JsonPropertyName("code")]
            public string? Code { get; set; }
        }

        private static decimal? SumLineValue(CentraOrder order)
        {
            if (order.Lines == null || order.Lines.Count == 0)
                return null;

            decimal total = 0m;
            var hasAny = false;
            foreach (var line in order.Lines)
            {
                if (line?.LineValue == null)
                    continue;
                total += line.LineValue.Value;
                hasAny = true;
            }

            return hasAny ? total : null;
        }

        private static string? GetLineCurrency(CentraOrder order)
        {
            if (order.Lines == null)
                return null;

            foreach (var line in order.Lines)
            {
                var code = line?.LineValue?.Currency?.Code;
                if (!string.IsNullOrWhiteSpace(code))
                    return code;
            }

            return null;
        }

        private static string? BuildCustomerName(CentraOrder order)
        {
            var first = order.ShippingAddress?.FirstName ?? order.BillingAddress?.FirstName;
            var last = order.ShippingAddress?.LastName ?? order.BillingAddress?.LastName;
            if (string.IsNullOrWhiteSpace(first) && string.IsNullOrWhiteSpace(last))
                return null;
            return string.Join(" ", new[] { first, last }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private static string BuildHttpMessage(System.Net.HttpStatusCode statusCode, string body)
        {
            return IntegrationLogSanitizer.HttpFailure(statusCode, body);
        }

        private static bool TryGetGraphQlErrorMessage(string body, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(body))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (!doc.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Array)
                    return false;

                var messages = new List<string>();
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                        messages.Add(msg.GetString() ?? string.Empty);
                }

                if (messages.Count == 0)
                    messages.Add("Unknown GraphQL error.");

                message = string.Join(" | ", messages);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }
}
