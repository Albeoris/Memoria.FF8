using System.Buffers.Binary;
using System.Numerics;
using static Memoria.FF8.Runtime.NativeBattleSonic139Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSonic139Preprocessing
{
    public static NativeBattleSonic139Frame Initialize(NativeBattleSonic139Frame frame, NativeBattleSonic139Resources resources)
    {
        frame = Normalize6A9C80(frame, CopiedMeshAddress, resources);
        frame = Reorder6A9DA0(frame);
        for (uint source = 0x11D85A0; source != Frame0Address; source -= FrameLength)
        {
            frame = Normalize6A9C80(frame, source, resources);
        }
        return frame;
    }

    public static NativeBattleGeometryRegisters PublishLeading45DCA0(NativeBattleGeometryRegisters geometry, uint value) => geometry with { Lzcs = value, Lzcr = (uint)BitOperations.LeadingZeroCount(unchecked((int)value) < 0 ? ~value : value) };

    public static NativeBattleSonic139Frame Normalize6A9C80(NativeBattleSonic139Frame frame, uint address, NativeBattleSonic139Resources resources)
    {
        int count = checked((int)frame.State.ReadDword(address + 4));
        if (count != VertexCount)
        {
            throw new InvalidDataException("Sonic Wave normalization requires the actual201-row source.");
        }
        for (int vertex = 0; vertex < count; vertex++)
        {
            uint row = checked(address + 8 + (uint)(vertex * 8));
            int x = unchecked((short)frame.State.ReadWord(row));
            int y = unchecked((short)frame.State.ReadWord(row + 2));
            int squared = unchecked(x * x + y * y);
            uint packed;
            if (squared == 0)
            {
                packed = 0;
            }
            else
            {
                NativeBattleGeometryRegisters geometry = PublishLeading45DCA0(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Sonic Wave normalization requires its current GTE data register owner."), unchecked((uint)squared));
                frame = frame with { Memory = frame.Memory with { GeometryRegisters = geometry } };
                int leading = checked((int)geometry.Lzcr!.Value) & ~1;
                int shift = leading - 24;
                int lookup = shift > 0 ? unchecked(squared << shift) : squared >> (24 - leading);
                ushort seed = BinaryPrimitives.ReadUInt16LittleEndian(resources.Read(unchecked(SqrtLookupAddress + (uint)(lookup * 2)), 2).AsSpan());
                int radius = unchecked(seed << ((31 - leading) >> 1)) >> 12;
                packed = frame.State.ReadDword(row + 4) & 0xFFFFF | unchecked((uint)((radius & ~1) << 19));
                int phase = Math.Clamp(1024 - (radius >> 2), 0, 1023);
                int factor = resources.SineWords4096[phase] >> 2;
                if (radius == 0)
                {
                    radius = 1;
                }
                int nx = unchecked(x * factor) / radius;
                int ny = unchecked(y * factor) / radius;
                packed = packed & 0xFFF00000 | unchecked((uint)nx) & 0x3FF | (unchecked((uint)ny) & 0x3FF) << 10;
            }
            frame = frame with { State = frame.State.WriteDword(row + 4, packed) };
        }
        return frame;
    }

    private static NativeBattleSonic139Frame Reorder6A9DA0(NativeBattleSonic139Frame frame)
    {
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(frame.Staging ?? throw new NotSupportedException("Sonic Wave mesh preprocessing requires its physical temporary owner."), 88);
        NativeBattleFileStaging staging = allocation.Staging;
        uint temporary = allocation.Address;
        NativeBattleEffectArena context = NativeBattleTemporaryMemory.ReadView(staging, temporary, 88).Write(0, BitConverter.GetBytes(CopiedMeshAddress)).Write(4, BitConverter.GetBytes(CopiedMeshAddress + 8));
        uint cursor = checked(CopiedMeshAddress + frame.State.ReadDword(CopiedMeshAddress) + 16);
        context = context.Write(32, BitConverter.GetBytes(cursor));
        int count = checked((int)frame.State.ReadDword(cursor));
        cursor += 4;
        context = context.Write(32, BitConverter.GetBytes(cursor));
        for (int face = 0; face < count; face++)
        {
            ushort[] indices = [frame.State.ReadWord(cursor + 4), frame.State.ReadWord(cursor + 6), frame.State.ReadWord(cursor + 8)];
            uint[] colors = [frame.State.ReadDword(cursor), frame.State.ReadDword(cursor + 12), frame.State.ReadDword(cursor + 16)];
            int[] x = new int[3];
            for (int corner = 0; corner < 3; corner++)
            {
                uint source = checked(CopiedMeshAddress + 8 + indices[corner] * 4u);
                context = NativeBattleEffectNodeContextMemory.Copy(frame.State.ReadView(source, 8), 0, context, 52 + corner * 8, 8);
                x[corner] = unchecked((short)frame.State.ReadWord(source));
            }
            int minimum = x[1] < x[0] ? 1 : 0;
            if (x[2] < x[minimum])
            {
                minimum = 2;
            }
            if (minimum != 0)
            {
                int[] order = minimum == 2 ? [2, 0, 1] : [1, 2, 0];
                for (int corner = 0; corner < 3; corner++)
                {
                    frame = frame with { State = frame.State.WriteWord(cursor + 4 + (uint)(corner * 2), indices[order[corner]]) };
                }
                frame = frame with { State = frame.State.WriteDword(cursor, colors[order[0]]).WriteDword(cursor + 12, colors[order[1]]).WriteDword(cursor + 16, colors[order[2]]) };
            }
            for (int corner = 0; corner < 3; corner++)
            {
                uint indexAddress = cursor + 4 + (uint)(corner * 2);
                ushort original = frame.State.ReadWord(indexAddress);
                frame = frame with { State = frame.State.WriteWord(indexAddress, unchecked((ushort)(original + (original >> 1)))) };
            }
            cursor += 20;
        }
        context = context.Write(32, BitConverter.GetBytes(cursor));
        staging = NativeBattleTemporaryMemory.PublishView(staging, temporary, context, 88);
        return frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(staging, 88) };
    }
}
