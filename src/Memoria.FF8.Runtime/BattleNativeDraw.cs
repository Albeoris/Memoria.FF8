namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDrawSlot(byte Id, byte Resistance);
public sealed record BattleNativeDrawContext(byte Selection, byte Quantity, byte Added, bool Failed);

public sealed partial class BattleCombatant
{
    private readonly NativeBattleDrawSlot[] nativeDrawSlots = Enumerable.Repeat(new NativeBattleDrawSlot(0, 0), 4).ToArray();
    public IReadOnlyList<NativeBattleDrawSlot> NativeDrawSlots => Array.AsReadOnly(nativeDrawSlots);
    private readonly HashSet<byte> drawnMagicIds = [];
    public IReadOnlyCollection<byte> DrawnMagicIds => drawnMagicIds.Order().ToArray();

    internal void RestoreDrawnMagic(byte[] ids)
    {
        if (ids is null || ids.Any(id => id is 0 or >= 64) || ids.Distinct().Count() != ids.Length)
        {
            throw new InvalidDataException("Consumed Draw magic requires distinct spell IDs.");
        }
        drawnMagicIds.Clear();
        drawnMagicIds.UnionWith(ids);
    }

    internal void ConsumeDrawMagic(byte id)
    {
        drawnMagicIds.Add(id);
        RestoreDrawSlots(nativeDrawSlots.ToArray());
    }

    internal static NativeBattleDrawSlot[] ExpandDrawSlots(IEnumerable<BattleDrawSpell> spells, IEnumerable<int> guardianForces)
    {
        NativeBattleDrawSlot[] entries = spells.Select(spell => new NativeBattleDrawSlot(checked((byte)spell.Id), checked((byte)spell.Resistance))).Concat(guardianForces.Select(id => new NativeBattleDrawSlot(checked((byte)(64 + id)), 0))).ToArray();
        if (entries.Length > 4)
        {
            throw new InvalidDataException("Native enemy Draw requires four physical slots.");
        }
        return entries.Concat(Enumerable.Repeat(new NativeBattleDrawSlot(0, 0), 4 - entries.Length)).ToArray();
    }

    internal void RestoreDrawSlots(NativeBattleDrawSlot[] slots)
    {
        ValidateDrawSlots(slots);
        for (int index = 0; index < slots.Length; index++)
        {
            NativeBattleDrawSlot slot = slots[index];
            nativeDrawSlots[index] = drawnMagicIds.Contains(slot.Id) ? slot with { Id = 0 } : slot;
        }
    }

    internal static void ValidateDrawSlots(NativeBattleDrawSlot[] slots)
    {
        if (slots is not { Length: 4 } || slots.Any(slot => slot is null || slot.Id >= 80))
        {
            throw new InvalidDataException("Native Draw bank requires four physical spell/GF bytes and their original resistance bytes.");
        }
    }
}
