using static Memoria.FF8.Runtime.BattlePresentationProcess;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueueNativeEnemyEntrance(BattleCombatant actor, BattleCombatant target, byte sequence)
    {
        if (producingExpandedAiCallback)
        {
            BattleModelOwner owner = ReadPresentationModelOwner(target.Id);
            if (target.IsPlayer || (NativeSavedStatusBits(target) & 1) != 0)
            {
                throw new NotSupportedException("Native entrance requires the live enemy selected by the script.");
            }
            NativeBattleActionHeaderPreparation prepared = PrepareNativeEntranceHeader(actor);
            NativeBattleContactPacket contact = PrepareNativeEntranceContact(target, owner, sequence);
            BattleActionPresentation command = new(actor.Id, target.Id, BattleActionPhase.Recovery, SelectBattleTarget(target), BattleAbility.EnemyAbility, 3)
            {
                NativeQueuedCommand = 0,
                NativeSequence = 0,
                NativeAiCallbackStage = nativeAiCallbackStage
            };
            RecordPublishedNativeTargetCount(1);
            BeginNativeBattleActionPause();
            StageExpandedHostedControl(new HostedDispatchControl.Barrier());
            ReserveExpandedAiInvocation(command, prepared);
            StageExpandedHostedInvocation(command, prepared, [new(target.Id, contact)], null);
            StageExpandedHostedControl(new HostedDispatchControl.Model(113, owner));
            StageExpandedHostedControl(new HostedDispatchControl.ResetCounts());
            return;
        }
            // TODO(TD-009): Logical-only components retain the participant activation without driving the physical sequence VM.
            Log("NativeSequence", actor.Id, target.Id, sequence, $"Native entrance sequence {sequence}.");
            ActivateNativeParticipant(target);
            return;
    }

    private static NativeBattleActionHeaderPreparation PrepareNativeEntranceHeader(BattleCombatant actor)
    {
        return new(CreatePresentationModelOwner(actor), 0, 0, -1, 3, 0, null);
    }

    private NativeBattleContactPacket PrepareNativeEntranceContact(BattleCombatant target, BattleModelOwner targetOwner, byte sequence)
    {
        target.NativeCombatantFlags &= ~0x4000u;
        target.LastNativeHealing = 1;
        StoreNativeAttackClass(target, 3);
        RefreshNativePresentationStatus(target);
        (ushort saved, uint battle) = NativeContactStatusMirror(target);
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(sequence, 1, 0, 3, covered: false);
        return new(new(targetOwner, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, saved, 0, battle), null);
    }

    private BattleNativeAction NativeGraphicContactAction(BattleCombatant actor, byte effect)
    {
        //48FE20's first switch defaults to actorBD5; its second switch252 sets ADD1 without4922B0.
        return new(252, 1, -1, -1) { MagicId = effect, Name = "Native graphical effect", AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = (byte)(3 | NativeGraphicsTargetFlags()), DamagePresentation = 0, Element = checked((byte)actor.ElementAttack), ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
    }

    private bool ResolveNativeGraphicHit(BattleCombatant actor, BattleCombatant recipient, BattleNativeAction native)
    {
        if (native.Command != 252)
        {
            return false;
        }
        recipient.NativeCombatantFlags &= ~0x4000u;
        MarkNativeContactFlags(1, 0);
        FinalizeNativeContactPresentation();
        ApplyDamage(actor, recipient, 0, "Native graphical effect", publishDamage: false);
        return true;
    }
}
