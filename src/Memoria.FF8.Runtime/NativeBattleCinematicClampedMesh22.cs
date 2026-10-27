using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Retained descriptor mesh drawing with a parent transform and an optional world Y ceiling.</summary>
public static class NativeBattleCinematicClampedMesh22
{
    public static int MeasureExtent(NativeBattleWaterExecutionFrame frame, uint shape, Func<int, byte> readStride)
    {
        int extent = 0x1C;
        int vertices = checked((int)Dword(shape + 0x18));
        if (vertices <= 0)
        {
            throw new NotSupportedException("Native clamped mesh requires its actual positive vertex count.");
        }
        Include(Dword(shape + 0x14), checked(vertices * 8));
        uint cursor = Dword(shape + 8);
        while (true)
        {
            Include(cursor, 4);
            byte[] header = frame.Arena.Read(ArenaOffset(checked(shape + cursor), 4), 4);
            short type = BinaryPrimitives.ReadInt16LittleEndian(header);
            short count = BinaryPrimitives.ReadInt16LittleEndian(header.AsSpan(2));
            if (count == -1)
            {
                return extent;
            }
            if (type < 0 || count <= 0)
            {
                throw new NotSupportedException("Native clamped mesh requires its actual selected primitive type and positive count.");
            }
            int length = checked(count * readStride(type));
            cursor = checked(cursor + 4);
            Include(cursor, length);
            cursor = checked(cursor + (uint)length);
        }

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));

        void Include(uint relative, int length)
        {
            _ = ArenaOffset(checked(shape + relative), length);
            extent = Math.Max(extent, checked((int)relative + length));
        }
    }

    public static NativeBattleScriptedMeshProjected Project(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, short, uint> resolveSlot, Func<NativeBattleWaterExecutionFrame, short, uint> resolveShape, NativeBattleCameraProjection projection, NativeBattleScriptedGraphicsMemory controls, NativeBattleScriptedLightingMemory lighting, NativeBattleGeometryRegisterStep step)
    {
        uint descriptor = SlotDword(0xB8);
        if (SlotDword(0xBC) == 0)
        {
            frame = WriteSlotDword(frame, 0xD8, resolveShape(frame, SignedWord(descriptor + 2)));
            frame = WriteSlotDword(frame, 0xBC, unchecked((uint)(int)SignedWord(descriptor)));
        }
        uint manager = CoreDword(frame, 0x27973EC);
        uint shape = SlotDword(0xD8);
        uint source = unchecked(shape + Dword(shape + 0x14));
        int count = checked((int)Dword(shape + 0x18));
        if (count <= 0)
        {
            throw new NotSupportedException("Native clamped mesh requires its actual positive vertex count.");
        }
        uint work = CoreDword(frame, manager + 0x74);
        uint transformed = unchecked(work + (uint)(count * 8));
        _ = ArenaOffset(source, checked(count * 8));
        _ = ArenaOffset(work, checked(count * 24));
        uint flags = unchecked((uint)(int)SignedWord(descriptor));
        frame = frame.WriteTemporaryDword(0x94, descriptor).WriteTemporaryDword(0x60, CoreDword(frame, manager + 0x7C));
        frame = frame.WriteTemporaryDword(0x50, unchecked((uint)(int)(short)SlotWord(frame, 0x92)) | 0xE1000000).WriteTemporaryDword(0x54, unchecked((uint)(int)(short)SlotWord(frame, 0x9A)));
        frame = frame.WriteTemporaryDword(0x84, SlotDword(0xCC) & 0x02FFFFFF);
        frame = frame.WriteTemporaryDword(0x8C, frame.ReadTemporaryDword(0x84) & 0x02000000).WriteTemporaryDword(0x5C, frame.ReadTemporaryDword(0x14C)).WriteTemporaryDword(0x90, ReadSlot(frame, 0xDE, 1)[0]);
        frame = frame.WriteTemporaryDword(0x98, SlotWord(frame, 0x9E)).WriteTemporaryDword(0x68, shape).WriteTemporaryDword(0x6C, unchecked(shape + Dword(shape + 8))).WriteTemporaryDword(0x74, source).WriteTemporaryDword(0x7C, work).WriteTemporaryDword(0x80, transformed).WriteTemporaryDword(0x78, flags);
        uint parent = resolveSlot(frame, SignedWord(descriptor + 6));
        NativeBattleActionMatrix local;
        (frame, local) = NativeBattleWaterMatrixCommands.BuildRotationB65590(frame, Vector(parent + 0x8C), unchecked((int)flags) >> 8);
        int scaleX = unchecked((short)SlotWord(frame, 0x8C)) << 4;
        int scaleY = unchecked((short)SlotWord(frame, 0x8E)) << 4;
        int scaleZ = unchecked((short)SlotWord(frame, 0x90)) << 4;
        frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)scaleX)).WriteTemporaryDword(0xF8, unchecked((uint)scaleY)).WriteTemporaryDword(0xFC, unchecked((uint)scaleZ));
        local = NativeBattlePoseMatrices.Scale56BEF0(local, scaleX, scaleY, scaleZ) with { X = SignedWord(parent + 0x94), Y = SignedWord(parent + 0x96), Z = SignedWord(parent + 0x98) };
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(0xE0 + lane * 2, unchecked((ushort)local.Rotation[lane]));
        }
        short? localPadding = frame.TemporaryFrame.IsKnown(0xF2, 2) ? unchecked((short)frame.ReadTemporaryWord(0xF2)) : null;
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, local, true, true, localPadding) };
        step = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(step, 0, SlotVector(0x94), unchecked((short)SlotWord(frame, 0x9A))));
        step = step with { Geometry = step.Geometry with { Translation = step.Colors.Mac } };
        for (int index = 0; index < count; index++)
        {
            uint input = unchecked(source + (uint)(index * 8));
            uint output = unchecked(transformed + (uint)(index * 16));
            step = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(step, 0, Vector(input), SignedWord(input + 6)));
            for (int lane = 0; lane < 3; lane++)
            {
                frame = StoreDword(frame, unchecked(output + (uint)(lane * 4)), unchecked((uint)(step.Colors.Mac[lane] ?? throw new NotSupportedException("Native clamped mesh transform requires its actual MAC output."))));
            }
            frame = StoreDword(frame, output + 12, step.Colors.Flags ?? throw new NotSupportedException("Native clamped mesh transform requires its current FLAG output."));
        }
        short ceiling = SignedWord(descriptor + 4);
        if ((flags & 8) != 0)
        {
            for (int index = 0; index < count; index++)
            {
                uint input = unchecked(transformed + (uint)(index * 16));
                uint output = unchecked(work + (uint)(index * 8));
                frame = StoreWord(frame, output, unchecked((ushort)SignedWord(input)));
                frame = StoreWord(frame, output + 4, unchecked((ushort)SignedWord(input + 8)));
                frame = StoreWord(frame, output + 2, unchecked((ushort)Math.Min(SignedWord(input + 4), ceiling)));
            }
        }
        (byte[] viewBytes, bool[] viewKnown) = frame.Core.ReadSnapshot(0x27979E8, 32);
        NativeBattleActionMatrix view = new NativeBattlePackedMatrix32(viewBytes.ToImmutableArray(), viewKnown.ToImmutableArray()).ReadMatrix();
        short? viewPadding = viewKnown[18] && viewKnown[19] ? BinaryPrimitives.ReadInt16LittleEndian(viewBytes.AsSpan(18)) : null;
        step = step with { Geometry = NativeBattleGeometry.LoadMatrix(step.Geometry, view, true, true, viewPadding) };
        for (int index = 0; index < count; index++)
        {
            uint current = unchecked(work + (uint)(index * 8));
            step = NativeBattleGeometry.ProjectSingle(NativeBattleGeometry.LoadVector(step, 0, Vector(current), SignedWord(current + 6)), projection, controls.Dqa, controls.Dqb);
            frame = StoreDword(frame, current, step.Geometry.Sxy[2] ?? throw new NotSupportedException("Native clamped mesh projection requires its current SXY output."));
            frame = StoreDword(frame, current + 4, (step.Geometry.Sz[3] ?? throw new NotSupportedException("Native clamped mesh projection requires its current SZ output.")) >> 2);
            frame = frame.WriteTemporaryDword(0xFC, step.Colors.Flags ?? throw new NotSupportedException("Native clamped mesh projection requires its current FLAG output."));
        }
        return new(frame, controls.Depths, lighting, step.Colors) { Geometry = step.Geometry, DepthScaleBits1877DA8 = controls.DepthScaleBits1877DA8 ?? throw new NotSupportedException("Native clamped mesh requires its retained depth scale.") };

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
        short SignedWord(uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(address, 2), 2));
        uint SlotDword(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
        NativeBattleVector Vector(uint address) => new(SignedWord(address), SignedWord(address + 2), SignedWord(address + 4));
        NativeBattleVector SlotVector(int offset) => new(unchecked((short)SlotWord(frame, offset)), unchecked((short)SlotWord(frame, offset + 2)), unchecked((short)SlotWord(frame, offset + 4)));
    }

    public static NativeBattleScriptedMeshSubmission Submit(NativeBattleScriptedMeshProjected projected, int shapeExtent, Func<int, uint> requireHandler, Func<int, byte> readStride, Func<uint, int> resolveOrderingBucket)
    {
        NativeBattleWaterExecutionFrame frame = projected.Frame;
        NativeBattleScriptedLightingMemory lighting = projected.Lighting;
        NativeBattleScriptedColorRegisters registers = projected.Registers;
        NativeBattleGeometryRegisters geometry = projected.Geometry ?? throw new NotSupportedException("Native clamped mesh requires its current geometry registers.");
        uint shape = frame.ReadTemporaryDword(0x68);
        int vertices = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(shape + 0x18, 4), 4)));
        ImmutableArray<byte> screens = frame.Arena.Read(ArenaOffset(frame.ReadTemporaryDword(0x7C), checked(vertices * 8)), vertices * 8).ToImmutableArray();
        ImmutableArray<uint> depths = Enumerable.Range(0, vertices).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(projected.DepthOwner.Read(index * 4, 4))).ToImmutableArray();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        uint cursor = frame.ReadTemporaryDword(0x6C);
        while (true)
        {
            NativeBattleScriptedMeshPreparation.RequireShape(shape, shapeExtent, cursor, 4);
            byte[] header = frame.Arena.Read(ArenaOffset(cursor, 4), 4);
            short type = BinaryPrimitives.ReadInt16LittleEndian(header);
            short count = BinaryPrimitives.ReadInt16LittleEndian(header.AsSpan(2));
            if (count == -1)
            {
                break;
            }
            if (type < 0 || count <= 0)
            {
                throw new NotSupportedException("Native clamped mesh requires its selected primitive type and positive count.");
            }
            int stride = readStride(type);
            uint handler = requireHandler(type);
            cursor = unchecked(cursor + 4);
            int length = checked(count * stride);
            uint next = checked(cursor + (uint)length);
            NativeBattleScriptedMeshPreparation.RequireShape(shape, shapeExtent, cursor, length);
            frame = frame.WriteTemporaryDword(0x70, unchecked((uint)(int)count)).WriteTemporaryDword(0x6C, cursor).WriteTemporaryDword(0x58, next);
            NativeBattleWaterMeshGroup group = new(type, count, frame.Arena.Read(ArenaOffset(cursor, length), length).ToImmutableArray());
            NativeBattleScriptedMeshSubmission submitted = NativeBattleScriptedMeshPackets.EmitSelected(frame, group, handler, shapeExtent, screens, depths, frame.ReadTemporaryDword(0x60), lighting, registers, resolveOrderingBucket, geometry, BitConverter.UInt32BitsToSingle(projected.DepthScaleBits1877DA8));
            foreach (NativeBattleGfGeometryAttempt attempt in submitted.Packets.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) });
            }
            allocated = checked(allocated + submitted.Packets.AllocationBytes);
            frame = submitted.Frame.WriteTemporaryDword(0x6C, next);
            lighting = submitted.Lighting;
            registers = submitted.Registers;
            geometry = submitted.Geometry ?? throw new NotSupportedException("Native clamped mesh packet writer requires its current geometry registers.");
            cursor = next;
        }
        frame = WriteCoreDword(frame, CoreDword(frame, 0x27973EC) + 0x7C, frame.ReadTemporaryDword(0x60));
        return new(frame, new(attempts.ToImmutable(), allocated), lighting, registers) { Geometry = geometry };
    }

    private static NativeBattleWaterExecutionFrame StoreDword(NativeBattleWaterExecutionFrame frame, uint address, uint value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 4), BitConverter.GetBytes(value)) };
    private static NativeBattleWaterExecutionFrame StoreWord(NativeBattleWaterExecutionFrame frame, uint address, ushort value) => frame with { Arena = frame.Arena.Write(ArenaOffset(address, 2), BitConverter.GetBytes(value)) };
}
