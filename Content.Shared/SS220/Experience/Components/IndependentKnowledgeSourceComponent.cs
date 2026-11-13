// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using Robust.Shared.Prototypes;
namespace Content.Shared.SS220.Experience;
/// <summary>Knowledge explicitly granted during the round, outside baseline recalculation.
/// This source survives removing a different antagonist.</summary>
[RegisterComponent]
public sealed partial class IndependentKnowledgeSourceComponent : Component
{
    public readonly HashSet<ProtoId<KnowledgePrototype>> Knowledges = new();
}
