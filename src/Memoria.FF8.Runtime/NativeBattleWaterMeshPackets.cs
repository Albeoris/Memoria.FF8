using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleWaterMeshGroup(short Type, int Count, ImmutableArray<byte> Faces);
public sealed record NativeBattleWaterMeshResult(NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch Packets);

/// <summary>Selected mesh packet stores over already projected native records and their separate depth owner.</summary>
public static class NativeBattleWaterMeshPackets
{
    public static NativeBattleWaterMeshResult EmitSelected(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, ImmutableArray<byte> projectedRecords, ImmutableArray<uint> separateDepths, uint outputPointer, Func<uint, int> resolveActualOtBucket, Func<uint, uint>? shadeMaterial = null, float depthScale = 1f, Func<uint, uint, uint, int>? clipScreens = null)
    {
        (int stride, int length, int corners, int indexOffset, int sxyOffset) = group.Type switch
        {
            7 => (20, 36, 3, 12, 16),
            9 => (28, 40, 3, 18, 8),
            17 => (24, 44, 4, 16, 16),
            _ => throw new NotSupportedException("Native Water selected mesh writer is unsupported.")
        };
        if (group.Count <= 0 || group.Faces.IsDefault || group.Faces.Length != checked(group.Count * stride) || projectedRecords.IsDefault || (projectedRecords.Length & 7) != 0 || separateDepths.IsDefault)
        {
            throw new InvalidDataException("Native Water mesh writer requires its complete current face and projected owners.");
        }
        NativeBattleWaterMeshColorSetup setup = NativeBattleWaterMeshColor.PrepareA9E8A0(frame);
        frame = setup.Frame.WriteTemporaryDword(0xF0, 0);
        uint flags = frame.ReadTemporaryDword(0x90);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int faceIndex = 0; faceIndex < group.Count; faceIndex++)
        {
            ReadOnlySpan<byte> face = group.Faces.AsSpan().Slice(faceIndex * stride, stride);
            byte[] data = new byte[length];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[length];
            uint[] screens = new uint[corners];
            uint[] depthAndClip = new uint[corners];
            int[] rawDepth = new int[4];
            for (int corner = 0; corner < corners; corner++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(indexOffset + 2 * corner));
                if ((offset & 7) != 0 || offset > projectedRecords.Length - 8 || offset / 8 >= separateDepths.Length)
                {
                    throw new InvalidDataException("Native Water face leaves its current projected vertex and depth records.");
                }
                screens[corner] = BinaryPrimitives.ReadUInt32LittleEndian(projectedRecords.AsSpan().Slice(offset));
                depthAndClip[corner] = BinaryPrimitives.ReadUInt32LittleEndian(projectedRecords.AsSpan().Slice(offset + 4));
                rawDepth[corner] = unchecked((int)(long)(unchecked((ushort)separateDepths[offset / 8]) * (double)depthScale));
                Dword(group.Type == 9 ? sxyOffset + 12 * corner : sxyOffset + 8 * corner, screens[corner]);
            }
            uint clip = depthAndClip.Aggregate(0u, (current, value) => current | value);
            bool rejected = (flags & 0x10) == 0 && (clipScreens is null ? NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]) : clipScreens(screens[0], screens[1], screens[2])) < 0;
            rejected |= ((clip >> 16) & 0x46) != 0;
            NativeBattleGeometryPrimitive? primitive = null;
            if (!rejected)
            {
                uint e1 = frame.ReadTemporaryDword(0x50);
                uint semi = frame.ReadTemporaryDword(0x8C);
                uint table = frame.ReadTemporaryDword(0x5C);
                data[3] = checked((byte)(length / 4 - 1));
                mask[3] = NativeBattleGfPacketByteState.Known;
                if (group.Type == 9)
                {
                    int clutDelta = unchecked((int)frame.ReadTemporaryDword(0x54));
                    Word(12, BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(12)));
                    Word(14, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(24)) + clutDelta)));
                    Word(24, BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(14)));
                    Word(26, unchecked((ushort)(e1 | BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(26)))));
                    Word(36, BinaryPrimitives.ReadUInt16LittleEndian(face.Slice(16)));
                }
                else
                {
                    Dword(4, e1);
                    Dword(8, 0);
                }
                for (int corner = 0; corner < corners; corner++)
                {
                    uint material = BinaryPrimitives.ReadUInt32LittleEndian(face.Slice(4 * corner));
                    if (corner == 0)
                    {
                        material |= semi | (group.Type == 7 ? 0x30000000u : group.Type == 9 ? 0x34000000u : 0x38000000u);
                    }
                    int offset = group.Type == 9 ? 4 + corner * 12 : 12 + corner * 8;
                    Dword(offset, shadeMaterial is null ? NativeBattleWaterMeshColor.Modulate4601B0(material, setup.Modulation) : shadeMaterial(material));
                }
                uint sum = 0;
                foreach (uint value in depthAndClip)
                {
                    sum = unchecked(sum + value);
                }
                uint otOffset = corners == 3 ? ((sum & 0xFFFF) / 3) & 0x3FFC : (sum >> 2) & 0x3FFC;
                int bucket = resolveActualOtBucket(unchecked(table + otOffset));
                ImmutableArray<uint> words = Enumerable.Range(0, length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(index * 4))).ToImmutableArray();
                primitive = new(bucket, words, rawDepth.ToImmutableArray());
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated = checked(allocated + length);
            }

            void Dword(int offset, uint value)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 4);
            }

            void Word(int offset, ushort value)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), value);
                Array.Fill(mask, NativeBattleGfPacketByteState.Known, offset, 2);
            }
        }
        frame = frame.WriteTemporaryDword(0x60, unchecked(outputPointer + (uint)allocated));
        return new(frame, new(attempts.ToImmutable(), allocated));
    }
}
