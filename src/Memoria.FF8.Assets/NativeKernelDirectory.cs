using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

internal static class NativeKernelDirectory
{
    public static int[] Read(byte[] kernel)
    {
        const int sectionCount = 56;
        const int headerSize = 4 + sectionCount * 4;
        if (kernel.Length < headerSize || BinaryPrimitives.ReadInt32LittleEndian(kernel) != sectionCount)
        {
            throw new InvalidDataException("Native kernel resources require a complete fifty-six-section directory.");
        }
        int[] sections = new int[sectionCount + 1];
        int previous = headerSize;
        for (int index = 0; index < sectionCount; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(kernel.AsSpan(4 + index * 4));
            if (offset < previous || offset > kernel.Length)
            {
                throw new InvalidDataException("Native kernel section offsets must remain ordered within their resource.");
            }
            sections[index] = offset;
            previous = offset;
        }
        sections[sectionCount] = kernel.Length;
        return sections;
    }
}
