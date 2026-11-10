using System.Buffers.Binary;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

/// <summary>Explicit debug result policy: defeat the formation's original enemies without combat turns, damage, steals or morphs.</summary>
public static class Ff7DebugVictory
{
    public static Ff7Progress Apply(Ff7Progress progress, SceneFormation formation, IReadOnlyList<KernelSection> kernel, long fieldGeneration, long requestId)
    {
        if (kernel.Count < 9)
        {
            throw new InvalidDataException("FFVII victory requires its original growth and equipment kernel sections.");
        }
        Ff7BattleRng random = new(kernel[2].Data.Span, Ff7BattleRng.BeginBattle(progress.BattleRng?.CrtSeed ?? 1));
        return ApplyResolved(progress, formation, kernel, fieldGeneration, requestId, random);
    }

    internal static Ff7Progress ApplyResolved(Ff7Progress progress, SceneFormation formation, IReadOnlyList<KernelSection> kernel, long fieldGeneration, long requestId, Ff7BattleRng random, Func<SceneEnemyInstance, bool>? defeated = null, int? dormantGuestStoryCharacter = null)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(formation);
        progress.Validate();
        Ff7BattleReceipt? previous = progress.CompletedBattles.SingleOrDefault(receipt => receipt.FieldGeneration == fieldGeneration && receipt.RequestId == requestId);
        if (previous is not null)
        {
            if (previous.Formation != formation.FormationId)
            {
                throw new InvalidDataException("The FFVII victory request changed formation identity.");
            }
            return progress;
        }
        if (kernel.Count < 9)
        {
            throw new InvalidDataException("FFVII victory requires its original growth and equipment kernel sections.");
        }
        Ff7BattleReward reward = ResolveRewards(formation, random, defeated);
        Ff7NativeGrowth growth = new(KernelGrowthTables.Read(kernel[2].Data.Span));
        List<Ff7MateriaState> materia = [.. progress.Materia];
        Dictionary<int, Ff7CharacterState> changed = [];
        IEnumerable<int> ordered = progress.Party.Where(id => id != 255).Concat(progress.Characters.Where(character => !progress.Party.Contains(character.Id) && (progress.AvailableCharacters & 1 << character.Id) != 0).Select(character => character.Id));
        foreach (int id in ordered)
        {
            if (id == dormantGuestStoryCharacter)
            {
                continue;
            }
            Ff7CharacterState character = progress.Characters.Single(character => character.Id == id);
            bool active = progress.Party.Contains(id);
            if (active && character.HitPoints == 0)
            {
                continue;
            }
            byte[] record = (byte[])character.NativeRecord.Clone();
            if (active)
            {
                ApplyMateria(record, reward.AbilityPoints, kernel, materia);
            }
            record = growth.Apply(record, active ? reward.Experience : reward.Experience / 2, random);
            changed.Add(id, character with { NativeRecord = record, Level = record[1], Experience = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(60)), HitPoints = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(44)), MagicPoints = BinaryPrimitives.ReadUInt16LittleEndian(record.AsSpan(48)) });
        }
        Ff7CharacterState[] characters = progress.Characters.Select(character => changed.GetValueOrDefault(character.Id, character)).ToArray();
        List<Ff7InventorySlot> inventory = [.. progress.Inventory];
        foreach (Ff7RewardItem item in reward.Items)
        {
            if (item.ItemId >= 384)
            {
                for (int count = 0; count < item.Quantity && materia.Count < 200; count++)
                {
                    materia.Add(new(item.ItemId - 384, 0));
                }
                continue;
            }
            int index = inventory.FindIndex(slot => slot.ItemId == item.ItemId);
            if (index >= 0)
            {
                inventory[index] = inventory[index] with { Quantity = Math.Min(99, inventory[index].Quantity + item.Quantity) };
            }
            else if (inventory.Count < 320)
            {
                inventory.Add(new(item.ItemId, Math.Min(99, item.Quantity)));
            }
        }
        Ff7Progress result = progress with { Characters = characters, Inventory = inventory.ToArray(), Materia = materia.ToArray(), Gil = unchecked(progress.Gil + reward.Gil), BattleRng = random.Capture(), CompletedBattles = [.. progress.CompletedBattles, new(fieldGeneration, requestId, checked((ushort)formation.FormationId), reward)] };
        result.Validate();
        return result;
    }

    public static Ff7BattleReward ResolveRewards(SceneFormation formation, Ff7BattleRng random, Func<SceneEnemyInstance, bool>? defeated = null)
    {
        uint experience = 0;
        uint gil = 0;
        uint ap = 0;
        List<Ff7RewardItem> items = [];
        foreach (SceneEnemyInstance instance in formation.Enemies.Where(enemy => enemy.EnemyId != -1 && (defeated is null || defeated(enemy))))
        {
            SceneEnemyReward enemy = formation.EnemyRewards.Single(enemy => enemy.EnemyId == instance.EnemyId);
            experience = unchecked(experience + enemy.Experience);
            gil = unchecked(gil + enemy.Gil);
            ap = unchecked(ap + enemy.AbilityPoints);
            foreach (SceneItemSlot slot in enemy.Items)
            {
                if ((slot.Rate & 192) != 0)
                {
                    continue;
                }
                if ((random.NextByte() & 63) > (slot.Rate & 63))
                {
                    continue;
                }
                if (slot.ItemId != -1)
                {
                    if (slot.ItemId is < 0 or > 638)
                    {
                        throw new NotSupportedException("The FFVII reward item identifier is unsupported.");
                    }
                    int existing = items.FindIndex(item => item.ItemId == slot.ItemId);
                    if (existing >= 0)
                    {
                        items[existing] = items[existing] with { Quantity = items[existing].Quantity + 1 };
                    }
                    else
                    {
                        items.Add(new(slot.ItemId, 1));
                    }
                }
                break;
            }
        }
        return new(gil, experience, ap, items.ToArray());
    }

    private static void ApplyMateria(byte[] record, uint ap, IReadOnlyList<KernelSection> kernel, List<Ff7MateriaState> inventory)
    {
        int weapon = record[28];
        int armor = record[29];
        if (weapon >= kernel[5].Data.Length / 44 || armor >= kernel[6].Data.Length / 36)
        {
            throw new InvalidDataException("FFVII equipped weapon or armor is outside the original kernel table.");
        }
        int weaponRate = kernel[5].Data.Span[weapon * 44 + 6];
        int armorRate = kernel[6].Data.Span[armor * 36 + 17];
        for (int slot = 0; slot < 16; slot++)
        {
            int offset = 64 + slot * 4;
            uint encoded = BinaryPrimitives.ReadUInt32LittleEndian(record.AsSpan(offset));
            int id = (byte)encoded;
            uint oldAp = encoded >> 8;
            if (id is 255 or 44 || oldAp == 0xFFFFFF)
            {
                continue;
            }
            int rate = slot < 8 ? weaponRate : armorRate;
            int multiplier = rate switch { 0 => 0, 2 => 2, 3 => 3, _ => 1 };
            uint updatedAp = unchecked(oldAp + (ushort)ap * (uint)multiplier) & 0xFFFFFF;
            if (id >= kernel[8].Data.Length / 20)
            {
                throw new InvalidDataException("FFVII equipped materia is outside the original kernel table.");
            }
            ReadOnlySpan<byte> definition = kernel[8].Data.Span.Slice(id * 20, 20);
            int maximum = 0;
            for (int level = 0; level < 4; level++)
            {
                ushort threshold = BinaryPrimitives.ReadUInt16LittleEndian(definition.Slice(level * 2));
                if (threshold != 65535)
                {
                    maximum = Math.Max(maximum, threshold * 100);
                }
            }
            if (maximum > 0 && oldAp < maximum && updatedAp >= maximum)
            {
                if (inventory.Count < 200)
                {
                    inventory.Add(new(id, 0));
                }
                updatedAp = 0xFFFFFF;
            }
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(offset), (updatedAp << 8) | (uint)id);
        }
    }
}
