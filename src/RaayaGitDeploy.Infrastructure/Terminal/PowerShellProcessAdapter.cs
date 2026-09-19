using System.Diagnostics;

namespace RaayaGitDeploy.Infrastructure.Terminal;

/// <summary>
/// Production terminal adapter backed by an interactive PowerShell process.
/// It provides a usable redirected terminal today while the native ConPTY renderer remains replaceable behind ITerminalProcessAdapter.
/// </summary>
public sealed class PowerShellProcessAdapter : ITerminalProcessAdapter
{
    private Process? _process;
    private CancellationTokenSource? _readCancellation;

    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? Exited;

    public Task StartAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        if (_process is not null) throw new InvalidOperationException("Terminal process is already started.");
        if (!Directory.Exists(workingDirectory)) throw new DirectoryNotFoundException(workingDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = "-NoLogo -NoExit -Command -",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
        startInfo.StandardErrorEncoding = System.Text.Encoding.UTF8;

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Exited += Process_Exited;
        if (!process.Start()) throw new InvalidOperationException("PowerShell terminal could not be started.");

        _process = process;
        _readCancellation = new CancellationTokenSource();
        _ = PumpAsync(process.StandardOutput, _readCancellation.Token);
        _ = PumpAsync(process.StandardError, _readCancellation.Token);
        return Task.CompletedTask;
    }

    public async Task WriteAsync(string input, CancellationToken cancellationToken)
    {
        var process = RequireRunning();
        await process.StandardInput.WriteLineAsync(input.AsMemory(), cancellationToken).ConfigureAwait(false);
        await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (columns <= 0 || rows <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        RequireRunning();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        var process = _process;
        if (process is null || process.HasExited) return;
        try
        {
            await process.StandardInput.WriteLineAsync("exit".AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.FlushAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _readCancellation?.Cancel();
        var process = _process;
        _process = null;
        if (process is not null)
        {
            process.Exited -= Process_Exited;
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync().ConfigureAwait(false);
            process.Dispose();
        }
        _readCancellation?.Dispose();
        _readCancellation = null;
    }

    private Process RequireRunning()
    {
        var process = _process;
        if (process is null || process.HasExited) throw new InvalidOperationException("Terminal process is not running.");
        return process;
    }

    private async Task PumpAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var buffer = new char[1024];
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (count == 0) break;
                OutputReceived?.Invoke(this, new string(buffer, 0, count));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
    }

    private void Process_Exited(object? sender, EventArgs e)
    {
        if (sender is Process process) Exited?.Invoke(this, process.ExitCode);
    }
}
