using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>The native flat textured quad with scaled depth payloads and a trailing draw-mode command.</summary>
public static class NativeBattleApocalypse221DepthQuad58
{
    public static NativeBattleScriptedMeshSubmission Emit(NativeBattleWaterExecutionFrame frame, NativeBattleWaterMeshGroup group, uint actualHandler, ImmutableArray<byte> projectedRecords, ImmutableArray<uint> depths, uint outputPointer, NativeBattleScriptedColorRegisters registers, Func<uint, int> resolveActualOtBucket, NativeBattleGeometryRegisters geometry, float depthScale)
    {
        if (group.Type != 18 || actualHandler != 0xAAAE60)
        {
            throw new NotSupportedException("Native Apocalypse depth quad requires its actual selected primitive58 handler.");
        }
        if (group.Count <= 0 || group.Faces.IsDefault || group.Faces.Length != checked(group.Count * 24) || projectedRecords.IsDefault || projectedRecords.Length % 8 != 0 || depths.IsDefault)
        {
            throw new InvalidDataException("Native Apocalypse depth quad requires its complete current face, projected and separate depth owners.");
        }
        NativeBattleScriptedMaterialStep prepared = NativeBattleScriptedMaterial.Prepare(frame, registers);
        frame = prepared.Frame.WriteTemporaryDword(0xF0, 0);
        NativeBattleScriptedLightingMemory lighting = prepared.Lighting;
        registers = prepared.Registers;
        uint table = frame.ReadTemporaryDword(0x5C);
        bool skipArea = (frame.ReadTemporaryDword(0x90) & 0x10) != 0;
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        for (int index = 0; index < group.Count; index++)
        {
            byte[] face = group.Faces.AsSpan().Slice(index * 24, 24).ToArray();
            int[] offsets = new int[4];
            int[] rawDepth = new int[4];
            for (int corner = 0; corner < 4; corner++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(12 + corner * 2));
                if (offset > projectedRecords.Length - 8 || offset / 8 >= depths.Length)
                {
                    throw new InvalidDataException("Native Apocalypse depth quad leaves its actual projected vertex or separate depth owner.");
                }
                offsets[corner] = offset;
                rawDepth[corner] = unchecked((int)(long)(unchecked((ushort)depths[offset / 8]) * (double)depthScale));
            }
            byte[] data = new byte[44];
            NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[44];
            uint[] screens = new uint[4];
            for (int corner = 0; corner < 4; corner++)
            {
                screens[corner] = BinaryPrimitives.ReadUInt32LittleEndian(projectedRecords.AsSpan().Slice(offsets[corner]));
                StoreDword(8 + corner * 8, screens[corner]);
            }
            bool rejected = false;
            if (!skipArea)
            {
                NativeBattleGeometryRegisterStep step = NativeBattleGeometry.Clip45EE10(new(geometry with { Sxy = [screens[0], screens[1], screens[2]] }, registers));
                geometry = step.Geometry;
                registers = step.Colors;
                rejected = unchecked((int)geometry.Mac0!.Value) < 0;
            }
            uint sum = 0;
            if (!rejected)
            {
                uint clip = 0;
                for (int corner = 0; corner < 4; corner++)
                {
                    uint depthAndClip = BinaryPrimitives.ReadUInt32LittleEndian(projectedRecords.AsSpan().Slice(offsets[corner] + 4));
                    clip |= depthAndClip;
                    sum = unchecked(sum + depthAndClip);
                }
                rejected = (clip & 0x460000) != 0;
            }
            NativeBattleGeometryPrimitive? primitive = null;
            if (!rejected)
            {
                data[3] = 10;
                mask[3] = NativeBattleGfPacketByteState.Known;
                StoreDword(0x28, 0xE1000220);
                StoreWord(0x16, unchecked((ushort)(frame.ReadTemporaryDword(0x50) | BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(0x16)))));
                StoreWord(0xE, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(0x14)) + frame.ReadTemporaryDword(0x54))));
                uint uvDelta = frame.ReadTemporaryWord(0x98);
                for (int corner = 0; corner < 4; corner++)
                {
                    StoreWord(12 + corner * 8, unchecked((ushort)(BinaryPrimitives.ReadUInt16LittleEndian(face.AsSpan(4 + corner * 2)) + uvDelta)));
                }
                uint material = BinaryPrimitives.ReadUInt32LittleEndian(face) | frame.ReadTemporaryDword(0x8C) | 0x2C000000;
                ImmutableArray<byte> input = registers.NormalInput ?? throw new NotSupportedException("Native Apocalypse depth quad requires its actual current packed normal input.");
                registers = NativeBattleScriptedNormalColor.Transform4601B0(lighting, NativeBattleScriptedMeshPreparation.Vector(input.AsSpan()), material, registers);
                uint color = registers.Rgb[2] ?? throw new NotSupportedException("Native Apocalypse depth quad requires its actual RGB FIFO output.");
                StoreDword(4, color);
                uint ordering = unchecked(table + ((sum >> 2) & 0x3FFC));
                ImmutableArray<uint> words = Enumerable.Range(0, 11).Select(word => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(word * 4))).ToImmutableArray();
                primitive = new(resolveActualOtBucket(ordering), words, rawDepth.ToImmutableArray()) { PhysicalWriteMask = mask.ToImmutableArray() };
            }
            attempts.Add(new(allocated, data.ToImmutableArray(), mask.ToImmutableArray(), primitive));
            if (primitive is not null)
            {
                allocated = checked(allocated + 44);
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
        return new(frame, new(attempts.ToImmutable(), allocated), lighting, registers) { Geometry = geometry };
    }
}
