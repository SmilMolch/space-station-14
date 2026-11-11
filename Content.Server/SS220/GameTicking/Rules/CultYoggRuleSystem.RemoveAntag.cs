// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Content.Shared.Roles.Components;
using Content.Shared.SS220.CultYogg.Cultists;
using Content.Shared.SS220.CultYogg.MiGo;
using Content.Shared.SS220.Roles;

namespace Content.Server.SS220.GameTicking.Rules;

/// <summary>
/// SS220 removeantag: a narrow, cult-specific bridge to the existing de-cult code.
/// This is NOT the complete removeantag command. Experience provenance, latejoin
/// accounting and per-session exclusions belong in the common removal orchestrator.
/// </summary>
public sealed partial class CultYoggRuleSystem
{
    /// <summary>
    /// Checks if the existing de-cult routine may be run for this body.
    /// The main admin command must additionally verify that the player is connected.
    /// </summary>
    public bool CanRemoveCultistRoleByAdmin(EntityUid body, out string reason)
    {
        if (TerminatingOrDeleted(body))
        {
            reason = Loc.GetString("ss220-removeantag-cult-error-deleted");
            return false;
        }

        if (HasComp<MiGoComponent>(body))
        {
            reason = Loc.GetString("ss220-removeantag-cult-error-migo");
            return false;
        }

        if (HasComp<AscendingComponent>(body))
        {
            reason = Loc.GetString("ss220-removeantag-cult-error-ascending");
            return false;
        }

        if (!HasComp<CultYoggComponent>(body))
        {
            reason = Loc.GetString("ss220-removeantag-cult-error-no-component");
            return false;
        }

        if (!TryGetCultGameRule(out _))
        {
            reason = Loc.GetString("ss220-removeantag-cult-error-no-rule");
            return false;
        }

        if (!_mind.TryGetMind(body, out var mindId, out var mind) ||
            mind.OwnedEntity != body ||
            !_role.MindHasRole<CultYoggRoleComponent>(mindId, out _))
        {
            reason = Loc.GetString("ss220-removeantag-cult-error-no-mind-role");
            return false;
        }

        var hasStationJob = false;
        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            if (!HasComp<JobRoleComponent>(role))
                continue;

            hasStationJob = true;
            break;
        }

        if (!hasStationJob)
        {
            reason = Loc.GetString("ss220-removeantag-cult-error-no-job");
            return false;
        }

        reason = string.Empty;
        return true;
    }

    /// <summary>
    /// The common removeantag orchestrator MUST run its preflight, capture/restore
    /// experience, handle other-role ownership and accounting before invoking this.
    /// The existing event already removes the CultYogg MindRole; do not remove it twice.
    /// </summary>
    public bool TryRemoveCultistRoleByAdmin(EntityUid body, out string reason)
    {
        if (!CanRemoveCultistRoleByAdmin(body, out reason))
            return false;

        // Existing SS220 path: DeCultMind + DeMakeCultist + Mi-Go teleport update.
        // DeMakeCultist calls TryDropCultEquipment, which DROPS tagged equipment
        // from the cultist's hands, inventory and hidden hands; it does not delete
        // the items or clean up already-placed cult structures.
        var ev = new CultYoggDeCultingEvent(body);
        RaiseLocalEvent(body, ref ev, true);

        if (HasComp<CultYoggComponent>(body) ||
            (_mind.TryGetMind(body, out var mindId, out _) &&
             _role.MindHasRole<CultYoggRoleComponent>(mindId, out _)))
        {
            reason = Loc.GetString("ss220-removeantag-cult-error-incomplete");
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
