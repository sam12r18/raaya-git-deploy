using System.Net;
using System.Text;
using RaayaGitDeploy.Android.Core.Api;
using RaayaGitDeploy.Android.Core.Security;

namespace RaayaGitDeploy.Android.Core.Tests.Api;

public sealed class CompanionDeploymentHttpClientTests
{
    [Fact]
    public async Task GetRepositoriesAsync_UsesBearerSession()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "[]");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") };
        var tokens = new FakeAccessTokenStore("session-token");
        var client = new CompanionDeploymentHttpClient(http, tokens);
        var repositories = await client.GetRepositoriesAsync(CancellationToken.None);
        Assert.Empty(repositories);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("session-token", handler.AuthorizationParameter);
        Assert.Equal("https://agent.example/api/companion/v1/repositories", handler.RequestUri?.AbsoluteUri);
        Assert.False(tokens.Cleared);
    }

    [Fact]
    public async Task GetRepositoriesAsync_Unauthorized_ClearsSessionAndRequiresAuthentication()
    {
        var handler = new RecordingHandler(HttpStatusCode.Unauthorized, "{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") };
        var tokens = new FakeAccessTokenStore("expired-token");
        var client = new CompanionDeploymentHttpClient(http, tokens);
        await Assert.ThrowsAsync<CompanionAuthenticationRequiredException>(() => client.GetRepositoriesAsync(CancellationToken.None));
        Assert.True(tokens.Cleared);
    }

    [Fact]
    public async Task GetRepositoriesAsync_RateLimited_PreservesSessionAndExposesRetryDelay()
    {
        var handler = new RecordingHandler(HttpStatusCode.TooManyRequests, "{}", TimeSpan.FromSeconds(30));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") };
        var tokens = new FakeAccessTokenStore("session-token");
        var client = new CompanionDeploymentHttpClient(http, tokens);
        var error = await Assert.ThrowsAsync<CompanionRateLimitedException>(() => client.GetRepositoriesAsync(CancellationToken.None));
        Assert.Equal(TimeSpan.FromSeconds(30), error.RetryAfter);
        Assert.False(tokens.Cleared);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetRepositoriesAsync_WithoutSession_DoesNotCallAgent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "[]");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") };
        var client = new CompanionDeploymentHttpClient(http, new FakeAccessTokenStore(null));
        await Assert.ThrowsAsync<CompanionAuthenticationRequiredException>(() => client.GetRepositoriesAsync(CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("token with spaces")]
    [InlineData("token\nwith-control")]
    public async Task GetRepositoriesAsync_MalformedSession_DoesNotCallAgent(string token)
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "[]");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") };
        var client = new CompanionDeploymentHttpClient(http, new FakeAccessTokenStore(token));
        await Assert.ThrowsAsync<CompanionAuthenticationRequiredException>(() => client.GetRepositoriesAsync(CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
        Assert.Null(handler.AuthorizationParameter);
    }

    [Fact]
    public async Task GetRepositoriesAsync_OversizedSession_DoesNotCallAgent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "[]");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") };
        var client = new CompanionDeploymentHttpClient(http, new FakeAccessTokenStore(new string('a', 8193)));
        await Assert.ThrowsAsync<CompanionAuthenticationRequiredException>(() => client.GetRepositoriesAsync(CancellationToken.None));
        Assert.Equal(0, handler.CallCount);
        Assert.Null(handler.AuthorizationParameter);
    }

    [Fact]
    public void Constructor_RejectsPlaintextAgentEndpointBeforeSessionCanBeSent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "[]");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://agent.example/") };
        var error = Assert.Throws<InvalidOperationException>(() => new CompanionDeploymentHttpClient(http, new FakeAccessTokenStore("session-token")));
        Assert.Contains("HTTPS", error.Message);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void Constructor_RejectsEndpointWithEmbeddedCredentials()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "[]");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://user:password@agent.example/") };
        var error = Assert.Throws<InvalidOperationException>(() => new CompanionDeploymentHttpClient(http, new FakeAccessTokenStore("session-token")));
        Assert.Contains("credentials", error.Message.ToLowerInvariant());
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public void Constructor_RejectsEndpointWithQueryBeforeSessionCanBeSent()
    {
        var handler = new RecordingHandler(HttpStatusCode.OK, "[]");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/?tenant=unsafe") };
        Assert.Throws<InvalidOperationException>(() => new CompanionDeploymentHttpClient(http, new FakeAccessTokenStore("session-token")));
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class FakeAccessTokenStore(string? token) : IAccessTokenStore
    {
        public bool Cleared { get; private set; }
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult(token);
        public Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken) { Cleared = true; return Task.CompletedTask; }
    }

    private sealed class RecordingHandler(HttpStatusCode statusCode, string body, TimeSpan? retryAfter = null) : HttpMessageHandler
    {
        public int CallCount { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }
        public Uri? RequestUri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            RequestUri = request.RequestUri;
            var response = new HttpResponseMessage(statusCode) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            if (retryAfter is not null) response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
            return Task.FromResult(response);
        }
    }
}
