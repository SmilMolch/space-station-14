// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.SS220.Experience;

/// <summary>Experience contributions grouped by their source ID (usually a MindRole).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AntagExperienceSourcesComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, ProtoId<ExperienceDefinitionPrototype>> Definitions = new();

    // Only the resulting ExperienceComponent needs to be synchronized.
    public readonly Dictionary<string, HashSet<ProtoId<KnowledgePrototype>>> Knowledges = new();
    public readonly Dictionary<string, int> AdditionalPoints = new();
}
