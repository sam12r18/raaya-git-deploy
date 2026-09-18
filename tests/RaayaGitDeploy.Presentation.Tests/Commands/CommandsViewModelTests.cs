using RaayaGitDeploy.Core.Commands;
using RaayaGitDeploy.Presentation.Commands;

namespace RaayaGitDeploy.Presentation.Tests.Commands;

public sealed class CommandsViewModelTests
{
    [Fact]
    public async Task Load_DoesNotExecuteCommands_AndSelectDoesNotExecuteImplicitly()
    {
        var command = new SavedCommand("status", "Git status", "git status", null);
        var store = new FakeSavedCommandStore(command);
        var executor = new FakeCommandExecutionService();
        var sut = new CommandsViewModel(store, executor);

        await sut.LoadAsync(CancellationToken.None);
        sut.SelectedCommand = command;

        Assert.Single(sut.Commands);
        Assert.Empty(executor.Executions);
    }

    [Fact]
    public async Task SaveAndDelete_PersistAndRefreshSelection()
    {
        var store = new FakeSavedCommandStore();
        var sut = new CommandsViewModel(store, new FakeCommandExecutionService());
        var command = new SavedCommand("tests", "Run tests", "dotnet test", @"C:\work");

        await sut.SaveAsync(command, CancellationToken.None);

        Assert.Equal(command, Assert.Single(sut.Commands));
        Assert.Equal(command, sut.SelectedCommand);

        await sut.DeleteSelectedAsync(CancellationToken.None);

        Assert.Empty(sut.Commands);
        Assert.Null(sut.SelectedCommand);
    }

    [Fact]
    public async Task RunSelected_RequiresExplicitAction_AndUsesWorkingDirectoryOverride()
    {
        var command = new SavedCommand("deploy", "Deploy", "./deploy.ps1", @"C:\release");
        var store = new FakeSavedCommandStore(command);
        var executor = new FakeCommandExecutionService();
        var sut = new CommandsViewModel(store, executor);
        sut.SetRepository(@"C:\repo");
        await sut.LoadAsync(CancellationToken.None);
        sut.SelectedCommand = command;

        await sut.RunSelectedAsync(CancellationToken.None);

        var execution = Assert.Single(executor.Executions);
        Assert.Equal("./deploy.ps1", execution.CommandText);
        Assert.Equal(@"C:\release", execution.WorkingDirectory);
    }

    [Fact]
    public async Task RunSelected_UsesRepositoryWhenNoOverride_AndRejectsMissingSelection()
    {
        var command = new SavedCommand("status", "Status", "git status", null);
        var executor = new FakeCommandExecutionService();
        var sut = new CommandsViewModel(new FakeSavedCommandStore(command), executor);
        sut.SetRepository(@"C:\repo");
        await sut.LoadAsync(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.RunSelectedAsync(CancellationToken.None));

        sut.SelectedCommand = command;
        await sut.RunSelectedAsync(CancellationToken.None);

        Assert.Equal(@"C:\repo", Assert.Single(executor.Executions).WorkingDirectory);
    }

    private sealed class FakeSavedCommandStore(params SavedCommand[] commands) : ISavedCommandStore
    {
        private readonly List<SavedCommand> _commands = [.. commands];
        public string StoragePath => "memory://commands";

        public Task<IReadOnlyList<SavedCommand>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SavedCommand>>([.. _commands]);

        public Task UpsertAsync(SavedCommand command, CancellationToken cancellationToken)
        {
            _commands.RemoveAll(existing => existing.Id == command.Id);
            _commands.Add(command);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string id, CancellationToken cancellationToken)
        {
            _commands.RemoveAll(command => command.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCommandExecutionService : ICommandExecutionService
    {
        public List<(string CommandText, string WorkingDirectory)> Executions { get; } = [];

        public Task ExecuteAsync(string commandText, string workingDirectory, CancellationToken cancellationToken)
        {
            Executions.Add((commandText, workingDirectory));
            return Task.CompletedTask;
        }
    }
}
