using RaayaGitDeploy.Infrastructure.GitCli;

namespace RaayaGitDeploy.Infrastructure.Tests.GitCli;

public sealed class GitProcessRunnerTests
{
    [Fact]
    public async Task RunAsync_GitVersion_ReturnsSuccessfulResult()
    {
        var runner = new GitProcessRunner();

        var result = await runner.RunAsync(
            Directory.GetCurrentDirectory(),
            new[] { "--version" },
            CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("git version ", result.StandardOutput.Trim());
        Assert.True(string.IsNullOrWhiteSpace(result.StandardError));
    }
}
