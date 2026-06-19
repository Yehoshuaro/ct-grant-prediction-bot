namespace GrantAI.Domain.Enums;

/// <summary>
/// Track of a master's-degree grant competition. Kazakhstan publishes grant
/// winners in two parallel rankings with different score scales, which means
/// scores in one track are <b>not</b> comparable to scores in the other:
/// <list type="bullet">
///   <item><see cref="Profile"/> — профильная магистратура (scale 0–70).</item>
///   <item><see cref="ScientificPedagogical"/> — научно-педагогическая (scale 0–150).</item>
/// </list>
/// </summary>
public enum MasterType
{
    Profile = 1,
    ScientificPedagogical = 2
}
