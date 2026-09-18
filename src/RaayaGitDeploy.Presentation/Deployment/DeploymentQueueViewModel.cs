using System.Collections.ObjectModel;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentQueueViewModel
{
    private readonly ObservableCollection<DeploymentQueueItem> _items = [];

    public IReadOnlyList<DeploymentQueueItem> Items => _items;

    public void AddGitSelection(string localPath) => Add(localPath, DeploymentQueueSource.GitSelection);

    public void AddFile(string localPath) => Add(localPath, DeploymentQueueSource.ManualFile);

    public void AddFolder(string localPath) => Add(localPath, DeploymentQueueSource.ManualFolder);

    public void Remove(DeploymentQueueItem item) => _items.Remove(item);

    public void Clear() => _items.Clear();

    private void Add(string localPath, DeploymentQueueSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        var normalizedPath = Normalize(localPath);
        if (_items.Any(item => string.Equals(Normalize(item.LocalPath), normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _items.Add(new DeploymentQueueItem(normalizedPath, source));
    }

    private static string Normalize(string path)
    {
        var normalized = path.Replace('\\', '/');
        var segments = new List<string>();

        foreach (var segment in normalized.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == ".." && segments.Count > 0 && segments[^1] != "..")
            {
                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return string.Join('/', segments);
    }
}
