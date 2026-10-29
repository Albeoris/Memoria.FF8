using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum DiagnosticShotOperation
{
    None,
    Trigger,
    End
}

internal sealed class DiagnosticShotCycleDriver
{
    private readonly BattleInput input;
    internal DiagnosticShotCycleReceipt Receipt { get; private set; }
    internal bool Completed => Receipt.Completed is not null;

    internal DiagnosticShotCycleDriver(DiagnosticShotCycleReceipt receipt, BattleInput input)
    {
        Receipt = receipt;
        this.input = input;
    }

    internal void Opened(DiagnosticShotObservation observation)
    {
        if (Receipt.Opening is not null || observation.Shot is not { Phase: BattleNativeShotPhase.Opening } || observation.Inventory != Receipt.BeforeOpening.Inventory)
        {
            throw new InvalidDataException("Shot opening did not create its ordinary native process.");
        }
        Receipt = Receipt with { Opening = observation };
    }

    internal void ObserveDispatch(BattleDiagnosticEffectDispatch dispatch)
    {
        int index = Receipt.Dispatches.Length;
        byte[] commands = [14, 237, 238];
        ushort[] effects = [Receipt.Declaration.OpeningEffect, Receipt.FiringEffect, Receipt.Declaration.EndingEffect];
        if (index > 2 || Receipt.Opening is null || index == 1 && Receipt.Triggered is null || index == 2 && Receipt.Ending is null || dispatch.Reflection is not null || dispatch.ActorId != Receipt.ActorId || dispatch.Ability != BattleAbility.LimitBreak || dispatch.ActionId != Receipt.Declaration.ExpectedHeaderAction(commands[index]) || dispatch.NativeCommand != commands[index] || dispatch.RequestedEffect != effects[index] || dispatch.ExecutedEffect != effects[index] || dispatch.NativeActorIndex != Receipt.ActorIndex || dispatch.NativeTargetMask != (index == 2 ? 0x80F8 : Receipt.Opening.Shot!.TargetMask) || dispatch.NativeHeaderIndex is null || dispatch.NativeHeaderGeneration is not > 0 || index > 0 && (dispatch.Serial <= Receipt.Dispatches[^1].Serial || dispatch.Callback < Receipt.Dispatches[^1].Callback) || Receipt.Dispatches.Any(previous => previous.NativeHeaderIndex == dispatch.NativeHeaderIndex && previous.NativeHeaderGeneration == dispatch.NativeHeaderGeneration))
        {
            throw new InvalidDataException("Shot dispatched an unrequested stage or changed its native invocation lineage.");
        }
        Receipt = Receipt with { Dispatches = [.. Receipt.Dispatches, dispatch] };
    }

    internal DiagnosticShotOperation Observe(DiagnosticShotObservation observation, bool drained)
    {
        if (Completed)
        {
            if (observation.Shot is not null || observation.Inventory != Receipt.Completed!.Inventory || observation.LearnedFlags != Receipt.Completed.LearnedFlags)
            {
                throw new InvalidDataException("Completed Shot cycle regained an active process or changed ammunition/learning.");
            }
            return DiagnosticShotOperation.None;
        }
        if (Receipt.Opening is null)
        {
            throw new InvalidOperationException("Shot cycle has no observed native opening.");
        }
        if (Receipt.Ending is not null)
        {
            if (observation.Shot is { Phase: not BattleNativeShotPhase.Ending })
            {
                throw new InvalidDataException("Shot cancellation lost its ending process.");
            }
            if (observation.Shot is null && drained)
            {
                DiagnosticShotCycleReceipt completed = Receipt with { Completed = observation };
                DiagnosticShotCycleValidation.ValidateReceipt(completed, input, completed.FiringEffect, completed.Dispatches, completed.BeforeOpening.Callback, observation.Callback);
                Receipt = completed;
            }
            return DiagnosticShotOperation.None;
        }
        if (observation.Shot is not { RemainingTimer: > 0, RemainingAmmo: > 0 } shot || shot.ActionId != Receipt.Declaration.AmmoAction || shot.TargetMask != Receipt.Opening.Shot!.TargetMask)
        {
            throw new InvalidDataException("Shot expired, exhausted ammunition or lost its actual process before requested cancellation.");
        }
        if (Receipt.Triggered is null)
        {
            if (shot.Phase == BattleNativeShotPhase.Opening)
            {
                return DiagnosticShotOperation.None;
            }
            if (shot.Phase != BattleNativeShotPhase.Ready || Receipt.Dispatches.Length != 1 || Receipt.ReadyToFire is not null)
            {
                throw new InvalidDataException("Shot firing requires its real opening-ready boundary and one trigger.");
            }
            Receipt = Receipt with { ReadyToFire = observation };
            return DiagnosticShotOperation.Trigger;
        }
        if (shot.Phase == BattleNativeShotPhase.AwaitingEffect)
        {
            return DiagnosticShotOperation.None;
        }
        if (shot.Phase != BattleNativeShotPhase.Ready || Receipt.Dispatches.Length != 2 || Receipt.ReadyToEnd is not null)
        {
            throw new InvalidDataException("Shot cancellation requires completion of the sole accepted trigger.");
        }
        Receipt = Receipt with { ReadyToEnd = observation };
        return DiagnosticShotOperation.End;
    }

    internal void Triggered(DiagnosticShotObservation observation)
    {
        if (Receipt.ReadyToFire is null || Receipt.Triggered is not null || observation.Shot?.Phase != BattleNativeShotPhase.AwaitingEffect || observation.Inventory != Receipt.ReadyToFire.Inventory - 1 || observation.Shot.RemainingAmmo != observation.Inventory)
        {
            throw new InvalidDataException("The ordinary Shot trigger was rejected or did not consume exactly one finite round.");
        }
        Receipt = Receipt with { Triggered = observation };
    }

    internal void Ended(DiagnosticShotObservation observation)
    {
        if (Receipt.ReadyToEnd is null || Receipt.Ending is not null || observation.Shot is not { Phase: BattleNativeShotPhase.Ending, RemainingTimer: -1 } || observation.Inventory != Receipt.ReadyToEnd.Inventory)
        {
            throw new InvalidDataException("Ordinary Shot cancellation did not retain its finite ammunition and ending process.");
        }
        Receipt = Receipt with { Ending = observation };
    }
}
