namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect30Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect30State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
public sealed record NativeBattleEffect30TaskStep(NativeBattleEffect30Frame Frame, int Result);
public sealed record NativeBattleEffect30PointStep(NativeBattleEffect30Frame Frame, NativeBattleVector Position);
public sealed record NativeBattleEffect30MatrixStep(NativeBattleEffect30Frame Frame, NativeBattlePackedMatrix32 Matrix);
