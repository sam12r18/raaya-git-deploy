using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage
{
    private (int Columns, int Rows)? _lastTerminalSize;

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

    private async void TerminalSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_terminal.IsRunning)
            return;

        // Consolas at 13px is roughly 8x17 device-independent pixels per cell.
        // Keep the estimate conservative so wrapping is stable while the pane is resized.
        var columns = Math.Max(20, (int)Math.Floor(e.NewSize.Width / 8d));
        var rows = Math.Max(5, (int)Math.Floor(e.NewSize.Height / 17d));
        var size = (Columns: columns, Rows: rows);
        if (_lastTerminalSize == size)
            return;

        _lastTerminalSize = size;
        try
        {
            await _terminal.ResizeAsync(columns, rows, CancellationToken.None);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }
}
