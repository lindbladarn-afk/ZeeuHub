using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using WebApp.Models.Integration;
using WebApp.Services.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineJeevesApiClient : IFlowEngineJeevesApiClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<IntegrationOptions> _integrationOptions;
    private readonly IJeevesAuthService _jeevesAuthService;

    public FlowEngineJeevesApiClient(
        IHttpClientFactory httpClientFactory,
        IOptions<IntegrationOptions> integrationOptions,
        IJeevesAuthService jeevesAuthService)
    {
        _httpClientFactory = httpClientFactory;
        _integrationOptions = integrationOptions;
        _jeevesAuthService = jeevesAuthService;
    }

    public IntegrationSourceConfig ResolveConfig(Guid companyId, string errorContext)
    {
        var companyConfig = _integrationOptions.Value.Companies.FirstOrDefault(company => company.CompanyId == companyId);
        var source = companyConfig?.GetSource(IntegrationSource.Jeeves);
        if (source is null ||
            string.IsNullOrWhiteSpace(source.BaseUrl) ||
            string.IsNullOrWhiteSpace(source.AuthUrl) ||
            string.IsNullOrWhiteSpace(source.AppId) ||
            string.IsNullOrWhiteSpace(source.AppSecret))
        {
            throw new InvalidOperationException($"Jeeves integration maste ha BaseUrl, AuthUrl, AppId och AppSecret for {errorContext}.");
        }

        return source;
    }

    public string BuildRequestUri(string baseUrl, string relativePath, IReadOnlyDictionary<string, string?>? query)
    {
        var baseUri = new Uri(baseUrl.TrimEnd('/') + "/");
        var builder = new UriBuilder(new Uri(baseUri, relativePath.TrimStart('/')));

        if (query is not null)
        {
            builder.Query = string.Join(
                "&",
                query.Where(entry => !string.IsNullOrWhiteSpace(entry.Value))
                    .Select(entry => $"{Uri.EscapeDataString(entry.Key)}={Uri.EscapeDataString(entry.Value!)}"));
        }

        return builder.Uri.ToString();
    }

    public async Task<FlowEngineJeevesApiResponse> SendAuthorizedAsync(
        Guid companyId,
        IntegrationSourceConfig config,
        HttpMethod method,
        string uri,
        string? jsonBody,
        CancellationToken cancellationToken)
    {
        var token = await GetJeevesAccessTokenAsync(companyId, config, cancellationToken);
        var response = await SendWithJeevesTokenAsync(token, method, uri, jsonBody, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        _jeevesAuthService.Invalidate($"{companyId}:jeeves");
        token = await GetJeevesAccessTokenAsync(companyId, config, cancellationToken);
        return await SendWithJeevesTokenAsync(token, method, uri, jsonBody, cancellationToken);
    }

    private async Task<string> GetJeevesAccessTokenAsync(Guid companyId, IntegrationSourceConfig config, CancellationToken cancellationToken)
    {
        var token = await _jeevesAuthService.GetAccessTokenAsync(
            $"{companyId}:jeeves",
            config.AuthUrl!,
            config.AppId!,
            config.AppSecret!,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Kunde inte hamta access token for Jeeves.");

        return token;
    }

    private async Task<FlowEngineJeevesApiResponse> SendWithJeevesTokenAsync(
        string token,
        HttpMethod method,
        string uri,
        string? jsonBody,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("Integration.Jeeves");
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.AcceptEncoding.Add(new StringWithQualityHeaderValue("gzip"));

        if (!string.IsNullOrWhiteSpace(jsonBody))
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        return new FlowEngineJeevesApiResponse(response.StatusCode, body);
    }
}
