namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    public BattleModelRoster ReadBattlePresentationRoster()
    {
        return new(ReadPresentationModelBindings().Select(binding => binding.Owner));
    }

    public (BattleModelRoster Roster, NativeBattleModelAliases Aliases) ReadBattlePresentationPacketBindings()
    {
        var bindings = ReadPresentationModelBindings().ToArray();
        BattleModelRoster roster = new(bindings.Select(binding => binding.Owner));
        List<(NativeBattleModelSlot Slot, BattleModelOwner Owner)> aliases = [];
        foreach (var binding in bindings)
        {
            if (binding.NativeSlot is int nativeSlot)
            {
                aliases.Add((NativeBattleModelSlot.Decode(checked((byte)nativeSlot)), binding.Owner));
            }
        }
        return (roster, new(roster, aliases));
    }

    private IEnumerable<(BattleRosterSlot Slot, int? NativeSlot, BattleModelOwner Owner)> ReadPresentationModelBindings()
    {
        HashSet<BattleRosterSlot> occupied = [];
        List<(BattleRosterSlot Slot, int? NativeSlot, BattleModelOwner Owner)> bindings = [];
        foreach (BattleCombatant actor in combatants)
        {
            if (actor.Slot is null)
            {
                continue;
            }
            (BattleRosterSlot slot, int? nativeSlot) = ReadPresentationSlot(actor);
            if (!occupied.Add(slot))
            {
                throw new InvalidDataException("Multiple battle actors own the same presentation slot.");
            }
            bindings.Add((slot, nativeSlot, CreatePresentationModelOwner(actor)));
        }

        return bindings.OrderBy(binding => binding.NativeSlot ?? int.MaxValue)
            .ThenBy(binding => binding.Slot.Side)
            .ThenBy(binding => binding.Slot.Position);
    }

    public BattleModelOwner ReadPresentationModelOwner(string actorId)
    {
        BattleCombatant actor = GetActor(actorId);
        ReadPresentationSlot(actor);
        return CreatePresentationModelOwner(actor);
    }

    private static BattleModelOwner RequirePresentationModelOwner(BattleModelRoster roster, BattleCombatant actor)
    {
        ReadPresentationSlot(actor);
        BattleModelOwner owner = CreatePresentationModelOwner(actor);
        if (!roster.Contains(owner))
        {
            throw new InvalidDataException("The requested actor does not own its presentation model.");
        }
        return owner;
    }

    private static (BattleRosterSlot Slot, int? NativeSlot) ReadPresentationSlot(BattleCombatant actor)
    {
        BattleRosterSlot slot = actor.Slot ?? throw new InvalidDataException($"Battle actor {actor.Id} has no allocated presentation slot.");
        if (slot.Side != actor.Side)
        {
            throw new InvalidDataException("The presentation slot belongs to a different battle side.");
        }
        if (actor.NativeActorIndex is int alias && alias != NativeBattleSlots.TryEncode(slot))
        {
            throw new InvalidDataException("The native battle alias disagrees with the actor's presentation slot.");
        }
        return (slot, actor.NativeActorIndex);
    }

    private static BattleModelOwner CreatePresentationModelOwner(BattleCombatant actor)
    {
        return actor.IsPlayer ? new BattlePartyActorModel(new(actor.Id)) : new BattleEnemyActorModel(new(actor.Id));
    }
}
