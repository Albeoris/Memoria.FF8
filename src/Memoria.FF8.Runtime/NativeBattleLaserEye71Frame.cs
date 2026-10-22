namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLaserEye71Frame(NativeBattleEffectMemory Memory, NativeBattleOriginalEffectState Effect, NativeCrtRandomState Random, NativeBattleLaserEye71State State)
{
    public NativeBattleFileStaging? Staging { get; init; }
}
