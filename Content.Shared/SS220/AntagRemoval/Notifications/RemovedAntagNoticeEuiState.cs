// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared.SS220.AntagRemoval.Notifications;

/// <summary>
/// Sent only to the target player after a successfully removed antagonist role.
/// </summary>
[Serializable, NetSerializable]
public sealed class RemovedAntagNoticeEuiState : EuiStateBase
{
    public readonly string RoleName;

    public RemovedAntagNoticeEuiState(string roleName)
    {
        RoleName = roleName;
    }
}
