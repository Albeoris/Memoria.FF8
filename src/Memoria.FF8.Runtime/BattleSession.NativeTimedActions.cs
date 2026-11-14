using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeBattleTimedAction(BattleActionPresentation Command, int Contacts);

public sealed partial class BattleSession
{
    private NativeBattleTimedAction? nativeTimedAction;
    private bool suppressNativeImmediateCounter;

    private void BeginExpandedRenzokuken(BattleAttackCommand queued, BattleCombatant actor, BattleCombatant target, ushort magicId, byte? finisher)
    {
        if (nativeTimedAction is not null || nativeActionCommitDepth != 0)
        {
            throw new InvalidOperationException("Expanded Renzokuken requires the serialized action boundary.");
        }
        BattleActionPresentation command = NativeCommandPresentation(queued);
        BeginNativeReactionBatch();
        nativeTimedAction = new(command, 0);
        nativeActionCommitDepth = 1;
        nativeRenzokukenProcesses.Add(actor.Id, new(magicId, 0, finisher, new()));
        nativeActionScratch = nativeActionScratch with { EventLane = 1, Command = 5, Action = finisher is null ? (ushort)0xFFFD : (ushort)0xFFFE, DeferredEffectsE00 = false };
        LogRenzokukenBoundary(command, "Windup", $"{actor.Name}: Renzokuken.");
        if (finisher is byte index)
        {
            ResolveExpandedRenzokukenFinisher(actor, target, command, FindInstalledNativeAction(249, index));
        }
    }

    private static void ValidateExpandedRenzokukenFinisher(BattleNativeAction finisher)
    {
        if (finisher is not { Command: 249, KernelSection: 5, ActionId: < 4, AttackType: 9, TargetFlags: 0x48 or 0x58, HitCount: > 0 })
        {
            throw new NotSupportedException("Expanded Renzokuken requires the installed hostile finisher formula and target mode.");
        }
    }

    private void ResolveExpandedRenzokukenFinisher(BattleCombatant actor, BattleCombatant target, BattleActionPresentation owner, BattleNativeAction finisher)
    {
        ValidateExpandedRenzokukenFinisher(finisher);
        bool group = (finisher.TargetFlags!.Value & 0x10) == 0;
        if (!group)
        {
            // Native483880 draws a single candidate before command250 substitutes command5's selected target.
            _ = NextRandomByte();
        }
        BattleCombatant[] recipients = group ? enemies.Where(IsExpandedRenzokukenTarget).ToArray() : [target];
        BattleActionPresentation command = owner with { ActionId = 249 << 16 | finisher.ActionId, NativeQueuedCommand = 250, NativeAction = finisher };
        BattleNativeAction formula = finisher with { ActionId = ushort.MaxValue };
        foreach (BattleCombatant recipient in recipients)
        {
            recipient.NativeRemainingHits = finisher.HitCount;
        }
        for (int hit = 0; hit < finisher.HitCount; hit++)
        {
            foreach (BattleCombatant recipient in recipients)
            {
                PublishExpandedRenzokukenContact(actor, recipient, command, formula, deferred: false);
            }
        }
    }

    private void ResolveExpandedRenzokukenTrigger(BattleNativeAction physical, short timing, bool last, bool finisher)
    {
        NativeBattleTimedAction timed = nativeTimedAction ?? throw new InvalidOperationException("Renzokuken trigger has no command owner.");
        BattleCombatant actor = GetActor(timed.Command.ActorId);
        BattleCombatant target = GetActor(timed.Command.TargetId);
        bool deferred = !last || finisher;
        byte sequence = !deferred && (NativeSavedStatusBits(target) & 1) != 0 ? (byte)4 : (byte)255;
        BattleNativeAction formula = physical with { Name = "Renzokuken", ActionId = finisher ? ushort.MaxValue : (ushort)0, DamagePresentation = sequence };
        BattleActionPresentation contact = timed.Command with { NativeQueuedCommand = finisher ? (byte)250 : (byte)5, NativeAction = formula, GunbladeTriggerTiming = timing };
        target.NativeRemainingHits = 1;
        PublishExpandedRenzokukenContact(actor, target, contact, formula, deferred);
        nativeTimedAction = timed with { Contacts = checked(timed.Contacts + 1) };
    }

    private void PublishExpandedRenzokukenContact(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command, BattleNativeAction formula, bool deferred)
    {
        BattleModelRoster roster = ReadExpandedPresentationRoster();
        _ = RequireExpandedPresentationRow(roster, actor);
        byte row = RequireExpandedPresentationRow(roster, target);
        BattleActionPresentation? previous = resolvingNativeAction;
        bool previousSuppression = suppressNativeImmediateCounter;
        nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = deferred, Command = formula.Command };
        resolvingNativeAction = command with { NativeAction = formula };
        suppressNativeImmediateCounter = deferred;
        try
        {
            BattleActionDefinition action = new(command.Ability, command.ActionId, formula.Command == 251 ? "Renzokuken" : formula.Name ?? "Renzokuken finisher", BattleActionEffect.Native, formula.Power, false) { NativeAction = formula };
            NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, target, resolvingNativeAction, action, formula, 255, presentationRow: row);
            CommitExpandedEnemyDraw(target.Id, ref pendingEnemyDraw);
            PublishExpandedImpacts(command, [new(target.Id, packet)]);
        }
        finally
        {
            suppressNativeImmediateCounter = previousSuppression;
            resolvingNativeAction = previous;
        }
    }

    private void BeginNativeTimedAction(BattleActionPresentation command, ushort magicId, byte? finisher, ushort finisherTargets)
    {
        if (nativeTimedAction is not null || nativeActionCommitDepth != 0 || HasNativePendingReactions)
        {
            throw new InvalidOperationException("A native timed command must begin at the serialized command boundary.");
        }
        BattleCombatant actor = GetActor(command.ActorId);
        _ = GetActor(command.TargetId);
        if (!actor.IsPlayer || command.NativeQueuedCommand != 5)
        {
            throw new InvalidDataException("Native timed transaction lacks its original Renzokuken command owner.");
        }
        BeginNativeBattleActionPause();
        BeginNativeReactionBatch();
        SetNativeActionEvent(actor, 1);
        nativeTimedAction = new(command, 0);
        nativeAffinityAction = null;
        nativeActionCommitDepth = 1;
        NativeBattleActionProductionContext? previous = nativeProductionContext;
        nativeProductionContext = new(0, 1, false, 0x47DE70);
        try
        {
            BattleNativeAction control = actor.NativeAttack! with { Command = 5, ActionId = checked((ushort)command.ActionId), KernelSection = -1, KernelIndex = -1, MagicId = magicId, AttackType = 0, Power = 0, HitCount = 1, AttackFlags = 3, DamagePresentation = 0, Element = 0, Statuses0 = 0, Statuses1 = 0, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
            BattleActionDefinition definition = new(command.Ability, command.ActionId, "Renzokuken", BattleActionEffect.Native, 0, false) { NativeAction = control };
            int header = nativeActionPackets.ActionCount;
            ResolveInstalledNativeAction(actor, GetActor(command.TargetId), command, definition);
            nativeActionPackets = nativeActionPackets.WritePlayerRepeatMode(header, 1);
            nativeTimedAction = nativeTimedAction! with { Command = command with { NativeHeaderIndex = header } };
            if (finisher is byte selected)
            {
                PrepareNativeRenzokukenFinisher(selected, finisherTargets);
            }
        }
        finally
        {
            nativeProductionContext = previous;
        }
        if (NativeGraphics is not null)
        {
            //485460 publishes the final event callback after both485610 subcommands and their104/finisher descriptors.
            QueueNativeActionClockCompletion(0x47DE70);
        }
    }

    private void ResolveNativeTimedContact(ushort targetMask, BattleNativeAction native, short timing, bool finalContact)
    {
        NativeBattleTimedAction timed = nativeTimedAction ?? throw new InvalidOperationException("Native contact requires its active command transaction.");
        if (nativeActionCommitDepth != 1 || native.Command == 251 && native.AttackType != 10)
        {
            throw new InvalidDataException("Native contact lacks its command-depth or original trigger timing contract.");
        }
        ValidateNativeActionContract(native);
        BattleCombatant actor = GetActor(timed.Command.ActorId);
        BattleCombatant target = GetActor(timed.Command.TargetId);
        BattleActionPresentation contact = timed.Command with { NativeAction = native, NativeTargetMask = targetMask, GunbladeTriggerTiming = timing, NativeMagicTargets = default };
        bool previous = suppressNativeImmediateCounter;
        suppressNativeImmediateCounter = !finalContact;
        try
        {
            if (native.Command == 251)
            {
                if (target.EncodeNativeSlot() is < 0 or >= 7 || targetMask != 1 << target.EncodeNativeSlot())
                {
                    throw new InvalidDataException("Native251 contact must retain its originally selected physical recipient.");
                }
                BattleActionPresentation? previousAction = resolvingNativeAction;
                resolvingNativeAction = contact;
                try
                {
                    BattleNativeRenzokukenState process = nativeRenzokukenProcesses[actor.Id];
                    short total = checked((short)RenzokukenResource(process.MagicId).Timeline.InputThresholds.Length);
                    RecomputeNativeTriggerContact(new(checked((byte)actor.EncodeNativeSlot()), checked((short)(timed.Contacts + 1)), total, timing, 32));
                }
                finally
                {
                    resolvingNativeAction = previousAction;
                }
            }
            else
            {
                NativeBattleActionProductionContext? previousContext = nativeProductionContext;
                nativeProductionContext = new(0, 1, false, 0x47DE70);
                try
                {
                    ResolveAction(actor, target, contact);
                }
                finally
                {
                    nativeProductionContext = previousContext;
                }
            }
            nativeTimedAction = timed with { Contacts = checked(timed.Contacts + 1) };
        }
        finally
        {
            suppressNativeImmediateCounter = previous;
        }
    }

    private void CompleteNativeTimedAction()
    {
        if (NativeGraphics is not null)
        {
            throw new InvalidOperationException("Raw native Renzokuken completes only through its final event descriptor.");
        }
        NativeBattleTimedAction timed = nativeTimedAction ?? throw new InvalidOperationException("Native timed completion requires its active command.");
        if (nativeActionCommitDepth != 1)
        {
            throw new InvalidOperationException("Native timed command cannot finish within a nested hit transaction.");
        }
        BattleCombatant actor = GetActor(timed.Command.ActorId);
        CompleteNativeTimedActionOwners(actor, timed.Command);
        nativeTimedAction = null;
        if (UsesExpandedRoster)
        {
            nativeActionScratch = nativeActionScratch with { DeferredEffectsE00 = false };
            CompleteNativeActionStatuses(actor);
            nativeActionCommitDepth = 0;
            FlushExpandedEnemyReactions();
            LogRenzokukenBoundary(timed.Command, "ActionComplete", "Renzokuken completed.");
            RefreshNativeKnownMagicStocks();
            RefreshNativeEnemyDrawSlots();
            return;
        }
        nativeActionCommitDepth = 0;
        QueueNativeActionClockCompletion(0x47DE70);
        RefreshNativeKnownMagicStocks();
        RefreshNativeEnemyDrawSlots();
    }

    private void ReleaseNativeRawRenzokukenActionOwners(uint callback)
    {
        if (callback != 0x47DE70 || NativeGraphics is null || nativeTimedAction is not { Command.NativeRenzokuken: not null } timed)
        {
            return;
        }
        BattleCombatant actor = GetActor(timed.Command.ActorId);
        BattleNativeRenzokukenState process = nativeRenzokukenProcesses.GetValueOrDefault(actor.Id) ?? throw new InvalidDataException("Native Renzokuken final descriptor lacks its input process.");
        if (nativeActionCommitDepth != 1 || timed.Contacts != RenzokukenResource(process.MagicId).Timeline.InputThresholds.Length || NativeGraphics.PendingTrigger is { } pending && pending.ActorIndex == actor.EncodeNativeSlot())
        {
            throw new InvalidDataException("Native Renzokuken final descriptor reached an unfinished contact transaction.");
        }
        CompleteNativeTimedActionOwners(actor, timed.Command);
        nativeTimedAction = null;
        nativeActionCommitDepth = 0;
        nativeRenzokukenProcesses.Remove(actor.Id);
        nativeLimitLevels.Remove(actor.Id);
        actor.IsBusy = actions.Any(command => command.ActorId == actor.Id);
        RefreshNativeKnownMagicStocks();
        RefreshNativeEnemyDrawSlots();
    }

    private void CompleteNativeTimedActionOwners(BattleCombatant actor, BattleActionPresentation command)
    {
        if (command.NativeQueuedCommand != 5)
        {
            throw new InvalidDataException("Native timed owner completion requires its original Renzokuken command5.");
        }
        //The retained timed owner is command5, not its251 contacts or250 finisher.
        //48D200's Silence rejection is exclusive to command2 and cannot reject this owner.
        CompleteNativeActionOwners(actor, command, silencedMagic: false);
    }

    private NativeBattleTimedAction? CaptureNativeTimedAction()
    {
        return nativeTimedAction;
    }

    private void LogRenzokukenBoundary(BattleActionPresentation command, string kind, string message)
    {
        BattleActionPresentation? previous = resolvingNativeAction;
        resolvingNativeAction = command;
        try
        {
            Log(kind, command.ActorId, command.TargetId, 0, message);
        }
        finally
        {
            resolvingNativeAction = previous;
        }
    }

    private void RestoreNativeTimedAction(NativeBattleTimedAction? saved)
    {
        nativeTimedAction = null;
        nativeActionCommitDepth = 0;
        if (saved is null)
        {
            return;
        }
        if (saved.Command is null || saved.Command.NativeQueuedCommand != 5 || !byId.TryGetValue(saved.Command.ActorId, out BattleCombatant? actor) || !actor.IsPlayer || !byId.ContainsKey(saved.Command.TargetId) || saved.Contacts < 0)
        {
            throw new InvalidDataException("Saved native timed command lacks its canonical actor, contact or reaction owners.");
        }
        if (UsesExpandedRoster && (saved.Command is not { NativeHeaderIndex: null, NativeTargetMask: 0, NativeAction: null, NativeRenzokuken.CrisisLevel: >= 1 and <= 4, Ability: BattleAbility.LimitBreak, ActionId: 5 << 16 } || actor.Atb != 0))
        {
            throw new InvalidDataException("Saved expanded Renzokuken lacks its logical command5 transaction.");
        }
        actor.IsBusy = true;
        nativeTimedAction = saved;
        nativeActionCommitDepth = 1;
    }
}
