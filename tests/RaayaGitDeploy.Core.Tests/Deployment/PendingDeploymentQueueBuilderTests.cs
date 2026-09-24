using RaayaGitDeploy.Core.Deployment;
using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class PendingDeploymentQueueBuilderTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "raaya-queue-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void Git_changes_map_to_upload_and_delete_operations()
    {
        Directory.CreateDirectory(_root);
        var changes = new GitChange[]
        {
            new("new.php", GitChangeKind.Added),
            new("app.php", GitChangeKind.Modified),
            new("gone.php", GitChangeKind.Deleted),
            new("renamed.php", GitChangeKind.Renamed, "old.php")
        };

        var result = new PendingDeploymentQueueBuilder()
            .Build(_root, changes, new DeploymentRuleSet([]));

        Assert.Empty(result.Warnings);
        Assert.Equal(5, result.Items.Count);
        AssertQueueItem(result.Items, "new.php", DeploymentQueueAction.Upload, DeploymentQueueSource.GitDetected);
        AssertQueueItem(result.Items, "app.php", DeploymentQueueAction.Upload, DeploymentQueueSource.GitDetected);
        AssertQueueItem(result.Items, "gone.php", DeploymentQueueAction.Delete, DeploymentQueueSource.GitDetected);
        AssertQueueItem(result.Items, "renamed.php", DeploymentQueueAction.Upload, DeploymentQueueSource.GitDetected);
        AssertQueueItem(result.Items, "old.php", DeploymentQueueAction.Delete, DeploymentQueueSource.GitDetected);
    }

    [Fact]
    public void Generated_directory_adds_files_and_outranks_duplicate_git_upload()
    {
        var buildDirectory = Path.Combine(_root, "public", "build");
        Directory.CreateDirectory(buildDirectory);
        File.WriteAllText(Path.Combine(buildDirectory, "app.js"), "js");
        File.WriteAllText(Path.Combine(buildDirectory, "app.css"), "css");

        var changes = new GitChange[]
        {
            new("public/build/app.js", GitChangeKind.Modified)
        };

        var result = new PendingDeploymentQueueBuilder()
            .Build(_root, changes, new DeploymentRuleSet(["public/build"]));

        Assert.Empty(result.Warnings);
        Assert.Equal(2, result.Items.Count);
        AssertQueueItem(result.Items, "public/build/app.js", DeploymentQueueAction.Upload, DeploymentQueueSource.GeneratedRule);
        AssertQueueItem(result.Items, "public/build/app.css", DeploymentQueueAction.Upload, DeploymentQueueSource.GeneratedRule);
        Assert.Single(result.Items.Where(item =>
            string.Equals(item.LocalPath, FullPath("public/build/app.js"), StringComparison.OrdinalIgnoreCase) &&
            item.Action == DeploymentQueueAction.Upload));
    }

    [Fact]
    public void Missing_generated_path_returns_actionable_warning()
    {
        Directory.CreateDirectory(_root);

        var result = new PendingDeploymentQueueBuilder()
            .Build(_root, [], new DeploymentRuleSet(["public/build"]));

        Assert.Empty(result.Items);
        var warning = Assert.Single(result.Warnings);
        Assert.Contains("public/build", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void Generated_rule_cannot_escape_repository_root()
    {
        Directory.CreateDirectory(_root);

        Assert.Throws<InvalidOperationException>(() =>
            new PendingDeploymentQueueBuilder().Build(
                _root,
                [],
                new DeploymentRuleSet(["../outside"])));
    }

    private void AssertQueueItem(
        IReadOnlyList<DeploymentQueueItem> items,
        string relativePath,
        DeploymentQueueAction action,
        DeploymentQueueSource source)
    {
        Assert.Contains(items, item =>
            string.Equals(item.LocalPath, FullPath(relativePath), StringComparison.OrdinalIgnoreCase) &&
            item.Action == action &&
            item.Source == source);
    }

    private string FullPath(string relativePath) =>
        Path.GetFullPath(Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar)));

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
