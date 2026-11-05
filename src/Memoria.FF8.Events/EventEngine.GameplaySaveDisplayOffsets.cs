using System.Numerics;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private void ValidateGameplayDisplayOffset(ActorState actor)
    {
        if (actor.NativeDisplayOffset is not { } state)
        {
            return;
        }
        GameplaySaveActorOffset current = new(state.X, state.Y, state.Z, state.StartX, state.StartY, state.StartZ, state.TargetX, state.TargetY, state.TargetZ, state.Duration, state.Counter, state.Mode);
        if (state.Version != 1 || state.Mode > 3 || state.Counter > state.Duration || state.Mode == 0 && state.Counter != 0 || state.Mode == 3 && state.Counter != state.Duration || actor.DisplayOffset != new Vector3(state.X, state.Y, state.Z) || state.Mode is 1 or 2 && (!Scene.NativeActorObjectIds.Contains(actor.ObjectId) || !ValidGameplayDisplayOffsetPhase(state, current)))
        {
            throw new InvalidDataException("Gameplay display offset requires allocated, mirrored words and a valid native interpolation phase.");
        }
    }
}
