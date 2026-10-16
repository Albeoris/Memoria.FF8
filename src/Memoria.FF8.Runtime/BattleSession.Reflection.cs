namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static bool NativeActionReflects(BattleCombatant recipient, BattleNativeAction native)
    {
        return native.Command != 247 && (native.AttackFlags & 0x10) != 0 && (recipient.NativeStatus1 & 0x80) != 0 && native.AttackType is 2 or 3 or 5 or 6 or 8 or 11 or 15 or 20 or 21 or 22 or 26 or 33;
    }

    private void ResolveNativeReflection(BattleCombatant actor, BattleCombatant reflector, BattleActionPresentation command, BattleNativeAction source)
    {
        //487DF0 consumes the first491AD0/493280/491940/493110 reflection receipt, retaining its original caster.
        BattleNativeAction reflected = FindInstalledNativeAction(247, unchecked((byte)source.ActionId));
        BattleCombatant? target = ChooseNativeRandomActionTarget(!reflector.IsPlayer);
        if (target is null)
        {
            return;
        }
        byte flags = reflected.TargetFlags.GetValueOrDefault();
        ushort mask = (ushort)((1 << target.NativeIndex) | ((flags & 1) != 0 ? 0x4000 : 0) | ((flags & 2) != 0 ? 0x2000 : 0));
        BattleAttackCommand packet = new(actor.Id, target.Id, BattleAbility.Magic, reflected.ActionId) { NativeQueuedCommand = 247, NativeAction = reflected, NativeTargetMask = mask, FreeNativeMagic = true };
        if (nativeAiDepth == 0)
        {
            if (!TryWriteNativeSecondCommand(packet))
            {
                //TODO(TD-009): Components without a graphical command bank admit the same second485610 producer within their CPU dispatch
                BattleNativeAction? affinity = nativeAffinityAction;
                ProduceNativeReflectedBankCommand(packet);
                nativeAffinityAction = affinity;
            }
            return;
        }
        BattleActionPresentation reflectedCommand = command with { Ability = BattleAbility.Magic, ActionId = reflected.ActionId, DrawCast = false, NativeDraw = null, DrawCastTargetId = null, NativeQueuedCommand = 247, NativeAction = reflected, NativeTargetMask = mask };
        BattleActionDefinition reflectedDefinition = new(BattleAbility.Magic, reflected.ActionId, reflected.Name ?? "Reflected magic", BattleActionEffect.Native, reflected.Power, actor.IsPlayer == target.IsPlayer) { NativeAction = reflected };
        ResolveInstalledNativeAction(actor, target, reflectedCommand, reflectedDefinition);
    }

    private void ProduceNativeReflectedBankCommand(BattleAttackCommand command)
    {
        if (command.NativeQueuedCommand != 247 || command.NativeAction?.Command != 247 || !command.FreeNativeMagic)
        {
            throw new InvalidDataException("Native reflected second packet lacks its247/E0C provenance.");
        }
        NativeBattleActionProductionContext? previous = nativeProductionContext;
        byte lane = nativeActionScratch.EventLane ?? throw new NotSupportedException("Native reflected485610 requires its selected physical command bank.");
        nativeProductionContext = new(0, lane, true, 0x47DE70);
        try
        {
            BattleAttackCommand produced = ProduceNativeQueuedCommand(GetActor(command.ActorId), GetActor(command.TargetId), command);
            actions.Enqueue(produced);
        }
        finally
        {
            nativeProductionContext = previous;
        }
    }
}
