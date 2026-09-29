using Android.Content;
using Android.Views;
using Android.Widget;
using RaayaGitDeploy.Android.Core.Shell;

namespace RaayaGitDeploy.Android;

/// <summary>
/// Android platform rendering for the transport-neutral companion deployment screen contract.
/// It can dispatch only typed allow-listed actions; no Git/SSH/FTP credential or arbitrary command
/// is accepted by this view.
/// </summary>
public sealed class DeploymentScreenView : LinearLayout
{
    private readonly TextView _title;
    private readonly TextView _status;
    private readonly TextView _detail;
    private readonly ProgressBar _progress;
    private readonly Button _primary;
    private readonly Button _secondary;

    public DeploymentScreenView(Context context) : base(context)
    {
        Orientation = Orientation.Vertical;
        SetPadding(32, 32, 32, 32);

        _title = new TextView(context) { TextSize = 22 };
        _status = new TextView(context) { TextSize = 16 };
        _detail = new TextView(context);
        _progress = new ProgressBar(context) { Indeterminate = true };
        _primary = new Button(context);
        _secondary = new Button(context);

        AddView(_title);
        AddView(_status);
        AddView(_detail);
        AddView(_progress);
        AddView(_primary);
        AddView(_secondary);
    }

    public void Render(CompanionDeploymentScreenState state, Func<CompanionDeploymentScreenAction, Task> dispatchAsync)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(dispatchAsync);

        _title.Text = state.Card.Title;
        _status.Text = state.Card.StatusText;
        _detail.Text = state.Card.Detail ?? string.Empty;
        _detail.Visibility = string.IsNullOrWhiteSpace(state.Card.Detail) ? ViewStates.Gone : ViewStates.Visible;
        _progress.Visibility = state.Card.ShowProgress ? ViewStates.Visible : ViewStates.Gone;

        BindAction(_primary, state.PrimaryAction, state.PrimaryActionText, state.PrimaryActionEnabled, dispatchAsync);
        BindAction(_secondary, state.SecondaryAction, state.SecondaryActionText, state.SecondaryActionEnabled, dispatchAsync);
    }

    private static void BindAction(Button button, CompanionDeploymentScreenAction action, string? text, bool enabled, Func<CompanionDeploymentScreenAction, Task> dispatchAsync)
    {
        button.Text = text ?? string.Empty;
        button.Enabled = enabled && action != CompanionDeploymentScreenAction.None;
        button.Visibility = action == CompanionDeploymentScreenAction.None ? ViewStates.Gone : ViewStates.Visible;
        button.Click -= button.Tag as EventHandler;

        if (action == CompanionDeploymentScreenAction.None)
        {
            button.Tag = null;
            return;
        }

        EventHandler handler = async (_, _) =>
        {
            button.Enabled = false;
            try { await dispatchAsync(action); }
            finally { button.Enabled = enabled; }
        };
        button.Tag = handler;
        button.Click += handler;
    }
}
