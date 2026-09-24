namespace RaayaGitDeploy.Core.Deployment;

public enum DeploymentOperationKind
{
    Upload,
    Delete,
    Skip
}

public sealed record DeploymentOperation(
    DeploymentOperationKind Kind,
    string LocalPath,
    string RemotePath);

public sealed record DeploymentPlan(
    IReadOnlyList<DeploymentOperation> Operations,
    bool IsDryRun,
    DeploymentPlanContext? Context = null);
