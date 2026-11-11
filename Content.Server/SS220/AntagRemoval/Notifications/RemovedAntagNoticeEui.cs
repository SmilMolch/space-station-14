// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using Content.Server.EUI;
using Content.Shared.Eui;
using Content.Shared.SS220.AntagRemoval.Notifications;

namespace Content.Server.SS220.AntagRemoval.Notifications;

/// <summary>
/// Private role-removal confirmation window for roles other than CultYogg.
/// CultYogg opens DeCultReminderEui in the original de-cult handler.
/// </summary>
public sealed class RemovedAntagNoticeEui : BaseEui
{
    private readonly string _roleName;

    public RemovedAntagNoticeEui(string roleName)
    {
        _roleName = roleName;
    }

    public override void Opened()
    {
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        return new RemovedAntagNoticeEuiState(_roleName);
    }
}
