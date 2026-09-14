namespace RaayaGitDeploy.Infrastructure.GitCli;

public interface IGitProcessRunner
{
    Task<GitCommandResult> RunAsync(
        string workingDirectory,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}
