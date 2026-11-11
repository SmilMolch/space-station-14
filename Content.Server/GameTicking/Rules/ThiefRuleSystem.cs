using Content.Server.Antag;
using Content.Server.SS220.AntagRemoval;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Roles;
using Content.Shared.Humanoid;
using Content.Shared.Roles.Components;
using Content.Shared.SS220.Experience;
using Content.Shared.SS220.Experience.Systems;

namespace Content.Server.GameTicking.Rules;

public sealed class ThiefRuleSystem : GameRuleSystem<ThiefRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThiefRuleComponent, AfterAntagEntitySelectedEvent>(AfterAntagSelected);

        SubscribeLocalEvent<ThiefRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    // Greeting upon thief activation
    private void AfterAntagSelected(Entity<ThiefRuleComponent> mindId, ref AfterAntagEntitySelectedEvent args)
    {
        var ent = args.EntityUid;

        // SS220-removeantag-fix05-begin
        // ThiefKnowledge exists in SS220 but is not added by ThiefGear or
        // ThiefRuleSystem. Register it as a persistent experience source, so
        // spending skill points won't erase it and removeantag can revoke it.
        if (HasComp<ExperienceComponent>(ent))
        {
            var sources = EnsureComp<AntagExperienceSourcesComponent>(ent);
            sources.Definitions[RemoveAntagRoles.Thief] = "ThiefRoleExperience";
            Dirty(ent, sources);
            EntityManager.System<ExperienceSystem>().RecalculateFromSources(ent);
        }
        // SS220-removeantag-fix05-end

        _antag.SendBriefing(ent, MakeBriefing(ent), null, null);
    }

    // Character screen briefing
    private void OnGetBriefing(Entity<ThiefRoleComponent> role, ref GetBriefingEvent args)
    {
        var ent = args.Mind.Comp.OwnedEntity;

        if (ent is null)
            return;
        args.Append(MakeBriefing(ent.Value));
    }

    private string MakeBriefing(EntityUid ent)
    {
        var isHuman = HasComp<HumanoidProfileComponent>(ent);
        var briefing = isHuman
            ? Loc.GetString("thief-role-greeting-human")
            : Loc.GetString("thief-role-greeting-animal");

        if (isHuman)
            briefing += "\n \n" + Loc.GetString("thief-role-greeting-equipment") + "\n";

        return briefing;
    }
}
