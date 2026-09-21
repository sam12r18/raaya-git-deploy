using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaayaGitDeploy.Android.Core.Security;
using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Api;

/// <summary>
/// Authenticated companion API client. Only a short-lived access token crosses the mobile boundary;
/// SSH/Git/deployment credentials remain on the controlled agent.
/// </summary>
public sealed class CompanionDeploymentHttpClient : ICompanionDeploymentApi
{
    private const string ApiRoot = "api/companion/v1/";
    private readonly HttpClient _httpClient;
    private readonly IAccessTokenStore _tokenStore;

    public CompanionDeploymentHttpClient(HttpClient httpClient, IAccessTokenStore tokenStore)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
    }

    public Task<IReadOnlyList<CompanionRepository>> GetRepositoriesAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<CompanionRepository>>(HttpMethod.Get, $"{ApiRoot}repositories", null, cancellationToken);

    public Task<IReadOnlyList<CompanionDeploymentProfile>> GetProfilesAsync(string repositoryId, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<CompanionDeploymentProfile>>(HttpMethod.Get, $"{ApiRoot}repositories/{Escape(repositoryId)}/profiles", null, cancellationToken);

    public Task<CompanionDeploymentPreview> DryRunAsync(CompanionDeploymentRequest request, CancellationToken cancellationToken) =>
        SendAsync<CompanionDeploymentPreview>(HttpMethod.Post, $"{ApiRoot}deployments/dry-run", request, cancellationToken);

    public Task<CompanionDeploymentRun> StartDeploymentAsync(string previewId, bool confirmed, CancellationToken cancellationToken) =>
        SendAsync<CompanionDeploymentRun>(HttpMethod.Post, $"{ApiRoot}deployments", new StartDeploymentRequest(previewId, confirmed), cancellationToken);

    public Task<CompanionDeploymentRun> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken) =>
        SendAsync<CompanionDeploymentRun>(HttpMethod.Get, $"{ApiRoot}deployments/{Escape(deploymentId)}", null, cancellationToken);

    public Task<IReadOnlyList<CompanionDeploymentRun>> GetDeploymentHistoryAsync(string repositoryId, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<CompanionDeploymentRun>>(HttpMethod.Get, $"{ApiRoot}repositories/{Escape(repositoryId)}/deployments", null, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string relativeUri, object? body, CancellationToken cancellationToken)
    {
        var token = await _tokenStore.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(token))
            throw new CompanionAuthenticationRequiredException();

        using var request = new HttpRequestMessage(method, relativeUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await _tokenStore.ClearAsync(cancellationToken).ConfigureAwait(false);
            throw new CompanionAuthenticationRequiredException();
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Companion API returned an empty response.");
    }

    private static string Escape(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Identifier is required.", nameof(value));
        return Uri.EscapeDataString(value);
    }

    private sealed record StartDeploymentRequest(string PreviewId, bool Confirmed);
}

public sealed class CompanionAuthenticationRequiredException : InvalidOperationException
{
    public CompanionAuthenticationRequiredException() : base("A valid companion session is required.") { }
}
