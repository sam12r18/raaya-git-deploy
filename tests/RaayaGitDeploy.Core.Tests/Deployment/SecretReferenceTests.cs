using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class SecretReferenceTests
{
    [Fact]
    public void Create_ProducesOpaquePersistableReference()
    {
        var reference = SecretReference.Create("ssh", "production-key");

        Assert.Equal("secret://ssh/production-key", reference.Value);
        Assert.Equal(reference.Value, reference.ToString());
    }

    [Theory]
    [InlineData("secret://ssh/production-key")]
    [InlineData("SECRET://git/github_token")]
    public void TryParse_AcceptsWellFormedReferences(string value)
    {
        Assert.True(SecretReference.TryParse(value, out var reference));
        Assert.False(string.IsNullOrWhiteSpace(reference.Value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-password")]
    [InlineData("secret://ssh")]
    [InlineData("secret://ssh/key/extra")]
    [InlineData("secret://ssh/../key")]
    public void TryParse_RejectsPlaintextOrAmbiguousValues(string value)
    {
        Assert.False(SecretReference.TryParse(value, out _));
    }
}
