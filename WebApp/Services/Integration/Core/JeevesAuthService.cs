using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WebApp.Services.Integration
{
    public class JeevesAuthService : IJeevesAuthService
    {
        private sealed class TokenEntry
        {
            public string Token { get; set; } = string.Empty;
            public DateTimeOffset ExpiresAtUtc { get; set; } = DateTimeOffset.UtcNow.AddMinutes(-1);
        }

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<JeevesAuthService> _logger;
        private readonly ConcurrentDictionary<string, TokenEntry> _cache = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public JeevesAuthService(IHttpClientFactory httpClientFactory, ILogger<JeevesAuthService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public async Task<string?> GetAccessTokenAsync(string cacheKey, string authUrl, string appId, string appSecret, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(authUrl) || string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(appSecret))
                return null;

            if (_cache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
                return cached.Token;

            var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                if (_cache.TryGetValue(cacheKey, out cached) && cached.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1))
                    return cached.Token;

                var json = await TryRequestTokenAsync(authUrl, new { appId, appSecret }, ct)
                           ?? await TryRequestTokenAsync(authUrl, new { applicationId = appId, applicationSecret = appSecret }, ct)
                           ?? await TryRequestTokenAsync(authUrl, new { ApplicationId = appId, ApplicationSecret = appSecret }, ct);

                if (json == null)
                    return null;

                if (!TryParseToken(json, out var token, out var expiresAt))
                {
                    _logger.LogWarning("Jeeves auth response missing token.");
                    return null;
                }

                _logger.LogInformation("Jeeves auth OK.");

                var entry = new TokenEntry
                {
                    Token = token,
                    ExpiresAtUtc = expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(30)
                };
                _cache[cacheKey] = entry;
                return entry.Token;
            }
            finally
            {
                gate.Release();
            }
        }

        public void Invalidate(string cacheKey)
        {
            _cache.TryRemove(cacheKey, out _);
        }

        private async Task<string?> TryRequestTokenAsync(string authUrl, object payload, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient("Integration.Jeeves.Auth");
            using var resp = await client.PostAsJsonAsync(authUrl, payload, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Jeeves auth failed: {Status}", resp.StatusCode);
                return null;
            }

            return json;
        }

        private static bool TryParseToken(string json, out string token, out DateTimeOffset? expiresAt)
        {
            token = string.Empty;
            expiresAt = null;

            var trimmed = json?.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
                return false;

            if (!trimmed.StartsWith("{") && !trimmed.StartsWith("["))
            {
                token = trimmed.Trim('"');
                return !string.IsNullOrWhiteSpace(token);
            }

            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;

            if (TryGetString(root, "accessToken", out token) ||
                TryGetString(root, "token", out token) ||
                TryGetString(root, "jwt", out token))
            {
                expiresAt = TryGetExpiry(root);
                return true;
            }

            if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
            {
                if (TryGetString(data, "accessToken", out token) ||
                    TryGetString(data, "token", out token))
                {
                    expiresAt = TryGetExpiry(data);
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetString(JsonElement root, string name, out string value)
        {
            value = string.Empty;
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.String)
                return false;
            value = el.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static DateTimeOffset? TryGetExpiry(JsonElement root)
        {
            if (root.TryGetProperty("expiresIn", out var expIn) && expIn.TryGetInt32(out var seconds))
                return DateTimeOffset.UtcNow.AddSeconds(seconds);
            if (root.TryGetProperty("expires_in", out expIn) && expIn.TryGetInt32(out seconds))
                return DateTimeOffset.UtcNow.AddSeconds(seconds);
            if (root.TryGetProperty("expiresAt", out var expAt) && expAt.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(expAt.GetString(), out var parsed))
                return parsed;
            if (root.TryGetProperty("expires_at", out expAt) && expAt.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(expAt.GetString(), out parsed))
                return parsed;
            return null;
        }
    }
}
