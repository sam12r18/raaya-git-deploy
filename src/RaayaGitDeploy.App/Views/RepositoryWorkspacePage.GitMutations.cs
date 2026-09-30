using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using RaayaGitDeploy.Presentation.Workspace;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage
{
    private RepositoryCommitWorkflow CommitWorkflow =>
        ((App)Application.Current).Services.GetRequiredService<RepositoryCommitWorkflow>();

    private IReadOnlyList<ChangeItemViewModel> SelectedChanges() =>
        ChangesList.SelectedItems.Cast<ChangeItemViewModel>().ToArray();

    private async void StageSelected_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selected = SelectedChanges();
            if (selected.Count == 0)
                throw new InvalidOperationException("Select one or more changed paths before staging.");

            await CommitWorkflow.StageAsync(selected, CancellationToken.None);
            RefreshGitMutationSurface($"Staged {selected.Count} selected path(s).");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void UnstageSelected_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var selected = SelectedChanges();
            if (selected.Count == 0)
                throw new InvalidOperationException("Select one or more changed paths before unstaging.");

            await CommitWorkflow.UnstageAsync(selected, CancellationToken.None);
            RefreshGitMutationSurface($"Unstaged {selected.Count} selected path(s).");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void CommitStaged_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var message = CommitMessageTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(message))
                throw new InvalidOperationException("Enter a commit message before committing staged changes.");

            var sha = await CommitWorkflow.CommitAsync(message, CancellationToken.None);
            CommitMessageTextBox.Text = string.Empty;
            ChangesList.SelectedItems.Clear();
            RefreshGitMutationSurface($"Commit created: {sha[..Math.Min(8, sha.Length)]}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void RefreshGitMutationSurface(string status)
    {
        GitMutationStatus.Text = status;
        ChangesList.ItemsSource = null;
        ChangesList.ItemsSource = ViewModel.Changes;
        CommitsList.ItemsSource = null;
        CommitsList.ItemsSource = ViewModel.Commits;
    }
}
