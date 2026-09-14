using RaayaGitDeploy.Core.Git;
using RaayaGitDeploy.Core.Git.Parsing;

namespace RaayaGitDeploy.Core.Tests.Git;

public sealed class GitNameStatusParserTests
{
    [Theory]
    [InlineData("A\0src/new.cs\0", "src/new.cs", GitChangeKind.Added)]
    [InlineData("M\0src/app.cs\0", "src/app.cs", GitChangeKind.Modified)]
    [InlineData("D\0src/old.cs\0", "src/old.cs", GitChangeKind.Deleted)]
    public void Parse_RecognizesSinglePathChanges(
        string payload,
        string expectedPath,
        GitChangeKind expectedKind)
    {
        var changes = GitNameStatusParser.Parse(payload);

        var change = Assert.Single(changes);
        Assert.Equal(expectedPath, change.Path);
        Assert.Equal(expectedKind, change.Kind);
        Assert.Null(change.OriginalPath);
    }

    [Fact]
    public void Parse_RenameUsesDestinationAsCurrentPath()
    {
        const string payload = "R100\0src/old-name.cs\0src/new-name.cs\0";

        var changes = GitNameStatusParser.Parse(payload);

        var change = Assert.Single(changes);
        Assert.Equal("src/new-name.cs", change.Path);
        Assert.Equal("src/old-name.cs", change.OriginalPath);
        Assert.Equal(GitChangeKind.Renamed, change.Kind);
    }

    [Fact]
    public void Parse_UnsupportedStatusThrowsFormatException()
    {
        const string payload = "T\0src/type-changed.cs\0";

        Assert.Throws<FormatException>(() => GitNameStatusParser.Parse(payload));
    }
}
