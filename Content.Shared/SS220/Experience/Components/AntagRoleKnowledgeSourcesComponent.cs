// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using Robust.Shared.Prototypes;
namespace Content.Shared.SS220.Experience;
/// <summary>Additional knowledge granted by an active specific MindRole.</summary>
[RegisterComponent]
public sealed partial class AntagRoleKnowledgeSourcesComponent : Component
{
    public readonly Dictionary<string, HashSet<ProtoId<KnowledgePrototype>>> ByRole = new();
}
