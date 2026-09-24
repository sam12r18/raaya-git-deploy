using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitUpdateServiceTests
{
    [Fact]
    public async Task Dirty_working_tree_stops_before_upstream_or_pull()
    {
        var runner = RecordingGitProcessRunner.WithResults(
            new GitCommandResult(0, "? untracked.txt\0", string.Empty));

        var service = new GitUpdateService(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateFastForwardOnlyAsync(@"C:\repo", TestContext.Current.CancellationToken));

        Assert.Contains("working tree", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Single(runner.Calls);
        Assert.Equal("status --porcelain=v2 -z --untracked-files=all", runner.Calls[0]);
    }

    [Fact]
    public async Task Missing_upstream_stops_before_pull()
    {
        var runner = RecordingGitProcessRunner.WithResults(
            new GitCommandResult(0, string.Empty, string.Empty),
            new GitCommandResult(0, "main\n", string.Empty),
            new GitCommandResult(128, string.Empty, "fatal: no upstream configured"));

        var service = new GitUpdateService(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateFastForwardOnlyAsync(@"C:\repo", TestContext.Current.CancellationToken));

        Assert.Contains("upstream", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, runner.Calls.Count);
        Assert.DoesNotContain(runner.Calls, call => call.StartsWith("pull", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Detached_head_stops_before_upstream_or_pull()
    {
        var runner = RecordingGitProcessRunner.WithResults(
            new GitCommandResult(0, string.Empty, string.Empty),
            new GitCommandResult(0, "\n", string.Empty));

        var service = new GitUpdateService(runner);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateFastForwardOnlyAsync(@"C:\repo", TestContext.Current.CancellationToken));

        Assert.Contains("detached", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, runner.Calls.Count);
        Assert.DoesNotContain(runner.Calls, call => call.Contains("@{u}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Successful_update_uses_ff_only_and_returns_before_after_heads()
    {
        var runner = RecordingGitProcessRunner.WithResults(
            new GitCommandResult(0, string.Empty, string.Empty),
            new GitCommandResult(0, "main\n", string.Empty),
            new GitCommandResult(0, "origin/main\n", string.Empty),
            new GitCommandResult(0, "aaaaaaaa\n", string.Empty),
            new GitCommandResult(0, "Updating aaaaaaaa..bbbbbbbb\n", string.Empty),
            new GitCommandResult(0, "bbbbbbbb\n", string.Empty));

        var result = await new GitUpdateService(runner)
            .UpdateFastForwardOnlyAsync(@"C:\repo", TestContext.Current.CancellationToken);

        Assert.Equal(@"C:\repo", result.RepositoryPath);
        Assert.Equal("main", result.Branch);
        Assert.Equal("aaaaaaaa", result.OldHead);
        Assert.Equal("bbbbbbbb", result.NewHead);
        Assert.True(result.Changed);
        Assert.Equal(
            [
                "status --porcelain=v2 -z --untracked-files=all",
                "branch --show-current",
                "rev-parse --abbrev-ref --symbolic-full-name @{u}",
                "rev-parse HEAD",
                "pull --ff-only",
                "rev-parse HEAD"
            ],
            runner.Calls);
    }

    private sealed class RecordingGitProcessRunner
        : IGitProcessRunner
    {
        private readonly Queue<GitCommandResult> _results;

        private RecordingGitProcessRunner(IEnumerable<GitCommandResult> results) =>
            _results = new Queue<GitCommandResult>(results);

        public List<string> Calls { get; } = [];

        public static RecordingGitProcessRunner WithResults(params GitCommandResult[] results) =>
            new(results);

        public Task<GitCommandResult> RunAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls.Add(string.Join(' ', arguments));

            if (_results.Count == 0)
                throw new InvalidOperationException("No Git command result was configured for this test call.");

            return Task.FromResult(_results.Dequeue());
        }
    }
}
