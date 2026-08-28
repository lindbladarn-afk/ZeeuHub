using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WebApp.Services.Application
{
    public sealed class OpenAiChatService : IOpenAiChatService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OpenAiOptions _options;

        public OpenAiChatService(IHttpClientFactory httpClientFactory, IOptions<OpenAiOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;

            if (string.IsNullOrWhiteSpace(_options.ApiKey))
                throw new InvalidOperationException("Missing OpenAI.ApiKey (Azure OpenAI key)");

            if (string.IsNullOrWhiteSpace(_options.Endpoint))
                throw new InvalidOperationException("Missing OpenAI.Endpoint");

            if (string.IsNullOrWhiteSpace(_options.Deployment))
                throw new InvalidOperationException("Missing OpenAI.Deployment");

            if (string.IsNullOrWhiteSpace(_options.ApiVersion))
                throw new InvalidOperationException("Missing OpenAI.ApiVersion");
        }

        public async Task<OpenAiChatResult> AskAsync(
            string userMessage,
            IReadOnlyList<OpenAiChatMessage>? history = null,
            CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                return new OpenAiChatResult
                {
                    Answer = string.Empty,
                    RawJson = null
                };
            }

            var client = _httpClientFactory.CreateClient("OpenAI");
            var timer = Stopwatch.StartNew();

            // Ensure endpoint base is correct (works with/without trailing slash)
            var baseEndpoint = _options.Endpoint.Trim();
            if (!baseEndpoint.EndsWith("/", StringComparison.Ordinal))
                baseEndpoint += "/";

            var requestUri =
                $"{baseEndpoint}openai/deployments/{Uri.EscapeDataString(_options.Deployment)}/chat/completions" +
                $"?api-version={Uri.EscapeDataString(_options.ApiVersion)}";

            // Build messages
            var messages = new List<object>();

            if (history != null)
            {
                foreach (var h in history)
                {
                    if (h == null) continue;

                    var role = string.IsNullOrWhiteSpace(h.Role) ? "user" : h.Role.Trim();
                    var content = h.Content ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(content))
                        continue;

                    messages.Add(new { role, content });
                }
            }

            messages.Add(new { role = "user", content = userMessage });

            var requestsStructuredJson = history?.Any(message =>
                message.Role.Equals("system", StringComparison.OrdinalIgnoreCase) &&
                message.Content.Contains("Output ONLY valid JSON", StringComparison.OrdinalIgnoreCase)) == true;

            var payload = new
            {
                messages,
                temperature = requestsStructuredJson ? 0.0 : 0.2,
                max_tokens = requestsStructuredJson ? 1200 : 900,
                response_format = requestsStructuredJson ? new { type = "json_object" } : null
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });
            var maxRetries = Math.Clamp(_options.MaxRetryCount, 0, 4);
            var retryCount = 0;
            string raw;

            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                request.Headers.Add("api-key", _options.ApiKey);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using var resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                raw = await resp.Content.ReadAsStringAsync(ct);

                if (resp.IsSuccessStatusCode)
                    break;

                if (retryCount >= maxRetries || !IsTransient(resp.StatusCode))
                {
                    throw new InvalidOperationException(
                        $"Azure OpenAI error {(int)resp.StatusCode} ({resp.ReasonPhrase}): {raw}");
                }

                retryCount++;
                var retryAfter = resp.Headers.RetryAfter?.Delta ??
                                 TimeSpan.FromMilliseconds(250 * Math.Pow(3, retryCount - 1));
                await Task.Delay(
                    retryAfter > TimeSpan.FromSeconds(5) ? TimeSpan.FromSeconds(5) : retryAfter,
                    ct);
            }

            // Extract choices[0].message.content
            string answer;
            int? promptTokens = null;
            int? completionTokens = null;
            int? totalTokens = null;
            using (var doc = JsonDocument.Parse(raw))
            {
                answer =
                    doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? string.Empty;

                if (doc.RootElement.TryGetProperty("usage", out var usage))
                {
                    if (usage.TryGetProperty("prompt_tokens", out var p) && p.TryGetInt32(out var pVal))
                        promptTokens = pVal;
                    if (usage.TryGetProperty("completion_tokens", out var c) && c.TryGetInt32(out var cVal))
                        completionTokens = cVal;
                    if (usage.TryGetProperty("total_tokens", out var t) && t.TryGetInt32(out var tVal))
                        totalTokens = tVal;
                }
            }

            // Clean common formatting that breaks SQL execution
            answer = CleanAnswer(answer);

            return new OpenAiChatResult
            {
                Answer = answer,
                RawJson = raw,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                TotalTokens = totalTokens,
                ModelDeployment = _options.Deployment,
                RetryCount = retryCount,
                DurationMs = timer.ElapsedMilliseconds
            };
        }

        private static bool IsTransient(HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.TooManyRequests ||
            statusCode == HttpStatusCode.RequestTimeout ||
            (int)statusCode >= 500;

        private static string CleanAnswer(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var s = text.Trim();

            // Remove markdown code fences if they appear
            s = s.Replace("```sql", "", StringComparison.OrdinalIgnoreCase)
                 .Replace("```", "", StringComparison.OrdinalIgnoreCase)
                 .Trim();

            // Sometimes models wrap with quotes
            s = s.Trim().Trim('"').Trim();

            return s;
        }
    }
}
