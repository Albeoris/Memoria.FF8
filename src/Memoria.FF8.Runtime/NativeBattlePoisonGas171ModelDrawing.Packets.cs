using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattlePoisonGas171ModelDrawing
{
    private static (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt Attempt) DrawBaseFace171(byte[] source, bool quad, NativeBattleEffectArena scratch, int vertexCount, NativeBattleEffectArena temporary, NativeBattleGeometryRegisterStep registers, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, int tableBaseBucket, byte depthShift, int offset)
    {
        byte[] bytes = new byte[quad ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        for (int corner = 0; corner < 3; corner++)
        {
            registers = Load(corner, corner);
        }
        registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
        uint material = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12));
        Write(20, material);
        Write(0, quad ? 0x09000000u : 0x07000000u);
        Write(4, U32(temporary, 0x1C) | (quad ? 0x2C000000u : 0x24000000u));
        Write(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
        uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Skinned body requires its reached RTPT flag.");
        temporary = Dword(temporary, 0x48, flags);
        if ((flags & 0x60000) != 0)
        {
            return Finish(null);
        }
        registers = NativeBattleGeometry.Clip45EE10(registers);
        uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Skinned body requires its reached NCLIP result.");
        temporary = Dword(temporary, 0x3C, area);
        if (unchecked((int)area) <= 0)
        {
            return Finish(null);
        }
        uint[] screen = registers.Geometry.Sxy.Select(value => value ?? throw new NotSupportedException("Skinned body requires its projected screen FIFO.")).ToArray();
        for (int corner = 0; corner < 3; corner++)
        {
            Write(8 + corner * 8, screen[corner]);
        }
        if (quad)
        {
            registers = Load(0, 3);
            registers = NativeBattleGeometry.ProjectSingle(registers, projection, controls.Dqa, controls.Dqb);
            uint uv = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(16));
            Write(28, uv);
            Write(36, unchecked((uint)(unchecked((int)uv) >> 16)));
            Write(32, registers.Geometry.Sxy[2] ?? throw new NotSupportedException("Skinned body requires its fourth projected screen coordinate."));
            registers = NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Skinned body requires its actual ZSF4."));
        }
        else
        {
            registers = NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Skinned body requires its actual ZSF3."));
            Write(28, unchecked((uint)(BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(4)) >> 16)));
        }
        uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Skinned body requires its reached averaged depth.");
        temporary = Dword(temporary, 0x44, depth);
        int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
        int[] payload = quad
            ? registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Skinned body requires its current depth FIFO.")))).ToArray()
            : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
        ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        return Finish(new(bucket, words, payload.ToImmutableArray()) { PhysicalWriteMask = mask.ToImmutableArray() });

        NativeBattleGeometryRegisterStep Load(int slot, int corner)
        {
            int index = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
            if (index >= vertexCount)
            {
                throw new InvalidDataException("Skinned body face leaves its deformed packed vertex bank.");
            }
            byte[] xyz = scratch.Read(checked(index * 8), 6);
            (byte[] w, bool[] known) = scratch.ReadSnapshot(checked(index * 8 + 6), 2);
            return NativeBattleGeometry.LoadVector(registers, slot, new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
        }

        void Write(int at, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
            mask.AsSpan(at, 4).Fill(NativeBattleGfPacketByteState.Known);
        }

        (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt Attempt) Finish(NativeBattleGeometryPrimitive? primitive)
        {
            return (temporary, registers, new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive));
        }
    }

    private static (NativeBattleEffectArena Temporary, NativeBattleGfGeometryAttempt Attempt) EmitOverlay171(NativeBattleGeometryPrimitive source, bool quad, NativeBattleEffectArena temporary, int tableBaseBucket, int offset)
    {
        uint color = U32(temporary, 0x6C) | (quad ? 0x2E000000u : 0x26000000u);
        uint before = U32(temporary, 0x88);
        uint after = U32(temporary, 0x8C);
        uint[] words = quad
            ? [0x0B000000u, before, color, source.Words[2], (source.Words[3] & 0xFFFF) | 0x3F540000u, source.Words[4], (source.Words[5] & 0xFFFF) | 0x00B90000u, source.Words[6], source.Words[7], source.Words[8], source.Words[9], after]
            : [0x09000000u, before, color, source.Words[2], (source.Words[3] & 0xFFFF) | 0x3F540000u, source.Words[4], (source.Words[5] & 0xFFFF) | 0x00B90000u, source.Words[6], source.Words[7], after];
        int delta = unchecked((int)U32(temporary, 0x84));
        if (delta != 0)
        {
            int[] uvWords = quad ? [4, 6, 8, 10] : [4, 6, 8];
            int[] sums = uvWords.Select(index => unchecked((byte)(words[index] >> 8) + delta)).ToArray();
            int height = S16(temporary, 0x3A);
            int adjustment = sums.Any(value => value >= 256) ? -height : sums.Any(value => value < 0) ? height : 0;
            for (int index = 0; index < sums.Length; index++)
            {
                sums[index] = unchecked(sums[index] + adjustment);
                temporary = Dword(temporary, 0x70 + index * 4, unchecked((uint)sums[index]));
                int at = uvWords[index];
                words[at] = (words[at] & 0xFFFF00FFu) | (uint)unchecked((byte)sums[index]) << 8;
            }
        }
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(source.Bucket > tableBaseBucket ? source.Bucket - 1 : source.Bucket, words.ToImmutableArray(), [0, 0, 0, 0]) { PhysicalWriteMask = mask };
        return (temporary, new(offset, bytes.ToImmutableArray(), mask, primitive));
    }
}
