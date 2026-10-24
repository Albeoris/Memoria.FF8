using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect207Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect207Lower
{
    public static NativeBattleEffect207Frame Preprocess6070(NativeBattleEffect207Frame frame, uint source, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        int count = checked((int)frame.State.ReadDword(source + 4));
        if (source != NativeBattleEffect207Profile.MutableMeshAddress || count < 1 || count > 241)
        {
            throw new InvalidDataException("Effect207 preprocessing requires its current copied vertex rows.");
        }
        for (int index = 0; index < count; index++)
        {
            uint vertex = checked(source + 8 + (uint)(index * 8));
            int x = unchecked((short)frame.State.ReadWord(vertex));
            int y = unchecked((short)frame.State.ReadWord(vertex + 2));
            int square = unchecked(x * x + y * y);
            if (square == 0)
            {
                frame = frame with { State = frame.State.WriteDword(vertex + 4, 0) };
                continue;
            }
            NativeBattleGeometryRegisters geometry = NativeBattleSonic139Preprocessing.PublishLeading45DCA0(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect207 normalization requires current leading registers."), unchecked((uint)square));
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
            int leading = checked((int)geometry.Lzcr!.Value) & ~1;
            int shift = leading - 24;
            int selector = shift > 0 ? unchecked(square << shift) : square >> (24 - leading);
            uint lookup = unchecked(NativeBattleEffect207Profile.SqrtTableAddress + (uint)(selector * 2));
            ushort seed = BinaryPrimitives.ReadUInt16LittleEndian(resources.ReadStatic(lookup, 2));
            short value = unchecked((short)(unchecked((int)seed << ((31 - leading) >> 1)) >> 17));
            frame = frame with { State = frame.State.WriteWord(vertex + 6, unchecked((ushort)value)) };
        }
        (frame, uint temporary) = Allocate(frame, 88);
        uint polygon = checked(source + frame.State.ReadDword(source) + 16);
        NativeBattleEffectArena context = L207Put(L207Put(L207Put(Context(frame, temporary, 88), 0, source), 4, source + 8), 0x20, polygon);
        frame = L207Reorder(frame, context, false, out context);
        frame = L207Reorder(frame, context, true, out context);
        frame = PublishContext(frame, temporary, context, 88);
        return Release(frame, 88);
    }

    private static NativeBattleEffect207Frame L207Reorder(NativeBattleEffect207Frame frame, NativeBattleEffectArena context, bool quad, out NativeBattleEffectArena output)
    {
        uint cursor = L207U(context, 0x20);
        int count = checked((int)frame.State.ReadDword(cursor));
        cursor += 4;
        context = L207Put(context, 0x20, cursor);
        int corners = quad ? 4 : 3;
        for (int face = 0; face < count; face++)
        {
            ushort[] indices = new ushort[corners];
            uint[] colors = new uint[corners];
            short[] x = new short[corners];
            uint vertices = L207U(context, 4);
            for (int corner = 0; corner < corners; corner++)
            {
                indices[corner] = frame.State.ReadWord(cursor + 4 + (uint)(corner * 2));
                colors[corner] = frame.State.ReadDword(cursor + (uint)(corner == 0 ? 0 : 8 + corner * 4));
                NativeBattleEffectArena row = frame.State.ReadView(checked(vertices + indices[corner] * 4u), 8);
                context = CopyBytes(row, 0, context, 0x34 + corner * 8, 8);
                x[corner] = L207S(row, 0);
            }
            int minimum = 0;
            for (int corner = 1; corner < corners; corner++)
            {
                if (x[corner] < x[minimum])
                {
                    minimum = corner;
                }
            }
            int[] order = minimum switch
            {
                1 when quad => [1, 3, 0, 2],
                2 when quad => [2, 0, 3, 1],
                3 => [3, 2, 1, 0],
                1 => [1, 2, 0],
                2 => [2, 0, 1],
                _ => quad ? [0, 1, 2, 3] : [0, 1, 2]
            };
            for (int corner = 0; corner < corners; corner++)
            {
                ushort index = indices[order[corner]];
                frame = frame with { State = frame.State.WriteWord(cursor + 4 + (uint)(corner * 2), unchecked((ushort)(index + (index >> 1)))) };
                if (minimum != 0)
                {
                    frame = frame with { State = frame.State.WriteDword(cursor + (uint)(corner == 0 ? 0 : 8 + corner * 4), colors[order[corner]]) };
                }
            }
            cursor += (uint)(quad ? 24 : 20);
        }
        output = L207Put(context, 0x20, cursor);
        return frame;
    }
}
