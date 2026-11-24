using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record BattleWeaponInstance(int Id, int WeaponId, int OwnerCharacterId);

public sealed record BattleWeaponInventory
{
    public ImmutableSortedDictionary<int, BattleWeaponInstance> Instances { get; init; } = ImmutableSortedDictionary<int, BattleWeaponInstance>.Empty;
    public ImmutableSortedDictionary<int, int> Equipped { get; init; } = ImmutableSortedDictionary<int, int>.Empty;

    public BattleWeaponInstance? GetEquipped(int character)
    {
        return Equipped.TryGetValue(character, out int instance) ? Instances[instance] : null;
    }

    internal BattleWeaponInventory Add(int character, int weapon)
    {
        int id = Instances.Count == 0 ? 1 : checked(Instances.Keys.Last() + 1);
        return this with { Instances = Instances.Add(id, new(id, weapon, character)), Equipped = Equipped.SetItem(character, id) };
    }

    internal BattleWeaponInventory Equip(int character, int id)
    {
        if (!Instances.TryGetValue(id, out BattleWeaponInstance? selected))
        {
            throw new InvalidOperationException("This weapon instance is unavailable.");
        }
        BattleWeaponInventory next = this with { Equipped = Equipped.SetItem(character, id) };
        if (selected.OwnerCharacterId == character)
        {
            return next;
        }
        BattleWeaponInstance current = GetEquipped(character) ?? throw new InvalidOperationException("The recipient has no equipped weapon to exchange.");
        int other = selected.OwnerCharacterId;
        return next with
        {
            Equipped = next.Equipped.SetItem(other, current.Id),
            Instances = Instances.SetItem(selected.Id, selected with { OwnerCharacterId = character }).SetItem(current.Id, current with { OwnerCharacterId = other })
        };
    }

    internal BattleWeaponInventory Remodel(int character, int weapon)
    {
        BattleWeaponInstance current = GetEquipped(character) ?? throw new InvalidOperationException("The character has no equipped weapon to remodel.");
        return this with { Instances = Instances.SetItem(current.Id, current with { WeaponId = weapon }) };
    }

    public void Validate()
    {
        if (Instances is null || Equipped is null)
        {
            throw new InvalidDataException("The weapon inventory is incomplete.");
        }
        foreach ((int id, BattleWeaponInstance item) in Instances)
        {
            if (id <= 0 || item is null || item.Id != id || item.WeaponId < 0 || item.OwnerCharacterId is < 0 or > 10 || !Equipped.ContainsKey(item.OwnerCharacterId))
            {
                throw new InvalidDataException($"Invalid weapon instance {id}.");
            }
        }
        foreach ((int character, int id) in Equipped)
        {
            if (!Instances.TryGetValue(id, out BattleWeaponInstance? item) || item.OwnerCharacterId != character)
            {
                throw new InvalidDataException($"Invalid equipped weapon for character {character}.");
            }
        }
    }
}
