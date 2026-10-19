using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleShellResources(NativeBattleEffectHeapAllocation Animation, NativeBattleGfAnimationSource Shield, ImmutableArray<byte> ActorBank, uint Sound)
{
    public const uint ActorBankAddress = 0x15EBD6C;
    public const int ActorBankLength = 0x70B0;
    public const uint ActorDirectory = 0x15F2D18;
    public const uint ChannelAddress = 0x26C30EC;
    public const int ChannelLength = 0x7A4;
}
public sealed record NativeBattleShellState([property: JsonRequired] ImmutableArray<byte> Blend, [property: JsonRequired] ImmutableArray<byte> ActorBank, [property: JsonRequired] bool PendingColdActorBank, [property: JsonRequired] ImmutableArray<byte> Channels, [property: JsonRequired] int? Source, [property: JsonRequired] int? Cursor, [property: JsonRequired] int? SecondaryCursor, [property: JsonRequired] int? Emitters, [property: JsonRequired] int? Geometry, [property: JsonRequired] ushort EmitterScan, [property: JsonRequired] ushort GeometryScan, [property: JsonRequired] ushort PassEmitters, [property: JsonRequired] ushort PassGeometry, [property: JsonRequired] uint ContextTask)
{
    public static NativeBattleShellState FromColdProcess() => new(new byte[0x2E0].ToImmutableArray(), [], true, new byte[NativeBattleShellResources.ChannelLength].ToImmutableArray(), null, 0, 0, null, null, 0, 0, 0, 0, 0);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShellState? Shell33 { get; init; }
}
public sealed record NativeBattleShellFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleShellOperation;
public sealed record NativeBattleShellPackets(NativeBattleGfGeometryBatch Batch) : NativeBattleShellOperation;
public sealed record NativeBattleShellStage(NativeBattleStageFadeWrite Write) : NativeBattleShellOperation;
public sealed record NativeBattleShellSound(NativeBattleModuleSound Sound) : NativeBattleShellOperation;
public sealed record NativeBattleShellContact(int Index) : NativeBattleShellOperation;
public sealed record NativeBattleShellInputs(NativeBattleActionPacketsState Packets, NativeBattleActionMatrix Camera, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleShellFrame, NativeBattleShellOperation, NativeBattleShellFrame> Execute);
