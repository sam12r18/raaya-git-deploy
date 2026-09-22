using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage
{
    private void TerminalInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != VirtualKey.Enter)
            return;

        e.Handled = true;
        TerminalSend_Click(sender, e);
    }
}
