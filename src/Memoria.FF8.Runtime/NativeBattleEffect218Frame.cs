namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect218Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleEffect218State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
