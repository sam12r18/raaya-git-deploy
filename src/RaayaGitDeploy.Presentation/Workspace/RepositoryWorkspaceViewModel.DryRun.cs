using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Workspace;

public sealed record DryRunPresentationSnapshot(
    string RemoteRoot,
    IReadOnlyList<DeploymentOperation> Operations,
    int UploadCount,
    int DeleteCount)
{
    public bool HasDestructiveOperations => DeleteCount > 0;
    public bool RequiresExplicitConfirmation => HasDestructiveOperations;
    public IReadOnlyList<string> DestructiveRemotePaths => Operations
        .Where(static operation => operation.Kind == DeploymentOperationKind.Delete)
        .Select(static operation => operation.RemotePath)
        .ToArray();
}

public partial class RepositoryWorkspaceViewModel
{
    public DryRunPresentationSnapshot PrepareDryRunPresentation(string remoteRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteRoot);
        var normalizedRemoteRoot = remoteRoot.Trim();
        var plan = PrepareDryRun(normalizedRemoteRoot);
        var operations = plan.Operations.ToArray();

        return new DryRunPresentationSnapshot(
            normalizedRemoteRoot,
            operations,
            operations.Count(static operation => operation.Kind == DeploymentOperationKind.Upload),
            operations.Count(static operation => operation.Kind == DeploymentOperationKind.Delete));
    }
}
