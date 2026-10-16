using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

[System.Text.Json.Serialization.JsonUnmappedMemberHandling(System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record NativeBattleTimedAction(BattleActionPresentation Command, int Contacts);

public sealed partial class BattleSession
{
    private NativeBattleTimedAction? nativeTimedAction;
    private bool suppressNativeImmediateCounter;

    private void BeginNativeTimedAction(BattleActionPresentation command)
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
        nativeTimedAction = new(command, 0);
        nativeAffinityAction = null;
        nativeActionCommitDepth = 1;
    }

    private void ResolveNativeTimedContact(ushort targetMask, BattleNativeAction native, short timing, bool finalContact)
    {
        NativeBattleTimedAction timed = nativeTimedAction ?? throw new InvalidOperationException("Native contact requires its active command transaction.");
        if (nativeActionCommitDepth != 1 || native.Command == 251 && (native.AttackType != 10 || timing is < 0 or > 32))
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
                if (target.NativeIndex is < 0 or >= 7 || targetMask != 1 << target.NativeIndex)
                {
                    throw new InvalidDataException("Native251 contact must retain its originally selected physical recipient.");
                }
                BattleActionDefinition action = new(contact.Ability, contact.ActionId, native.Name ?? "Renzokuken", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
                BattleActionPresentation? previousAction = resolvingNativeAction;
                resolvingNativeAction = contact;
                try
                {
                    ResolveInstalledNativeHit(actor, target, contact, action, native);
                }
                finally
                {
                    resolvingNativeAction = previousAction;
                }
            }
            else
            {
                ResolveAction(actor, target, contact);
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
        NativeBattleTimedAction timed = nativeTimedAction ?? throw new InvalidOperationException("Native timed completion requires its active command.");
        if (nativeActionCommitDepth != 1)
        {
            throw new InvalidOperationException("Native timed command cannot finish within a nested hit transaction.");
        }
        BattleCombatant actor = GetActor(timed.Command.ActorId);
        CompleteNativeActionOwners(actor, timed.Command);
        nativeTimedAction = null;
        nativeActionCommitDepth = 0;
        QueueNativeActionClockCompletion(0x47DE70);
        FlushNativeReactions();
        RefreshNativeKnownMagicStocks();
        RefreshNativeEnemyDrawSlots();
    }

    private NativeBattleTimedAction? CaptureNativeTimedAction()
    {
        return nativeTimedAction;
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
        actor.IsBusy = true;
        nativeTimedAction = saved;
        nativeActionCommitDepth = 1;
    }
}
