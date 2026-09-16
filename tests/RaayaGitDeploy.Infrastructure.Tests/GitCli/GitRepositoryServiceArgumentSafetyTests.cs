using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitRepositoryServiceArgumentSafetyTests
{
    [Fact]
    public async Task GetChangesSinceAsync_SeparatesOptionsFromUserControlledBaseRef()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var runner = new RecordingRunner();
        var service = new GitRepositoryService(runner);

        await service.GetChangesSinceAsync(
            @"I:\Projects\sample",
            new GitComparisonRequest("--output=unexpected"),
            cancellationToken);

        var diffArguments = Assert.Single(runner.Commands, command => command.Count > 0 && command[0] == "diff");
        var separatorIndex = diffArguments.IndexOf("--");
        var comparisonIndex = diffArguments.IndexOf("--output=unexpected...HEAD");

        Assert.True(separatorIndex >= 0, "git diff must include an option terminator before the user-controlled comparison ref.");
        Assert.True(separatorIndex < comparisonIndex, "The option terminator must precede the user-controlled comparison ref.");
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

            return Task.FromResult(new GitCommandResult(0, string.Empty, string.Empty));
        }
    }
}

internal static class GitArgumentListTestExtensions
{
    public static int IndexOf(this IReadOnlyList<string> arguments, string value)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], value, StringComparison.Ordinal)) return index;
        }

        return -1;
    }
}
