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
        for (int repeat = 0; repeat < roll.Repetitions; repeat++)
        {
            QueueNativeCommand(actor, 16, action.ActionId, sequence, NativeAutomaticActionTargetMask(action.TargetFlags!.Value), crisis);
        }
    }
}
