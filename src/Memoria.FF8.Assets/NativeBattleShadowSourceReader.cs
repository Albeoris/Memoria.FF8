using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public static class NativeBattleShadowSourceReader
{
    public static NativeBattleShadowSource Read(byte[] executable)
    {
        if (Convert.ToHexString(SHA256.HashData(executable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
        {
            throw new NotSupportedException("Native5088A0 constants require the authenticated English executable.");
        }
        using PEReader reader = new(new MemoryStream(executable));
        byte[] matrixBytes = ReadLoaderBytes(0xB8B9D0, 32);
        NativeBattleActionMatrix matrix = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(matrixBytes.AsSpan(2 * index))).ToImmutableArray(), BinaryPrimitives.ReadInt32LittleEndian(matrixBytes.AsSpan(20)), BinaryPrimitives.ReadInt32LittleEndian(matrixBytes.AsSpan(24)), BinaryPrimitives.ReadInt32LittleEndian(matrixBytes.AsSpan(28)), 4096);
        byte[] scale = ReadLoaderBytes(0xB8B9B0, 12);
        if (BinaryPrimitives.ReadInt32LittleEndian(scale) != 4096 || BinaryPrimitives.ReadInt32LittleEndian(scale.AsSpan(4)) != 0 || BinaryPrimitives.ReadInt32LittleEndian(scale.AsSpan(8)) != 4096)
        {
            throw new InvalidDataException("Native5088A0 row scale differs from its original flattening vector.");
        }
        byte[] vertices = ReadLoaderBytes(0x1D999D0, 80);
        NativeBattleShadowSource source = new(matrix, new(4096, 0, 4096), Enumerable.Range(0, 10).Select(index => new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan(8 * index)), BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan(8 * index + 2)), BinaryPrimitives.ReadInt16LittleEndian(vertices.AsSpan(8 * index + 4)))).ToImmutableArray());
        source.Validate();
        return source;

        byte[] ReadLoaderBytes(uint address, int count)
        {
            long relative = address - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(candidate => relative >= candidate.VirtualAddress && relative + count <= candidate.VirtualAddress + Math.Max(candidate.VirtualSize, candidate.SizeOfRawData));
            int inSection = checked((int)(relative - section.VirtualAddress));
            int available = Math.Clamp(section.SizeOfRawData - inSection, 0, count);
            byte[] data = new byte[count];
            if (available != 0)
            {
                executable.AsSpan(section.PointerToRawData + inSection, available).CopyTo(data);
            }
            return data;
        }
    }
}
