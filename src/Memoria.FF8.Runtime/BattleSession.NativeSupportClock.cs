namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void AdvanceNativeSupportCallbacks()
    {
        ObserveNativeUnknownStackCallback(0x482F80, "admitted support clock");
        if (nativeSupport is null)
        {
            return;
        }
        if (!nativeSupport.EntryInitialized)
        {
            throw new InvalidOperationException("Native482F80 was admitted before47CCB0 case3 initialized its support owners.");
        }
        if (nativeSupport.Cooldown != 0)
        {
            nativeSupport = nativeSupport with { Cooldown = (ushort)(nativeSupport.Cooldown - 1) };
            return;
        }
        byte flags = NativeSupportFlags((byte)8);
        if ((flags & 8) != 0 && nativeSupport.GilgameshUsed == 0 && (HasAcquiredGuardianForce) && NextRandomByte() <= 12)
        {
            QueueNativeAutomaticGuardian(checked((byte)(NextRandomByte() / 64 + 7)));
            nativeSupport = nativeSupport with { GilgameshUsed = 1, Cooldown = definition.NativeSupportRules!.Cooldown };
            return;
        }
            RunExpandedPeriodicAngelo();
        nativeSupport = nativeSupport with { Cooldown = definition.NativeSupportRules!.Cooldown };
    }

    private void RunNativePeriodicAngelo(BattleCombatant rinoa)
    {
        NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Native periodic Angelo support requires the actual saved learned flags.");
        BattleCombatant[] party = combatants.Where(actor => actor.NativeActorIndex is >= 0 and < 3).ToArray();
        if ((progress.AngeloFlags & 2) != 0 && party.Any(actor => actor != rinoa && actor.IsActive && (NativeSavedStatusBits(actor) & 0x101) == 0x100) && NextRandomByte() <= 8)
        {
            QueueNativePeriodicAngelo(12, FilterAiTargets(0, 200, 0, 8).Request);
            return;
        }
        if ((progress.AngeloFlags & 4) != 0)
        {
            if ((NativeSavedStatusBits(rinoa) & 1) != 0 && NextRandomByte() <= 8)
            {
                QueueNativePeriodicAngelo(13, ApplyOriginalAiTargetFlags(SelectAiActor(rinoa), 0, allowsKnockedOutAllies: true));
                return;
            }
            if (party.Any(actor => actor != rinoa && actor.IsActive && (NativeSavedStatusBits(actor) & 1) != 0) && NextRandomByte() <= 2)
            {
                QueueNativePeriodicAngelo(13, ApplyOriginalAiTargetFlags(FilterAiTargets(0, 200, 0, 0), 0, allowsKnockedOutAllies: true));
                return;
            }
        }
        if ((progress.AngeloFlags & 8) != 0 && (NativeSavedStatusBits(rinoa) & 5) == 0 && (NativeBattleStatusBits(rinoa) & 0x4009) == 0 && NextRandomByte() <= 8)
        {
            QueueNativePeriodicAngelo(14, SelectAiActor(rinoa).Request);
        }
    }

    private void QueueNativePeriodicAngelo(ushort id, BattleTargetRequest targets)
    {
        //4831C0 writes the shared selector/mask, then queues stage8 on486080's first allocated party actor.
        NativeBattleSupportState state = nativeSupport ?? throw new InvalidOperationException("Native periodic support lacks its battle-local selector owners.");
        nativeSupport = state with { SelectedAction = checked((byte)id), SelectedTargets = targets };
        QueueNativeCallback(NativeSupportActor(), 8);
    }

    private void ExpandNativePeriodicAngelo(BattleCombatant actor)
    {
        byte id = nativeSupport?.SelectedAction ?? throw new InvalidDataException("Native stage8 lacks its actual selector BYTE1D28E14.");
        BattleTargetRequest targets = nativeSupport.SelectedTargets ?? throw new InvalidDataException("Native stage8 lacks its actual target WORD1D28DE6.");
        QueueNativePlayerReaction(actor, BattleNativePlayerReaction.AngeloPeriodic, 240, id, targets);
    }
}
