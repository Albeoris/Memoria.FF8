using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public sealed partial class BattleAssets
{
    public bool HasEmptyNativeEnemyAiSection(int modelId) => HasEmptyNativeEnemyAiSection(Read($"c0m{modelId:000}.dat"));

    /// <summary>Inspects a valid original DAT directory without inventing an absent AI bank.</summary>
    public static bool HasEmptyNativeEnemyAiSection(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 16)
        {
            return false;
        }
        int sections = BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if (sections is not (2 or 11))
        {
            return false;
        }
        int directoryBytes = (sections + 2) * 4;
        if (bytes.Length < directoryBytes)
        {
            return false;
        }
        int previous = directoryBytes;
        for (int index = 0; index <= sections; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4 + index * 4, 4));
            if (offset < previous || offset > bytes.Length)
            {
                return false;
            }
            previous = offset;
        }
        int aiIndex = sections == 11 ? 7 : 1;
        int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4 + aiIndex * 4, 4));
        int end = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + aiIndex * 4, 4));
        return start == end;
    }
}
