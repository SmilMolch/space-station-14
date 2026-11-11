// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using System.Linq;
using Content.Shared.SS220.Experience;
using Robust.Shared.Prototypes;

namespace Content.Shared.SS220.Experience.Systems;

public sealed partial class ExperienceSystem
{
    /// <summary>Refreshes skills, knowledge and point budget from active sources.</summary>
    public void RecalculateFromSources(EntityUid body)
    {
        var ev = new RecalculateEntityExperience();
        RaiseLocalEvent(body, ref ev);
    }

    private void AddRoleKnowledge(Entity<AntagRoleKnowledgeSourcesComponent> entity, ref KnowledgeInitialResolve args)
    {
        if (args.DenyChanges) return;
        foreach (var knowledge in entity.Comp.ByRole.Values)
            args.Knowledges.UnionWith(knowledge);
    }

    public void GrantAntagKnowledge(EntityUid body, string roleId, ProtoId<KnowledgePrototype> knowledge)
    {
        var source = EnsureComp<AntagRoleKnowledgeSourcesComponent>(body);
        if (!source.ByRole.TryGetValue(roleId, out var knowledgeSet))
            source.ByRole[roleId] = knowledgeSet = new HashSet<ProtoId<KnowledgePrototype>>();
        if (!knowledgeSet.Add(knowledge)) return;
        var recalc = new RecalculateEntityExperience();
        RaiseLocalEvent(body, ref recalc);
    }

    public void RevokeAntagKnowledge(EntityUid body, string roleId)
    {
        if (TryComp<AntagRoleKnowledgeSourcesComponent>(body, out var source))
            source.ByRole.Remove(roleId);
    }

    private void SkillAddFromAntagSources(Entity<AntagExperienceSourcesComponent> ent, ref SkillTreeAdded args)
    {
        if (args.DenyChanges)
            return;
        foreach (var defId in ent.Comp.Definitions.Values)
        {
            if (_prototype.TryIndex(defId, out ExperienceDefinitionPrototype? def) &&
                def.Skills.TryGetValue(args.SkillTree, out var info))
                args.Info.Add(info);
        }
    }

    private void KnowledgeAddFromAntagSources(Entity<AntagExperienceSourcesComponent> ent, ref KnowledgeInitialResolve args)
    {
        if (args.DenyChanges)
            return;
        foreach (var defId in ent.Comp.Definitions.Values)
            if (_prototype.TryIndex(defId, out ExperienceDefinitionPrototype? def))
                args.Knowledges.UnionWith(def.Knowledges);
    }

    private void PointsAddFromAntagSources(Entity<AntagExperienceSourcesComponent> ent, ref SublevelAdditionPointInitialResolve args)
    {
        if (args.DenyChanges)
            return;
        foreach (var defId in ent.Comp.Definitions.Values)
            if (_prototype.TryIndex(defId, out ExperienceDefinitionPrototype? def))
                args.FreeSublevelPoints += def.AddSublevelPoints;
    }
    /// <summary>
    /// Cancels only editor-purchased sublevels that are no longer funded after
    /// removal of an experience source. Genuine earned progression and the job's
    /// base levels are unaffected. Last purchased sublevels are refunded first.
    /// </summary>
    public int RollBackUnfundedSkillPoints(EntityUid uid)
    {
        if (!TryComp<ExperienceComponent>(uid, out _) ||
            !TryComp<JobBackgroundSublevelAddComponent>(uid, out var purchases))
            return 0;

        // Event is the same event used by InitializeExperienceComp. It already
        // accounts for profession, other antagonist sources, and points spent.
        // Unlike ExperienceComponent.FreeSublevelPoints, its raw result can be
        // negative, letting us detect previously spent points no longer funded.
        var budget = new SublevelAdditionPointInitialResolve(0);
        RaiseLocalEvent(uid, ref budget);
        var excess = Math.Max(0, -budget.FreeSublevelPoints);
        if (excess == 0)
            return 0;

        var refunded = 0;
        for (var i = purchases.PurchaseHistory.Count - 1; i >= 0 && excess > 0; i--)
        {
            var purchase = purchases.PurchaseHistory[i];
            if (!purchases.Skills.TryGetValue(purchase.SkillTree, out var invested) || invested <= 0)
            {
                purchases.PurchaseHistory.RemoveAt(i);
                continue;
            }

            var take = Math.Min(excess, Math.Min(invested, purchase.Amount));
            if (take <= 0)
                continue;

            var left = invested - take;
            if (left == 0)
                purchases.Skills.Remove(purchase.SkillTree);
            else
                purchases.Skills[purchase.SkillTree] = left;

            purchase.Amount -= take;
            if (purchase.Amount == 0)
                purchases.PurchaseHistory.RemoveAt(i);

            excess -= take;
            refunded += take;
        }

        // Legacy/editor allocations made before the purchase journal existed.
        // Only JobBackgroundSublevelAddComponent.Skills are affected: never
        // touch ExperienceComponent.EarnedSkillSublevel or other sources.
        if (excess > 0)
        {
            foreach (var tree in purchases.Skills.Keys.ToArray())
            {
                if (excess <= 0)
                    break;

                var take = Math.Min(excess, purchases.Skills[tree]);
                if (take <= 0)
                    continue;
                var left = purchases.Skills[tree] - take;
                if (left == 0)
                    purchases.Skills.Remove(tree);
                else
                    purchases.Skills[tree] = left;

                excess -= take;
                refunded += take;
            }
        }

        if (refunded > 0)
        {
            purchases.SpentSublevelPoints = Math.Max(0, purchases.SpentSublevelPoints - refunded);
            Dirty(uid, purchases);
        }

        return refunded;
    }
}
