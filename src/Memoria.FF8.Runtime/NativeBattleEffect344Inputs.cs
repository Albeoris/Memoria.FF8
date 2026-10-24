namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect344Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeBattleEffect344State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}

public sealed record NativeBattleEffect344Step(NativeBattleEffect344Frame Frame, int Result);

public sealed record NativeBattleEffect344Inputs(Func<NativeBattleEffect344Frame, uint> ReadFlags, Func<NativeBattleEffect344Frame, NativeBattleActionPacketsState> ReadPackets, Func<NativeBattleEffect344Frame, NativeBattleEffect344Operation, NativeBattleEffect344Frame> Execute);

public abstract record NativeBattleEffect344Operation;
public sealed record NativeBattleEffect344MusicFade46BBC0(int Channel, int Duration, int Gain) : NativeBattleEffect344Operation;
public sealed record NativeBattleEffect344StageMusic501A20(uint SourceAddress, uint CompletionByteAddress) : NativeBattleEffect344Operation;
public sealed record NativeBattleEffect344RegisterCamera5099A0(uint SourceAddress) : NativeBattleEffect344Operation;
public sealed record NativeBattleEffect344SetSequence505C00(int Actor, byte Sequence) : NativeBattleEffect344Operation;
public sealed record NativeBattleEffect344MusicGain501B10(int Duration, int Gain) : NativeBattleEffect344Operation;
public sealed record NativeBattleEffect344ConsumeFirstContact506690(int ContactIndex) : NativeBattleEffect344Operation;
