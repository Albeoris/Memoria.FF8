using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private ushort? ReadNativeFieldBattleMode()
    {
        if (diagnosticBattle is { } diagnostic)
        {
            return diagnostic.Request.FieldBattleMode;
        }
        return Events.Scene.NativeBattleFlagWrites is { Count: > 0 } history ? history[^1].PersistentFlags : null;
    }

    private uint? ReadNativeElapsedCounter()
    {
        if (diagnosticBattle is { } diagnostic)
        {
            return diagnostic.Request.ElapsedCounter;
        }
        NativeScalarValue value = Events.EventMemory.Read(4, 4);
        return value.IsExact ? unchecked((uint)value.Values[0]) : null;
    }

    private static BattleNativeGrowthChange[] CaptureNativeGrowthChanges(BattleSession active, BattleSnapshot completed)
    {
        List<BattleNativeGrowthChange> changes = [];
        foreach (BattleCombatant actor in active.Combatants.Where(actor => actor.IsPlayer))
        {
            byte row = checked((byte)(actor.MagicRecordId ?? throw new InvalidDataException("Native party growth requires its admitted row.")));
            BattleNativeCharacterGrowth? before = ReadAdmittedPartyMember(completed.Definition, row).NativeGrowth;
            if (before is null || actor.NativeGrowth is not { } after || before.SameSources(after))
            {
                continue;
            }
            changes.Add(new(row, before, after));
        }
        return changes.ToArray();
    }
}
