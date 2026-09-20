using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed record DryRunOperationItem(
    DeploymentOperationKind Kind,
    string LocalPath,
    string RemotePath,
    bool IsDestructive,
    bool IsProtected);

public sealed class DryRunSummaryViewModel
{
    private readonly DeploymentPlanner _planner;

    public DryRunSummaryViewModel(DeploymentPlanner planner)
    {
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
    }

    public IReadOnlyList<DryRunOperationItem> Operations { get; private set; } = Array.Empty<DryRunOperationItem>();
    public int UploadCount { get; private set; }
    public int DeleteCount { get; private set; }
    public int ProtectedSkipCount { get; private set; }
    public bool HasDestructiveOperations => DeleteCount > 0;
    public bool IsReady { get; private set; }
    public string? ErrorMessage { get; private set; }

    public bool Build(DeploymentProject project, IEnumerable<string> localPaths)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(localPaths);

        try
        {
            var plan = _planner.Plan(project, localPaths, dryRun: true);
            Operations = plan.Operations
                .Select(operation => new DryRunOperationItem(
                    operation.Kind,
                    operation.LocalPath,
                    operation.RemotePath,
                    operation.Kind == DeploymentOperationKind.Delete,
                    operation.Kind == DeploymentOperationKind.Skip))
                .ToArray();

            UploadCount = Operations.Count(item => item.Kind == DeploymentOperationKind.Upload);
            DeleteCount = Operations.Count(item => item.Kind == DeploymentOperationKind.Delete);
            ProtectedSkipCount = Operations.Count(item => item.IsProtected);
            ErrorMessage = null;
            IsReady = true;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            Operations = Array.Empty<DryRunOperationItem>();
            UploadCount = 0;
            DeleteCount = 0;
            ProtectedSkipCount = 0;
            IsReady = false;
            ErrorMessage = exception.Message;
            return false;
        }
    }
}
