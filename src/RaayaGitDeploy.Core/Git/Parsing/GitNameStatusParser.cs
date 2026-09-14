namespace RaayaGitDeploy.Core.Git.Parsing;

public static class GitNameStatusParser
{
    public static IReadOnlyList<GitChange> Parse(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var tokens = payload.Split('\0', StringSplitOptions.None);
        var changes = new List<GitChange>();

        var index = 0;
        while (index < tokens.Length)
        {
            var status = tokens[index++];
            if (string.IsNullOrEmpty(status))
            {
                continue;
            }

            if (status.StartsWith('R'))
            {
                var originalPath = ReadRequiredToken(tokens, ref index, status);
                var currentPath = ReadRequiredToken(tokens, ref index, status);
                changes.Add(new GitChange(
                    currentPath,
                    GitChangeKind.Renamed,
                    originalPath));
                continue;
            }

            var kind = status switch
            {
                "A" => GitChangeKind.Added,
                "M" => GitChangeKind.Modified,
                "D" => GitChangeKind.Deleted,
                _ => throw new FormatException($"Unsupported git name-status token '{status}'.")
            };

            changes.Add(new GitChange(
                ReadRequiredToken(tokens, ref index, status),
                kind));
        }

        return changes;
    }

    private static string ReadRequiredToken(
        IReadOnlyList<string> tokens,
        ref int index,
        string status)
    {
        if (index >= tokens.Count || string.IsNullOrEmpty(tokens[index]))
        {
            throw new FormatException(
                $"Git name-status token '{status}' is missing a required path.");
        }

        return tokens[index++];
    }
}
