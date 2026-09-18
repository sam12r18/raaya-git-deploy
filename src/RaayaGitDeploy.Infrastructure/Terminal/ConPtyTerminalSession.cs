using RaayaGitDeploy.Core.Terminal;

namespace RaayaGitDeploy.Infrastructure.Terminal;

public sealed class ConPtyTerminalSession : ITerminalSession
{
    private readonly ITerminalProcessAdapter _adapter;
    private bool _disposed;

    public ConPtyTerminalSession(ITerminalProcessAdapter adapter)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _adapter.OutputReceived += OnOutputReceived;
        _adapter.Exited += OnExited;
    }

    public TerminalSessionState State { get; private set; } = TerminalSessionState.Created;
    public event EventHandler<string>? OutputReceived;
    public event EventHandler<int>? Exited;

    public async Task StartAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (State != TerminalSessionState.Created) throw new InvalidOperationException("Terminal session has already been started.");
        if (string.IsNullOrWhiteSpace(workingDirectory)) throw new ArgumentException("Working directory is required.", nameof(workingDirectory));
        await _adapter.StartAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        State = TerminalSessionState.Running;
    }

    public Task WriteAsync(string input, CancellationToken cancellationToken)
    {
        EnsureRunning();
        return _adapter.WriteAsync(input, cancellationToken);
    }

    public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken)
    {
        EnsureRunning();
        if (columns <= 0) throw new ArgumentOutOfRangeException(nameof(columns));
        if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
        return _adapter.ResizeAsync(columns, rows, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        if (State != TerminalSessionState.Running) return;
        await _adapter.StopAsync(cancellationToken).ConfigureAwait(false);
        State = TerminalSessionState.Exited;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        if (State == TerminalSessionState.Running)
        {
            try { await _adapter.StopAsync(CancellationToken.None).ConfigureAwait(false); }
            finally { State = TerminalSessionState.Exited; }
        }
        _adapter.OutputReceived -= OnOutputReceived;
        _adapter.Exited -= OnExited;
        await _adapter.DisposeAsync().ConfigureAwait(false);
        _disposed = true;
    }

    private void EnsureRunning()
    {
        ThrowIfDisposed();
        if (State != TerminalSessionState.Running) throw new InvalidOperationException("Terminal session is not running.");
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void OnOutputReceived(object? sender, string output) => OutputReceived?.Invoke(this, output);

    private void OnExited(object? sender, int exitCode)
    {
        State = TerminalSessionState.Exited;
        Exited?.Invoke(this, exitCode);
    }
}
