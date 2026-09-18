namespace RaayaGitDeploy.Core.Deployment;

public enum DeploymentQueueSource
{
    GitSelection,
    ManualFile,
    ManualFolder
}

public sealed record DeploymentQueueItem(
    string LocalPath,
    DeploymentQueueSource Source,
    string? RemotePath = null);
