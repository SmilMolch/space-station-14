// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Shared.SS220.Experience.Systems;

public sealed partial class ExperienceSystem : EntitySystem
{
    // Only baseline reconstruction suppresses recording individual grants.
    private int _knowledgeRebuildDepth;

    private void OnIndependentKnowledgeResolve(Entity<IndependentKnowledgeSourceComponent> entity, ref KnowledgeInitialResolve args)
    {
        if (!args.DenyChanges)
            args.Knowledges.UnionWith(entity.Comp.Knowledges);
    }

    public bool TryAddKnowledge(Entity<ExperienceComponent?> entity, [ForbidLiteral] ProtoId<KnowledgePrototype> knowledge)
    {
        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return false;

        if (!_prototype.Resolve(knowledge, out var knowledgePrototype))
            return false;

        if (_knowledgeRebuildDepth == 0)
            EnsureComp<IndependentKnowledgeSourceComponent>(entity).Knowledges.Add(knowledge);
        entity.Comp.ConstantKnowledge.Add(knowledge);
        entity.Comp.ResolvedKnowledge.Add(knowledge);

        foreach (var additionKnowledge in knowledgePrototype.AdditionalKnowledges)
        {
            entity.Comp.ResolvedKnowledge.Add(additionKnowledge);
        }

        if (knowledgePrototype.MessageOnAcquiring is not null)
            _popup.PopupClient(Loc.GetString(knowledgePrototype.MessageOnAcquiring), entity);

        DirtyFields(entity, null, [nameof(ExperienceComponent.ConstantKnowledge), nameof(ExperienceComponent.ResolvedKnowledge)]);

        var ev = new KnowledgeGainedEvent(knowledge);
        RaiseLocalEvent(entity, ref ev);

        return true;
    }

    public bool TryRemoveKnowledge(Entity<ExperienceComponent?> entity, ProtoId<KnowledgePrototype> knowledge)
    {
        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return false;

        if (!_prototype.Resolve(knowledge, out var knowledgePrototype))
            return false;

        if (TryComp<IndependentKnowledgeSourceComponent>(entity, out var independent))
            independent.Knowledges.Remove(knowledge);
        entity.Comp.ConstantKnowledge.Remove(knowledge);
        entity.Comp.ResolvedKnowledge.Clear();

        foreach (var constantKnowledge in entity.Comp.ConstantKnowledge)
        {
            if (!_prototype.Resolve(constantKnowledge, out var constantKnowledgePrototype))
                continue;

            // SS220-removeantag: the base knowledge itself must survive removal
            // of another knowledge; previously only dependent knowledge was rebuilt.
            entity.Comp.ResolvedKnowledge.Add(constantKnowledge);
            foreach (var additionKnowledge in constantKnowledgePrototype.AdditionalKnowledges)
            {
                entity.Comp.ResolvedKnowledge.Add(additionKnowledge);
            }
        }

        if (knowledgePrototype.MessageOnLosing is not null)
            _popup.PopupClient(Loc.GetString(knowledgePrototype.MessageOnLosing), entity);

        DirtyFields(entity, null, [nameof(ExperienceComponent.ConstantKnowledge), nameof(ExperienceComponent.ResolvedKnowledge)]);

        var ev = new KnowledgeLostEvent(knowledge);
        RaiseLocalEvent(entity, ref ev);

        return true;
    }

    /// <summary>
    /// Doesn't change hashset if entity don't have <see cref="ExperienceComponent"/> </br>
    /// Return only knowledges that entity have without resolving additional knowledges
    /// </summary>
    public bool TryGetEntityKnowledge(Entity<ExperienceComponent?> entity, ref HashSet<ProtoId<KnowledgePrototype>> knowledges)
    {
        if (HasComp<BypassKnowledgeCheckComponent>(entity))
        {
            knowledges = [.. _prototype.EnumeratePrototypes<KnowledgePrototype>().Select(x => x.ID)];
            return true;
        }

        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return false;

        knowledges = [.. entity.Comp.ConstantKnowledge];
        return true;
    }

    public bool HaveKnowledge(Entity<ExperienceComponent?> entity, [ForbidLiteral] ProtoId<KnowledgePrototype> knowledge)
    {
        if (HasComp<BypassKnowledgeCheckComponent>(entity))
            return true;

        if (!Resolve(entity.Owner, ref entity.Comp, false))
            return false;

        return entity.Comp.ResolvedKnowledge.Contains(knowledge);
    }
}
