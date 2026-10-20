using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Authenticated ordinary cinematic material and normal-lit packet writers over current projected owners.</summary>
public static class NativeBattleScriptedMeshPackets
{
    public static NativeBattleScriptedMeshSubmission EmitSelected(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, uint actualHandler, int shapeExtent, ImmutableArray<byte> projectedRecords, ImmutableArray<uint> depths, uint outputPointer, NativeBattleScriptedLightingMemory lighting, NativeBattleScriptedColorRegisters registers, Func<uint, int> resolveActualOtBucket, float depthScale = 1f)
    {
        if (group.Type == 18 && actualHandler == 0xAAAE60)
        {
            return NativeBattleApocalypse221DepthQuad58.Emit(frame, group, actualHandler, projectedRecords, depths, outputPointer, registers, resolveActualOtBucket, depthScale);
        }
        if (group.Type == 19 && actualHandler == 0xB117F0)
        {
            return NativeBattleScriptedClippedMeshPackets.EmitGt4(frame, group, actualHandler, projectedRecords, depths, outputPointer, lighting, registers, resolveActualOtBucket, depthScale);
        }
        if (group.Type == 18 && actualHandler == 0xB11580)
        {
            return NativeBattleScriptedClippedMeshPackets.EmitFt4(frame, group, actualHandler, projectedRecords, depths, outputPointer, lighting, registers, resolveActualOtBucket, depthScale);
        }
        if (group.Type == 8 && actualHandler == 0xB11100)
        {
            return NativeBattleScriptedClippedMeshPackets.EmitFt3(frame, group, actualHandler, projectedRecords, depths, outputPointer, lighting, registers, resolveActualOtBucket, depthScale);
        }
        if (group.Type == 9 && actualHandler is 0xB053E0 or 0xB11340)
        {
            return NativeBattleScriptedClippedMeshPackets.EmitGt3(frame, group, actualHandler, projectedRecords, depths, outputPointer, lighting, registers, resolveActualOtBucket, depthScale);
        }
        (uint expected, int stride, int length, int corners, int indices) = group.Type switch
        {
            6 => (0xAEB5B0u, 12, 28, 3, 4),
            2 => (0xB5C720u, 20, 28, 3, 14),
            7 => (0xB27E90u, 20, 36, 3, 12),
            8 => (0xB28110u, 20, 32, 3, 10),
            9 => (0xB283B0u, 28, 40, 3, 18),
            12 => (0xB28690u, 28, 36, 4, 20),
            16 => (0xB105B0u, 12, 32, 4, 4),
            17 => (0xB28B00u, 24, 44, 4, 16),
            18 => (0xB28DD0u, 24, 40, 4, 12),
            19 => (0xB290B0u, 36, 52, 4, 24),
            _ => throw new NotSupportedException("Native cinematic primitive writer requires its selected proved handler.")
        };
        if (actualHandler != expected && !(group.Type == 16 && actualHandler is 0xAEC030 or 0xB50540))
        {
            throw new NotSupportedException($"Native cinematic primitive {group.Type:X} selected unsupported handler {actualHandler:X8}.");
        }
        if (group.Count <= 0 || group.Faces.IsDefault || group.Faces.Length != checked(stride * group.Count) || projectedRecords.IsDefault || projectedRecords.Length % 8 != 0 || depths.IsDefault)
        {
            throw new InvalidDataException("Native cinematic writer requires its complete current face, projected and depth owners.");
        }
        if (group.Type is not (2 or 12))
        {
            NativeBattleScriptedMaterialStep setup = NativeBattleScriptedMaterial.Prepare(frame, registers);
            frame = setup.Frame;
            lighting = setup.Lighting;
            registers = setup.Registers;
        }
        else
        {
            frame = frame.WriteTemporaryDword(0x84, group.Type == 2 ? 0x32000000u : 0x3A000000u);
            if (group.Type == 2)
            {
                frame = frame.WriteTemporaryDword(0xF0, 0);
            }
        }
        if (group.Type is 7 or 9 or 17)
        {
            NativeBattleWaterMeshResult reused = NativeBattleWaterMeshPackets.EmitSelected(frame, group, projectedRecords, depths, outputPointer, resolveActualOtBucket, Shade, depthScale);
            return new(reused.Frame, reused.Packets, lighting, registers);
        }
        if (group.Type is 6 or 16)
        {
            frame = frame.WriteTemporaryDword(0xF0, 0);
        }
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int faceIndex = 0; faceIndex < group.Count; faceIndex++)
        {
            byte[] face = group.Faces.AsSpan().Slice(faceIndex * stride, stride).ToArray();
            byte[] data = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            uint[] screens = new uint[corners];
            uint[] depthAndClip = new uint[corners];
            int[] rawDepth = new int[4];
            for (int corner = 0; corner < corners; corner++)
            {
                int offset = Word(face, indices + 2 * corner);
                if ((offset & 7) != 0 || offset > projectedRecords.Length - 8 || offset / 8 >= depths.Length)
                {
                    throw new InvalidDataException("Native cinematic face leaves its actual projected vertex or depth records.");
                }
                screens[corner] = BinaryPrimitives.ReadUInt32LittleEndian(projectedRecords.AsSpan().Slice(offset));
                depthAndClip[corner] = BinaryPrimitives.ReadUInt32LittleEndian(projectedRecords.AsSpan().Slice(offset + 4));
                double scaledDepth = unchecked((ushort)depths[offset / 8]) * (double)depthScale;
                //The Type16 clones use55AD90's signed64-bit truncation and consume its low DWORD.
                rawDepth[corner] = group.Type == 16 ? unchecked((int)(long)scaledDepth) : NativePrecision53.RoundInteger(scaledDepth);
                StoreDword(group.Type is 6 or 16 ? 16 + corner * 4 : group.Type == 19 ? 8 + corner * 12 : 8 + corner * 8, screens[corner]);
            }
            bool reject = (frame.ReadTemporaryDword(0x90) & 0x10) == 0 && NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) < 0;
            reject |= ((depthAndClip.Aggregate(0u, (value, next) => value | next) >> 16) & 0x46) != 0;
            NativeBattleGeometryPrimitive? primitive = null;
            int bucket = 0;
            if (!reject)
            {
                data[3] = (byte)(length / 4 - 1);
                mask[3] = NativeBattleGfPacketByteState.Known;
                if (group.Type is 2 or 12)
                {
                    uint normal = unchecked(frame.ReadTemporaryDword(0x78) + Word(face, group.Type == 2 ? 12 : 16));
                    uint shape = frame.ReadTemporaryDword(0x68);
                    NativeBattleScriptedMeshPreparation.RequireShape(shape, shapeExtent, normal, 8);
                    byte[] raw = frame.Arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(normal, 8), 8);
                    registers = registers with { NormalInput = raw.ToImmutableArray(), NormalInputKnown = null };
                    for (int corner = 0; corner < corners; corner++)
                    {
                        uint material = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(corner * 4)) | (group.Type == 2 ? 0x32000000u : 0x3A000000u);
                        StoreDword(4 + corner * 8, Shade(material));
                    }
                }
                else if (group.Type is 6 or 16)
                {
                    StoreDword(4, frame.ReadTemporaryDword(0x50));
                    StoreDword(8, 0);
                    uint material = BinaryPrimitives.ReadUInt32LittleEndian(face) | frame.ReadTemporaryDword(0x8C) | (group.Type == 6 ? 0x20000000u : 0x28000000u);
                    StoreDword(12, Shade(material));
                }
                else
                {
                    uint code = group.Type == 8 ? 0x24000000u : group.Type == 18 ? 0x2C000000u : 0x3C000000u;
                    uint semi = frame.ReadTemporaryDword(0x8C);
                    int colorCount = group.Type == 19 ? 4 : 1;
                    for (int corner = 0; corner < colorCount; corner++)
                    {
                        uint material = BinaryPrimitives.ReadUInt32LittleEndian(face.AsSpan(corner * 4));
                        if (corner == 0)
                        {
                            material |= code | semi;
                        }
                        StoreDword(4 + corner * 12, Shade(material));
                    }
                    int uvBase = group.Type == 19 ? 16 : 4;
                    int packetUvStride = group.Type == 19 ? 12 : 8;
                    uint uvDelta = frame.ReadTemporaryDword(0x98);
                    for (int corner = 0; corner < corners; corner++)
                    {
                        StoreWord(12 + corner * packetUvStride, unchecked((ushort)(Word(face, uvBase + corner * 2) + uvDelta)));
                    }
                    int metadata = group.Type == 8 ? 16 : group.Type == 18 ? 20 : 32;
                    StoreWord(14, unchecked((ushort)(Word(face, metadata) + frame.ReadTemporaryDword(0x54))));
                    StoreWord(group.Type == 19 ? 26 : 22, unchecked((ushort)(Word(face, metadata + 2) | frame.ReadTemporaryDword(0x50))));
                }
                uint sum = depthAndClip.Aggregate(0u, (value, next) => unchecked(value + next));
                uint offset = corners == 3 ? ((sum & 0xFFFF) / 3) & 0x3FFC : (sum >> 2) & 0x3FFC;
                bucket = resolveActualOtBucket(unchecked(frame.ReadTemporaryDword(0x5C) + offset));
                primitive = new(bucket, Words(data), rawDepth.ToImmutableArray());
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated = checked(allocated + length);
                if (group.Type is 2 or 12)
                {
                    byte[] mode = new byte[12];
                    BinaryPrimitives.WriteUInt32LittleEndian(mode, 0x02000000);
                    BinaryPrimitives.WriteUInt32LittleEndian(mode.AsSpan(4), frame.ReadTemporaryDword(0x50));
                    NativeBattleGeometryPrimitive modePrimitive = new(bucket, Words(mode), rawDepth.ToImmutableArray());
                    attempts.Add(new(allocated, mode.ToImmutableArray(), Enumerable.Repeat(NativeBattleGfPacketByteState.Known, 12).ToImmutableArray(), modePrimitive));
                    allocated += 12;
                }
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
        frame = frame.WriteTemporaryDword(0x60, unchecked(outputPointer + (uint)allocated));
        return new(frame, new(attempts.ToImmutable(), allocated), lighting, registers);

        uint Shade(uint material)
        {
            ImmutableArray<byte> input = registers.NormalInput ?? throw new NotSupportedException("Native cinematic material requires its actual current packed normal input.");
            registers = NativeBattleScriptedNormalColor.Transform4601B0(lighting, NativeBattleScriptedMeshPreparation.Vector(input.AsSpan()), material, registers);
            return registers.Rgb[2] ?? throw new NotSupportedException("Native cinematic material shader did not publish RGB2.");
        }
    }

    private static ushort Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset));
    private static ImmutableArray<uint> Words(byte[] bytes) => Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
}
