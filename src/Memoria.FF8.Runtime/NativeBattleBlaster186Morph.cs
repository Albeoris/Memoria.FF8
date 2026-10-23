using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleBlaster186Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleBlaster186Morph
{
    public static NativeBattleBlaster186State Initialize(NativeBattleBlaster186State state, NativeBattleBlaster186Resources resources)
    {
        resources.ValidateSources();
        for (int pair = 0; pair < 4; pair++)
        {
            for (int step = 0; step < 4; step++)
            {
                state = Interpolate(state, resources, MutableFrameAddresses[pair * 4 + step], SourceAddresses[pair], SourceAddresses[(pair + 1) % 4], 0x333 * (step + 1));
            }
        }
        return state;
    }

    public static NativeBattleBlaster186State Interpolate(NativeBattleBlaster186State state, NativeBattleBlaster186Resources resources, uint destination, uint from, uint to, int scale)
    {
        if (!MutableFrameAddresses.Contains(destination))
        {
            throw new NotSupportedException("Blaster morph output requires one of its sixteen original physical frame banks.");
        }
        byte[] first = resources.ReadSourceVertices(from).Read(0, VertexFrameBytes);
        byte[] second = resources.ReadSourceVertices(to).Read(0, VertexFrameBytes);
        for (int vertex = 0; vertex < VertexCount; vertex++)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                int offset = vertex * VertexStride + axis * 2;
                short left = BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(offset, 2));
                short right = BinaryPrimitives.ReadInt16LittleEndian(second.AsSpan(offset, 2));
                int product = unchecked((right - left) * scale);
                ushort result = unchecked((ushort)(left + (product >> 12)));
                state = state.WriteWord(checked(destination + (uint)offset), result);
            }
        }
        return state;
    }
}
