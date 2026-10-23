namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBlaster186Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleBlaster186State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleBlaster186PointStep(NativeBattleBlaster186Frame Frame, NativeBattleEffectArena Point8);
public sealed record NativeBattleBlaster186Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, uint GraphicsFlags, Func<NativeBattleBlaster186Frame, NativeBattlePackedMatrix32> ReadView, Func<NativeBattleBlaster186Frame, NativeBattleCameraProjection> ReadProjection, Func<NativeBattleBlaster186Frame, int, NativeBattleGraphicsModel> ReadModel, Func<NativeBattleBlaster186Frame, int, int, int, NativeBattleBlaster186PointStep> ReadBone502170, Func<NativeBattleBlaster186Frame, int, NativeBattleBlaster186PointStep> ReadAnchor571400, Func<NativeBattleBlaster186Frame, NativeBattleBlaster186Operation, NativeBattleBlaster186Frame> Execute);

public abstract record NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186Sound(uint Packed, uint Flags, byte Pan) : NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186ContactsAll(int HeaderIndex) : NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186SetActorZWord(int Actor, short Value) : NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186Stage5713E0(int Value, uint Flags) : NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186DrawCommonRectangle571250(int Red, int Green, int Blue) : NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186DrawStatic88(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186DrawSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186DrawCommonSprite180(uint TemporaryAddress, uint SourceAddress, int TableBaseBucket = 17, int DepthShift = 2) : NativeBattleBlaster186Operation;
public sealed record NativeBattleBlaster186ReleaseCommon(uint SourceAddress, uint TaskAddress) : NativeBattleBlaster186Operation;
