using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleStatusMagicResources(ushort Effect, NativeBattleEffectHeapAllocation Animation, ImmutableArray<NativeBattleGfAnimationSource> Layouts, uint Sound)
{
    public uint Entry => Effect switch { 120 => 0x6BE960, 122 => 0x6BD8E0, _ => throw new NotSupportedException("Native status layout requires effect120 or122.") };
    public uint RootCallback => Effect == 120 ? 0x6BEA10u : 0x6BD980u;
    public uint TargetCallback => Effect == 120 ? 0x6BEB80u : 0x6BDAC0u;
    public uint RootDescriptor => Effect == 120 ? 0x2521580u : 0x25214E8u;
    public uint RootBank => Effect == 120 ? 0x2521554u : 0x25214BCu;
    public uint TargetDescriptor => Effect == 120 ? 0x2521570u : 0x25214D8u;
    public ushort TargetStride => Effect == 120 ? (ushort)0x658 : (ushort)0x4AC;
    public ushort TargetCapacity => Effect == 120 ? (ushort)1 : (ushort)3;
    public int MorphOffset => Effect == 120 ? 0x658 : 0xE04;
    public int FirstArena => Effect == 120 ? 0x660 : 0x1124;
    public int SecondArena => Effect == 120 ? 0x18660 : 0x11124;
}

public sealed record NativeBattleStatusMagicFrame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect);
public sealed record NativeBattleStatusMagicStep(NativeBattleStatusMagicFrame Frame, ImmutableArray<int> Contacts, ImmutableArray<NativeBattleModuleSound> Sounds);
public abstract record NativeBattleStatusMagicOperation;
public sealed record NativeBattleStatusMagicPackets(NativeBattleGfGeometryBatch Batch) : NativeBattleStatusMagicOperation;
public sealed record NativeBattleStatusMagicFade : NativeBattleStatusMagicOperation;
public sealed record NativeBattleStatusMagicClearFlash : NativeBattleStatusMagicOperation;
public sealed record NativeBattleStatusMagicInputs(NativeBattleActionPacketsState Packets, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, NativeBattlePoseSource> ReadPoseSource, Action<int, ushort> WriteFlags, Func<NativeBattleStatusMagicFrame, NativeBattleStatusMagicOperation, NativeBattleStatusMagicFrame> Execute)
{
    public Func<NativeBattleStatusMagicFrame, int, NativeBattleStatusMagicFrame>? ApplyContact { get; init; }
}
