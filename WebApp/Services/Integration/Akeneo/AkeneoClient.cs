using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.Akeneo
{
    public class AkeneoClient : IAkeneoClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<AkeneoOptions> _options;
        private readonly ILogger<AkeneoClient> _logger;

        public AkeneoClient(
            IHttpClientFactory httpClientFactory,
            IOptions<AkeneoOptions> options,
            ILogger<AkeneoClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
            _logger = logger;
        }

        public async Task<IReadOnlyList<AkeneoProduct>> FetchProductsAsync(int limit, CancellationToken ct = default)
        {
            var opt = _options.Value;
            if (!opt.Enabled || string.IsNullOrWhiteSpace(opt.BaseUrl))
                throw new IntegrationSourceException(IntegrationSource.Akeneo, null, "missing_config");

            var token = await GetAccessTokenAsync(ct);
            if (string.IsNullOrWhiteSpace(token))
                throw new IntegrationSourceException(IntegrationSource.Akeneo, 401, "auth_failed");

            limit = limit <= 0 ? opt.PageSize : Math.Clamp(limit, 1, 1000);
            var pageSize = Math.Clamp(opt.PageSize, 1, 100);

            var client = _httpClientFactory.CreateClient("Integration.Akeneo");
            var baseUrl = opt.BaseUrl.TrimEnd('/');
            var nextUrl = $"{baseUrl}/api/rest/v1/products?limit={pageSize}";

            var results = new List<AkeneoProduct>();

            while (!string.IsNullOrWhiteSpace(nextUrl) && results.Count < limit)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                using var response = await client.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Akeneo list failed: {Status} {Diagnostic}", response.StatusCode, IntegrationLogSanitizer.Diagnostic(body));
                    throw new IntegrationSourceException(IntegrationSource.Akeneo, (int)response.StatusCode, BuildHttpMessage(response.StatusCode, body));
                }

                var json = await response.Content.ReadAsStringAsync(ct);
                var (items, next) = ParseProducts(json);
                results.AddRange(items);
                nextUrl = next;
            }

            return results.Take(limit).ToList();
        }

        public async Task<IReadOnlyList<AkeneoProduct>> FetchProductsBySkusAsync(IReadOnlyList<string> skus, int limit, CancellationToken ct = default)
        {
            if (skus == null || skus.Count == 0)
                return Array.Empty<AkeneoProduct>();

            var normalized = skus
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (normalized.Count == 0)
                return Array.Empty<AkeneoProduct>();

            var products = await FetchProductsAsync(Math.Max(limit, normalized.Count), ct);
            return products
                .Where(product => !string.IsNullOrWhiteSpace(product.Identifier) && normalized.Contains(product.Identifier))
                .Take(limit <= 0 ? normalized.Count : limit)
                .ToList();
        }

        private async Task<string?> GetAccessTokenAsync(CancellationToken ct)
        {
            var opt = _options.Value;
            if (string.IsNullOrWhiteSpace(opt.ClientId) ||
                string.IsNullOrWhiteSpace(opt.ClientSecret) ||
                string.IsNullOrWhiteSpace(opt.Username) ||
                string.IsNullOrWhiteSpace(opt.Password) ||
                string.IsNullOrWhiteSpace(opt.BaseUrl))
            {
                throw new IntegrationSourceException(IntegrationSource.Akeneo, null, "missing_credentials");
            }

            var client = _httpClientFactory.CreateClient("Integration.Akeneo");
            var tokenUrl = $"{opt.BaseUrl.TrimEnd('/')}/api/oauth/v1/token";

            var payload = new Dictionary<string, string>
            {
                ["grant_type"] = "password",
                ["client_id"] = opt.ClientId,
                ["client_secret"] = opt.ClientSecret,
                ["username"] = opt.Username,
                ["password"] = opt.Password
            };

            using var response = await client.PostAsync(tokenUrl, new FormUrlEncodedContent(payload), ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Akeneo auth failed: {Status}", response.StatusCode);
                throw new IntegrationSourceException(IntegrationSource.Akeneo, (int)response.StatusCode, BuildHttpMessage(response.StatusCode, body));
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var tokenEl))
            {
                return tokenEl.GetString();
            }

            _logger.LogWarning("Akeneo auth response missing access_token.");
            return null;
        }

        private static (List<AkeneoProduct> Items, string? NextUrl) ParseProducts(string json)
        {
            var list = new List<AkeneoProduct>();
            string? next = null;

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("_embedded", out var embedded) &&
                embedded.TryGetProperty("items", out var items) &&
                items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    var attributes = ExtractAttributes(item);
                    var product = new AkeneoProduct
                    {
                        Identifier = GetString(item, "identifier"),
                        Family = GetString(item, "family"),
                        Updated = GetString(item, "updated"),
                        Enabled = GetBool(item, "enabled"),
                        Name = ExtractName(item),
                        Attributes = attributes
                    };
                    ApplyKnownAttributes(product, attributes);
                    list.Add(product);
                }
            }

            if (root.TryGetProperty("_links", out var links) &&
                links.TryGetProperty("next", out var nextLink) &&
                nextLink.TryGetProperty("href", out var href) &&
                href.ValueKind == JsonValueKind.String)
            {
                next = href.GetString();
            }

            return (list, next);
        }

        private static string? ExtractName(JsonElement item)
        {
            if (!item.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Object)
                return null;

            if (!values.TryGetProperty("name", out var nameValues) || nameValues.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var entry in nameValues.EnumerateArray())
            {
                if (entry.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.String)
                    return data.GetString();
            }

            return null;
        }

        private static Dictionary<string, string> ExtractAttributes(JsonElement item)
        {
            var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (!item.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Object)
                return attributes;

            foreach (var property in values.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                    continue;

                var preferred = SelectPreferredValue(property.Value);
                var normalized = NormalizeAttributeValue(preferred);
                if (normalized is not null)
                    attributes[property.Name] = normalized;
            }

            return attributes;
        }

        private static JsonElement? SelectPreferredValue(JsonElement candidates)
        {
            JsonElement? fallback = null;

            foreach (var candidate in candidates.EnumerateArray())
            {
                if (!candidate.TryGetProperty("data", out var data))
                    continue;

                if (fallback is null)
                    fallback = data.Clone();

                var locale = candidate.TryGetProperty("locale", out var localeElement) && localeElement.ValueKind == JsonValueKind.String
                    ? localeElement.GetString()
                    : null;
                var scope = candidate.TryGetProperty("scope", out var scopeElement) && scopeElement.ValueKind == JsonValueKind.String
                    ? scopeElement.GetString()
                    : null;

                if (string.Equals(locale, "sv_SE", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrWhiteSpace(scope))
                {
                    return data.Clone();
                }

                if (string.IsNullOrWhiteSpace(locale) && string.IsNullOrWhiteSpace(scope))
                    return data.Clone();
            }

            return fallback;
        }

        private static string? NormalizeAttributeValue(JsonElement? dataElement)
        {
            if (dataElement is null)
                return null;

            var data = dataElement.Value;
            return data.ValueKind switch
                {
                    JsonValueKind.String => data.GetString(),
                    JsonValueKind.Number => data.GetRawText(),
                    JsonValueKind.True => "1",
                    JsonValueKind.False => string.Empty,
                    JsonValueKind.Null => string.Empty,
                    JsonValueKind.Array => string.Join(",", data.EnumerateArray()
                        .Select(NormalizeArrayValue)
                        .Where(value => value is not null)),
                    JsonValueKind.Object => JsonSerializer.Serialize(data, JsonOptions),
                    _ => null
                };
        }

        private static string? NormalizeArrayValue(JsonElement value)
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.True => "1",
                JsonValueKind.False => string.Empty,
                JsonValueKind.Object when value.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String => code.GetString(),
                JsonValueKind.Object when value.TryGetProperty("identifier", out var identifier) && identifier.ValueKind == JsonValueKind.String => identifier.GetString(),
                JsonValueKind.Object => JsonSerializer.Serialize(value, JsonOptions),
                _ => null
            };
        }

        private static void ApplyKnownAttributes(AkeneoProduct product, IReadOnlyDictionary<string, string> attributes)
        {
            product.ArtNr = GetAttribute(attributes, "ArtNr") ?? product.Identifier;
            product.ArtBeskr = GetAttribute(attributes, "ArtBeskr");
            product.ArtBeskrSpec = GetAttribute(attributes, "ArtBeskrSpec");
            product.ArtKat = GetAttribute(attributes, "ArtKat");
            product.ArtNrEan = GetAttribute(attributes, "ArtNrEAN");
            product.ArtRitnNr = GetAttribute(attributes, "ArtRitnNr");
            product.VaruGruppKod = GetAttribute(attributes, "VaruGruppKod");
            product.ShopifySync = GetAttribute(attributes, "jeeves_synk") ?? GetAttribute(attributes, "shopify_sync");
            product.Directive = GetAttribute(attributes, "directive");
            product.MainImage = GetAttribute(attributes, "main_image");
            product.WebBeskr = GetAttribute(attributes, "webBeskr");
            product.DescriptionLong = GetAttribute(attributes, "q_jis_artbeskr_long");
        }

        private static string? GetAttribute(IReadOnlyDictionary<string, string> attributes, string key)
            => attributes.TryGetValue(key, out var value) ? value : null;

        private static string? GetString(JsonElement item, string property)
        {
            return item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static bool? GetBool(JsonElement item, string property)
        {
            return item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True
                ? true
                : item.TryGetProperty(property, out value) && value.ValueKind == JsonValueKind.False
                    ? false
                    : null;
        }

        private static string BuildHttpMessage(HttpStatusCode statusCode, string body)
        {
            return IntegrationLogSanitizer.HttpFailure(statusCode, body);
        }
    }
}
