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
        byte flags = NativeSupportFlags(0x18);
        if ((flags & 8) != 0 && nativeSupport.GilgameshUsed == 0 && NextRandomByte() <= 12)
        {
            QueueNativeAutomaticGuardian(checked((byte)(NextRandomByte() / 64 + 7)));
            nativeSupport = nativeSupport with { GilgameshUsed = 1, Cooldown = definition.NativeSupportRules!.Cooldown };
            return;
        }
        if ((flags & 0x10) == 0)
        {
            BattleCombatant? rinoa = combatants.Where(actor => actor.NativeIndex is >= 0 and < 7 && actor.ModelId == 4).OrderBy(actor => actor.NativeIndex).FirstOrDefault();
            if (rinoa is not null)
            {
                RunNativePeriodicAngelo(rinoa);
            }
        }
        nativeSupport = nativeSupport with { Cooldown = definition.NativeSupportRules!.Cooldown };
    }

    private void RunNativePeriodicAngelo(BattleCombatant rinoa)
    {
        NativeLimitProgress progress = definition.LimitProgress ?? throw new NotSupportedException("Native periodic Angelo support requires the actual saved learned flags.");
        BattleCombatant[] party = combatants.Where(actor => actor.NativeIndex is >= 0 and < 3).ToArray();
        if ((progress.AngeloFlags & 2) != 0 && party.Any(actor => actor != rinoa && actor.IsActive && (NativeSavedStatusBits(actor) & 0x101) == 0x100) && NextRandomByte() <= 8)
        {
            QueueNativePeriodicAngelo(12, FilterNativeAiTargetMask(0, 200, 0, 8));
            return;
        }
        if ((progress.AngeloFlags & 4) != 0)
        {
            if ((NativeSavedStatusBits(rinoa) & 1) != 0 && NextRandomByte() <= 8)
            {
                QueueNativePeriodicAngelo(13, checked((ushort)((1 << rinoa.NativeIndex) | 0x4000)));
                return;
            }
            if (party.Any(actor => actor != rinoa && actor.IsActive && (NativeSavedStatusBits(actor) & 1) != 0) && NextRandomByte() <= 2)
            {
                QueueNativePeriodicAngelo(13, (ushort)(FilterNativeAiTargetMask(0, 200, 0, 0) | 0x4000));
                return;
            }
        }
        if ((progress.AngeloFlags & 8) != 0 && (NativeSavedStatusBits(rinoa) & 5) == 0 && (NativeBattleStatusBits(rinoa) & 0x4009) == 0 && NextRandomByte() <= 8)
        {
            QueueNativePeriodicAngelo(14, checked((ushort)(1 << rinoa.NativeIndex)));
        }
    }

    private void QueueNativePeriodicAngelo(ushort id, ushort mask)
    {
        //4831C0 writes the shared selector/mask, then queues stage8 on486080's first allocated party actor.
        NativeBattleSupportState state = nativeSupport ?? throw new InvalidOperationException("Native periodic support lacks its battle-local selector owners.");
        nativeSupport = state with { SelectedAction = checked((byte)id), SelectedTargets = mask };
        QueueNativeCallback(NativeSupportActor(), 8);
    }

    private void ExpandNativePeriodicAngelo(BattleCombatant actor)
    {
        byte id = nativeSupport?.SelectedAction ?? throw new InvalidDataException("Native stage8 lacks its actual selector BYTE1D28E14.");
        ushort targets = nativeSupport.SelectedTargets ?? throw new InvalidDataException("Native stage8 lacks its actual target WORD1D28DE6.");
        QueueNativePlayerReaction(actor, BattleNativePlayerReaction.AngeloPeriodic, 240, id, targets);
    }
}
