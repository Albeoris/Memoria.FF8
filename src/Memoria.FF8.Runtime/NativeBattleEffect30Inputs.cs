namespace Memoria.FF8.Runtime;

public abstract record NativeBattleEffect30Operation;
public sealed record NativeBattleEffect30RegisterCamera(NativeBattleEffectHeapAllocation Camera) : NativeBattleEffect30Operation;
public sealed record NativeBattleEffect30EnqueueTim(int ArenaOffset) : NativeBattleEffect30Operation;
public sealed record NativeBattleEffect30Stage(short Depth, bool ClearRgb) : NativeBattleEffect30Operation;
public sealed record NativeBattleEffect30Contact(uint TaskAddress) : NativeBattleEffect30Operation;
public sealed record NativeBattleEffect30Sound(uint Value, uint Flags, byte Pan) : NativeBattleEffect30Operation;

public sealed record NativeBattleEffect30Inputs(NativeBattleActionPacketsState Packets, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> SelectActors, Func<NativeBattleEffect30Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleEffect30Frame, NativeBattleEffect30Operation, NativeBattleEffect30Frame> Execute)
{
    public required Func<NativeBattleEffect30Frame, uint, NativeBattleEffect30Frame> RefreshRoot { get; init; }
    public required Func<NativeBattleEffect30Frame, uint, NativeBattleEffect30Frame> PrepareTarget { get; init; }
    public required Func<NativeBattleEffect30Frame, uint, NativeBattleEffect30TaskStep> AdvanceNodes { get; init; }
    public required Func<NativeBattleEffect30Frame, uint, NativeBattleEffect30Frame> PrepareBeam144 { get; init; }
    public required Func<NativeBattleEffect30Frame, uint, NativeBattleEffect30Frame> DrawBeam144 { get; init; }
    public required Func<NativeBattleEffect30Frame, uint, NativeBattleEffect30Frame> EmitBeamParticles { get; init; }
    public required Func<NativeBattleEffect30Frame, uint, NativeBattleEffect30TaskStep> AdvanceParticles96 { get; init; }
}

public sealed record NativeBattleEffect30LowerInputs(Func<NativeBattleEffect30Frame, byte, byte, short, NativeBattleEffect30PointStep> ReadBone502170, Func<NativeBattleEffect30Frame, byte, NativeBattlePackedMatrix32> ReadActorBasis, Func<NativeBattleEffect30Frame, NativeBattlePackedMatrix32, NativeBattleVector, NativeBattleVector, NativeBattleEffect30MatrixStep> LookBasis8DDA50, Func<NativeBattleEffect30Frame, NativeBattlePackedMatrix32> ReadCamera, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> SelectActors, Func<NativeBattleEffect30Frame, NativeBattleGfGeometryBatch, NativeBattleEffect30Frame> DeliverPrivate, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors)
{
    public bool CapturePresentation { get; init; }
}
