using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleReflectResources(NativeBattleEffectHeapAllocation Animation, NativeBattleStaticMeshResource Wall, NativeBattleStaticMeshResource Hexagon, NativeBattleStaticMeshResource Ring, ImmutableArray<byte> PolarTable, uint Sound);
public sealed record NativeBattleReflectState([property: JsonRequired] short WallMinimum, [property: JsonRequired] short WallSpan, [property: JsonRequired] NativeBattleEffectArena Packets, [property: JsonRequired] int? Cursor)
{
    public static NativeBattleReflectState FromColdProcess() => new(0, 0, NativeBattleEffectArena.Clear(), 0);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleReflectState? Reflect106
    {
        get => effectState.Reflect106;
        init => effectState = effectState with { Reflect106 = value };
    }
}
public sealed record NativeBattleReflectFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);
public sealed record NativeBattleReflectStep(NativeBattleReflectFrame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);
public sealed record NativeBattleReflectInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleReflectFrame, NativeBattleGfGeometryBatch, NativeBattleReflectFrame> Emit)
{
    public Func<NativeBattleReflectFrame, int, NativeBattleReflectFrame>? ApplyContact { get; init; }
}
