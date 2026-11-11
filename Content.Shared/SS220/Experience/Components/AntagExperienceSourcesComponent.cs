// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.SS220.Experience;

/// <summary>
/// Separates additional role experience from the profession's RoleExperienceAddComponent.
/// Key: exact MindRole prototype ID. Other roles remain when one is revoked.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AntagExperienceSourcesComponent : Component
{
    [DataField, AutoNetworkedField]
    public Dictionary<string, ProtoId<ExperienceDefinitionPrototype>> Definitions = new();
}
