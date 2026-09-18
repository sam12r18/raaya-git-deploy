using RaayaGitDeploy.Core.Terminal;

namespace RaayaGitDeploy.Infrastructure.Terminal;

public sealed class ConPtyTerminalSessionFactory : ITerminalSessionFactory
{
    private readonly Func<ITerminalProcessAdapter> _adapterFactory;

    public ConPtyTerminalSessionFactory(Func<ITerminalProcessAdapter> adapterFactory)
    {
        _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
    }

    public ITerminalSession Create() => new ConPtyTerminalSession(_adapterFactory());
}
