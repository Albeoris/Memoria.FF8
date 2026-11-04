using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum SaveBoundaryStatus
{
    Ready,
    Faulted,
    NotPlaying,
    InterfaceOpen,
    AutomaticPlayActive,
    BattleOrCardPending,
    InteractionPending,
    FieldNotReady,
    ScriptedMovementActive,
    WorldNotReady
}

/// <summary>Eligibility for preparing a continuation; readiness alone does not establish that field scripts can be restarted.</summary>
internal sealed record SaveBoundaryState
{
    public bool Faulted { get; init; }
    public SceneKind Scene { get; init; }
    public bool StartupActive { get; init; }
    public bool WorldLoaded { get; init; }
    public bool InterfaceOpen { get; init; }
    public bool AutomaticPlayActive { get; init; }
    public bool BattleOrCardPending { get; init; }
    public bool InteractionPending { get; init; }
    public bool FieldReady { get; init; }
    public bool ScriptedMovementActive { get; init; }
    public bool WorldReady { get; init; }

    public SaveBoundaryStatus Assess()
    {
        if (Faulted)
        {
            return SaveBoundaryStatus.Faulted;
        }
        if (StartupActive || !WorldLoaded || Scene is not (SceneKind.Field or SceneKind.WorldMap))
        {
            return SaveBoundaryStatus.NotPlaying;
        }
        if (InterfaceOpen)
        {
            return SaveBoundaryStatus.InterfaceOpen;
        }
        if (AutomaticPlayActive)
        {
            return SaveBoundaryStatus.AutomaticPlayActive;
        }
        if (BattleOrCardPending)
        {
            return SaveBoundaryStatus.BattleOrCardPending;
        }
        if (InteractionPending)
        {
            return SaveBoundaryStatus.InteractionPending;
        }
        if (Scene == SceneKind.Field)
        {
            if (!FieldReady)
            {
                return SaveBoundaryStatus.FieldNotReady;
            }
            if (ScriptedMovementActive)
            {
                return SaveBoundaryStatus.ScriptedMovementActive;
            }
        }
        else if (!WorldReady)
        {
            return SaveBoundaryStatus.WorldNotReady;
        }
        return SaveBoundaryStatus.Ready;
    }
}
