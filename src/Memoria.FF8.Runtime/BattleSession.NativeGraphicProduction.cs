using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void QueueNativeEnemyEntrance(BattleCombatant actor, BattleCombatant target, byte sequence)
    {
        if (NativeGraphics is not { } graphics)
        {
            // TODO(TD-009): Logical-only components retain the participant activation without driving the physical sequence VM.
            Log("NativeSequence", actor.Id, target.Id, sequence, $"Native entrance sequence {sequence}.");
            ActivateNativeParticipant(target);
            return;
        }
        BattleModelOwner targetOwner = CreatePresentationModelOwner(target);
        if (target.IsPlayer || (NativeSavedStatusBits(target) & 1) != 0 || !graphics.ActorModels.Contains(targetOwner))
        {
            throw new NotSupportedException("Native entrance requires the live, loaded physical enemy selected by the script.");
        }
        // 487DF0 selects command 0/id 3. 48FE20 writes ADC from the second operand and suppresses action memory with DF5.
        (nativeActionPackets, BattleActionHeaderReference header) = nativeActionPackets.Prepare(new(CreatePresentationModelOwner(actor), 0, 0, -1, 3, 0, null));
        target.NativeCombatantFlags &= ~0x4000u;
        target.LastNativeHealing = 1;
        StoreNativeAttackClass(target, 3);
        RefreshNativePresentationStatus(target);
        (ushort saved, uint battle) = NativeContactStatusMirror(target);
        NativeBattleContactPresentation presentation = NativeBattleContactPresentations.Complete(sequence, 1, 0, 3, covered: false);
        (nativeActionPackets, _) = nativeActionPackets.AppendContact(new(new(targetOwner, presentation.Sequence, presentation.Flags2, presentation.HitFlags3, saved, 0, battle), null));
        (nativeActionPackets, _) = nativeActionPackets.Publish(1);
        nativeActionScratch = nativeActionScratch with { LastPublishedTargetCountE05 = 1 };
        BeginNativeBattleActionPause();
        graphics = QueueNativeDescriptor(graphics, 112, 0);
        graphics = QueueNativeActionDescriptor(graphics, header);
        graphics = QueueNativeModelDescriptor(graphics, 113, 0x487670, targetOwner);
        NativeGraphics = QueueNativeDescriptor(graphics, 10, 0x47DD90);
    }

    private void ResolveNativeGraphicCommand(BattleCombatant actor, BattleActionPresentation command, BattleNativeGraphicCommand graphic)
    {
        NativeBattleActionProductionContext context = nativeProductionContext ?? throw new NotSupportedException("Native command252 requires its original485610 dispatch context.");
        byte sequence = ResolveNativeDispatchSequence(actor, 252, command.NativeSequence ?? 0, 1);
        NativeBattleActionHeaderPreparation header = new(CreatePresentationModelOwner(actor), 252, sequence, -1, 1, graphic.MagicId, null);
        (nativeActionPackets, BattleActionHeaderReference headerIndex) = nativeActionPackets.Prepare(header);
        nativeActionScratch = nativeActionScratch with { ActorIndex = checked((byte)actor.EncodeNativeSlot()), Command = 252, Action = 1, DeferredEffectsE00 = false };

        //48D200 caseFC leaves AE3=1;48E830 resolves its single target mask before48FE20.
        BattleCombatant[] targets = ResolveNativeActionMask(command.NativeTargetMask);
        foreach (BattleCombatant recipient in combatants.Where(candidate => candidate.EncodeNativeSlot() is >= 0 and < 7))
        {
            recipient.NativeRemainingHits = targets.Contains(recipient) ? (byte)1 : (byte)0;
        }
        bool aborted = targets.Length == 0 || !NativeFirstHitAdmitted(actor, command, context);
        if (!aborted)
        {
            foreach (BattleCombatant recipient in targets)
            {
                if ((NativeSavedStatusBits(recipient) & 1) != 0 && ((command.NativeTargetMask & 0x4000) == 0 || recipient.EncodeNativeSlot() >= 3))
                {
                    aborted = true;
                    break;
                }
                BattleNativeAction native = NativeGraphicContactAction(actor, graphic.MagicId);
                BattleActionPresentation resolved = command with { NativeAction = native };
                BattleActionDefinition action = new(BattleAbility.EnemyAbility, 1, "Native graphical effect", BattleActionEffect.Native, 0, false) { NativeAction = native };
                BattleActionPresentation? previous = resolvingNativeAction;
                resolvingNativeAction = resolved;
                try
                {
                    NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, recipient, resolved, action, native);
                    (nativeActionPackets, _) = nativeActionPackets.AppendContact(packet);
                }
                finally
                {
                    resolvingNativeAction = previous;
                }
            }
        }
        byte count = checked((byte)targets.Length);
        (nativeActionPackets, _) = nativeActionPackets.Publish(count);
        nativeActionScratch = nativeActionScratch with { LastPublishedTargetCountE05 = count };
        if (NativeGraphics is { } graphics)
        {
            NativeGraphics = QueueNativeActionDescriptor(graphics, headerIndex);
        }
        if (nativeAiDepth == 0 && context.MultiCastMode != 0 && !aborted)
        {
            nativeProductionContext = context with { RepeatMode = unchecked((byte)(context.RepeatMode + 1)) };
        }
    }

    private BattleNativeAction NativeGraphicContactAction(BattleCombatant actor, byte effect)
    {
        //48FE20's first switch defaults to actorBD5; its second switch252 sets ADD1 without4922B0.
        return new(252, 1, -1, -1) { MagicId = effect, Name = "Native graphical effect", AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = UsesExpandedRoster ? (byte)(3 | NativeGraphicsTargetFlags()) : (byte)3, DamagePresentation = 0, Element = UsesExpandedRoster ? checked((byte)actor.ElementAttack) : ReadNativeElementAttack(actor), ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
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
