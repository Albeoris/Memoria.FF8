using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleSandStorm176Resources(NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource Sand, NativeBattleActionMatrix FunnelMatrix, uint WindSound, uint LandingSound);
public sealed record NativeBattleSandStorm176State([property: JsonRequired] NativeBattleEffectArena Module, [property: JsonRequired] NativeBattleActionMatrix? FunnelMatrix, [property: JsonRequired] bool PendingColdMatrix)
{
    public static NativeBattleSandStorm176State FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, true);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleSandStorm176State? SandStorm176
    {
        get => effectState.SandStorm176;
        init => effectState = effectState with { SandStorm176 = value };
    }
}
public sealed record NativeBattleSandStorm176Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleSandStorm176Operation;
public sealed record NativeBattleSandStorm176Funnel(NativeBattleGfGeometryBatch Batch) : NativeBattleSandStorm176Operation;
public sealed record NativeBattleSandStorm176Sprite(NativeBattleModulePacketBatch Batch) : NativeBattleSandStorm176Operation;
public sealed record NativeBattleSandStorm176Sound(uint Sound) : NativeBattleSandStorm176Operation;
public sealed record NativeBattleSandStorm176Contact(int Index) : NativeBattleSandStorm176Operation;
public sealed record NativeBattleSandStorm176Actor(int Index, NativeBattleGraphicsModel Model) : NativeBattleSandStorm176Operation;
public sealed record NativeBattleSandStorm176Inputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, bool DrawOnly, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleSandStorm176Frame, NativeBattleSandStorm176Operation, NativeBattleSandStorm176Frame> Execute);
