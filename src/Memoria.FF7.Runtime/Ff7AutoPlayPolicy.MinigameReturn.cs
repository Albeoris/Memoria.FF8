using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7AutoPlayPolicy
{
    private void ObserveOwnedMinigameReturn(Ff7NativeMinigameReturn receipt)
    {
        if (State.Active is not { } active || ReadCompletedMinigameReturn(active, receipt, session.MapId, session.FieldGeneration) is not { } completed)
        {
            return;
        }
        State.Active = completed;
        Publish();
    }

    internal static Ff7AutoPlayActiveEvent? ReadCompletedMinigameReturn(Ff7AutoPlayActiveEvent active, Ff7NativeMinigameReturn receipt, int actualMap, long actualGeneration)
    {
        if (!active.Accepted || active.CompletionRecorded)
        {
            return null;
        }
        if (receipt.Request.Command is not FieldMinigameCommand { Game: 4 } command || command.Return.Map != actualMap || receipt.SourceGeneration == long.MaxValue || receipt.DestinationGeneration != receipt.SourceGeneration + 1 || receipt.DestinationGeneration != actualGeneration || ReadSourceActivation(active, receipt.SourceActivation, receipt.SourceGeneration) is not { ExecutionProvenance: { } provenance } tracked || receipt.Tick < provenance.RootDispatchTick || !provenance.Owners.Any(owner => !owner.Queued && owner.Entity == receipt.Request.Entity && owner.Priority == receipt.Request.Priority))
        {
            return null;
        }
        return tracked with
        {
            TransitionOwned = true,
            ExecutionProvenance = provenance with { AcceptedTransition = receipt.Request, AcceptedTransitionTick = receipt.Tick }
        };
    }
}
