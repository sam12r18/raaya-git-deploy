using RaayaGitDeploy.Core.Companion;

namespace RaayaGitDeploy.Android.Core.Deployment;

/// <summary>
/// Mobile-safe deployment workflow. The companion only works with agent-issued repository/profile IDs
/// and preview IDs; host credentials and destructive transport details never cross this boundary.
/// </summary>
public sealed class CompanionDeploymentWorkflow
{
    private readonly ICompanionDeploymentApi _api;

    public CompanionDeploymentWorkflow(ICompanionDeploymentApi api) =>
        _api = api ?? throw new ArgumentNullException(nameof(api));

    public CompanionRepository? SelectedRepository { get; private set; }
    public CompanionDeploymentProfile? SelectedProfile { get; private set; }
    public IReadOnlyList<CompanionRepository> Repositories { get; private set; } = [];
    public IReadOnlyList<CompanionDeploymentProfile> Profiles { get; private set; } = [];
    public CompanionDeploymentPreview? Preview { get; private set; }

    public async Task LoadRepositoriesAsync(CancellationToken cancellationToken)
    {
        Repositories = await _api.GetRepositoriesAsync(cancellationToken).ConfigureAwait(false);
        SelectedRepository = null;
        SelectedProfile = null;
        Profiles = [];
        Preview = null;
    }

    public async Task SelectRepositoryAsync(string repositoryId, CancellationToken cancellationToken)
    {
        SelectedRepository = Repositories.FirstOrDefault(item => item.Id == repositoryId)
            ?? throw new InvalidOperationException("Select an authorized repository returned by the companion agent.");

        Profiles = await _api.GetProfilesAsync(SelectedRepository.Id, cancellationToken).ConfigureAwait(false);
        SelectedProfile = null;
        Preview = null;
    }

    public void SelectProfile(string profileId)
    {
        SelectedProfile = Profiles.FirstOrDefault(item => item.Id == profileId)
            ?? throw new InvalidOperationException("Select an authorized deployment profile returned by the companion agent.");
        Preview = null;
    }

    public async Task<CompanionDeploymentPreview> DryRunAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var repository = SelectedRepository ?? throw new InvalidOperationException("Select a repository before Dry Run.");
        var profile = SelectedProfile ?? throw new InvalidOperationException("Select a deployment profile before Dry Run.");
        if (paths.Count == 0 || paths.Any(string.IsNullOrWhiteSpace))
            throw new InvalidOperationException("Select at least one valid repository path before Dry Run.");

        Preview = await _api.DryRunAsync(
            new CompanionDeploymentRequest(repository.Id, profile.Id, paths, DryRun: true),
            cancellationToken).ConfigureAwait(false);
        return Preview;
    }

    public async Task<CompanionDeploymentRun> StartDeploymentAsync(bool confirmed, CancellationToken cancellationToken)
    {
        var preview = Preview ?? throw new InvalidOperationException("Run and review Dry Run before deployment.");
        var profile = SelectedProfile ?? throw new InvalidOperationException("Select a deployment profile before deployment.");
        if (profile.RequiresConfirmation && !confirmed)
            throw new InvalidOperationException("This deployment profile requires explicit confirmation.");

        return await _api.StartDeploymentAsync(preview.Id, confirmed, cancellationToken).ConfigureAwait(false);
    }
}
