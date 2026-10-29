using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>B36F40's flat textured quad uses its material table and truncates the four separate depth payloads.</summary>
public static class NativeBattleCinematicFlatFt4Depth14
{
    public static NativeBattleScriptedMeshSubmission Emit(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, uint actualHandler, ImmutableArray<byte> projectedRecords, ImmutableArray<uint> depths, uint outputPointer, NativeBattleScriptedLightingMemory lighting, NativeBattleScriptedColorRegisters registers, Func<uint, int> resolveActualOtBucket, float depthScale, IReadOnlyList<System.Numerics.Vector3>? pilotWorldVertices = null, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        if (group.Type != 14 || actualHandler != 0xB36F40)
        {
            throw new NotSupportedException("Native cinematic primitive14 requires its actual B36F40 handler.");
        }
        if (group.Count <= 0 || group.Faces.IsDefault || group.Faces.Length != checked(group.Count * 20) || projectedRecords.IsDefault || projectedRecords.Length % 8 != 0 || depths.IsDefault)
        {
            throw new InvalidDataException("Native cinematic primitive14 requires its complete current face, projected and separate depth owners.");
        }
        frame = frame.WriteTemporaryDword(0xF0, 0);
        bool skipArea = (frame.ReadTemporaryDword(0x90) & 0x10) != 0;
        uint table = frame.ReadTemporaryDword(0x5C);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < group.Count; index++)
        {
            byte[] face = group.Faces.AsSpan().Slice(index * 20, 20).ToArray();
            byte[] data = new byte[40];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[40];
            int[] offsets = new int[4];
            uint[] screens = new uint[4];
            int[] payloads = new int[4];
            for (int corner = 0; corner < 4; corner++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(12 + corner * 2));
                if (offset > projectedRecords.Length - 8 || offset / 8 >= depths.Length)
                {
                    throw new InvalidDataException("Native cinematic primitive14 leaves its actual projected vertex or separate depth owner.");
                }
                offsets[corner] = offset;
                screens[corner] = BinaryPrimitives.ReadUInt32LittleEndian(projectedRecords.AsSpan().Slice(offset));
                payloads[corner] = unchecked((int)(long)(unchecked((ushort)depths[offset / 8]) * (double)depthScale));
                StoreDword(8 + corner * 8, screens[corner]);
            }
            bool rejected = false;
            if (!skipArea)
            {
                int area = NativeBattleModulePackets.Nclip(screens[0], screens[1], screens[2]);
                registers = registers with { Flags = unchecked((uint)area) };
                rejected = area < 0;
            }
            uint sum = 0;
            if (!rejected)
            {
                uint clip = 0;
                foreach (int offset in offsets)
                {
                    uint depthAndClip = BinaryPrimitives.ReadUInt32LittleEndian(projectedRecords.AsSpan().Slice(offset + 4));
                    clip |= depthAndClip;
                    sum = unchecked(sum + depthAndClip);
                }
                rejected = (clip & 0x460000) != 0;
            }
            NativeBattleGeometryPrimitive? primitive = null;
            if (!rejected)
            {
                data[3] = 9;
                mask[3] = NativeBattleGfPacketByteState.Known;
                uint materialAddress = unchecked(frame.ReadTemporaryDword(0x80) + BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(8)));
                uint material = BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(NativeBattleWaterExecutionFields.ArenaOffset(materialAddress, 4), 4));
                StoreDword(4, material | 0x2C000000);
                for (int corner = 0; corner < 4; corner++)
                {
                    StoreWord(12 + corner * 8, BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(corner * 2)));
                }
                StoreWord(14, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(10)) + frame.ReadTemporaryDword(0x54))));
                StoreWord(22, unchecked((ushort)frame.ReadTemporaryDword(0x50)));
                uint ordering = unchecked(table + ((sum >> 2) & 0x3FFC));
                ImmutableArray<uint> words = Enumerable.Range(0, 10).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
                primitive = new(resolveActualOtBucket(ordering), words, payloads.ToImmutableArray()) { PhysicalWriteMask = mask.ToImmutableArray() };
                primitive = NativeBattleCinematicVisualIdentity.Capture(primitive, pilotWorldVertices, pilotIdentity, face, 12, 4, index, depthScale);
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated = checked(allocated + 40);
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
    }
}
