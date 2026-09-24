namespace RaayaGitDeploy.Core.Deployment;

public enum DeploymentQueueSource
{
    GitSelection,
    GitDetected,
    GeneratedRule,
    ManualFile,
    ManualFolder
}

public enum DeploymentQueueAction
{
    Upload,
    Delete
}

public sealed record DeploymentQueueItem(
    string LocalPath,
    DeploymentQueueSource Source,
    string? RemotePath = null,
    DeploymentQueueAction Action = DeploymentQueueAction.Upload);
