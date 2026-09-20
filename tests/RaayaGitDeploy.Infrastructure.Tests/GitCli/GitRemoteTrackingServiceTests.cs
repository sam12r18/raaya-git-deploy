using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRemoteTrackingServiceTests
{
    [Fact]
    public async Task FetchAsync_UsesOptionTerminatorBeforeRemoteName()
    {
        var runner = new RecordingRunner();
        var service = new GitRemoteTrackingService(runner);

        await service.FetchAsync("C:/repo", "origin", CancellationToken.None);

        Assert.Contains(runner.Calls, call => call.Arguments.SequenceEqual(new[] { "fetch", "--prune", "--", "origin" }));
    }

    [Fact]
    public async Task GetRemoteRevisionAsync_UsesFullyQualifiedRemoteRef()
    {
        var runner = new RecordingRunner();
        var service = new GitRemoteTrackingService(runner);

        var revision = await service.GetRemoteRevisionAsync("C:/repo", "origin", "main", CancellationToken.None);

        Assert.Equal("abc123", revision);
        Assert.Contains(runner.Calls, call => call.Arguments.Contains("refs/remotes/origin/main^{commit}"));
    }

    [Fact]
    public async Task FetchAsync_RejectsOptionLikeRemoteName()
    {
        var service = new GitRemoteTrackingService(new RecordingRunner());
        await Assert.ThrowsAsync<ArgumentException>(() => service.FetchAsync("C:/repo", "--upload-pack=evil", CancellationToken.None));
    }

    private sealed class RecordingRunner : IGitProcessRunner
    {
        public List<(string WorkingDirectory, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public Task<GitCommandResult> RunAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Calls.Add((workingDirectory, arguments));
            var output = arguments.Contains("--show-toplevel") ? "C:/repo\n" : arguments.Contains("--verify") ? "abc123\n" : string.Empty;
            return Task.FromResult(new GitCommandResult(0, output, string.Empty));
        }
    }
}
