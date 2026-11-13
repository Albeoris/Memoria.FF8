using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public readonly record struct Ff7BattleCameraOffset(short X, short Y, short Z);

public readonly record struct Ff7BattleCameraModelParameters(ushort ScaleX, ushort ScaleY, ushort ScaleZ, short FocusYOffset)
{
    public static Ff7BattleCameraOffset ReadTargetOffset(ReadOnlySpan<byte> actionBank, int index, bool focus)
    {
        if (actionBank.Length < 0x68 || index is < 0 or > 1)
        {
            throw new InvalidDataException("FFVII camera target offset requires an original action header and bank index.");
        }
        int offset = 0x4C + index * 0xC + (focus ? 6 : 0);
        return new(BinaryPrimitives.ReadInt16LittleEndian(actionBank[offset..]), BinaryPrimitives.ReadInt16LittleEndian(actionBank[(offset + 2)..]), BinaryPrimitives.ReadInt16LittleEndian(actionBank[(offset + 4)..]));
    }

    public static Ff7BattleCameraModelParameters Read(ReadOnlySpan<byte> actionBank)
    {
        if (actionBank.Length < 0x68)
        {
            throw new InvalidDataException("FFVII camera model parameters require the complete actor action header.");
        }
        return new(BinaryPrimitives.ReadUInt16LittleEndian(actionBank[0x44..]), BinaryPrimitives.ReadUInt16LittleEndian(actionBank[0x46..]), BinaryPrimitives.ReadUInt16LittleEndian(actionBank[0x48..]), BinaryPrimitives.ReadInt16LittleEndian(actionBank[0x64..]));
    }
}
