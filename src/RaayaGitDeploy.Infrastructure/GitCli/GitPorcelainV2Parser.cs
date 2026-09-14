using RaayaGitDeploy.Core.Git;

namespace RaayaGitDeploy.Infrastructure.GitCli;

internal static class GitPorcelainV2Parser
{
    public static IReadOnlyList<GitWorkingTreeChange> Parse(string output)
    {
        if (string.IsNullOrEmpty(output))
        {
            return Array.Empty<GitWorkingTreeChange>();
        }

        var records = output.Split('\0');
        var changes = new List<GitWorkingTreeChange>();

        for (var index = 0; index < records.Length; index++)
        {
            var record = records[index];
            if (string.IsNullOrEmpty(record))
            {
                continue;
            }

            switch (record[0])
            {
                case '?':
                    changes.Add(new GitWorkingTreeChange(
                        record.Length > 2 ? record[2..] : string.Empty,
                        GitChangeKind.Untracked,
                        OriginalPath: null,
                        IsStaged: false,
                        IsUnstaged: true));
                    break;

                case '1':
                    changes.Add(ParseOrdinary(record));
                    break;

                case '2':
                    if (index + 1 >= records.Length || string.IsNullOrEmpty(records[index + 1]))
                    {
                        throw new InvalidOperationException(
                            "Git porcelain v2 rename/copy record is missing its original path.");
                    }

                    changes.Add(ParseRenameOrCopy(record, records[++index]));
                    break;

                case 'u':
                    changes.Add(ParseUnmerged(record));
                    break;
            }
        }

        return changes;
    }

    private static GitWorkingTreeChange ParseOrdinary(string record)
    {
        var parts = record.Split(' ', 9, StringSplitOptions.None);
        if (parts.Length != 9 || parts[1].Length != 2)
        {
            throw new InvalidOperationException("Invalid Git porcelain v2 ordinary record.");
        }

        var xy = parts[1];

        return new GitWorkingTreeChange(
            parts[8],
            MapKind(xy),
            OriginalPath: null,
            IsStaged: HasStatus(xy[0]),
            IsUnstaged: HasStatus(xy[1]));
    }

    private static GitWorkingTreeChange ParseRenameOrCopy(
        string record,
        string originalPath)
    {
        var parts = record.Split(' ', 10, StringSplitOptions.None);
        if (parts.Length != 10 || parts[1].Length != 2 || string.IsNullOrEmpty(parts[8]))
        {
            throw new InvalidOperationException("Invalid Git porcelain v2 rename/copy record.");
        }

        var xy = parts[1];
        var operation = parts[8][0];
        var kind = operation switch
        {
            'R' => GitChangeKind.Renamed,
            'C' => GitChangeKind.Added,
            _ => throw new InvalidOperationException(
                $"Unsupported Git porcelain v2 rename/copy operation '{operation}'.")
        };

        return new GitWorkingTreeChange(
            parts[9],
            kind,
            originalPath,
            IsStaged: HasStatus(xy[0]),
            IsUnstaged: HasStatus(xy[1]));
    }

    private static GitWorkingTreeChange ParseUnmerged(string record)
    {
        var parts = record.Split(' ', 11, StringSplitOptions.None);
        if (parts.Length != 11 || parts[1].Length != 2)
        {
            throw new InvalidOperationException("Invalid Git porcelain v2 unmerged record.");
        }

        var xy = parts[1];

        return new GitWorkingTreeChange(
            parts[10],
            GitChangeKind.Conflicted,
            OriginalPath: null,
            IsStaged: HasStatus(xy[0]),
            IsUnstaged: HasStatus(xy[1]));
    }

    private static GitChangeKind MapKind(string xy)
    {
        if (xy.Contains('U'))
        {
            return GitChangeKind.Conflicted;
        }

        if (xy.Contains('R'))
        {
            return GitChangeKind.Renamed;
        }

        if (xy.Contains('D'))
        {
            return GitChangeKind.Deleted;
        }

        if (xy.Contains('A'))
        {
            return GitChangeKind.Added;
        }

        if (xy.Contains('M') || xy.Contains('T'))
        {
            return GitChangeKind.Modified;
        }

        throw new InvalidOperationException($"Unsupported Git status '{xy}'.");
    }

    private static bool HasStatus(char status) => status != '.';
}
