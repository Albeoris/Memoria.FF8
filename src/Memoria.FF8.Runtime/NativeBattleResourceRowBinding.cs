namespace Memoria.FF8.Runtime;

/// <summary>Original502270 pointer rebinding against the caller's current addressed resource owners.</summary>
public static class NativeBattleResourceRowBinding
{
    public static void Bind502270(uint skeletonObject, uint destinationRow, byte replacementMask, Func<uint, uint> readDword, Action<uint, uint> writeDword)
    {
        uint sourceRow = readDword(unchecked(skeletonObject + 4));
        CopyFields502270(sourceRow, destinationRow, replacementMask, (row, field) => readDword(unchecked(row + (uint)field * 4)), (row, field, value) => writeDword(unchecked(row + (uint)field * 4), value));
        writeDword(unchecked(skeletonObject + 4), destinationRow);
    }

    public static void CopyFields502270<TRow, TValue>(TRow sourceRow, TRow destinationRow, byte replacementMask, Func<TRow, int, TValue> readField, Action<TRow, int, TValue> writeField)
    {
        for (int field = 0; field < 4; field++)
        {
            if ((replacementMask & (1 << field)) == 0)
            {
                TValue value = readField(sourceRow, field);
                writeField(destinationRow, field, value);
            }
        }
    }
}
