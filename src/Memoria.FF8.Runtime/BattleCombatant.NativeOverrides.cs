namespace Memoria.FF8.Runtime;

public sealed partial class BattleCombatant
{
    internal int? NativeDisplayNameIndex { get; private set; }

    internal void SetNativeDisplayName(int? index)
    {
        if (index is int selected && (definition.NativeAi is not { } ai || (uint)selected >= ai.DisplayNames.Length || (uint)selected >= ai.NameBuffers.Length || ai.NameBuffers[selected] is not { Length: 24 }))
        {
            throw new InvalidDataException("Native display-name override requires its complete original24-byte text buffer.");
        }
        NativeDisplayNameIndex = index;
    }

    internal void SetNativeElementDefenses(int[] values)
    {
        if (values is not { Length: 8 } || values.Any(value => value is < 0 or > ushort.MaxValue))
        {
            throw new InvalidDataException("Native element defenses require eight original WORD values.");
        }
        definition = definition with { ElementDefenses = values.ToArray() };
    }

    internal void SetNativeElementDefense(int index, int value)
    {
        if ((uint)index >= 8)
        {
            throw new InvalidDataException("Native element-defense write exceeds its eight allocated WORDs.");
        }
        int[] values = definition.ElementDefenses.ToArray();
        values[index] = value;
        SetNativeElementDefenses(values);
    }
}
