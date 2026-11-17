using System.Collections.Immutable;
using static Memoria.FF8.Runtime.BattlePresentationProcess;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool HasQueuedRosterOperations => actions.Any(command => command.RosterOperation is not null);

    private void StageOrQueueRosterOperation(BattleRosterMutation.Operation operation)
    {
        BattleCombatant actor = FindTargetActor(operation.Source) ?? throw new InvalidDataException("Roster operation requires a bound AI actor.");
        if (producingExpandedAiCallback)
        {
            StageRosterOperation(actor, operation);
            return;
        }
        StopTurnAdvance();
        actions.Enqueue(new(actor.Id, actor.Id, BattleTargetRequest.None, BattleAbility.EnemyAbility) { RosterOperation = operation });
    }

    private BattleCombatant ValidateQueuedRosterOperation(BattleAttackCommand command, BattleRosterMutation.Operation operation)
    {
        BattleCombatant actor = FindTargetActor(operation.Source) ?? throw new InvalidDataException("Queued roster operation lost its source actor.");
        BattleAttackCommand expected = new(actor.Id, actor.Id, BattleTargetRequest.None, BattleAbility.EnemyAbility) { RosterOperation = operation };
        if (!command.MagicCasts.IsEmpty || (command with { MagicCasts = expected.MagicCasts }) != expected || actor.NativeAi is null ||
            operation is BattleRosterMutation.Operation.DeathRelease && actor.NativeAi.DeathSequenceReleasesAllocation != true)
        {
            throw new InvalidDataException("Queued roster operation contradicts its typed source or native AI contract.");
        }
        return actor;
    }

    private void DispatchQueuedRosterOperation(BattleAttackCommand command, BattleRosterMutation.Operation operation)
    {
        _ = ValidateQueuedRosterOperation(command, operation);
        BattlePresentationProcess process = Presentation ?? throw new InvalidOperationException("Queued roster operation requires its presentation process.");
        BattleSnapshot before = CaptureState();
        BattleSession candidate = CreatePreparedHostedActionCandidate(before);
        if (!candidate.actions.TryDequeue(out BattleAttackCommand? first) || first != command)
        {
            throw new InvalidDataException("Queued roster operation differs from the prepared FIFO head.");
        }
        candidate.producingExpandedAiCallback = true;
        try
        {
            candidate.StageRosterOperation(candidate.GetActor(command.ActorId), operation);
        }
        finally
        {
            candidate.producingExpandedAiCallback = false;
        }

        ImmutableArray<HostedModelLoadAdmission> loads = candidate.PrepareExpandedModelLoadAdmissions(process);
        ExpandedHostedPrefix prefix = candidate.PrepareExpandedHostedPrefix(process, allocations: loads);
        process.ValidatePreparedDispatch(prefix.Dispatch);
        PreparedActionPublication publication = new(this, candidate, Restore(before));
        publication.Publish();
        process.CommitDispatch(prefix.Dispatch);
    }

    private void StageRosterOperation(BattleCombatant caller, BattleRosterMutation.Operation operation)
    {
        switch (operation)
        {
            case BattleRosterMutation.Operation.Barrier:
                StageExpandedHostedControl(new HostedDispatchControl.Barrier());
                break;

            case BattleRosterMutation.Operation.Remove remove:
                BattleCombatant target = remove.Target switch
                {
                    BattlePreviousTarget.Actor actor => FindTargetActor(actor.Reference) ?? throw new InvalidDataException("Roster removal requires its captured actor."),
                    NativeBattleTargetRequestAdapter.OriginalPreviousOperand original => FindNativeActor(original.Operand),
                    _ => throw new InvalidDataException("Roster removal requires a typed actor or original slot operand.")
                };
                StageExpandedHostedModelControl(105, target);
                break;

            case BattleRosterMutation.Operation.Allocate allocate:
                AllocateNativeEnemy(caller, allocate.SceneSlot, allocate.OriginalSlot?.Offset ?? 0, allocate.ActivateAfterLoading);
                break;

            case BattleRosterMutation.Operation.Activate activate:
                StageHostedNativeActivation(activate.Target);
                break;

            case BattleRosterMutation.Operation.ActivateLastAllocation:
                StageHostedNativeActivation(nativeLastAllocatedTarget);
                break;

            case BattleRosterMutation.Operation.DeathRelease:
                if (caller.IsActive && (NativeSavedStatusBits(caller) & 1) != 0)
                {
                    StageExpandedHostedModelControl(105, caller);
                }
                break;
        }
    }
}
