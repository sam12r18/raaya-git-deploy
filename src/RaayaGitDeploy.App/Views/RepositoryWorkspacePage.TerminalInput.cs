using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage
{
    private async void TerminalInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.C &&
            (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0)
        {
            e.Handled = true;
            try
            {
                if (_terminal.IsRunning)
                    await _terminal.SendAsync("\u0003", CancellationToken.None);
            }
            catch (Exception ex)
            {
                ShowError(ex.Message);
            }
            return;
        }

        if (e.Key != VirtualKey.Enter)
            return;

        e.Handled = true;
        TerminalSend_Click(sender, e);
    }
}
