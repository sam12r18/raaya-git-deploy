using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.App.Views;

internal static class DeploymentConfirmationDialogFactory
{
    public static ContentDialog Create(XamlRoot xamlRoot, DeploymentReviewSnapshot review)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(review);

        var confirmation = DeploymentReviewConfirmation.From(review);
        return new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = confirmation.Title,
            Content = new ScrollViewer
            {
                MaxHeight = 420,
                Content = new TextBlock
                {
                    Text = confirmation.Detail,
                    TextWrapping = TextWrapping.Wrap,
                    IsTextSelectionEnabled = true
                }
            },
            PrimaryButtonText = confirmation.PrimaryActionText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
    }
}
