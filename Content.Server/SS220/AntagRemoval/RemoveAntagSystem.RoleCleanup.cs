// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using System.Linq;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Mind;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Roles.Components;
using Content.Shared.SS220.CultYogg.Cultists;
using Content.Shared.SS220.Experience;
using Content.Shared.Store.Components;
using Content.Shared.Strip.Components;
using Content.Shared.Zombies;
using Content.Shared.Actions;
using Content.Shared.Roles.RoleCodeword;
using Content.Server.SS220.Experience;

namespace Content.Server.SS220.AntagRemoval;

public sealed partial class RemoveAntagSystem
{
    /// <summary>
    /// Apply only per-role effects. Common cleanup of MindRole, objective,
    /// experience, faction and notifications is done by TryRemove.
    /// </summary>
    private bool TryRevokeRoleEffects(
        EntityUid body,
        EntityUid mindId,
        MindComponent mind,
        string roleId,
        AntagRemovalGrantComponent provenance,
        AntagGrantRecord grant,
        out bool removedRoleByHandler,
        out string result)
    {
        removedRoleByHandler = false;
        result = string.Empty;
        switch (roleId)
        {
            case RemoveAntagRoles.Traitor:
                RevokeUplinks(mindId, body, grant);
                if (TryComp<RoleCodewordComponent>(mindId, out var codewords))
                    _codewords.RemoveRoleCodewords((mindId, codewords), "traitor");
                _traitor.RemoveTraitorFromRoundAccounting(mindId);
                break;

            case RemoveAntagRoles.Thief:
                // The role provides two independent effects on the player's body.
                // ThievingSystem clears its stealth-steal alert on component removal.
                // Remove only components added at role assignment, and preserve
                // components also attributed to another active antagonist role.
                if (grant.GrantedThieving &&
                    !provenance.Grants.Values.Any(g => g.RoleId != roleId && g.GrantedThieving))
                    RemComp<ThievingComponent>(body);

                if (grant.GrantedPacified &&
                    !provenance.Grants.Values.Any(g => g.RoleId != roleId && g.GrantedPacified))
                {
                    RemComp<PacifiedComponent>(body);
                    // If a cultist previously removed Thief's Pacified,
                    // do not re-add it later when that cultist is deconverted.
                    provenance.HadPacifiedBeforeCultConversion = false;
                }
                break;

            case RemoveAntagRoles.Cult:
                if (!_cult.TryRemoveCultistRoleByAdmin(body, out result))
                    return false;
                removedRoleByHandler = true;
                // De-cult drops all tagged Yogg equipment. Existing ExperienceEditorSystem
                // does not get notified by DeMakeCultist, so explicitly revoke cult knowledge.
                EntityManager.System<ExperienceEditorSystem>().RevokeCultKnowledge(body);
                // Cult removes Pacified on conversion. Restore its pre-cult state
                // (including Thief pacifism), independently of cult removal.
                if (provenance.HadPacifiedBeforeCultConversion)
                    EnsureComp<PacifiedComponent>(body);
                break;

            case RemoveAntagRoles.InitialInfected:
                if (grant.GrantedIncurableZombie &&
                    TryComp<IncurableZombieComponent>(body, out var infection))
                {
                    if (infection.Action is { } action)
                        _actions.RemoveAction(body, action);
                    RemComp<IncurableZombieComponent>(body);
                }
                if (grant.GrantedPatientZero)
                    RemComp<InitialInfectedComponent>(body);
                if (grant.GrantedZombifyOnDeath)
                    RemComp<ZombifyOnDeathComponent>(body);
                if (grant.GrantedPendingZombie)
                    RemComp<PendingZombieComponent>(body);
                break;

            case RemoveAntagRoles.HeadRevolutionary:
                RemComp<HeadRevolutionaryComponent>(body);
                // A player can have several mind roles: preserve the general
                // revolutionary component if a separate Revolutionary role remains.
                if (!HasRolePrototype(mind, RemoveAntagRoles.Revolutionary))
                    RemComp<RevolutionaryComponent>(body);
                break;

            default:
                result = Loc.GetString("ss220-removeantag-error-handler-missing");
                return false;
        }

        return true;
    }
}
