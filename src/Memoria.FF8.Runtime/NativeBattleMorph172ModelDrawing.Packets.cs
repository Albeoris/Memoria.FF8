using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMorph172ModelDrawing
{
    private static (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt Attempt) DrawBaseFace172(byte[] source, bool quad, NativeBattleEffectArena scratch, int vertexCount, NativeBattleEffectArena temporary, NativeBattleGeometryRegisterStep registers, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, int tableBaseBucket, byte depthShift, int offset, NativeBattlePilotVisualIdentity? pilotIdentity = null)
    {
        System.Numerics.Vector3[]? world = pilotIdentity.HasValue ? new System.Numerics.Vector3[quad ? 4 : 3] : null;
        NativeBattleActionMatrix? pilotMatrix = null;
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
        if (quad)
        {
            CopyCorner(3);
        }
        registers = NativeBattleGeometry.Clip45EE10(registers);
        uint area = registers.Geometry.Mac0 ?? throw new NotSupportedException("Skinned body requires its reached NCLIP result.");
        temporary = Dword(temporary, 0x48, area);
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
        temporary = Dword(temporary, 0x50, depth);
        int bucket = unchecked(tableBaseBucket + (unchecked((int)depth) >> depthShift));
        int[] payload = quad
            ? registers.Geometry.Sz.Select(value => unchecked((int)(value ?? throw new NotSupportedException("Skinned body requires its current depth FIFO.")))).ToArray()
            : [unchecked((int)registers.Geometry.Sz[1]!.Value), unchecked((int)registers.Geometry.Sz[2]!.Value), unchecked((int)registers.Geometry.Sz[3]!.Value), 0];
        ImmutableArray<uint> words = Enumerable.Range(0, bytes.Length / 4).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(index * 4))).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(bucket, words, payload.ToImmutableArray()) { PhysicalWriteMask = mask.ToImmutableArray() };
        if (pilotIdentity is { } identity && world is not null)
        {
            primitive = primitive with { PilotGeometry = new(identity with { Primitive = unchecked(identity.Primitive * 397) }, world[0], world[1], world[2], quad ? world[3] : default, checked((byte)world.Length)) { SourceDepths = new(payload[0], payload[1], payload[2], payload[3]), UseSourceDepths = true } };
        }
        return Finish(primitive);

        NativeBattleGeometryRegisterStep Load(int slot, int corner)
        {
            int index = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
            if (index >= vertexCount)
            {
                throw new InvalidDataException("Skinned body face leaves its deformed packed vertex bank.");
            }
            if (corner != 3)
            {
                CopyCorner(corner);
            }
            int at = 0x58 + corner * 8;
            byte[] xyz = temporary.Read(at, 6);
            (byte[] w, bool[] known) = temporary.ReadSnapshot(at + 6, 2);
            NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4)));
            if (world is not null)
            {
                pilotMatrix ??= NativeBattleGeometry.CaptureMatrix(registers.Geometry);
                world[corner] = NativeBattlePilotVisualGeometry.CameraToWorld(NativeBattlePilotVisualGeometry.Transform(pilotMatrix, point), projection);
            }
            return NativeBattleGeometry.LoadVector(registers, slot, point, known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(w) : null);

        }

        void CopyCorner(int corner)
        {
            int index = BinaryPrimitives.ReadUInt16LittleEndian(source.AsSpan(corner * 2)) & 0xFFF;
            if (index >= vertexCount)
            {
                throw new InvalidDataException("Morph face leaves its current physical skin workspace.");
            }
            temporary = NativeBattleEffectNodeContextMemory.Copy(scratch, checked(index * 8), temporary, 0x58 + corner * 8, 8);
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

    private static (NativeBattleEffectArena Temporary, NativeBattleGeometryRegisterStep Registers, NativeBattleGfGeometryAttempt? Attempt) EmitOverlay172(NativeBattleGeometryPrimitive source, bool quad, NativeBattleEffectArena temporary, NativeBattleGeometryRegisterStep registers, int offset, NativeBattleModelRendererRegisters far, NativeBattleSummonColors colors)
    {
        bool inside = false;
        int count = quad ? 4 : 3;
        for (int corner = 0; corner < count; corner++)
        {
            int at = 0x58 + corner * 8;
            int[] xyz = new int[3];
            for (int axis = 0; axis < 3; axis++)
            {
                short delta = unchecked((short)(S16(temporary, at + axis * 2) - S16(temporary, 0xA8 + axis * 4)));
                xyz[axis] = delta;
                temporary = temporary.Write(at + axis * 2, BitConverter.GetBytes(delta));
            }
            int radius = NativeBattleRayBomb10Geometry.Sqrt56BEC0(unchecked(xyz[0] * xyz[0] + xyz[1] * xyz[1] + xyz[2] * xyz[2]));
            int threshold = unchecked((int)U32(temporary, 0xB4));
            if (radius < threshold)
            {
                int factor = unchecked(4096 - unchecked(radius << 12) / threshold);
                temporary = temporary.Write(at + 6, BitConverter.GetBytes(unchecked((short)factor)));
                inside = true;
            }
        }
        if (!inside)
        {
            return (temporary, registers, null);
        }
        uint[] words = new uint[quad ? 9 : 7];
        words[0] = quad ? 0x08000000u : 0x06000000u;
        uint material = quad ? 0x3A000000u : 0x32000000u;
        for (int corner = 0; corner < count; corner++)
        {
            uint cue = unchecked((uint)(int)S16(temporary, 0x5E + corner * 8));
            NativeBattleScriptedDpcsResult result = NativeBattleScriptedDpcs.Transform45F270(registers.Colors, material, cue, far, colors);
            registers = registers with { Colors = result.Registers };
            words[1 + corner * 2] = result.PackedRgb;
            words[2 + corner * 2] = source.Words[2 + corner * 2];
        }
        byte[] bytes = new byte[words.Length * 4];
        for (int index = 0; index < words.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(index * 4), words[index]);
        }
        ImmutableArray<NativeBattleGfPacketByteState> mask = Enumerable.Repeat(NativeBattleGfPacketByteState.Known, bytes.Length).ToImmutableArray();
        NativeBattleGeometryPrimitive primitive = new(unchecked(source.Bucket - 1), words.ToImmutableArray(), source.VertexPayload4) { PhysicalWriteMask = mask };
        if (source.PilotGeometry is { } geometry)
        {
            primitive = primitive with { PilotGeometry = geometry with { Identity = geometry.Identity with { Primitive = unchecked(geometry.Identity.Primitive + 1) } } };
        }
        return (temporary, registers, new(offset, bytes.ToImmutableArray(), mask, primitive));
    }
}
