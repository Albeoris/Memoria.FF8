namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLaserEye71PointStep(NativeBattleLaserEye71Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleLaserEye71MatrixStep(NativeBattleLaserEye71Frame Frame, NativeBattlePackedMatrix32 Matrix);
public sealed record NativeBattleLaserEye71LowerInputs(Func<NativeBattleLaserEye71Frame, byte, byte, short, NativeBattleLaserEye71PointStep> ReadBone502170, Func<NativeBattleLaserEye71Frame, byte, NativeBattlePackedMatrix32> ReadActorBasis, Func<NativeBattleLaserEye71Frame, NativeBattlePackedMatrix32, NativeBattleVector, NativeBattleVector, NativeBattleLaserEye71MatrixStep> LookBasis8DDA50, Func<NativeBattleLaserEye71Frame, NativeBattlePackedMatrix32> ReadCamera, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> SelectActors, Func<NativeBattleLaserEye71Frame, NativeBattleGfGeometryBatch, NativeBattleLaserEye71Frame> DeliverPrivate, NativeBattleCameraProjection Projection, NativeBattleSummonColors Colors);
