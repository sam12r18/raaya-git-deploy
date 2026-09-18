namespace RaayaGitDeploy.Presentation.Commands;

public interface ICommandExecutionService
{
    Task ExecuteAsync(string commandText, string workingDirectory, CancellationToken cancellationToken);
}
