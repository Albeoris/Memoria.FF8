using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleProtectResources(NativeBattleEffectHeapAllocation Animation, NativeBattleGfAnimationSource Shield, NativeBattleModuleResource Particle, ImmutableArray<byte> ParticleCounts, ImmutableArray<uint> FlashScript, uint Sound);
public sealed record NativeBattleProtectState([property: JsonRequired] ImmutableArray<byte> Blend, [property: JsonRequired] int? Source, [property: JsonRequired] int? Cursor, [property: JsonRequired] int? SecondaryCursor)
{
    public static NativeBattleProtectState FromColdProcess() => new(new byte[0x1C4].ToImmutableArray(), null, 0, 0);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleProtectState? Protect32 { get; init; }
}
public sealed record NativeBattleProtectFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleProtectOperation;
public sealed record NativeBattleProtectPackets(NativeBattleGfGeometryBatch Batch, ushort Flags20 = 0) : NativeBattleProtectOperation;
public sealed record NativeBattleProtectSprite(NativeBattleModuleDraw Draw) : NativeBattleProtectOperation;
public sealed record NativeBattleProtectStage(NativeBattleStageFadeWrite Write) : NativeBattleProtectOperation;
public sealed record NativeBattleProtectSound(NativeBattleModuleSound Sound) : NativeBattleProtectOperation;
public sealed record NativeBattleProtectContact(int Index) : NativeBattleProtectOperation;
public sealed record NativeBattleProtectInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleProtectFrame, NativeBattleProtectOperation, NativeBattleProtectFrame> Execute);

public sealed record NativeBattleShieldPartDraw(ImmutableArray<byte> Blend, NativeBattleGfGeometryBatch Batch, uint? FarColor);
