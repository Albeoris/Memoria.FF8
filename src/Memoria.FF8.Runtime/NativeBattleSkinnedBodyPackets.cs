using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleSkinnedBodyPackets
{
    public static (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt Attempt) EmitBaseFace(byte[] source, bool quad, NativeBattleEffectArena scratch, int vertexCount, NativeBattleEffectArena temporary, NativeBattleGeometryRegisterStep registers, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, int tableBaseBucket, byte depthShift, int offset, int flagOffset, int areaOffset, int depthOffset, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        NativeBattleActionMatrix? sourceMatrix = pilotIdentity.HasValue ? NativeBattleGeometry.CaptureMatrix(registers.Geometry) : null;
        System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[quad ? 4 : 3] : null;
        byte[] bytes = new byte[quad ? 40 : 32];
        NativeBattleGfPacketByteState[] mask = new NativeBattleGfPacketByteState[bytes.Length];
        for (int corner = 0; corner < 3; corner++)
        {
            registers = Load(corner, corner);
        }
        registers = NativeBattleGeometry.ProjectTriple(registers, projection, controls.Dqa, controls.Dqb);
        uint material = BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(12));
        Write(20, material & 0x01FFFFFF);
        Write(0, quad ? 0x09000000u : 0x07000000u);
        Write(4, (material & 0x02000000) | U32(temporary, 0x1C) | (quad ? 0x2C000000u : 0x24000000u));
        Write(12, BinaryPrimitives.ReadUInt32LittleEndian(source.AsSpan(8)));
        uint flags = registers.Colors.Flags ?? throw new NotSupportedException("Skinned body requires its reached RTPT flag.");
        temporary = Dword(temporary, flagOffset, flags);
        if ((flags & 0x60000) != 0)
        {
            return Finish(null);
        }
        registers = NativeBattleGeometry.Clip45EE10(registers);
        uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Skinned body requires its reached NCLIP result.");
        temporary = Dword(temporary, areaOffset, area);
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
        temporary = Dword(temporary, depthOffset, depth);
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
            NativeBattleVector vertex = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
            if (world is not null)
            {
                world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(sourceMatrix!, vertex), projection);
            }
            return NativeBattleGeometry.LoadVector(registers, slot, vertex, known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);
        }

        void Write(int at, uint value)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(at), value);
            mask.AsSpan(at, 4).Fill(NativeBattleGfPacketByteState.Known);
        }

        (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt Attempt) Finish(NativeBattleGeometryPrimitive? primitive)
        {
            if (primitive is not null && pilotIdentity is { } identity && world is not null)
            {
                primitive = primitive with { PilotGeometry = new(identity, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)world.Length)) };
            }
            return (temporary, registers, new(offset, bytes.ToImmutableArray(), mask.ToImmutableArray(), primitive));
        }
    }
    private static uint U32(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value) => arena.Write(offset, BitConverter.GetBytes(value));
}
