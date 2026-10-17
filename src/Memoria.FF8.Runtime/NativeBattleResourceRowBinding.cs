namespace Memoria.FF8.Runtime;

/// <summary>Original502270 pointer rebinding against the caller's current addressed resource owners.</summary>
public static class NativeBattleResourceRowBinding
{
    public static void Bind502270(uint skeletonObject, uint destinationRow, byte replacementMask, Func<uint, uint> readDword, Action<uint, uint> writeDword)
    {
        uint sourceRow = readDword(unchecked(skeletonObject + 4));
        for (int field = 0; field < 4; field++)
        {
            if ((replacementMask & (1 << field)) == 0)
            {
                uint offset = (uint)field * 4;
                uint value = readDword(unchecked(sourceRow + offset));
                writeDword(unchecked(destinationRow + offset), value);
            }
        }
        writeDword(unchecked(skeletonObject + 4), destinationRow);
    }
}
