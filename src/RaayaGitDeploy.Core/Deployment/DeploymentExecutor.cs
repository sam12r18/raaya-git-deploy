namespace RaayaGitDeploy.Core.Deployment;

public enum DeploymentItemStatus
{
    Succeeded,
    Failed,
    Blocked
}

public sealed record DeploymentItemResult(
    DeploymentOperation Operation,
    DeploymentItemStatus Status,
    string? Error = null)
{
    public bool Succeeded => Status == DeploymentItemStatus.Succeeded;
}

public sealed record DeploymentResult(IReadOnlyList<DeploymentItemResult> Items)
{
    public bool Succeeded => Items.All(item => item.Succeeded);
}

public sealed class DeploymentExecutor(IRemoteTransport transport)
{
    public async Task<DeploymentResult> ExecuteAsync(
        ServerProfile profile,
        DeploymentPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.IsDryRun)
            throw new InvalidOperationException("A dry-run deployment plan cannot mutate the remote server.");

        var results = new List<DeploymentItemResult>(plan.Operations.Count);
        var uploads = plan.Operations.Where(operation => operation.Kind == DeploymentOperationKind.Upload).ToArray();
        var deletes = plan.Operations.Where(operation => operation.Kind == DeploymentOperationKind.Delete).ToArray();
        var skips = plan.Operations.Where(operation => operation.Kind == DeploymentOperationKind.Skip).ToArray();
        var uploadFailed = false;

        foreach (var operation in uploads)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await transport.UploadAsync(profile, operation.LocalPath, operation.RemotePath, cancellationToken);
                results.Add(new(operation, DeploymentItemStatus.Succeeded));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                uploadFailed = true;
                results.Add(new(operation, DeploymentItemStatus.Failed, exception.Message));
            }
        }

        foreach (var operation in skips)
            results.Add(new(operation, DeploymentItemStatus.Succeeded));

        if (uploadFailed)
        {
            foreach (var operation in deletes)
                results.Add(new(operation, DeploymentItemStatus.Blocked, "Delete blocked because at least one upload failed."));

            return new DeploymentResult(results);
        }

        foreach (var operation in deletes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await transport.DeleteAsync(profile, operation.RemotePath, cancellationToken);
                results.Add(new(operation, DeploymentItemStatus.Succeeded));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                results.Add(new(operation, DeploymentItemStatus.Failed, exception.Message));
            }
        }

        return new DeploymentResult(results);
    }
}
