namespace Memoria.FF8.Runtime;

/// <summary>The host explicitly chooses actor-relative effect geometry when no native caller scratch exists.</summary>
public enum NativeBattleEffectModelTransformPolicy
{
    NativeCallerScratch,
    HostActorRelative
}
