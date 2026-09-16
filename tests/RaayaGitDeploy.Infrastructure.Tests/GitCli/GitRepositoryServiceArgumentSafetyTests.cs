using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryServiceArgumentSafetyTests
{
    [Fact]
    public async Task GetChangesSinceAsync_ResolvesUserControlledBaseRefBeforeDiff()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var runner = new RecordingRunner();
        var service = new GitRepositoryService(runner);

        await service.GetChangesSinceAsync(
            @"I:\Projects\sample",
            new GitComparisonRequest("--output=unexpected"),
            cancellationToken);

        Assert.Contains(
            runner.Commands,
            command => command.SequenceEqual(new[]
            {
                "rev-parse", "--verify", "--end-of-options", "--output=unexpected^{commit}"
            }));

        var diffArguments = Assert.Single(runner.Commands, command => command.Count > 0 && command[0] == "diff");
        Assert.Contains("0123456789abcdef0123456789abcdef01234567...HEAD", diffArguments);
        Assert.DoesNotContain("--output=unexpected...HEAD", diffArguments);
    }

    private sealed class RecordingRunner : IGitProcessRunner
    {
        public List<List<string>> Commands { get; } = [];

        public Task<GitCommandResult> RunAsync(
            string workingDirectory,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Commands.Add(arguments.ToList());

            if (arguments.SequenceEqual(new[] { "rev-parse", "--show-toplevel" }))
            {
                return Task.FromResult(new GitCommandResult(0, @"I:\Projects\sample", string.Empty));
            }

            if (arguments.SequenceEqual(new[]
                {
                    "rev-parse", "--verify", "--end-of-options", "--output=unexpected^{commit}"
                }))
            {
                return Task.FromResult(new GitCommandResult(
                    0,
                    "0123456789abcdef0123456789abcdef01234567",
                    string.Empty));
            }

            return Task.FromResult(new GitCommandResult(0, string.Empty, string.Empty));
        }
    }
}
