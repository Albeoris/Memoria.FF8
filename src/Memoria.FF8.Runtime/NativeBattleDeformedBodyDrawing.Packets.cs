using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleDeformedBodyDrawing
{
    private static (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt Attempt) DrawFace(byte[] source, bool quad, NativeBattleEffectArena scratch, int vertexCount, NativeBattleEffectArena temporary, NativeBattleGeometryRegisterStep registers, NativeBattleScriptedGraphicsMemory controls, int tableBaseBucket, byte depthShift, int offset)
    {
        int corners = quad ? 4 : 3;
        byte[] bytes = new byte[quad ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        byte combined = 0;
        byte common = 0xFF;
        for (int corner = 0; corner < corners; corner++)
        {
            int vertex = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
            if (vertex >= vertexCount)
            {
                throw new InvalidDataException("Acid face leaves its actual projected vertex bank.");
            }
            byte[] row = scratch.Read(checked(vertex * 8), 8);
            temporary = temporary.Write(0x50 + corner * 8, row);
            combined |= row[6];
            common &= row[6];
        }
        if (combined >= 0x10 || common != 0)
        {
            return Finish(null);
        }
        registers = registers with { Geometry = registers.Geometry with { Sxy = [U32(temporary, 0x50), U32(temporary, 0x58), U32(temporary, 0x60)] } };
        registers = NativeBattleGeometry.Clip45EE10(registers);
        uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Acid requires its reached NCLIP result.");
        temporary = Dword(temporary, 0x70, area);
        if (unchecked((int)area) < 0)
        {
            return Finish(null);
        }
        ImmutableArray<uint?> oldDepth = registers.Geometry.Sz;
        registers = registers with
        {
            Geometry = registers.Geometry with
            {
                Sz = quad
                    ? [U16(temporary, 0x54), U16(temporary, 0x5C), U16(temporary, 0x64), U16(temporary, 0x6C)]
                    : [oldDepth[0], U16(temporary, 0x54), U16(temporary, 0x5C), U16(temporary, 0x64)]
            }
        };
        registers = quad
            ? NativeBattleGeometry.Average45E610(registers, controls.Zsf4 ?? throw new NotSupportedException("Acid requires its actual ZSF4."))
            : NativeBattleGeometry.Average45E5C0(registers, controls.Zsf3 ?? throw new NotSupportedException("Acid requires its actual ZSF3."));
        uint depth = registers.Geometry.Otz ?? throw new NotSupportedException("Acid requires its reached averaged depth.");
        uint material = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12));
        Write(0, quad ? 0x09000000u : 0x07000000u, 4);
        Write(4, U32(temporary, 0x48) | (material & 0x02000000) | (quad ? 0x2C000000u : 0x24000000u), 4);
        for (int corner = 0; corner < corners; corner++)
        {
            Write(8 + corner * 8, U32(temporary, 0x50 + corner * 8), 4);
        }
        Write(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)), 4);
        Write(20, material & 0xFDFFFFFF, 4);
        Write(28, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(quad ? 16 : 6)), 2);
        if (quad)
        {
            Write(36, BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(18)), 2);
        }
        temporary = Dword(temporary, 0x78, depth);
        int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
        ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        ImmutableArray<int> payload = quad
            ? registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Acid requires its reached four-depth FIFO.")))).ToImmutableArray()
            : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
        return Finish(new(bucket, words, payload) { PhysicalWriteMask = mask.ToImmutableArray() });

        void Write(int at, uint value, int length)
        {
            if (length == 2)
            {
                BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(at), unchecked((ushort)value));
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
            }
            mask.AsSpan(at, length).Fill(NativeBattleGfPacketByteState.Known);
        }
        (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt Attempt) Finish(NativeBattleGeometryPrimitive? primitive) => (temporary, registers, new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive));
    }
}
