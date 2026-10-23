using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleBreath129Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBreath129Preprocessing
{
    public static NativeBattleBreath129Frame Initialize(NativeBattleBreath129Frame frame, NativeBattleBreath129Resources resources)
    {
        frame = Normalize6B7CB0(frame, MeshAddress, resources);
        return Normalize6B7CB0(frame, IndexedVerticesAddress, resources);
    }

    public static NativeBattleBreath129Frame Normalize6B7CB0(NativeBattleBreath129Frame frame, uint source, NativeBattleBreath129Resources resources)
    {
        int count = checked((int)frame.State.ReadDword(source + 4));
        if (source != MeshAddress && source != IndexedVerticesAddress || count != (source == MeshAddress ? VertexCount : 88))
        {
            throw new InvalidDataException("Breath preprocessing requires its actual addressed vertex source.");
        }
        int minimum = Word(source + 28);
        int maximum = minimum;
        for (int index = 0; index < count - 1; index++)
        {
            int value = Word(checked(source + 28 + (uint)(index * 8)));
            if (value < minimum)
            {
                minimum = value;
            }
            else if (value > maximum)
            {
                maximum = value;
            }
        }
        int divisor = maximum - minimum;
        for (int index = 0; index < count; index++)
        {
            uint coordinate = checked(source + 12 + (uint)(index * 8));
            int numerator = unchecked((int)(short)frame.State.ReadWord(coordinate) << 12);
            frame = frame with { State = frame.State.WriteWord(coordinate + 2, unchecked((ushort)(numerator / divisor))) };
        }
        return frame;

        short Word(uint address)
        {
            if (address >= MeshAddress && address + 2 <= CameraAddress)
            {
                return unchecked((short)frame.State.ReadWord(address));
            }
            return BinaryPrimitives.ReadInt16LittleEndian(resources.Read(address, 2).AsSpan());
        }
    }
}
