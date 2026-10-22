namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBadBreath131Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleBadBreath131State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleBadBreath131AnchorStep(NativeBattleBadBreath131Frame Frame, NativeBattleModuleAnchor Anchor);
public sealed record NativeBattleBadBreath131PointStep(NativeBattleBadBreath131Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleBadBreath131ActStep(NativeBattleBadBreath131Frame Frame, int Remaining);
public sealed record NativeBattleBadBreath131Inputs(NativeBattleActionPacketsState Packets, NativeBattleSummonColors Colors, Func<int, NativeBattleGraphicsModel> ReadModel, Func<int, int> ReadBoneCount, Func<NativeBattleBadBreath131Frame, NativeBattlePackedMatrix32> ReadCamera, Func<NativeBattleBadBreath131Frame, int, NativeBattleBadBreath131AnchorStep> ReadAnchor, Func<NativeBattleBadBreath131Frame, int, short, int, NativeBattleBadBreath131PointStep> ReadBone, Func<NativeBattleBadBreath131Frame, uint, NativeBattleGfAnimationSource, int, int, NativeBattleEffectArena, NativeBattleBadBreath131ActStep> AdvanceAct, Func<NativeBattleBadBreath131Frame, NativeBattleBadBreath131Operation, NativeBattleBadBreath131Frame> Execute);

public abstract record NativeBattleBadBreath131Operation;
public sealed record NativeBattleBadBreath131Sound(uint Value, uint Flags, byte Pan) : NativeBattleBadBreath131Operation;
public sealed record NativeBattleBadBreath131ActorFlags(byte Actor, ushort ClearMask, ushort SetMask) : NativeBattleBadBreath131Operation;
public sealed record NativeBattleBadBreath131ActorColor(byte Actor, uint Color) : NativeBattleBadBreath131Operation;
public sealed record NativeBattleBadBreath131Contact(int Index) : NativeBattleBadBreath131Operation;
public sealed record NativeBattleBadBreath131FlashEnvelope(int Age) : NativeBattleBadBreath131Operation;
public sealed record NativeBattleBadBreath131DrawStrip92(uint TemporaryAddress, uint SourceAddress, uint SelectedVerticesAddress) : NativeBattleBadBreath131Operation;
public sealed record NativeBattleBadBreath131DrawSprite180(uint TemporaryAddress, uint SourceAddress) : NativeBattleBadBreath131Operation;
