using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace RaayaGitDeploy.App.Views;

public sealed partial class RepositoryWorkspacePage
{
    private (int Columns, int Rows)? _lastTerminalSize;
    private readonly List<string> _terminalInputHistory = [];
    private int _terminalInputHistoryIndex;
    private string _terminalInputDraft = string.Empty;

    private async void TerminalInput_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        var controlDown = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;

        if (e.Key == VirtualKey.C && controlDown)
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

        if (e.Key == VirtualKey.L && controlDown)
        {
            e.Handled = true;
            _terminal.ClearOutput();
            TerminalOutput.Text = string.Empty;
            TerminalScrollViewer.ChangeView(null, 0, null, disableAnimation: true);
            return;
        }

        if (e.Key is VirtualKey.Up or VirtualKey.Down)
        {
            e.Handled = true;
            NavigateTerminalInputHistory(e.Key == VirtualKey.Up ? -1 : 1);
            return;
        }

        if (e.Key != VirtualKey.Enter)
            return;

        e.Handled = true;
        var command = TerminalInput.Text.Trim();
        if (!string.IsNullOrWhiteSpace(command))
        {
            if (_terminalInputHistory.Count == 0 || !string.Equals(_terminalInputHistory[^1], command, StringComparison.Ordinal))
                _terminalInputHistory.Add(command);
            _terminalInputHistoryIndex = _terminalInputHistory.Count;
            _terminalInputDraft = string.Empty;
        }
        TerminalSend_Click(sender, e);
    }

    private void NavigateTerminalInputHistory(int delta)
    {
        if (_terminalInputHistory.Count == 0)
            return;

        if (delta < 0 && _terminalInputHistoryIndex == _terminalInputHistory.Count)
            _terminalInputDraft = TerminalInput.Text;

        _terminalInputHistoryIndex = Math.Clamp(
            _terminalInputHistoryIndex + delta,
            0,
            _terminalInputHistory.Count);

        TerminalInput.Text = _terminalInputHistoryIndex == _terminalInputHistory.Count
            ? _terminalInputDraft
            : _terminalInputHistory[_terminalInputHistoryIndex];
        TerminalInput.SelectionStart = TerminalInput.Text.Length;
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
