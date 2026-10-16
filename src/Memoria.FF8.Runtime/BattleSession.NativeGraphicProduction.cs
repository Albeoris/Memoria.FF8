using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private void ResolveNativeGraphicCommand(BattleCombatant actor, BattleActionPresentation command, BattleNativeGraphicCommand graphic)
    {
        NativeBattleActionProductionContext context = nativeProductionContext ?? throw new NotSupportedException("Native command252 requires its original485610 dispatch context.");
        byte sequence = ResolveNativeDispatchSequence(actor, 252, command.NativeSequence ?? 0, 1);
        NativeBattleActionHeaderPreparation header = new(checked((byte)actor.NativeIndex), 252, sequence, -1, 1, graphic.MagicId, null);
        (nativeActionPackets, int headerIndex) = nativeActionPackets.Prepare(header);
        nativeActionScratch = nativeActionScratch with { ActorIndex = header.Actor, Command = 252, Action = 1, DeferredEffectsE00 = false };

        //48D200 caseFC leaves AE3=1;48E830 resolves its single target mask before48FE20.
        BattleCombatant[] targets = ResolveNativeActionMask(command.NativeTargetMask);
        foreach (BattleCombatant recipient in combatants.Where(candidate => candidate.NativeIndex is >= 0 and < 7))
        {
            recipient.NativeRemainingHits = targets.Contains(recipient) ? (byte)1 : (byte)0;
        }
        if (NativeFirstHitAdmitted(actor, command, context))
        {
            foreach (BattleCombatant recipient in targets)
            {
                if ((NativeSavedStatusBits(recipient) & 1) != 0 && ((command.NativeTargetMask & 0x4000) == 0 || recipient.NativeIndex >= 3))
                {
                    break;
                }
                BattleNativeAction native = NativeGraphicContactAction(actor, graphic.MagicId);
                BattleActionPresentation resolved = command with { NativeAction = native };
                BattleActionDefinition action = new(BattleAbility.EnemyAbility, 1, "Native graphical effect", BattleActionEffect.Native, 0, false) { NativeAction = native };
                BattleActionPresentation? previous = resolvingNativeAction;
                resolvingNativeAction = resolved;
                try
                {
                    NativeBattleContactPacket packet = ResolveInstalledNativeHitWithContact(actor, recipient, resolved, action, native, 255);
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
            NativeGraphics = QueueNativeDescriptor(graphics, 104, checked(0x1D280C4u + 20u * (uint)headerIndex));
        }
        if (nativeAiDepth == 0 && context.MultiCastMode != 0)
        {
            nativeProductionContext = context with { RepeatMode = unchecked((byte)(context.RepeatMode + 1)) };
        }
    }

    private BattleNativeAction NativeGraphicContactAction(BattleCombatant actor, byte effect)
    {
        //48FE20's first switch defaults to actorBD5; its second switch252 sets ADD1 without4922B0.
        return new(252, 1, -1, -1) { MagicId = effect, AttackType = 0, Power = 0, HitCount = 1, TargetFlags = null, AttackFlags = 3, DamagePresentation = 0, Element = ReadNativeElementAttack(actor), ElementPercent = null, StatusAttack = 0, Statuses0 = 0, Statuses1 = 0, Hit = null, Critical = null, PowerModifier = null, LevelModifier = null, RawRecord = ImmutableArray<byte>.Empty, SupplementalRecord = ImmutableArray<byte>.Empty };
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
