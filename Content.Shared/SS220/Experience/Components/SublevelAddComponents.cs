// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.SS220.Experience;

/// <summary>
/// Base component for adding sublevels which handles only add without any reset
/// </summary>
public abstract partial class BaseSublevelAddComponent : Component
{
    /// <summary>
    /// This field contains only Sublevel from ExperienceSkillInfo which means that it starts from StartSublevel
    /// </summary>
    [ViewVariables(VVAccess.ReadOnly)]
    [DataField] // to make copying possible for engine
    [AutoNetworkedField]
    public Dictionary<ProtoId<SkillTreePrototype>, int> Skills = new();

    [ViewVariables(VVAccess.ReadOnly)]
    [DataField] // to make copying possible for engine
    [AutoNetworkedField]
    public int SpentSublevelPoints;

    // Only editor purchases are revocable; earned progression is not.
    public readonly List<ExperienceSublevelPurchase> PurchaseHistory = new();
}

[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class JobBackgroundSublevelAddComponent : BaseSublevelAddComponent;

/// <summary>One free-sublevel allocation through the experience editor.</summary>
public sealed class ExperienceSublevelPurchase
{
    public ProtoId<SkillTreePrototype> SkillTree;
    public int Amount;

    public ExperienceSublevelPurchase(ProtoId<SkillTreePrototype> skillTree, int amount)
    {
        SkillTree = skillTree;
        Amount = amount;
    }
}
