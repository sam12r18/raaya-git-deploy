using RaayaGitDeploy.Core.Terminal;
using RaayaGitDeploy.Presentation.Terminal;

namespace RaayaGitDeploy.Presentation.Tests.Terminal;

public sealed class TerminalViewModelTests
{
    [Fact]
    public async Task Start_UsesRepositoryWorkingDirectory_AndStreamsOutput()
    {
        var session = new FakeTerminalSession();
        var sut = new TerminalViewModel(new FakeTerminalSessionFactory(session));
        sut.SetRepository(@"C:\work\repo");

        await sut.StartAsync(CancellationToken.None);
        session.EmitOutput("PS C:\\work\\repo> ");

        Assert.Equal(@"C:\work\repo", session.StartedWorkingDirectory);
        Assert.True(sut.IsRunning);
        Assert.Contains("PS C:\\work\\repo> ", sut.Output);
    }

    [Fact]
    public async Task SendAndResize_ForwardOnlyAfterExplicitStart()
    {
        var session = new FakeTerminalSession();
        var sut = new TerminalViewModel(new FakeTerminalSessionFactory(session));
        sut.SetRepository(@"C:\repo");

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.SendAsync("git status\r\n", CancellationToken.None));
        await sut.StartAsync(CancellationToken.None);
        await sut.SendAsync("git status\r\n", CancellationToken.None);
        await sut.ResizeAsync(132, 42, CancellationToken.None);

        Assert.Equal("git status\r\n", session.LastInput);
        Assert.Equal((132, 42), session.LastSize);
    }

    [Fact]
    public async Task RepositorySwitch_StopsOldSession_ClearsOutput_AndRequiresExplicitRestart()
    {
        var first = new FakeTerminalSession();
        var second = new FakeTerminalSession();
        var factory = new QueueTerminalSessionFactory(first, second);
        var sut = new TerminalViewModel(factory);
        sut.SetRepository(@"C:\repo-a");
        await sut.StartAsync(CancellationToken.None);
        first.EmitOutput("old output");

        await sut.SwitchRepositoryAsync(@"C:\repo-b", CancellationToken.None);

        Assert.Equal(1, first.StopCount);
        Assert.False(sut.IsRunning);
        Assert.Equal(string.Empty, sut.Output);
        Assert.Null(second.StartedWorkingDirectory);

        await sut.StartAsync(CancellationToken.None);
        Assert.Equal(@"C:\repo-b", second.StartedWorkingDirectory);
    }

    [Fact]
    public async Task Stop_AndNaturalExit_UpdateRunningState()
    {
        var first = new FakeTerminalSession();
        var second = new FakeTerminalSession();
        var sut = new TerminalViewModel(new QueueTerminalSessionFactory(first, second));
        sut.SetRepository(@"C:\repo");
        await sut.StartAsync(CancellationToken.None);

        await sut.StopAsync(CancellationToken.None);
        Assert.False(sut.IsRunning);

        await sut.StartAsync(CancellationToken.None);
        second.EmitExit(7);
        Assert.False(sut.IsRunning);
        Assert.Equal(7, sut.ExitCode);
    }

    private sealed class FakeTerminalSessionFactory(ITerminalSession session) : ITerminalSessionFactory
    {
        public ITerminalSession Create() => session;
    }

    private sealed class QueueTerminalSessionFactory(params ITerminalSession[] sessions) : ITerminalSessionFactory
    {
        private readonly Queue<ITerminalSession> _sessions = new(sessions);
        public ITerminalSession Create() => _sessions.Dequeue();
    }

    private sealed class FakeTerminalSession : ITerminalSession
    {
        public TerminalSessionState State { get; private set; } = TerminalSessionState.Created;
        public string? StartedWorkingDirectory { get; private set; }
        public string? LastInput { get; private set; }
        public (int Columns, int Rows)? LastSize { get; private set; }
        public int StopCount { get; private set; }
        public event EventHandler<string>? OutputReceived;
        public event EventHandler<int>? Exited;

        public Task StartAsync(string workingDirectory, CancellationToken cancellationToken)
        {
            StartedWorkingDirectory = workingDirectory;
            State = TerminalSessionState.Running;
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
            State = TerminalSessionState.Exited;
            Exited?.Invoke(this, 0);
            return Task.CompletedTask;
        }

        public void EmitOutput(string text) => OutputReceived?.Invoke(this, text);
        public void EmitExit(int code)
        {
            State = TerminalSessionState.Exited;
            Exited?.Invoke(this, code);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
