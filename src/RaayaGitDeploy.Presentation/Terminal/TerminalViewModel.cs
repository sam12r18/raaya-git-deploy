using RaayaGitDeploy.Core.Terminal;

namespace RaayaGitDeploy.Presentation.Terminal;

public sealed class TerminalViewModel : IAsyncDisposable
{
    private readonly ITerminalSessionFactory _sessionFactory;
    private ITerminalSession? _session;
    private string? _repositoryPath;

    public TerminalViewModel(ITerminalSessionFactory sessionFactory)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public string Output { get; private set; } = string.Empty;
    public bool IsRunning => _session?.State == TerminalSessionState.Running;
    public int? ExitCode { get; private set; }

    public void SetRepository(string repositoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        if (_session is not null)
        {
            throw new InvalidOperationException("Use SwitchRepositoryAsync while a terminal session exists.");
        }

        _repositoryPath = repositoryPath;
        Output = string.Empty;
        ExitCode = null;
    }

    public async Task SwitchRepositoryAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryPath);
        await ReleaseSessionAsync(stopRunning: true, cancellationToken).ConfigureAwait(false);
        _repositoryPath = repositoryPath;
        Output = string.Empty;
        ExitCode = null;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_repositoryPath))
        {
            throw new InvalidOperationException("Select a repository before starting the terminal.");
        }

        if (IsRunning)
        {
            throw new InvalidOperationException("The terminal is already running.");
        }

        if (_session is not null)
        {
            await ReleaseSessionAsync(stopRunning: false, cancellationToken).ConfigureAwait(false);
        }

        var session = _sessionFactory.Create();
        Attach(session);
        _session = session;
        ExitCode = null;

        try
        {
            await session.StartAsync(_repositoryPath, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            Detach(session);
            _session = null;
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public Task SendAsync(string input, CancellationToken cancellationToken)
    {
        if (!IsRunning || _session is null)
        {
            throw new InvalidOperationException("Start the terminal before sending input.");
        }

        return _session.WriteAsync(input, cancellationToken);
    }

    public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken)
    {
        if (!IsRunning || _session is null)
        {
            throw new InvalidOperationException("Start the terminal before resizing it.");
        }

        return _session.ResizeAsync(columns, rows, cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_session is null)
        {
            return;
        }

        if (_session.State == TerminalSessionState.Running)
        {
            await _session.StopAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseSessionAsync(stopRunning: true, CancellationToken.None).ConfigureAwait(false);
    }

    private void Attach(ITerminalSession session)
    {
        session.OutputReceived += OnOutputReceived;
        session.Exited += OnExited;
    }

    private void Detach(ITerminalSession session)
    {
        session.OutputReceived -= OnOutputReceived;
        session.Exited -= OnExited;
    }

    private void OnOutputReceived(object? sender, string text)
    {
        Output += text;
    }

    private void OnExited(object? sender, int exitCode)
    {
        ExitCode = exitCode;
    }

    private async Task ReleaseSessionAsync(bool stopRunning, CancellationToken cancellationToken)
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        if (stopRunning && session.State == TerminalSessionState.Running)
        {
            await session.StopAsync(cancellationToken).ConfigureAwait(false);
        }

        Detach(session);
        _session = null;
        await session.DisposeAsync().ConfigureAwait(false);
    }
}
