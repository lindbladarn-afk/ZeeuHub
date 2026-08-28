using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.Sources
{
    public class OngoingOrderSource : IOrderSourceClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<IntegrationOptions> _options;
        private readonly ILogger<OngoingOrderSource> _logger;

        public OngoingOrderSource(
            IHttpClientFactory httpClientFactory,
            IOptions<IntegrationOptions> options,
            ILogger<OngoingOrderSource> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        public IntegrationSource Source => IntegrationSource.Ongoing;

        public async Task<IReadOnlyList<IntegrationOrder>> FetchOrdersAsync(IntegrationFetchRequest request, CancellationToken ct = default)
        {
            var config = _options.Value.Companies.Find(c => c.CompanyId == request.CompanyId);
            var source = config?.GetSource(IntegrationSource.Ongoing);
            if (source == null || string.IsNullOrWhiteSpace(source.BaseUrl))
                throw new IntegrationSourceException(IntegrationSource.Ongoing, null, "missing_config");

            if (string.IsNullOrWhiteSpace(source.Username) || string.IsNullOrWhiteSpace(source.Password))
                throw new IntegrationSourceException(IntegrationSource.Ongoing, null, "missing_credentials");

            if (string.IsNullOrWhiteSpace(request.ExternalOrderId))
                return Array.Empty<IntegrationOrder>();

            var goodsOwnerId = source.GoodsOwnerId;
            if (goodsOwnerId is null)
                throw new IntegrationSourceException(IntegrationSource.Ongoing, null, "missing_goods_owner_id");

            var client = _httpClientFactory.CreateClient("Integration.Ongoing");
            client.BaseAddress = new Uri(source.BaseUrl.TrimEnd('/') + "/");

            var authToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{source.Username}:{source.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authToken);
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var query = $"orders?goodsOwnerId={goodsOwnerId.Value}&orderNumber={Uri.EscapeDataString(request.ExternalOrderId)}";
            using var response = await client.GetAsync(query, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Ongoing order lookup failed: {Status} {Diagnostic}", response.StatusCode, IntegrationLogSanitizer.Diagnostic(body));
                throw new IntegrationSourceException(IntegrationSource.Ongoing, (int)response.StatusCode, BuildHttpMessage(response.StatusCode, body));
            }

            var orders = TryParseOrders(body);
            if (orders.Count == 0)
            {
                _logger.LogInformation("Ongoing order not found for orderNumber {OrderNo}.", request.ExternalOrderId);
            }

            return orders;
        }

        private static List<IntegrationOrder> TryParseOrders(string body)
        {
            var results = new List<IntegrationOrder>();
            if (string.IsNullOrWhiteSpace(body))
                return results;

            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in root.EnumerateArray())
                    {
                        var order = MapOrder(item, body);
                        if (order != null) results.Add(order);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetArray(root, "orders", out var ordersArray) || TryGetArray(root, "items", out ordersArray))
                    {
                        foreach (var item in ordersArray.EnumerateArray())
                        {
                            var order = MapOrder(item, body);
                            if (order != null) results.Add(order);
                        }
                    }
                    else
                    {
                        var order = MapOrder(root, body);
                        if (order != null) results.Add(order);
                    }
                }
            }
            catch (JsonException)
            {
                return results;
            }

            return results;
        }

        private static bool TryGetArray(JsonElement root, string name, out JsonElement array)
        {
            array = default;
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
                return false;
            array = el;
            return true;
        }

        private static IntegrationOrder? MapOrder(JsonElement element, string rawJson)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            var orderNo = GetString(element, "orderNumber")
                          ?? GetString(element, "orderNo")
                          ?? GetString(element, "orderNr")
                          ?? GetString(element, "ordernr");

            var status = GetString(element, "status")
                         ?? GetString(element, "orderStatus");

            var orderDate = GetDate(element, "orderDate")
                            ?? GetDate(element, "createdAt")
                            ?? GetDate(element, "created_at");

            return new IntegrationOrder
            {
                ExternalId = orderNo ?? string.Empty,
                OrderNo = orderNo,
                OrderDate = orderDate,
                Status = status,
                RawJson = rawJson
            };
        }

        private static string? GetString(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var el))
                return null;
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.GetRawText(),
                _ => null
            };
        }

        private static DateTime? GetDate(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
                return null;
            return DateTime.TryParse(el.GetString(), out var parsed) ? parsed : null;
        }

        private static string BuildHttpMessage(System.Net.HttpStatusCode statusCode, string body)
        {
            return IntegrationLogSanitizer.HttpFailure(statusCode, body);
        }
    }
}
