using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Memoria.FF7.Resources;

public sealed record Ff7WorldTextureDescriptor(string ResourceName, ushort UOffset, ushort VOffset, IReadOnlyList<string>? AnimationFrames = null);

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<Ff7WorldTextureDescriptor> LoadWorldTextureDescriptors(Ff7Installation installation, int world)
    {
        (uint namesAddress, uint offsetsAddress, int count) = world switch
        {
            0 => (0x969CE8u, 0x96A330u, 282),
            2 => (0x96A300u, 0x96A798u, 8),
            3 => (0x96A320u, 0x96A7B8u, 4),
            _ => throw new NotSupportedException("FFVII world texture tables require an original supported world number.")
        };
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] pointers = ReadVirtual(stream, pe, namesAddress, count * 4);
        byte[] offsets = ReadVirtual(stream, pe, offsetsAddress, count * 4);
        Dictionary<int, IReadOnlyList<string>> animations = new();
        if (world == 0)
        {
            byte[] ids = ReadVirtual(stream, pe, 0x96B448, 22 * 2);
            byte[] framePointers = ReadVirtual(stream, pe, 0x96A150, 108 * 4);
            int firstFrame = 0;
            for (int group = 0; group < 22; group++)
            {
                int textureIndex = BinaryPrimitives.ReadUInt16LittleEndian(ids.AsSpan(group * 2));
                int frameCount = group < 5 ? 8 : 4;
                string[] frames = new string[frameCount];
                for (int frame = 0; frame < frameCount; frame++)
                {
                    frames[frame] = ReadWorldTextureName(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(framePointers.AsSpan((firstFrame + frame) * 4)));
                }
                animations.Add(textureIndex, Array.AsReadOnly(frames));
                firstFrame += frameCount;
            }
        }
        Ff7WorldTextureDescriptor[] result = new Ff7WorldTextureDescriptor[count];
        for (int index = 0; index < count; index++)
        {
            animations.TryGetValue(index, out IReadOnlyList<string>? frames);
            string name = frames?[0] ?? ReadWorldTextureName(stream, pe, BinaryPrimitives.ReadUInt32LittleEndian(pointers.AsSpan(index * 4)));
            result[index] = new(name, BinaryPrimitives.ReadUInt16LittleEndian(offsets.AsSpan(index * 4)), BinaryPrimitives.ReadUInt16LittleEndian(offsets.AsSpan(index * 4 + 2)), frames);
        }
        return Array.AsReadOnly(result);
    }

    private static string ReadWorldTextureName(Stream stream, PEReader pe, uint address)
    {
        byte[] bytes = ReadVirtual(stream, pe, address, 32);
        int terminator = Array.IndexOf(bytes, (byte)0);
        if (terminator < 1 || bytes.AsSpan(0, terminator).ContainsAnyInRange((byte)128, byte.MaxValue))
        {
            throw new InvalidDataException("FFVII world texture name is not bounded ASCII.");
        }
        string name = Encoding.ASCII.GetString(bytes, 0, terminator);
        if (Path.GetFileName(name) != name || !name.EndsWith(".tim", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("FFVII world texture identity is invalid.");
        }
        return Path.ChangeExtension(name, ".tex");
    }
}
