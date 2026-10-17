using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets;

public sealed record NativeWorldContactShape(short Length, short Width, short Height, short Kind);

public sealed partial class WorldAssets
{
    private byte[]? contactExecutable;
    private readonly Dictionary<byte, NativeWorldContactShape> contactShapes = [];

    public NativeWorldContactShape ReadNativeContactShape(byte shape)
    {
        if (contactShapes.TryGetValue(shape, out NativeWorldContactShape? result))
        {
            return result;
        }
        if (contactExecutable is null)
        {
            contactExecutable = GameDataResources.ReadNativeExecutable(resources);
            if (Convert.ToHexString(SHA256.HashData(contactExecutable)) != NativeFieldRuntimeProfile.FF8EnExecutableHash)
            {
                throw new InvalidDataException("Native contact shapes require the verified original executable.");
            }
        }
        int address = shape < 64 ? shape == 1 ? 0xC76760 : 0xC76768 : 0xC76460 + shape * 8;
        ReadOnlySpan<byte> bytes = contactExecutable;
        int pe = BinaryPrimitives.ReadInt32LittleEndian(bytes[0x3C..]);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(pe + 6)..]);
        int optional = BinaryPrimitives.ReadUInt16LittleEndian(bytes[(pe + 20)..]);
        int rva = address - 0x400000;
        for (int index = 0; index < count; index++)
        {
            int header = pe + 24 + optional + index * 40;
            int start = BinaryPrimitives.ReadInt32LittleEndian(bytes[(header + 12)..]);
            int length = BinaryPrimitives.ReadInt32LittleEndian(bytes[(header + 16)..]);
            if (rva < start || rva >= start + length)
            {
                continue;
            }
            int raw = BinaryPrimitives.ReadInt32LittleEndian(bytes[(header + 20)..]) + rva - start;
            ReadOnlySpan<byte> record = bytes.Slice(raw, 8);
            result = new NativeWorldContactShape(BinaryPrimitives.ReadInt16LittleEndian(record), BinaryPrimitives.ReadInt16LittleEndian(record[2..]), BinaryPrimitives.ReadInt16LittleEndian(record[4..]), BinaryPrimitives.ReadInt16LittleEndian(record[6..]));
            contactShapes.Add(shape, result);
            return result;
        }
        throw new InvalidDataException("Native contact shape address is absent from the original executable.");
    }
}
