// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using System.Linq;
using Content.Server.Antag;
using Content.Server.Chat.Managers;
using Content.Server.EUI;
using Content.Server.SS220.AntagRemoval.Notifications;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Server.SS220.GameTicking.Rules;
using Content.Server.SS220.Experience;
using Content.Shared.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;
using Content.Shared.GameTicking;
using Content.Shared.NPC.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Roles;
using Content.Shared.Roles.RoleCodeword;
using Content.Shared.SS220.CultYogg.Cultists;
using Content.Shared.SS220.CultYogg.MiGo;
using Content.Shared.SS220.Experience;
using Content.Shared.SS220.Experience.Systems;
using Content.Shared.SS220.Roles;
using Content.Shared.Store.Components;
using Content.Shared.Revolutionary.Components;
using Content.Shared.Zombies;
using Content.Shared.Strip.Components;
using Content.Shared.CombatMode.Pacification;
using Content.Server.PDA.Ringer;
using Content.Shared.PDA;
using Content.Server.Traitor.Uplink;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.SS220.AntagRemoval;

/// <summary>
/// Removes one supported roundstart antagonist role from a CONNECTED station player.
/// The mind/body/job are preserved. Unsupported roles (including malfunctioning AI)
/// fail closed until a specialized handler has been implemented.
/// </summary>
public sealed partial class RemoveAntagSystem : EntitySystem
{
    [Dependency] private EuiManager _euiManager = default!;
    [Dependency] private IChatManager _chat = default!;
    [Dependency] private ISharedAdminLogManager _log = default!;
    [Dependency] private AntagSelectionSystem _selection = default!;
    [Dependency] private RoleSystem _roles = default!;
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private NpcFactionSystem _factions = default!;
    [Dependency] private CultYoggRuleSystem _cult = default!;
    [Dependency] private TraitorRuleSystem _traitor = default!;
    [Dependency] private SharedRoleCodewordSystem _codewords = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private ExperienceSystem _experience = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
        EntityManager.EntityAdded += OnGrantedEntityAdded;
    }

    public override void Shutdown()
    {
        EntityManager.EntityAdded -= OnGrantedEntityAdded;
        base.Shutdown();
    }

    private void OnRoundEnded(RoundEndedEvent ev)
    {
        _selection.ResetRemovedRolesForNewRound();
    }

    private static readonly Dictionary<string, string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        ["traitor"] = RemoveAntagRoles.Traitor,
        ["thief"] = RemoveAntagRoles.Thief,
        ["cult"] = RemoveAntagRoles.Cult,
        ["cultist"] = RemoveAntagRoles.Cult,
        ["initialinfected"] = RemoveAntagRoles.InitialInfected,
        ["headrev"] = RemoveAntagRoles.HeadRevolutionary,
    };

    public string GetRemovableRoles(ICommonSession session)
    {
        if (!TryResolvePlayer(session, out var body, out var mindId, out var mind, out var error))
            return error;

        var roles = Supported
            .Where(p => p.Key != "cultist" && HasRolePrototype(mind, p.Value))
            .Select(p => p.Key)
            .ToArray();
        return Loc.GetString("ss220-removeantag-list-roles",
            ("player", session.Name),
            ("character", Name(body)),
            ("job", GetJob(mindId)?.ID ?? "—"),
            ("mind", mindId.ToString()),
            ("body", body.ToString()),
            ("roles", roles.Length == 0
                ? Loc.GetString("ss220-removeantag-list-empty")
                : string.Join(", ", roles)));
    }

    public bool TryRemove(ICommonSession? administrator, ICommonSession session, string kind, out string result)
    {
        if (!Supported.TryGetValue(kind, out var roleId))
        {
            result = Loc.GetString("ss220-removeantag-error-unsupported");
            return false;
        }

        if (!TryResolvePlayer(session, out var body, out var mindId, out var mind, out result))
            return false;

        // Validate the MindRole BEFORE checking the job. After an admin mind
        // transfer, the current Mind may be different from the one that
        // received the antagonist role.
        if (!HasRolePrototype(mind, roleId))
        {
            result = Loc.GetString("ss220-removeantag-error-role-missing", ("role", kind));
            return false;
        }

        // Every supported removal must have a verified provenance record.
        // The early-return guard also proves that both references are non-null
        // for the entire removal operation.
        if (!TryComp<AntagRemovalGrantComponent>(body, out var provenance) ||
            !provenance.Grants.TryGetValue(roleId, out var grant) ||
            grant is null ||
            !grant.VerifiedProvenance)
        {
            var otherBody = FindTrackedGrantBody(mindId, roleId);
            result = otherBody is { } previousBody && previousBody != body
                ? Loc.GetString("ss220-removeantag-error-other-body",
                    ("entity", previousBody.ToString()))
                : Loc.GetString("ss220-removeantag-error-provenance");
            return false;
        }

        // Role ownership is not equivalent to the entity currently controlled.
        // Never remove a MindRole using another Mind's body-side grant record.
        if (grant.GrantedMindId != mindId)
        {
            result = Loc.GetString("ss220-removeantag-error-mind-mismatch");
            return false;
        }

        // Use the engine's canonical job lookup. SetPreference is a UI flag,
        // not proof that a Mind has no station job. A genuinely jobless Mind
        // is still unsupported by the station-job-preserving removal contract.
        var job = GetJob(mindId);
        if (job is null)
        {
            result = Loc.GetString("ss220-removeantag-error-job-required");
            return false;
        }

        if (!CanRemove(body, mindId, roleId, out result))
            return false;

        // No changes must occur if the old shared bonus cannot be attributed
        // to this role or has changed since it was recorded. In particular,
        // do not blindly deduct 5 or 10 points based on a role name.
        if (!ValidateLegacyExperienceProvenance(body, provenance, grant, out result))
            return false;

        if (roleId == RemoveAntagRoles.InitialInfected &&
            ((HasComp<InitialInfectedComponent>(body) && !grant.GrantedPatientZero) ||
             (HasComp<ZombifyOnDeathComponent>(body) && !grant.GrantedZombifyOnDeath) ||
             (HasComp<PendingZombieComponent>(body) && !grant.GrantedPendingZombie) ||
             (HasComp<IncurableZombieComponent>(body) && !grant.GrantedIncurableZombie)))
        {
            result = Loc.GetString("ss220-removeantag-error-infection-provenance");
            return false;
        }

        // Audit before any mutation: exceptions in downstream ECS handlers
        // must not erase the identity of the administrator who initiated it.
        _log.Add(LogType.Mind, LogImpact.High,
            $"removeantag BEGIN: actor={FormatAdministrator(administrator)}, target={session.Name} ({session.UserId}), role={roleId}");

        // The supported non-cult handlers are irreversible but cannot reject a
        // validated role at this point. Remove its MindRole FIRST so a failed
        // MindRemoveRole cannot leave its powers partially revoked.
        // Cult uses the existing single de-cult operation, which owns MindRole removal.
        var cultRemovesItsOwnRole = roleId == RemoveAntagRoles.Cult;
        if (!cultRemovesItsOwnRole && !_roles.MindRemoveRole(mindId, roleId))
        {
            result = Loc.GetString("ss220-removeantag-error-role-missing", ("role", kind));
            return false;
        }

        if (!TryRevokeRoleEffects(body, mindId, mind, roleId, provenance, grant,
                out var removedRoleByHandler, out result))
        {
            // A failure here is unexpected for a prevalidated non-cult role;
            // the cult routine can still fail internally, so surface the error.
            _log.Add(LogType.Mind, LogImpact.High,
                $"removeantag ERROR: actor={FormatAdministrator(administrator)}, target={session.Name} ({session.UserId}), role={roleId}, failure={result}");
            return false;
        }

        if (cultRemovesItsOwnRole && !removedRoleByHandler)
        {
            result = Loc.GetString("ss220-removeantag-error-partial-removal");
            return false;
        }

        // 2. Revoke only recorded objective entities for this role. Cult's de-cult
        //    route already removes its current stage objectives.
        if (TryComp<MindComponent>(mindId, out var currentMind))
        {
            foreach (var objective in grant.Objectives.ToArray())
                if (currentMind.Objectives.Contains(objective))
                    EntityManager.System<SharedMindSystem>().TryRemoveObjective(mindId, currentMind, objective);
        }

        // 3. The MindRole was removed above, or by the cult handler.
        // Other MindRoles and the station job are untouched.

        // 4. First revoke the old role-owned bonus, THEN recalculate.
        // RollBackUnfundedSkillPoints will now see the real smaller budget.
        RevokeRecordedLegacyExperience(body, provenance, grant);
        RecalculateExperience(body, job, roleId);

        // 5. Remove ORIGINAL gear generated on granting the role. This does NOT
        //    remove later acquisitions. De-cult already drops Yogg-tagged gear.
        if (roleId != RemoveAntagRoles.Cult)
            RevokeStartingGear(grant);

        // 6. Restore factions while respecting other surviving antag roles.
        ReconcileFactions(body, provenance, grant);

        // 7. Re-open the freed selection slot and prohibit reacquisition for
        //    the same user and same role until the end of this round.
        // If the same definition granted multiple active roles, removing just
        // one of them must NOT free the definition's selection slot.
        var assignmentStillActive = provenance.Grants.Values.Any(other =>
            other != grant && other.GameRule == grant.GameRule &&
            grant.SelectionDefinitionIndex >= 0 &&
            other.SelectionDefinitionIndex == grant.SelectionDefinitionIndex &&
            HasRolePrototype(mind, other.RoleId));
        _selection.RevokeAssignment(session, mindId, roleId, grant.GameRule,
            assignmentStillActive ? -1 : grant.SelectionDefinitionIndex);
        provenance.Grants.Remove(roleId);

        // Refresh Character window even if removing this role did not change RoleType.
        RaiseNetworkEvent(new MindRoleTypeChangedEvent(), session.Channel);

        var roleName = RoleName(roleId);
        var message = Loc.GetString("ss220-removeantag-hidden", ("role", roleName));
        var wrapped = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
        _chat.ChatMessageToOne(ChatChannel.Server, message, wrapped, default, false, session.Channel);

        // The cult system already opens the native DeCultReminderEui.
        // Other roles display a similarly styled, private EUI only on success.
        if (roleId != RemoveAntagRoles.Cult)
            _euiManager.OpenEui(new RemovedAntagNoticeEui(roleName), session);
        _log.Add(LogType.Mind, LogImpact.High,
            $"removeantag: actor={FormatAdministrator(administrator)}, target={session.Name} ({session.UserId}), character={Name(body)}, removed={roleId}, stationJob={job.ID}");

        result = Loc.GetString("ss220-removeantag-result-success",
            ("role", roleName), ("player", session.Name), ("job", job.ID));
        return true;
    }

    private static string FormatAdministrator(ICommonSession? administrator)
        => administrator is null
            ? "SERVER_CONSOLE"
            : $"{administrator.Name} ({administrator.UserId})";

    private bool TryResolvePlayer(ICommonSession session, out EntityUid body,
        out EntityUid mindId, out MindComponent mind, out string reason)
    {
        body = default;
        mindId = default;
        mind = default!;
        reason = string.Empty;

        if (session.Status is SessionStatus.Disconnected or SessionStatus.Zombie ||
            session.AttachedEntity is not { } entity || TerminatingOrDeleted(entity))
        {
            reason = Loc.GetString("ss220-removeantag-error-not-playing");
            return false;
        }

        if (!EntityManager.System<SharedMindSystem>().TryGetMind(entity, out mindId, out var resolvedMind) ||
            resolvedMind is null ||
            resolvedMind.OwnedEntity != entity ||
            resolvedMind.UserId != session.UserId)
        {
            reason = Loc.GetString("ss220-removeantag-error-no-mind");
            return false;
        }

        mind = resolvedMind;
        body = entity;
        return true;
    }

    private bool CanRemove(EntityUid body, EntityUid mindId, string roleId, out string reason)
    {
        reason = string.Empty;
        if (roleId == RemoveAntagRoles.Cult)
            return _cult.CanRemoveCultistRoleByAdmin(body, out reason);

        if (roleId == RemoveAntagRoles.InitialInfected && HasComp<ZombieComponent>(body))
        {
            reason = Loc.GetString("ss220-removeantag-error-already-zombie");
            return false;
        }

        if (roleId == RemoveAntagRoles.HeadRevolutionary && !HasComp<HeadRevolutionaryComponent>(body))
        {
            reason = Loc.GetString("ss220-removeantag-error-not-headrev");
            return false;
        }

        return true;
    }

    private JobPrototype? GetJob(EntityUid mindId)
        => _jobs.MindTryGetJob(mindId, out var job) ? job : null;

    /// <summary>
    /// Diagnostic-only lookup. Rare admin command path, never a world scan on
    /// regular ECS updates. It does not authorize removal across bodies.
    /// </summary>
    private EntityUid? FindTrackedGrantBody(EntityUid mindId, string roleId)
    {
        EntityUid? found = null;
        var query = EntityQueryEnumerator<AntagRemovalGrantComponent>();
        while (query.MoveNext(out var entity, out var tracking))
        {
            if (!tracking.Grants.TryGetValue(roleId, out var entry) ||
                entry.GrantedMindId != mindId)
                continue;
            if (found is not null)
                return null; // Ambiguous: two bodies claim the same grant.
            found = entity;
        }
        return found;
    }

    private bool HasRolePrototype(MindComponent mind, string roleId)
    {
        foreach (var role in mind.MindRoleContainer.ContainedEntities)
            if (MetaData(role).EntityPrototype?.ID == roleId)
                return true;
        return false;
    }

    /// <summary>
    /// Verify both the exact grant delta and any unexplained increase in the
    /// legacy shared point provider. If attribution is uncertain, fail closed.
    /// </summary>
    private bool ValidateLegacyExperienceProvenance(EntityUid body,
        AntagRemovalGrantComponent origin, AntagGrantRecord removed, out string reason)
    {
        reason = string.Empty;
        if (removed.LegacyPointsAmbiguous)
        {
            reason = Loc.GetString("ss220-removeantag-error-ambiguous-xp");
            return false;
        }

        var current = TryComp<AntagFreeSublevelPointsAddComponent>(body, out var legacy)
            ? legacy.AddFreeSublevelPoints : 0;
        if (current < removed.LegacyPointsGranted)
        {
            reason = Loc.GetString("ss220-removeantag-error-xp-changed");
            return false;
        }

        var originallyPresent = origin.LegacyPointsBeforeFirstAntag ?? 0;
        var attributable = origin.Grants.Values.Sum(g => g.LegacyPointsGranted);
        if (current > originallyPresent + attributable)
        {
            reason = Loc.GetString("ss220-removeantag-error-xp-unknown",
                ("current", current), ("initial", originallyPresent), ("tracked", attributable));
            return false;
        }

        return true;
    }

    /// <summary>
    /// Subtract ONLY the positive delta observed at role assignment. Preserve
    /// any component and points that existed before the first antagonist.
    /// </summary>
    private void RevokeRecordedLegacyExperience(EntityUid body,
        AntagRemovalGrantComponent origin, AntagGrantRecord removed)
    {
        if (removed.LegacyPointsGranted <= 0 ||
            !TryComp<AntagFreeSublevelPointsAddComponent>(body, out var legacy))
            return;

        legacy.AddFreeSublevelPoints -= removed.LegacyPointsGranted;
        if (legacy.AddFreeSublevelPoints == 0 && !origin.LegacyComponentBeforeFirstAntag &&
            !origin.Grants.Values.Any(g => g != removed && g.LegacyPointsGranted > 0))
            RemComp<AntagFreeSublevelPointsAddComponent>(body);
        else
            Dirty(body, legacy);
    }

    /// <summary>
    /// Rebuilds skills, knowledge and budget from *remaining active sources*.
    /// Never repopulate ConstantKnowledge from a pre-removal snapshot: doing so
    /// resurrects CultYoggKnowledge and other revoked abilities.
    /// </summary>
    private void RecalculateExperience(EntityUid body, JobPrototype job, string removedRole)
    {
        if (!HasComp<ExperienceComponent>(body))
            return;

        // Profession is the permanent role source; antagonist grants are separate.
        var profession = EnsureComp<RoleExperienceAddComponent>(body);
        if (profession.DefinitionId != job.ExperienceDefinition)
        {
            profession.DefinitionId = job.ExperienceDefinition;
            Dirty(body, profession);
        }

        if (TryComp<AntagExperienceSourcesComponent>(body, out var antagSources))
        {
            antagSources.Definitions.Remove(removedRole);
            Dirty(body, antagSources);
        }

        _experience.RevokeAntagKnowledge(body, removedRole);
        if (removedRole == RemoveAntagRoles.Cult)
            RemComp<CultYoggExperienceSourceComponent>(body);

        // Source was removed FIRST. Negative raw budget now detects purchases
        // funded by the revoked role, without damaging earned learning progress.
        _experience.RollBackUnfundedSkillPoints(body);

        _experience.RecalculateFromSources(body);
    }

    private void RevokeUplinks(EntityUid mindId, EntityUid body, AntagGrantRecord grant)
    {
        // Revoke exact stores created by THIS traitor assignment, not all stores
        // belonging to this Mind. Handles both PDA codes and implanted uplinks.
        var stores = grant.UplinkStores.Where(uid => !TerminatingOrDeleted(uid)).ToHashSet();

        if (TryComp<ImplantedComponent>(body, out var implanted))
        {
            foreach (var implant in implanted.ImplantContainer.ContainedEntities.ToArray())
                if (grant.UplinkImplants.Contains(implant))
                    _implants.ForceRemove((body, implanted), implant);
        }

        foreach (var implant in grant.UplinkImplants)
        {
            if (!TerminatingOrDeleted(implant))
                QueueDel(implant); // Also handles an implant removed onto the floor.
        }

        var query = EntityQueryEnumerator<RemoteStoreComponent>();
        while (query.MoveNext(out var uid, out var remote))
            if (remote.Store is { } store && stores.Contains(store))
            {
                remote.Store = null;
                Dirty(uid, remote);
            }

        foreach (var store in stores)
            QueueDel(store);
    }

    private void RevokeStartingGear(AntagGrantRecord grant)
    {
        foreach (var item in grant.Gear)
        {
            if (TerminatingOrDeleted(item))
                continue;

            // Keep the contents of the original role's bag, even if acquired later.
            if (TryComp<ContainerManagerComponent>(item, out var manager))
            {
                foreach (var container in _containers.GetAllContainers(item, manager))
                    foreach (var contents in container.ContainedEntities.ToArray())
                        _containers.Remove(contents, container, force: true);
            }
            QueueDel(item);
        }
    }

    private void ReconcileFactions(EntityUid body, AntagRemovalGrantComponent origin,
        AntagGrantRecord removed)
    {
        // Undo only the selected role's faction delta. Never blanket-delete
        // Syndicate/Revolutionary/CultYogg/Zombie from an unrelated role or job.
        foreach (var faction in removed.FactionsAdded)
        {
            var stillGranted = origin.Grants.Values.Any(other => other != removed &&
                other.FactionsAdded.Contains(faction));
            if (!stillGranted && origin.FactionsBeforeFirstAntag?.Contains(faction) != true)
                _factions.RemoveFaction(body, faction.Id);
        }
        foreach (var faction in removed.FactionsRemoved)
        {
            var stillRemoved = origin.Grants.Values.Any(other => other != removed &&
                other.FactionsRemoved.Contains(faction));
            if (!stillRemoved)
                _factions.AddFaction(body, faction.Id);
        }

        // The built-in de-cult routine unconditionally adds NanoTrasen. Ensure
        // surviving antagonist faction deltas take precedence after that routine.
        foreach (var other in origin.Grants.Values)
        {
            if (other == removed)
                continue;
            foreach (var faction in other.FactionsAdded)
                _factions.AddFaction(body, faction.Id);
            foreach (var faction in other.FactionsRemoved)
                _factions.RemoveFaction(body, faction.Id);
        }
    }

    private string RoleName(string roleId)
    {
        var key = roleId switch
        {
            RemoveAntagRoles.Traitor => "ss220-removeantag-role-traitor",
            RemoveAntagRoles.Thief => "ss220-removeantag-role-thief",
            RemoveAntagRoles.Cult => "ss220-removeantag-role-cult",
            RemoveAntagRoles.InitialInfected => "ss220-removeantag-role-initial-infected",
            RemoveAntagRoles.HeadRevolutionary => "ss220-removeantag-role-headrev",
            _ => null
        };
        return key is null ? roleId : Loc.GetString(key);
    }
}
