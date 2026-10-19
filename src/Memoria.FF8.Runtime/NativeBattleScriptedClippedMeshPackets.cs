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
        return EmitPolygon(frame, group, records, depths, output, lighting, registers, resolveBucket, depthScale);
    }

    public static NativeBattleScriptedMeshSubmission EmitFt3(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, uint handler, ImmutableArray<byte> records, ImmutableArray<uint> depths, uint output, NativeBattleScriptedLightingMemory lighting, NativeBattleScriptedColorRegisters registers, Func<uint, int> resolveBucket, float depthScale)
    {
        if (group.Type != 8 || handler != 0xB11100)
        {
            throw new NotSupportedException("Native clipped FT3 requires its selected cinematic handler.");
        }
        return EmitPolygon(frame, group, records, depths, output, lighting, registers, resolveBucket, depthScale);
    }

    public static NativeBattleScriptedMeshSubmission EmitFt4(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, uint handler, ImmutableArray<byte> records, ImmutableArray<uint> depths, uint output, NativeBattleScriptedLightingMemory lighting, NativeBattleScriptedColorRegisters registers, Func<uint, int> resolveBucket, float depthScale)
    {
        if (group.Type != 18 || handler != 0xB11580)
        {
            throw new NotSupportedException("Native clipped FT4 requires its selected cinematic handler.");
        }
        return EmitPolygon(frame, group, records, depths, output, lighting, registers, resolveBucket, depthScale);
    }

    public static NativeBattleScriptedMeshSubmission EmitGt4(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, uint handler, ImmutableArray<byte> records, ImmutableArray<uint> depths, uint output, NativeBattleScriptedLightingMemory lighting, NativeBattleScriptedColorRegisters registers, Func<uint, int> resolveBucket, float depthScale)
    {
        if (group.Type != 19 || handler != 0xB117F0)
        {
            throw new NotSupportedException("Native clipped GT4 requires its selected cinematic handler.");
        }
        return EmitPolygon(frame, group, records, depths, output, lighting, registers, resolveBucket, depthScale);
    }

    private static NativeBattleScriptedMeshSubmission EmitPolygon(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, ImmutableArray<byte> records, ImmutableArray<uint> depths, uint output, NativeBattleScriptedLightingMemory lighting, NativeBattleScriptedColorRegisters registers, Func<uint, int> resolveBucket, float depthScale)
    {
        bool gouraud = group.Type is 9 or 19;
        bool quad = group.Type is 18 or 19;
        int corners = quad ? 4 : 3;
        int stride = quad ? gouraud ? 36 : 24 : gouraud ? 28 : 20;
        int packetLength = quad && gouraud ? 52 : quad || gouraud ? 40 : 32;
        int vertexStride = gouraud ? 12 : 8;
        int indexOffset = quad ? gouraud ? 24 : 12 : gouraud ? 18 : 10;
        int uvOffset = gouraud ? quad ? 16 : 12 : 4;
        if (group.Count <= 0 || group.Faces.IsDefault || group.Faces.Length != checked(group.Count * stride) || records.IsDefault || records.Length % 8 != 0 || depths.IsDefault)
        {
            throw new InvalidDataException("Native clipped GT3 requires its complete face, projected and depth owners.");
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < group.Count; index++)
        {
            ReadOnlySpan<byte> face = group.Faces.AsSpan().Slice(index * stride, stride);
            uint[] screens = new uint[corners];
            int[] projectedOffsets = new int[corners];
            int[] rawDepth = new int[4];
            uint sum = 0;
            ushort intersection = ushort.MaxValue;
            for (int corner = 0; corner < corners; corner++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(indexOffset + corner * 2));
                if ((offset & 7) != 0 || offset > records.Length - 8 || offset / 8 >= depths.Length)
                {
                    throw new InvalidDataException("Native clipped GT3 leaves its current projected vertex or depth records.");
                }
                rawDepth[corner] = NativePrecision53.RoundInteger(unchecked((ushort)depths[offset / 8]) * (double)depthScale);
                projectedOffsets[corner] = offset;
                intersection &= BinaryPrimitives.ReadUInt16LittleEndian(records.AsSpan().Slice(offset + 6, 2));
            }
            byte[] data = new byte[packetLength];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[packetLength];
            NativeBattleGeometryPrimitive? primitive = null;
            if (intersection == 0)
            {
                for (int corner = 0; corner < corners; corner++)
                {
                    screens[corner] = BinaryPrimitives.ReadUInt32LittleEndian(records.AsSpan().Slice(projectedOffsets[corner], 4));
                    StoreDword(8 + corner * vertexStride, screens[corner]);
                }
                if (NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) >= 0)
                {
                    data[3] = unchecked((byte)(packetLength / 4 - 1));
                    mask[3] = NativeBattleGfPacketByteState.Known;
                    for (int corner = 0; corner < corners; corner++)
                    {
                        sum += BinaryPrimitives.ReadUInt16LittleEndian(records.AsSpan().Slice(projectedOffsets[corner] + 4, 2));
                        if (gouraud || corner == 0)
                        {
                            uint color = BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(corner * 4));
                            StoreDword(4 + corner * vertexStride, corner == 0 ? color | (quad ? gouraud ? 0x3C000000u : 0x2C000000u : gouraud ? 0x34000000u : 0x24000000u) : color);
                        }
                        StoreWord(12 + corner * vertexStride, BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(uvOffset + corner * 2)));
                    }
                    StoreWord(14, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(quad ? gouraud ? 32 : 20 : gouraud ? 24 : 16)) + frame.ReadTemporaryDword(0x54))));
                    StoreWord(gouraud ? 26 : 22, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(quad ? gouraud ? 34 : 22 : gouraud ? 26 : 18)) | frame.ReadTemporaryDword(0x50))));
                    int bucket = resolveBucket(unchecked(frame.ReadTemporaryDword(0x5C) + ((sum / (uint)corners) & 0x3FFCu)));
                    ImmutableArray<uint> words = Enumerable.Range(0, packetLength / 4).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
                    primitive = new(bucket, words, rawDepth.ToImmutableArray());
                }
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated += packetLength;
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
