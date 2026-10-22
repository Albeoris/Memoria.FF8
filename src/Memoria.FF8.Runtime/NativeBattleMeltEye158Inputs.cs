namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeltEye158Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleMeltEye158State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleMeltEye158PointStep(NativeBattleMeltEye158Frame Frame, NativeBattleVector Position);
public abstract record NativeBattleMeltEye158Operation;
public sealed record NativeBattleMeltEye158Sound(uint Packed) : NativeBattleMeltEye158Operation;
public sealed record NativeBattleMeltEye158BulkContact(int First, int Count) : NativeBattleMeltEye158Operation;
public sealed record NativeBattleMeltEye158DrawStatic88(uint Temporary88, uint MeshAddress, byte DepthShift) : NativeBattleMeltEye158Operation;
public sealed record NativeBattleMeltEye158DrawSprite180(uint Temporary180, uint SpriteAddress, NativeBattleVector Position, short? W, short Scale, short Depth, short Phase) : NativeBattleMeltEye158Operation;
public sealed record NativeBattleMeltEye158Inputs(uint GraphicsFlags, NativeBattleActionPacketsState Packets, Func<NativeBattleMeltEye158Frame, int, byte, short, NativeBattleMeltEye158PointStep> ReadBone502170, Func<NativeBattleMeltEye158Frame, ushort, NativeBattleMeltEye158PointStep> ReadTargetCenter5020A0, Func<NativeBattleMeltEye158Frame, int, NativeBattlePackedMatrix32> ReadWorld, Func<NativeBattleMeltEye158Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleMeltEye158Frame, NativeBattleMeltEye158Operation, NativeBattleMeltEye158Frame> Execute);
