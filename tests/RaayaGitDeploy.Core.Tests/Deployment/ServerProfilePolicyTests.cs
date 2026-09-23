using RaayaGitDeploy.Core.Deployment;

namespace RaayaGitDeploy.Core.Tests.Deployment;

public sealed class ServerProfilePolicyTests
{
    [Theory]
    [InlineData(ServerTransportKind.Sftp, 22)]
    [InlineData(ServerTransportKind.Ftp, 21)]
    [InlineData(ServerTransportKind.Ftps, 21)]
    public void DefaultPort_UsesTransportConvention(ServerTransportKind transport, int expectedPort)
    {
        Assert.Equal(expectedPort, ServerProfilePolicy.DefaultPort(transport));
    }

    [Fact]
    public void Create_PreservesTransportAndUsesDefaultPort()
    {
        var profile = ServerProfilePolicy.Create(
            "cpanel",
            "cPanel production",
            "ftp.example.com",
            null,
            "deploy-user",
            "/public_html",
            "credential:cpanel-production",
            ServerTransportKind.Ftps);

        Assert.Equal(ServerTransportKind.Ftps, profile.Transport);
        Assert.Equal(21, profile.Port);
        Assert.Equal("credential:cpanel-production", profile.KeyReference);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    public void Create_RejectsInvalidPort(int port)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ServerProfilePolicy.Create(
            "server", "Server", "example.com", port, "user", "/app", "credential:server", ServerTransportKind.Ftp));
    }
}
