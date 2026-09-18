using RaayaGitDeploy.Core.Terminal;
using RaayaGitDeploy.Infrastructure.Terminal;

namespace RaayaGitDeploy.Infrastructure.Tests;

public sealed class ConPtyTerminalSessionTests
{
    [Fact]
    public async Task Lifecycle_RejectsInvalidTransitions_AndPropagatesWorkingDirectory()
    {
        var adapter = new FakeTerminalProcessAdapter();
        var session = new ConPtyTerminalSession(adapter);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.WriteAsync("pwd\r\n", CancellationToken.None));
        await session.StartAsync(@"C:\work\repo", CancellationToken.None);

        Assert.Equal(@"C:\work\repo", adapter.StartedWorkingDirectory);
        Assert.Equal(TerminalSessionState.Running, session.State);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(@"C:\other", CancellationToken.None));

        await session.WriteAsync("git status\r\n", CancellationToken.None);
        await session.ResizeAsync(120, 40, CancellationToken.None);
        await session.StopAsync(CancellationToken.None);

        Assert.Equal("git status\r\n", adapter.LastInput);
        Assert.Equal((120, 40), adapter.LastSize);
        Assert.Equal(TerminalSessionState.Exited, session.State);
        Assert.Equal(1, adapter.StopCount);
    }

    [Fact]
    public async Task Factory_CreatesIndependentSessions()
    {
        var adapters = new Queue<FakeTerminalProcessAdapter>([new(), new()]);
        var factory = new ConPtyTerminalSessionFactory(() => adapters.Dequeue());

        var first = factory.Create();
        var second = factory.Create();

        Assert.NotSame(first, second);
        await first.StartAsync(@"C:\repo-a", CancellationToken.None);
        await second.StartAsync(@"C:\repo-b", CancellationToken.None);

        Assert.Equal(TerminalSessionState.Running, first.State);
        Assert.Equal(TerminalSessionState.Running, second.State);
    }

    [Fact]
    public async Task OutputAndExit_AreForwarded_AndExitUpdatesState()
    {
        var adapter = new FakeTerminalProcessAdapter();
        var session = new ConPtyTerminalSession(adapter);
        string? output = null;
        int? exitCode = null;
        session.OutputReceived += (_, value) => output = value;
        session.Exited += (_, value) => exitCode = value;

        await session.StartAsync(@"C:\repo", CancellationToken.None);
        adapter.EmitOutput("hello\r\n");
        adapter.EmitExit(23);

        Assert.Equal("hello\r\n", output);
        Assert.Equal(23, exitCode);
        Assert.Equal(TerminalSessionState.Exited, session.State);
    }

    private sealed class FakeTerminalProcessAdapter : ITerminalProcessAdapter
    {
        public string? StartedWorkingDirectory { get; private set; }
        public string? LastInput { get; private set; }
        public (int Columns, int Rows)? LastSize { get; private set; }
        public int StopCount { get; private set; }
        public event EventHandler<string>? OutputReceived;
        public event EventHandler<int>? Exited;

        public Task StartAsync(string workingDirectory, CancellationToken cancellationToken)
        {
            StartedWorkingDirectory = workingDirectory;
            return Task.CompletedTask;
        }

        public Task WriteAsync(string input, CancellationToken cancellationToken)
        {
            LastInput = input;
            return Task.CompletedTask;
        }

        public Task ResizeAsync(int columns, int rows, CancellationToken cancellationToken)
        {
            LastSize = (columns, rows);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            EmitExit(0);
            return Task.CompletedTask;
        }

        public void EmitOutput(string output) => OutputReceived?.Invoke(this, output);
        public void EmitExit(int exitCode) => Exited?.Invoke(this, exitCode);

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
