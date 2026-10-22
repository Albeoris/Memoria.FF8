namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLaserEye71TaskStep(NativeBattleLaserEye71Frame Frame, int Result);
public abstract record NativeBattleLaserEye71Operation;
public sealed record NativeBattleLaserEye71RegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleLaserEye71Operation;
public sealed record NativeBattleLaserEye71EnqueueTim(int ArenaOffset) : NativeBattleLaserEye71Operation;
public sealed record NativeBattleLaserEye71Stage(short Depth, bool ClearRgb) : NativeBattleLaserEye71Operation;
public sealed record NativeBattleLaserEye71BindCaster(byte Actor, uint SaveAddress) : NativeBattleLaserEye71Operation;
public sealed record NativeBattleLaserEye71Ring(byte Actor, int Sequence) : NativeBattleLaserEye71Operation;
public sealed record NativeBattleLaserEye71Sound(uint Value, uint Flags, byte Pan) : NativeBattleLaserEye71Operation;
public sealed record NativeBattleLaserEye71Contact(uint TaskAddress) : NativeBattleLaserEye71Operation;
public sealed record NativeBattleLaserEye71Inputs(NativeBattleActionPacketsState Packets, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> SelectActors, Func<NativeBattleLaserEye71Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleLaserEye71Frame, NativeBattleLaserEye71Operation, NativeBattleLaserEye71Frame> Execute)
{
    public required Func<NativeBattleLaserEye71Frame, uint, NativeBattleLaserEye71Frame> RefreshRoot { get; init; }
    public required Func<NativeBattleLaserEye71Frame, uint, NativeBattleLaserEye71Frame> PrepareTarget { get; init; }
    public required Func<NativeBattleLaserEye71Frame, uint, NativeBattleLaserEye71TaskStep> AdvanceNodes { get; init; }
    public required Func<NativeBattleLaserEye71Frame, uint, NativeBattleLaserEye71Frame> PrepareBeam148 { get; init; }
    public required Func<NativeBattleLaserEye71Frame, uint, NativeBattleLaserEye71Frame> DrawBeam148 { get; init; }
    public required Func<NativeBattleLaserEye71Frame, uint, NativeBattleLaserEye71Frame> EmitBeamParticles { get; init; }
    public required Func<NativeBattleLaserEye71Frame, uint, NativeBattleLaserEye71TaskStep> AdvanceParticles96 { get; init; }
}
