using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleMeltdownDrawing
{
    public static NativeBattleGfGeometryBatch Prim612EE0(NativeBattleStaticMeshResource resource, NativeBattleActionMatrix matrix, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, uint flags, int fade, int scrollU, int scrollV, ushort originU, ushort originV, short wrapU, short wrapV)
    {
        ImmutableArray<byte> source = resource.Bytes;
        int cursor = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan());
        int[] lengths = [12, 12, 20, 24, 20, 24, 28, 36];
        int allocated = 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        uint window = unchecked((((((uint)(originV & 0xF8) | 0xFFFE2000u) << 5 | (uint)(originU & 0xF8)) << 5 | (0u - unchecked((ushort)wrapV) & 0xF8)) << 2) | ((0u - unchecked((ushort)wrapU)) >> 3 & 0x1F));
        int deltaU = scrollU % wrapU;
        int deltaV = scrollV % wrapV;
        for (int group = 0; group < 8; group++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan().Slice(cursor));
            cursor += 4;
            if (group is 0 or 1 or 4 or 6)
            {
                if (count != 0)
                {
                    throw new InvalidDataException("Native Meltdown custom primitive directory contains a nonempty skipped group.");
                }
                continue;
            }
            for (int index = 0; index < count; index++)
            {
                ImmutableArray<byte> face = source.Slice(cursor, lengths[group]);
                int corners = (group & 1) == 0 ? 3 : 4;
                ushort[] offsets = Enumerable.Range(0, corners).Select(corner => BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan().Slice(4 + corner * 2))).ToArray();
                uint projectionFlags = 0;
                int area = 0;
                NativeBattleMeshPacketVariant variant = new(null, null, false, false) { SkipOffscreenRejection = true, ObserveFlags = value => projectionFlags = value, ObserveArea = value => area = value };
                NativeBattleGfGeometryAttempt original = NativeBattleGfGeometryPackets.EmitAttempt(face.AsSpan(), new NativeBattleGfGeometryPackets.ProjectedCorners(offsets, Project), group, new(flags, 0, fade, null, null, 0, 0, 17, 2), colors, allocated, variant: variant);
                bool textured = group != 5;
                int prefix = textured ? 4 : 0;
                byte[] data = new byte[original.PacketLength + (textured ? 8 : 0)];
                NativeBattleGfPacketByteState[] mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Untouched, data.Length).ToArray();
                original.Data.AsSpan().Slice(0, 4).CopyTo(data);
                original.WriteMask.AsSpan().Slice(0, 4).CopyTo(mask);
                original.Data.AsSpan().Slice(4).CopyTo(data.AsSpan(4 + prefix));
                original.WriteMask.AsSpan().Slice(4).CopyTo(mask.AsSpan(4 + prefix));
                BinaryPrimitives.WriteUInt32LittleEndian(data, (uint)(data.Length / 4 - 1) << 24);
                if (textured && (projectionFlags & 0x60000) == 0)
                {
                    int[] uv = Enumerable.Range(0, corners).Select(corner => group == 7 ? 16 + corner * 12 : 16 + corner * 8).ToArray();
                    Scroll(uv, deltaU, wrapU);
                    if (area >= 0 || (flags & (group == 7 ? 0x20u : 0x10u)) != 0)
                    {
                        Scroll(uv.Select(offset => offset + 1).ToArray(), deltaV, wrapV);
                    }
                }
                NativeBattleGeometryPrimitive? primitive = null;
                if (original.Primitive is { } accepted)
                {
                    if (textured)
                    {
                        Put(4, window);
                        Put(data.Length - 4, 0xE2000000);
                    }
                    primitive = accepted with { Words = Enumerable.Range(0, data.Length / 4).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray() };
                    allocated += data.Length;
                }
                attempts.Add(new(original.ByteOffsetRelative, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
                cursor += lengths[group];

                void Put(int offset, uint value)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                    mask.AsSpan(offset, 4).Fill(NativeBattleGfPacketByteState.Known);
                }
                void Scroll(int[] selected, int delta, int wrap)
                {
                    if (delta == 0)
                    {
                        return;
                    }
                    int[] values = selected.Select(offset => data[offset] + delta).ToArray();
                    bool over = values.Any(value => value >= 256);
                    bool under = !over && values.Any(value => value < 0);
                    for (int corner = 0; corner < selected.Length; corner++)
                    {
                        data[selected[corner]] = unchecked((byte)(values[corner] + (over ? -wrap : under ? wrap : 0)));
                        mask[selected[corner]] = NativeBattleGfPacketByteState.Known;
                    }
                }
            }
        }
        return new(attempts.ToImmutable(), allocated);

        NativeBattleModuleProjectedVertex Project(ushort offset)
        {
            ReadOnlySpan<byte> bytes = source.AsSpan().Slice(8 + offset * 4, 6);
            return NativeBattleModuleProjection.Project45FE10(matrix, new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..])), projection);
        }
    }
}
