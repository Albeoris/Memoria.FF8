namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private readonly Dictionary<string, BattleNativeSlotsRoll> nativeSlotsRolls = new(StringComparer.Ordinal);

    public BattleNativeSlotsRoll? GetNativeSlotsRoll(string actorId)
    {
        _ = GetActor(actorId);
        return nativeSlotsRolls.GetValueOrDefault(actorId);
    }

    public BattleNativeSlotsRoll RollNativeSlots(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        byte crisis = GetNativeLimitLevel(actorId);
        if (Outcome != BattleOutcome.Active || !actor.IsPlayer || !actor.IsReady || actor.NativeLimit is not { Command: 16 } rules || crisis == 0)
        {
            throw new InvalidOperationException("Slots roll requires Selphie's ready owned Limit selection.");
        }
        if (rules.SlotsSelectionBytes.IsDefaultOrEmpty || rules.SlotsSpellBytes.IsDefaultOrEmpty)
        {
            throw new NotSupportedException("Native Slots roll requires its actual contiguous kernel26/27 tables.");
        }
        BattleNativeSlotsRoll roll = NativeBattleSlotsRules.Roll(rules, actor.Level, crisis, (byte)NextRandomByte(), (byte)NextRandomByte(), (byte)NextRandomByte(), (byte)NextRandomByte());
        _ = FindInstalledNativeAction(16, roll.MagicId);
        nativeSlotsRolls[actorId] = roll;
        return roll;
    }

    private void QueueNativeSlots(BattleCombatant actor, BattleNativeAction action, byte sequence, byte? crisis)
    {
        ValidateNativeActionContract(action);
        if (action.ActionId >= 51 && definition.LimitProgress?.SpecialMagicFlags is null)
        {
            throw new NotSupportedException("Special Slots magic requires the actual saved learned BYTE1CFE771 owner.");
        }
        BattleNativeSlotsRoll roll = nativeSlotsRolls[actor.Id];
        if (roll.Repetitions is < 1 or > 3)
        {
            throw new NotSupportedException("Native Slots input exceeds its three physical target WORDs.");
        }
        ushort mask = NativeAutomaticActionTargetMask(action.TargetFlags!.Value);
        BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 7 && (mask & (1 << candidate.NativeIndex)) != 0) ?? actor;
        BattleAttackCommand command = new(actor.Id, target.Id, BattleAbility.EnemyAbility, action.ActionId) { NativeQueuedCommand = 16, NativeAction = action, NativeTargetMask = mask, NativeSequence = sequence, NativeSlots = roll };
        EnqueueNativePacket(actor, command, action.Name ?? "Slots", playerInput: true);
    }

    private void ResolveNativeSlotsGroup(BattleCombatant actor, BattleActionPresentation command)
    {
        BattleNativeSlotsRoll slots = command.NativeSlots ?? throw new InvalidDataException("Native Slots group lacks its accepted roll.");
        BattleNativeAction native = command.NativeAction ?? throw new InvalidDataException("Native Slots group lacks its installed magic.");
        BattleActionDefinition action = new(command.Ability, command.ActionId, native.Name ?? "Slots", BattleActionEffect.Native, native.Power, false) { NativeAction = native };
        NativeBattleCommandLocation location = nativeDispatchCommandLocation ?? throw new InvalidDataException("Native Slots requires its selected physical command entry.");
        NativeBattleCommandPacket packet = nativeCommandBanks.Banks[location.Bank].Packets[2 * location.Slot];
        for (int repeat = 0; repeat < slots.Repetitions; repeat++)
        {
            ushort mask = packet.ReadWord(6 + 2 * repeat);
            BattleCombatant target = combatants.FirstOrDefault(candidate => candidate.NativeIndex is >= 0 and < 7 && (mask & (1 << candidate.NativeIndex)) != 0) ?? GetActor(command.TargetId);
            if (!ResolveInstalledNativeAction(actor, target, command with { NativeSlots = null, NativeTargetMask = mask }, action))
            {
                break;
            }
        }
    }
}
