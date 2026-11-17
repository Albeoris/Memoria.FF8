namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private bool TryTakeQueuedCommand(out BattleAttackCommand command)
    {
        if (nativeShotProcesses.Count != 0)
        {
            (string actorId, BattleNativeShotState shot) = nativeShotProcesses.Single();
            if (shot.Phase != BattleNativeShotPhase.Opening)
            {
                // The native Shot UI directly submits 237/238 to the event that owns the loaded effect.
                return TryTakeHostedShotCommand(actorId, out command);
            }
        }
        while (actions.TryPeek(out BattleAttackCommand? queued))
        {
            if (queued.ExpandedAiCallback is not null || queued.RosterOperation is not null || queued.NativeDuel is not null || queued.NativeRenzokuken is not null)
            {
                command = queued;
                return true;
            }
            BattleAttackCommand? prepared = queued;
            if (RequiresExpandedCompelledReplacement(GetActor(queued.ActorId), queued))
            {
                // Keep the source command in the FIFO until replacement preparation commits.
                prepared = PrepareExpandedCompelledCommand(GetActor(queued.ActorId), queued);
            }
            actions.Dequeue();
            if (prepared is not null)
            {
                command = prepared;
                return true;
            }
        }
        command = null!;
        return false;
    }
}
