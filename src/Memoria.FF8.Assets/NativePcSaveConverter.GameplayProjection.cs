using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static partial class NativePcSaveConverter
{
    private static GameState ProjectNativeGameplay(GameState state)
    {
        NativeCharacterVitals source = state.CharacterVitals!;
        if (!source.MagicSystem.RequiresMemoriaFormat)
        {
            return state;
        }
        source.Validate();
        if (source.MagicSystem.Reservations.Length != 0)
        {
            throw new NotSupportedException("Original FF8 export requires all reserved magic casts to finish first.");
        }
        NativeCharacterVitals projected = source.DeepCopy();
        List<NativeMagicStock>[] banks = projected.Records.Select(record => record.MagicInventory.Where(stock => stock.SpellId != 0 && stock.Count != 0).ToList()).ToArray();
        banks[0].AddRange(source.MagicSystem.RecordZeroExtension);
        if (banks.Any(bank => bank.Any(stock => stock.SpellId > 56 || stock.Count > 100) || bank.Select(stock => stock.SpellId).Distinct().Count() != bank.Count))
        {
            throw new InvalidDataException("Original FF8 export requires valid, distinct magic stock within each saved physical bank.");
        }
        if (source.MagicSystem.SharedMagicStock)
        {
            foreach (NativeMagicStock stock in banks[0].ToArray())
            {
                int? owner = MagicJunctionPolicy.GetOwner(source, stock.SpellId);
                if (owner is > 0)
                {
                    int remaining = MoveNativeMagic(banks[owner.Value], stock);
                    int index = banks[0].FindIndex(entry => entry.SpellId == stock.SpellId);
                    if (remaining == 0)
                    {
                        banks[0].RemoveAt(index);
                    }
                    else
                    {
                        banks[0][index] = stock with { Count = (byte)remaining };
                    }
                }
            }
        }
        for (int row = 0; row < banks.Length; row++)
        {
            while (banks[row].Count > 32)
            {
                int index = banks[row].FindLastIndex(stock => !projected.Records[row].Junction.MagicJunctions.Take(19).Contains(stock.SpellId));
                NativeMagicStock stock = banks[row][index];
                int remaining = stock.Count;
                for (int target = 0; target < banks.Length && remaining != 0; target++)
                {
                    if (target != row && banks[target].Count <= 32)
                    {
                        remaining = MoveNativeMagic(banks[target], stock with { Count = (byte)remaining }, 32);
                    }
                }
                if (remaining != 0)
                {
                    throw new NotSupportedException("Original FF8 export cannot fit the saved magic stock into eight native banks of thirty-two spells.");
                }
                banks[row].RemoveAt(index);
            }
        }
        for (int row = 0; row < banks.Length; row++)
        {
            projected.Records[row] = projected.Records[row] with { MagicInventory = banks[row].Concat(Enumerable.Repeat(new NativeMagicStock(0, 0), 32 - banks[row].Count)).ToArray() };
        }
        projected = projected with { MagicSystem = new MagicSystemState() };
        projected = MagicModeCoordinator.Preview(projected, new GameplayPreferences()).Vitals;
        return state with { CharacterVitals = projected };
    }

    private static int MoveNativeMagic(List<NativeMagicStock> target, NativeMagicStock stock, int capacity = int.MaxValue)
    {
        int index = target.FindIndex(entry => entry.SpellId == stock.SpellId);
        if (index >= 0)
        {
            int accepted = Math.Min(stock.Count, 100 - target[index].Count);
            target[index] = target[index] with { Count = (byte)(target[index].Count + accepted) };
            return stock.Count - accepted;
        }
        if (target.Count >= capacity)
        {
            return stock.Count;
        }
        target.Add(stock);
        return 0;
    }
}
