// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using Content.Client.Eui;
using Content.Shared.Eui;
using Content.Shared.SS220.AntagRemoval.Notifications;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client.SS220.AntagRemoval.Notifications;

[UsedImplicitly]
public sealed class RemovedAntagNoticeEui : BaseEui
{
    private readonly RemovedAntagNoticeWindow _window = new();

    public RemovedAntagNoticeEui()
    {
        _window.AcceptButton.OnPressed += _ => _window.Close();
    }

    public override void Opened()
    {
        IoCManager.Resolve<IClyde>().RequestWindowAttention();
        _window.OpenCentered();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not RemovedAntagNoticeEuiState notice)
            return;

        _window.TextLabel.Text = Loc.GetString("ss220-removeantag-window-text", ("role", notice.RoleName));
    }

    public override void Closed()
    {
        _window.Close();
    }
}
