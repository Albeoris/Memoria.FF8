using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleModelRoster ReadExpandedPresentationRoster()
    {
        BattleModelOwner?[] rows = new BattleModelOwner?[NativeBattleGraphicsModelAddress.MaximumRowCount];
        int length = NativeBattleGraphicsModelAddress.NativeRowCount;
        foreach (BattleCombatant actor in combatants)
        {
            if (actor.Slot is null)
            {
                continue;
            }
            byte row = ExpandedPresentationRow(actor);
            if (rows[row] is not null)
            {
                throw new InvalidDataException("Multiple battle actors own the same presentation slot.");
            }
            rows[row] = new(actor.Id, actor.IsPlayer);
            length = Math.Max(length, row + 1);
        }
        return new(rows.AsSpan(0, length).ToArray().ToImmutableArray());
    }

    public byte ReadExpandedPresentationRow(string actorId)
    {
        return ExpandedPresentationRow(GetActor(actorId));
    }

    private static byte RequireExpandedPresentationRow(BattleModelRoster roster, BattleCombatant actor)
    {
        byte row = ExpandedPresentationRow(actor);
        if (!StringComparer.Ordinal.Equals(roster[row].ActorId, actor.Id))
        {
            throw new InvalidDataException("The requested actor does not own its presentation slot.");
        }
        return row;
    }

    private static byte ExpandedPresentationRow(BattleCombatant actor)
    {
        BattleRosterSlot slot = actor.Slot ?? throw new InvalidDataException($"Battle actor {actor.Id} has no allocated presentation slot.");
        if (slot.Side != actor.Side)
        {
            throw new InvalidDataException("The presentation slot belongs to a different battle side.");
        }
        int? native = NativeBattleSlots.TryEncode(slot);
        if (actor.NativeActorIndex is int alias && alias != native)
        {
            throw new InvalidDataException("The native battle alias disagrees with the actor's presentation slot.");
        }
        return slot.Side switch
        {
            BattleSide.Allies when slot.Position < NativeBattleSlots.AllyCount => checked((byte)slot.Position),
            BattleSide.Enemies when slot.Position < NativeBattleSlots.EnemyCount => checked((byte)(NativeBattleSlots.AllyCount + slot.Position)),
            BattleSide.Allies when slot.Position < ExpandedBattleFormation.MaximumAllies => checked((byte)(NativeBattleSlots.EnemyCount + slot.Position)),
            BattleSide.Enemies when slot.Position < ExpandedBattleFormation.MaximumEnemies => checked((byte)(ExpandedBattleFormation.MaximumAllies + slot.Position)),
            _ => throw new InvalidDataException("The allocated battle slot exceeds the presentation roster.")
        };
    }
}
