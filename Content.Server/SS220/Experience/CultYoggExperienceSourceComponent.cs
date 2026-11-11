// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
namespace Content.Server.SS220.Experience;

/// <summary>
/// Active contribution of the Cult of Yog-Sothoth to a character's experience.
/// Server-only: the resulting knowledge and free points are networked through
/// ExperienceComponent. It is removed when LiberationFromCultEvent is raised.
/// </summary>
[RegisterComponent]
public sealed partial class CultYoggExperienceSourceComponent : Component;
