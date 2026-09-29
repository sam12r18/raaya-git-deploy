using System.Collections.ObjectModel;
using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Presentation.Deployment;

public sealed class DeploymentQueueViewModel
{
    private readonly ObservableCollection<DeploymentQueueItem> _items = [];

    public IReadOnlyList<DeploymentQueueItem> Items => _items;
    public long Version { get; private set; }

    public void AddGitSelection(string localPath) => Add(localPath, DeploymentQueueSource.GitSelection);

    public void AddFile(string localPath) => Add(localPath, DeploymentQueueSource.ManualFile);

    public void AddFolder(string localPath) => Add(localPath, DeploymentQueueSource.ManualFolder);

    public void Remove(DeploymentQueueItem item)
    {
        if (_items.Remove(item)) Version++;
    }

    public void Clear()
    {
        if (_items.Count == 0) return;
        _items.Clear();
        Version++;
    }

    private void Add(string localPath, DeploymentQueueSource source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        var normalizedPath = Normalize(localPath);
        if (_items.Any(item => string.Equals(Normalize(item.LocalPath), normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _items.Add(new DeploymentQueueItem(normalizedPath, source));
        Version++;
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
