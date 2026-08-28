using System.Collections.Generic;
using System.Net;
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
    public class JeevesApiOrderSource : IOrderSourceClient
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<IntegrationOptions> _options;
        private readonly IJeevesAuthService _auth;
        private readonly ILogger<JeevesApiOrderSource> _logger;

        public JeevesApiOrderSource(
            IHttpClientFactory httpClientFactory,
            IOptions<IntegrationOptions> options,
            IJeevesAuthService auth,
            ILogger<JeevesApiOrderSource> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _auth = auth;
            _logger = logger;
        }

        public IntegrationSource Source => IntegrationSource.Jeeves;

        public async Task<IReadOnlyList<IntegrationOrder>> FetchOrdersAsync(IntegrationFetchRequest request, CancellationToken ct = default)
        {
            var config = _options.Value.Companies.Find(c => c.CompanyId == request.CompanyId);
            var source = config?.GetSource(IntegrationSource.Jeeves);
            if (source == null || string.IsNullOrWhiteSpace(source.BaseUrl) || string.IsNullOrWhiteSpace(source.AuthUrl))
                throw new IntegrationSourceException(IntegrationSource.Jeeves, null, "missing_config");

            if (request.JeevesCompanyCode is null)
                throw new IntegrationSourceException(IntegrationSource.Jeeves, null, "missing_company_code");

            var token = await _auth.GetAccessTokenAsync(
                cacheKey: $"{request.CompanyId}:jeeves",
                authUrl: source.AuthUrl,
                appId: source.AppId ?? string.Empty,
                appSecret: source.AppSecret ?? string.Empty,
                ct: ct);

            if (string.IsNullOrWhiteSpace(token))
                throw new IntegrationSourceException(IntegrationSource.Jeeves, 401, "auth_failed");

            var client = _httpClientFactory.CreateClient("Integration.Jeeves");
            client.BaseAddress = new System.Uri(source.BaseUrl.TrimEnd('/') + "/");

            if (!string.IsNullOrWhiteSpace(request.ExternalOrderId))
            {
                var response = await SendWithTokenAsync(client, token, request, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _auth.Invalidate($"{request.CompanyId}:jeeves");
                    token = await _auth.GetAccessTokenAsync(
                        cacheKey: $"{request.CompanyId}:jeeves",
                        authUrl: source.AuthUrl,
                        appId: source.AppId ?? string.Empty,
                        appSecret: source.AppSecret ?? string.Empty,
                        ct: ct);

                    if (string.IsNullOrWhiteSpace(token))
                        throw new IntegrationSourceException(IntegrationSource.Jeeves, 401, "auth_failed");

                    response = await SendWithTokenAsync(client, token, request, ct);
                }

                return await HandleSingleResponseAsync(response, request.ExternalOrderId, ct);
            }

            return await FetchAllPagesAsync(client, token, request, ct);
        }

        private static async Task<HttpResponseMessage> SendWithTokenAsync(HttpClient client, string token, IntegrationFetchRequest request, CancellationToken ct)
        {
            var query = BuildQueryString(request);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "orders" + query);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return await client.SendAsync(httpRequest, ct);
        }

        private static string BuildQueryString(IntegrationFetchRequest request, int? pageSize = null, int? pageNumber = null)
        {
            var sb = new StringBuilder();
            var hasAny = false;

            if (!string.IsNullOrWhiteSpace(request.ExternalOrderId))
            {
                sb.Append(hasAny ? "&" : "?");
                sb.Append("c_extordernr=");
                sb.Append(Uri.EscapeDataString(request.ExternalOrderId));
                hasAny = true;
            }
            else if (pageSize is not null && pageNumber is not null)
            {
                sb.Append(hasAny ? "&" : "?");
                sb.Append("c_pagesize=");
                sb.Append(pageSize.Value);
                sb.Append("&c_pagenumber=");
                sb.Append(pageNumber.Value);
                hasAny = true;
            }

            if (request.JeevesCompanyCode is not null)
            {
                sb.Append(hasAny ? "&" : "?");
                sb.Append("c_foretagkod=");
                sb.Append(Uri.EscapeDataString(request.JeevesCompanyCode.Value.ToString()));
                hasAny = true;
            }

            if (request.FromUtc is not null)
            {
                sb.Append(hasAny ? "&" : "?");
                sb.Append("c_orddatum_after=");
                sb.Append(Uri.EscapeDataString(request.FromUtc.Value.ToString("o")));
                hasAny = true;
            }

            if (request.ToUtc is not null)
            {
                sb.Append(hasAny ? "&" : "?");
                sb.Append("c_orddatum_before=");
                sb.Append(Uri.EscapeDataString(request.ToUtc.Value.ToString("o")));
            }

            return sb.ToString();
        }

        private async Task<IReadOnlyList<IntegrationOrder>> FetchAllPagesAsync(
            HttpClient client,
            string token,
            IntegrationFetchRequest request,
            CancellationToken ct)
        {
            const int pageSize = 50;
            const int maxPages = 1;
            var results = new List<IntegrationOrder>();
            string? refreshedToken;

            for (var page = 1; page <= maxPages; page++)
            {
                var response = await SendListWithTokenAsync(client, token, request, pageSize, page, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _auth.Invalidate($"{request.CompanyId}:jeeves");
                    refreshedToken = await _auth.GetAccessTokenAsync(
                        cacheKey: $"{request.CompanyId}:jeeves",
                        authUrl: _options.Value.Companies.Find(c => c.CompanyId == request.CompanyId)?.GetSource(IntegrationSource.Jeeves)?.AuthUrl ?? string.Empty,
                        appId: _options.Value.Companies.Find(c => c.CompanyId == request.CompanyId)?.GetSource(IntegrationSource.Jeeves)?.AppId ?? string.Empty,
                        appSecret: _options.Value.Companies.Find(c => c.CompanyId == request.CompanyId)?.GetSource(IntegrationSource.Jeeves)?.AppSecret ?? string.Empty,
                        ct: ct);

                    if (string.IsNullOrWhiteSpace(refreshedToken))
                        throw new IntegrationSourceException(IntegrationSource.Jeeves, 401, "auth_failed");

                    token = refreshedToken;
                    response = await SendListWithTokenAsync(client, token, request, pageSize, page, ct);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var bodyError = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Jeeves list failed: {Status} {Diagnostic}", response.StatusCode, IntegrationLogSanitizer.Diagnostic(bodyError));
                    throw new IntegrationSourceException(IntegrationSource.Jeeves, (int)response.StatusCode, BuildHttpMessage(response.StatusCode, bodyError));
                }

                var body = await response.Content.ReadAsStringAsync(ct);
                var orders = TryParseOrders(body, null);
                if (orders.Count == 0)
                    break;

                results.AddRange(orders);
            }

            if (results.Count >= pageSize * maxPages)
            {
                _logger.LogInformation("Jeeves list limited to {Max} orders.", pageSize * maxPages);
            }

            return results;
        }

        private static async Task<HttpResponseMessage> SendListWithTokenAsync(
            HttpClient client,
            string token,
            IntegrationFetchRequest request,
            int pageSize,
            int pageNumber,
            CancellationToken ct)
        {
            var query = BuildQueryString(request, pageSize, pageNumber);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "orders" + query);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return await client.SendAsync(httpRequest, ct);
        }

        private async Task<IReadOnlyList<IntegrationOrder>> HandleSingleResponseAsync(
            HttpResponseMessage response,
            string? externalOrderId,
            CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
            {
                var bodyError = await response.Content.ReadAsStringAsync(ct);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Jeeves order not found for c_extordernr {ExternalOrderId}.", externalOrderId);
                    return new List<IntegrationOrder>();
                }

                _logger.LogWarning("Jeeves API failed: {Status} {Diagnostic}", response.StatusCode, IntegrationLogSanitizer.Diagnostic(bodyError));
                throw new IntegrationSourceException(IntegrationSource.Jeeves, (int)response.StatusCode, BuildHttpMessage(response.StatusCode, bodyError));
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var orders = TryParseOrders(body, externalOrderId);
            if (orders.Count == 0)
            {
                _logger.LogWarning("Jeeves response parsed with 0 orders.");
            }

            return orders;
        }

        private static List<IntegrationOrder> TryParseOrders(string body, string? externalOrderId)
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
                        var order = MapOrder(item, body, externalOrderId);
                        if (order != null) results.Add(order);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (TryGetArray(root, "orders", out var ordersArray) || TryGetArray(root, "items", out ordersArray))
                    {
                        foreach (var item in ordersArray.EnumerateArray())
                        {
                            var order = MapOrder(item, body, externalOrderId);
                            if (order != null) results.Add(order);
                        }
                    }
                    else
                    {
                        var order = MapOrder(root, body, externalOrderId);
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

        private static IntegrationOrder? MapOrder(JsonElement element, string rawJson, string? fallbackExternalId)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;

            var externalId = GetString(element, "c_extordernr")
                             ?? GetString(element, "externalOrderId")
                             ?? GetString(element, "extOrderNo")
                             ?? GetString(element, "extOrderNr")
                             ?? GetString(element, "extorderNr")
                             ?? fallbackExternalId
                             ?? string.Empty;

            var orderNo = GetString(element, "c_ordernr")
                          ?? GetString(element, "orderNo")
                          ?? GetString(element, "ordernr")
                          ?? GetString(element, "orderNumber")
                          ?? GetString(element, "orderNr");

            var orderDate = GetDate(element, "orderDate")
                            ?? GetDate(element, "createdAt")
                            ?? GetDate(element, "created_at")
                            ?? GetDate(element, "ordDatum");

            var status = GetString(element, "ordStatBeskr")
                         ?? GetString(element, "status")
                         ?? GetString(element, "ordStat");

            return new IntegrationOrder
            {
                ExternalId = externalId,
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

        private static System.DateTime? GetDate(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
                return null;
            return System.DateTime.TryParse(el.GetString(), out var parsed) ? parsed : null;
        }

        private static string BuildHttpMessage(HttpStatusCode statusCode, string body)
        {
            return IntegrationLogSanitizer.HttpFailure(statusCode, body);
        }
    }
}
