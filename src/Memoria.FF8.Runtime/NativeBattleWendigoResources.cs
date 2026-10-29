using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWendigoResources(ushort Effect, uint ActorBankAddress, uint ActorDirectory, ImmutableArray<byte> ActorBank, NativeBattleEffectHeapAllocation Camera, NativeBattleModuleResource Dust, uint GrabSound, uint LandSound)
{
    public ImmutableArray<byte> Radii { get; init; } = [];
    public ImmutableArray<byte> KickScript { get; init; } = [];
    public ImmutableArray<byte> PassScript { get; init; } = [];
    public NativeBattleModuleResource? SmallBurst { get; init; }
    public NativeBattleModuleResource? LargeBurst { get; init; }
    public NativeBattleGfAnimationSource? BallPrim { get; init; }
    public NativeBattleGfAnimationSource? KickPrim { get; init; }
    public ImmutableArray<byte> ConstantChannels { get; init; } = [];

    public byte[] ReadConstantChannel(uint address, int count)
    {
        uint start = Effect == 41 ? 0x2698AB8u : Effect == 42 ? 0x267B024u : throw new InvalidDataException("Native Wendigo constant channels require their selected effect.");
        int length = Effect == 41 ? 0x16A8 : 0x834;
        if (ConstantChannels.IsDefault || ConstantChannels.Length != length || address < start || count < 0 || (ulong)address + (uint)count > start + (uint)length)
        {
            throw new NotSupportedException($"Native Wendigo constant channel0x{address:X8}+0x{count:X} leaves its original loader allocation.");
        }
        return ConstantChannels.AsSpan(checked((int)(address - start)), count).ToArray();
    }
}
public sealed record NativeBattleWendigoState([property: JsonRequired] NativeBattleEffectArena Module, [property: JsonRequired] NativeBattleNodeSource? ActorBank, [property: JsonRequired] bool PendingColdSources, [property: JsonRequired] int? Source, [property: JsonRequired] int? PrimaryCursor, [property: JsonRequired] int? SecondaryCursor, [property: JsonRequired] int? Emitters, [property: JsonRequired] int? Geometry, [property: JsonRequired] ushort EmitterScan, [property: JsonRequired] ushort GeometryScan, [property: JsonRequired] ushort PassEmitters, [property: JsonRequired] ushort PassGeometry)
{
    public static NativeBattleWendigoState FromColdProcess() => new(NativeBattleEffectArena.Clear(), null, true, null, null, null, null, null, 0, 0, 0, 0);
}
public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWendigoState? Dribble41
    {
        get => effectState.Dribble41;
        init => effectState = effectState with { Dribble41 = value };
    }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleWendigoState? Shoot42
    {
        get => effectState.Shoot42;
        init => effectState = effectState with { Shoot42 = value };
    }
}
public sealed record NativeBattleWendigoFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random);
public abstract record NativeBattleWendigoOperation
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattlePilotVisualIdentity? PilotIdentity { get; init; }
}
public sealed record NativeBattleWendigoContact(int Index) : NativeBattleWendigoOperation;
public sealed record NativeBattleWendigoSound(uint Sound, ushort Flags) : NativeBattleWendigoOperation;
public sealed record NativeBattleWendigoPackets(NativeBattleGfGeometryBatch Batch, bool Secondary) : NativeBattleWendigoOperation;
public sealed record NativeBattleWendigoSprite(NativeBattleModuleDraw Draw, bool Secondary) : NativeBattleWendigoOperation;
public sealed record NativeBattleWendigoModel(NativeBattleGraphicsModel Model) : NativeBattleWendigoOperation;
public sealed record NativeBattleWendigoSequence(byte Actor, byte Sequence) : NativeBattleWendigoOperation;
public sealed record NativeBattleWendigoDrawBall(int Actor) : NativeBattleWendigoOperation;
public sealed record NativeBattleWendigoInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, short CameraRoll, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Func<NativeBattleWendigoFrame, NativeBattleWendigoOperation, NativeBattleWendigoFrame> Execute);
