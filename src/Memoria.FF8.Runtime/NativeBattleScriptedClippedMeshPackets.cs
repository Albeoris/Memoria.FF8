using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleScriptedClippedMeshPackets
{
    public static NativeBattleScriptedMeshSubmission EmitGt3(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, uint handler, ImmutableArray<byte> records, ImmutableArray<uint> depths, uint output, NativeBattleScriptedLightingMemory lighting, NativeBattleScriptedColorRegisters registers, Func<uint, int> resolveBucket, float depthScale)
    {
        if (group.Type != 9 || handler is not (0xB053E0 or 0xB11340))
        {
            throw new NotSupportedException("Native clipped GT3 requires its selected cinematic handler.");
        }
        if (group.Count <= 0 || group.Faces.IsDefault || group.Faces.Length != checked(group.Count * 28) || records.IsDefault || records.Length % 8 != 0 || depths.IsDefault)
        {
            throw new InvalidDataException("Native clipped GT3 requires its complete face, projected and depth owners.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < group.Count; index++)
        {
            ReadOnlySpan<byte> face = group.Faces.AsSpan().Slice(index * 28, 28);
            uint[] screens = new uint[3];
            int[] projectedOffsets = new int[3];
            int[] rawDepth = new int[4];
            uint sum = 0;
            ushort intersection = ushort.MaxValue;
            for (int corner = 0; corner < 3; corner++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(18 + corner * 2));
                if ((offset & 7) != 0 || offset > records.Length - 8 || offset / 8 >= depths.Length)
                {
                    throw new InvalidDataException("Native clipped GT3 leaves its current projected vertex or depth records.");
                }
                rawDepth[corner] = NativePrecision53.RoundInteger(unchecked((ushort)depths[offset / 8]) * (double)depthScale);
                projectedOffsets[corner] = offset;
                intersection &= BinaryPrimitives.ReadUInt16LittleEndian(records.AsSpan().Slice(offset + 6, 2));
            }
            byte[] data = new byte[40];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            NativeBattleGeometryPrimitive? primitive = null;
            if (intersection == 0)
            {
                for (int corner = 0; corner < 3; corner++)
                {
                    screens[corner] = BinaryPrimitives.ReadUInt32LittleEndian(records.AsSpan().Slice(projectedOffsets[corner], 4));
                    StoreDword(8 + corner * 12, screens[corner]);
                }
                if (NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) >= 0)
                {
                    data[3] = 9;
                    mask[3] = NativeBattleGfPacketByteState.Known;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        sum += BinaryPrimitives.ReadUInt16LittleEndian(records.AsSpan().Slice(projectedOffsets[corner] + 4, 2));
                        uint color = BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(corner * 4));
                        StoreDword(4 + corner * 12, corner == 0 ? color | 0x34000000u : color);
                        StoreWord(12 + corner * 12, BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(12 + corner * 2)));
                    }
                    StoreWord(14, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(24)) + frame.ReadTemporaryDword(0x54))));
                    StoreWord(26, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(26)) | frame.ReadTemporaryDword(0x50))));
                    int bucket = resolveBucket(unchecked(frame.ReadTemporaryDword(0x5C) + ((sum / 3) & 0x3FFCu)));
                    ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
                    primitive = new(bucket, words, rawDepth.ToImmutableArray());
                }
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated += 40;
            }

            void StoreDword(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }

            void StoreWord(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
            }
        }
        frame = frame.WriteTemporaryDword(0x60, unchecked(output + (uint)allocated));
        return new(frame, new(attempts.ToImmutable(), allocated), lighting, registers);
    }
}
