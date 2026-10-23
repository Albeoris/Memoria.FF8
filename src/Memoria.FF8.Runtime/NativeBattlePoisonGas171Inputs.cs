namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePoisonGas171Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattlePoisonGas171State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattlePoisonGas171PointStep(NativeBattlePoisonGas171Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattlePoisonGas171Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattlePoisonGas171Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattlePoisonGas171Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattlePoisonGas171Frame, short> ReadFocalWord, Func<NativeBattlePoisonGas171Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattlePoisonGas171Frame, int, NativeBattlePackedMatrix32> ReadModelWorld, Func<NativeBattlePoisonGas171Frame, int, int, int, NativeBattlePoisonGas171PointStep> ReadBone502170, Func<NativeBattlePoisonGas171Frame, NativeBattlePoisonGas171Operation, NativeBattlePoisonGas171Frame> Execute);

public abstract record NativeBattlePoisonGas171Operation;
public sealed record NativeBattlePoisonGas171Stage5713E0(int Value, uint Flags) : NativeBattlePoisonGas171Operation;
public sealed record NativeBattlePoisonGas171Sound501330(uint Packed, uint Flags, int Pan) : NativeBattlePoisonGas171Operation;
public sealed record NativeBattlePoisonGas171Contacts506BA0(int HeaderIndex) : NativeBattlePoisonGas171Operation;
public sealed record NativeBattlePoisonGas171ActorFlags(int Actor, ushort ClearMask, ushort SetMask) : NativeBattlePoisonGas171Operation;
public sealed record NativeBattlePoisonGas171DrawBody176(int Actor, uint TemporaryAddress) : NativeBattlePoisonGas171Operation;
public sealed record NativeBattlePoisonGas171DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 4) : NativeBattlePoisonGas171Operation;
public sealed record NativeBattlePoisonGas171DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattlePoisonGas171Operation;
