using System.Net;
using RaayaGitDeploy.Android.Core.Api;
using RaayaGitDeploy.Android.Core.Security;

namespace RaayaGitDeploy.Android.Core.Tests.Api;

public sealed class CompanionDeploymentConfirmationTests
{
    [Fact]
    public async Task StartDeploymentAsync_WithoutExplicitConfirmation_DoesNotCallAgent()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://agent.example/") };
        var client = new CompanionDeploymentHttpClient(http, new StaticTokenStore("session-token"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.StartDeploymentAsync("preview-123", confirmed: false, CancellationToken.None));

        Assert.Contains("explicit confirmation", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, handler.CallCount);
    }

    private sealed class StaticTokenStore(string token) : IAccessTokenStore
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(token);
        public Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
