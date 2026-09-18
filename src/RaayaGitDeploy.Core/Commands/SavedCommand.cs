namespace RaayaGitDeploy.Core.Commands;

public sealed record SavedCommand(
    string Id,
    string Name,
    string CommandText,
    string? WorkingDirectoryOverride);
