// © SS220, An EULA/CLA with a hosting restriction, full text: https://raw.githubusercontent.com/SerbiaStrong-220/space-station-14/master/CLA.txt
namespace Content.Server.SS220.AntagRemoval;

/// <summary>
/// Shared protagonist role prototype identifiers. Keep in sync with the
/// corresponding MindRole entity prototypes. Future roles must be registered
/// only when their full cleanup handler is implemented.
/// </summary>
internal static class RemoveAntagRoles
{
    public const string Traitor = "MindRoleTraitor";
    public const string Thief = "MindRoleThief";
    public const string Cult = "MindRoleCultYogg";
    public const string InitialInfected = "MindRoleInitialInfected";
    public const string HeadRevolutionary = "MindRoleHeadRevolutionary";
    public const string Revolutionary = "MindRoleRevolutionary";
}
