using System.Buffers.Binary;

namespace Memoria.FF7.Formats;

public readonly record struct Ff7BattleCameraModelParameters(ushort ScaleX, ushort ScaleY, ushort ScaleZ, short FocusYOffset)
{
    public static Ff7BattleCameraModelParameters Read(ReadOnlySpan<byte> actionBank)
    {
        if (actionBank.Length < 0x68)
        {
            throw new InvalidDataException("FFVII camera model parameters require the complete actor action header.");
        }
        return new(BinaryPrimitives.ReadUInt16LittleEndian(actionBank[0x44..]), BinaryPrimitives.ReadUInt16LittleEndian(actionBank[0x46..]), BinaryPrimitives.ReadUInt16LittleEndian(actionBank[0x48..]), BinaryPrimitives.ReadInt16LittleEndian(actionBank[0x64..]));
    }
}
