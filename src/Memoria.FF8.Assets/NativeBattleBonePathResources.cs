using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Assets;

public static class NativeBattleBonePathResources
{
    public static void Validate(ImmutableArray<byte> bytes, int rootCount)
    {
        int Word(int index)
        {
            if (index < 0 || index >= bytes.Length / 2)
            {
                throw new InvalidDataException("Electrocute bone path leaves its complete source.");
            }
            return BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan().Slice(index * 2, 2));
        }
        if (Word(0) != rootCount)
        {
            throw new InvalidDataException("Electrocute requires its original bone path directory.");
        }
        for (int root = 0; root < rootCount; root++)
        {
            _ = Walk(Word(root + 1), 0);
        }
        int Walk(int index, int depth)
        {
            if (depth >= 32)
            {
                throw new InvalidDataException("Electrocute bone path has excessive nested forks.");
            }
            for (int count = 0; count < bytes.Length / 2; count++)
            {
                int value = Word(index);
                if (value == short.MaxValue)
                {
                    return index + 1;
                }
                if (value < 0)
                {
                    int next = Walk(index + 1, depth + 1);
                    if (next != index - value)
                    {
                        throw new InvalidDataException("Electrocute fork length disagrees with its terminated child path.");
                    }
                    index = next;
                }
                else
                {
                    if (value >= 128 || Word(index + 1) < 0)
                    {
                        throw new InvalidDataException("Electrocute bone path contains an invalid signed bone or vertex selector.");
                    }
                    index += 2;
                }
            }
            throw new InvalidDataException("Electrocute bone path does not terminate inside its source.");
        }
    }

}
