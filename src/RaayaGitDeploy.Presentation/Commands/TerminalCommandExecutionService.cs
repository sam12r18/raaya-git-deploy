using RaayaGitDeploy.Core.Terminal;

namespace RaayaGitDeploy.Presentation.Commands;

public sealed class TerminalCommandExecutionService : ICommandExecutionService
{
    private readonly ITerminalSessionFactory _sessionFactory;

    public TerminalCommandExecutionService(ITerminalSessionFactory sessionFactory)
    {
        _sessionFactory = sessionFactory ?? throw new ArgumentNullException(nameof(sessionFactory));
    }

    public async Task ExecuteAsync(string commandText, string workingDirectory, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(commandText);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        await using var session = _sessionFactory.Create();
        var exited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Exited += (_, code) => exited.TrySetResult(code);
        await session.StartAsync(workingDirectory, cancellationToken).ConfigureAwait(false);
        await session.WriteAsync(commandText + Environment.NewLine + "exit" + Environment.NewLine, cancellationToken).ConfigureAwait(false);

        var exitCode = await exited.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (exitCode != 0)
            throw new InvalidOperationException($"Saved command exited with code {exitCode}.");
    }
}
