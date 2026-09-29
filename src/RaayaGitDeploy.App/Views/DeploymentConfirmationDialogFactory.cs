using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using RaayaGitDeploy.Presentation.Deployment;

namespace RaayaGitDeploy.App.Views;

internal static class DeploymentConfirmationDialogFactory
{
    public static ContentDialog Create(XamlRoot xamlRoot, DeploymentReviewSnapshot review)
    {
        ArgumentNullException.ThrowIfNull(xamlRoot);
        ArgumentNullException.ThrowIfNull(review);

        var confirmation = DeploymentReviewConfirmation.From(review);
        var detail = new TextBlock
        {
            Text = confirmation.Detail,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            FontFamily = new FontFamily("Consolas")
        };

        var content = new StackPanel { Spacing = 10 };
        if (confirmation.HasDestructiveOperations)
        {
            content.Children.Add(new InfoBar
            {
                IsOpen = true,
                IsClosable = false,
                Severity = InfoBarSeverity.Warning,
                Title = "Remote deletion is part of this reviewed plan",
                Message = "Only continue if the target profile, remote root, and destructive paths below are correct."
            });
        }
        content.Children.Add(detail);

        return new ContentDialog
        {
            XamlRoot = xamlRoot,
            Title = confirmation.Title,
            Content = new ScrollViewer
            {
                MaxHeight = 460,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = content
            },
            PrimaryButtonText = confirmation.PrimaryActionText,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close
        };
    }
}
