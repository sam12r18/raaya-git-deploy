namespace RaayaGitDeploy.Core.Terminal;

public interface ITerminalSession : IAsyncDisposable
{
    TerminalSessionState State { get; }
    event EventHandler<string>? OutputReceived;
    event EventHandler<int>? Exited;
    Task StartAsync(string workingDirectory, CancellationToken cancellationToken);
    Task WriteAsync(string input, CancellationToken cancellationToken);
    Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}
