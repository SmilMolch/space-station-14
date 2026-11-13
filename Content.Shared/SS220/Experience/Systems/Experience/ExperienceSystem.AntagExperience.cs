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

    /// <summary>Assigns a source's points and knowledge. Repeated calls do not stack points.</summary>
    public void GrantAntagBonuses(EntityUid body, string sourceId, int points,
        params ProtoId<KnowledgePrototype>[] knowledges)
    {
        var sources = EnsureComp<AntagExperienceSourcesComponent>(body);
        var changed = !sources.AdditionalPoints.TryGetValue(sourceId, out var existing) || existing != points;
        sources.AdditionalPoints[sourceId] = points;

        if (!sources.Knowledges.TryGetValue(sourceId, out var existingKnowledges))
            sources.Knowledges[sourceId] = existingKnowledges = new HashSet<ProtoId<KnowledgePrototype>>();

        foreach (var knowledge in knowledges)
            changed |= existingKnowledges.Add(knowledge);

        if (changed)
            RecalculateFromSources(body);
    }

    public void GrantAntagKnowledge(EntityUid body, string sourceId, ProtoId<KnowledgePrototype> knowledge)
    {
        var sources = EnsureComp<AntagExperienceSourcesComponent>(body);
        if (!sources.Knowledges.TryGetValue(sourceId, out var knowledges))
            sources.Knowledges[sourceId] = knowledges = new HashSet<ProtoId<KnowledgePrototype>>();

        if (knowledges.Add(knowledge))
            RecalculateFromSources(body);
    }

    /// <summary>Removes only this source. Callers perform any required rollback and recalculation.</summary>
    public bool RevokeAntagSource(EntityUid body, string sourceId)
    {
        if (!TryComp<AntagExperienceSourcesComponent>(body, out var sources))
            return false;

        var removedDefinition = sources.Definitions.Remove(sourceId);
        var changed = removedDefinition;
        changed |= sources.Knowledges.Remove(sourceId);
        changed |= sources.AdditionalPoints.Remove(sourceId);

        if (removedDefinition)
            Dirty(body, sources);

        return changed;
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
        foreach (var knowledges in ent.Comp.Knowledges.Values)
            args.Knowledges.UnionWith(knowledges);
    }

    private void PointsAddFromAntagSources(Entity<AntagExperienceSourcesComponent> ent, ref SublevelAdditionPointInitialResolve args)
    {
        if (args.DenyChanges)
            return;
        foreach (var defId in ent.Comp.Definitions.Values)
            if (_prototype.TryIndex(defId, out ExperienceDefinitionPrototype? def))
                args.FreeSublevelPoints += def.AddSublevelPoints;
        foreach (var points in ent.Comp.AdditionalPoints.Values)
            args.FreeSublevelPoints += points;
    }
    /// <summary>Revokes unfunded purchases, starting from the most recent.</summary>
    public int RollBackUnfundedSkillPoints(EntityUid uid)
    {
        if (!TryComp<ExperienceComponent>(uid, out _) ||
            !TryComp<JobBackgroundSublevelAddComponent>(uid, out var purchases))
            return 0;

        // Raw budget can be negative; ExperienceComponent.FreeSublevelPoints cannot.
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

        // Legacy purchases made before PurchaseHistory existed.
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
