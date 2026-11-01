using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Process-retained50D300 contact DWORD and target yaw WORD; group and self selections preserve unwritten globals.</summary>
public sealed record NativeBattleActionGeometryGlobals([property: JsonRequired] int? ContactDistance, [property: JsonRequired] short? TargetYaw)
{
    public static NativeBattleActionGeometryGlobals FromColdProcess() => new(0, 0);

    public static NativeBattleActionGeometryGlobals Unknown() => new(null, null);

    public NativeBattleActionGeometryGlobals Observe50D300(NativeBattleActionGeometry context) => new(context.ContactDistance, context.TargetYaw);
}

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleActionGeometryGlobals? ActionGeometryGlobals { get; init; }
}
