namespace RaayaGitDeploy.Core.Deployment;

public sealed record DeploymentPlanContext(
    string RepositoryPath,
    string Branch,
    string? FromHead,
    string ToHead,
    string ServerProfileId);
