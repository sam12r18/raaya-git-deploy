namespace RaayaGitDeploy.Infrastructure.GitCli;

public sealed record GitCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
