using System.Buffers.Binary;

namespace Memoria.FF8.Assets;

public sealed record NativeWorldContactShape(short Length, short Width, short Height, short Kind);

public sealed partial class WorldAssets
{
    private IGameNativeMemorySource NativeWorldMemory
    {
        get
        {
            IGameNativeMemorySource memory = GameDataResources.ReadNativeMemory(resources);
            WorldNativeMemoryContracts.RequireSupported(memory);
            return memory;
        }
    }

    private readonly Dictionary<byte, NativeWorldContactShape> contactShapes = [];

    public NativeWorldContactShape ReadNativeContactShape(byte shape)
    {
        if (contactShapes.TryGetValue(shape, out NativeWorldContactShape? result))
        {
            return result;
        }
        uint address = shape < 64 ? shape == 1 ? 0xC76760u : 0xC76768u : 0xC76460u + (uint)shape * 8;
        ReadOnlySpan<byte> record = NativeWorldMemory.Read(address, 8);
        result = new NativeWorldContactShape(BinaryPrimitives.ReadInt16LittleEndian(record), BinaryPrimitives.ReadInt16LittleEndian(record[2..]), BinaryPrimitives.ReadInt16LittleEndian(record[4..]), BinaryPrimitives.ReadInt16LittleEndian(record[6..]));
        contactShapes.Add(shape, result);
        return result;
    }
}
