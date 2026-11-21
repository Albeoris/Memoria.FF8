using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace Memoria.FF7.Resources;

public sealed record Ff7SnowboardTextureBinding(int X, int Y, int WidthWords, int Height, string Name, int MaterialType);

public sealed partial class Ff7PcExecutableData
{
    public static IReadOnlyList<Ff7SnowboardTextureBinding> LoadSnowboardTextureBindings(Ff7Installation installation)
    {
        using FileStream stream = OpenSupportedImage(installation);
        using PEReader pe = new(stream, PEStreamOptions.LeaveOpen);
        byte[] records = ReadVirtual(stream, pe, 0x9624A0, 16 * 0x4C);
        Ff7SnowboardTextureBinding[] result = new Ff7SnowboardTextureBinding[16];
        for (int index = 0; index < result.Length; index++)
        {
            ReadOnlySpan<byte> record = records.AsSpan(index * 0x4C, 0x4C);
            uint address = BinaryPrimitives.ReadUInt32LittleEndian(record[0x2C..]);
            byte[] name = ReadVirtual(stream, pe, address, 32);
            int end = Array.IndexOf(name, (byte)0);
            if (end < 0)
            {
                throw new InvalidDataException("The native snowboard texture name is unterminated.");
            }
            result[index] = new(Int(record, 0x10), Int(record, 0x14), Int(record, 0x18), Int(record, 0x1C), Path.ChangeExtension(Encoding.ASCII.GetString(name, 0, end), ".tex"), Int(record, 0x34));
        }
        return Array.AsReadOnly(result);

        static int Int(ReadOnlySpan<byte> record, int offset)
        {
            return BinaryPrimitives.ReadInt32LittleEndian(record[offset..]);
        }
    }
}
