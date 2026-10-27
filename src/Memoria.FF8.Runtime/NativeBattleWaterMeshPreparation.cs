using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleMeshLocalClip(NativeBattleVector Position, ushort Flags);
public sealed record NativeBattleWaterMeshPrepared(NativeBattleWaterExecutionFrame Frame, NativeBattleActionMatrix Matrix, short Focal)
{
    public NativeBattleMeshLocalClip? LocalClip { get; init; }
}
public sealed record NativeBattleWaterMeshProjected(NativeBattleWaterExecutionFrame Frame, NativeBattleEffectArena DepthOwner, double ProxyScale);
public sealed record NativeBattleWaterMeshSubmission(NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch Packets);

/// <summary>The selected ordinary mesh wrappers over actual resource, work and separate depth owners.</summary>
public static class NativeBattleWaterMeshPreparation
{
    // Water overwrites its current vertex prefix of the process-retained shared workspace.
    public const int DepthLength = NativeBattleScriptedGraphicsMemory.DepthLength;

    public static (NativeBattleWaterMeshPrepared Prepared, NativeBattleGeometryRegisterStep Registers) PrepareWrapper2(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, int index, short globalFocal, NativeBattleGeometryRegisterStep registers)
    {
        int slot = bank.SlotOffset(frame.Arena, index);
        uint address = NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, index, Word(frame.Arena, slot + 0x9C));
        (byte[] bytes, bool[] known) = frame.Core.ReadSnapshot(address, 32);
        NativeBattlePackedMatrix32 packed = new(bytes.ToImmutableArray(), known.ToImmutableArray());
        NativeBattleActionMatrix basis = packed.ReadMatrix();
        short? padding = known[18] && known[19] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(18)) : null;
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, basis, true, true, padding) };
        (byte[] pointBytes, bool[] pointKnown) = frame.Arena.ReadSnapshot(slot + 0x94, 8);
        NativeBattleVector point = new(BinaryPrimitives.ReadInt16LittleEndian(pointBytes), BinaryPrimitives.ReadInt16LittleEndian(pointBytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(pointBytes.AsSpan(4)));
        if (!pointKnown.AsSpan(0, 6).ToArray().All(value => value))
        {
            throw new NotSupportedException("Native mesh preparation requires its actual anchor XYZ bytes.");
        }
        short? w = pointKnown[6] && pointKnown[7] ? BinaryPrimitives.ReadInt16LittleEndian(pointBytes.AsSpan(6)) : null;
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, w));
        registers = registers with { Geometry = registers.Geometry with { Translation = registers.Colors.Mac } };
        NativeBattleWaterMeshPrepared prepared = PrepareWrapper(frame, bank, index, 2, globalFocal);
        frame = prepared.Frame;
        frame.TemporaryFrame.SetKnownness(0xE0 + 18, known.AsSpan(18, 14));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, prepared.Matrix, true, false, padding) };
        return (prepared with { Frame = frame, Focal = globalFocal }, registers);
    }

    public static NativeBattleWaterMeshPrepared PrepareWrapper(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, int index, byte wrapper, short globalFocal)
    {
        if (wrapper == 7)
        {
            return NativeBattleCinematicMesh7.Prepare(frame, globalFocal);
        }
        if (wrapper == 9)
        {
            return PrepareClipped(frame, bank, index, globalFocal);
        }
        if (wrapper == 0x1B)
        {
            return PrepareRotatedUniform(frame, bank, index, globalFocal);
        }
        if (wrapper == 0x39)
        {
            return PrepareRotatedUniform(frame, bank, index, globalFocal, 3);
        }
        if (wrapper is not (1 or 2))
        {
            throw new NotSupportedException("Native Water mesh wrapper is outside the selected ordinary preparation.");
        }
        int slot = bank.SlotOffset(frame.Arena, index);
        uint morph = Dword(frame.Arena, slot + 0xBC);
        if (wrapper == 2)
        {
            frame = frame.WriteTemporaryDword(0x4C, morph);
        }
        frame = frame.WriteTemporaryDword(0x48, 0).WriteTemporaryDword(0x44, 0).WriteTemporaryDword(0x40, 0);
        if (wrapper == 1)
        {
            frame = frame.WriteTemporaryDword(0x4C, morph);
        }
        uint matrixAddress = NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, index, Word(frame.Arena, slot + 0x9C));
        (byte[] raw, bool[] sourceKnown) = frame.Core.ReadSnapshot(matrixAddress, 32);
        short focalOverride = wrapper == 2 ? (short)0 : BinaryPrimitives.ReadInt16LittleEndian(frame.Core.Read(matrixAddress + 18, 2));
        short focal = focalOverride == 0 ? globalFocal : focalOverride;
        NativeBattleActionMatrix basis = new NativeBattlePackedMatrix32(raw.ToImmutableArray(), sourceKnown.ToImmutableArray()).ReadMatrix();
        NativeBattleVector point = new(SignedWord(frame.Arena, slot + 0x94), SignedWord(frame.Arena, slot + 0x96), SignedWord(frame.Arena, slot + 0x98));
        (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(basis, point);
        NativeBattleActionMatrix matrix = basis with { X = unchecked(basis.X + x), Y = unchecked(basis.Y + y), Z = unchecked(basis.Z + z) };
        if (wrapper == 2)
        {
            int sx = SignedWord(frame.Arena, slot + 0x8C) << 4;
            int sy = SignedWord(frame.Arena, slot + 0x8E) << 4;
            int sz = SignedWord(frame.Arena, slot + 0x90) << 4;
            frame = frame.WriteTemporaryDword(0xD4, unchecked((uint)sx)).WriteTemporaryDword(0xD8, unchecked((uint)sy)).WriteTemporaryDword(0xDC, unchecked((uint)sz)).WriteTemporary(0xE0, raw);
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, sx, sy, sz);
            for (int lane = 0; lane < 9; lane++)
            {
                frame = frame.WriteTemporaryWord(0xE0 + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
            }
            frame.TemporaryFrame.SetKnownness(0xE0 + 18, sourceKnown.AsSpan(18, 14));
        }
        return new(frame, matrix, focal);
    }

    private static NativeBattleWaterMeshPrepared PrepareClipped(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, int index, short globalFocal)
    {
        int slot = bank.SlotOffset(frame.Arena, index);
        frame = frame.WriteTemporaryDword(0x4C, Dword(frame.Arena, slot + 0xBC)).WriteTemporaryDword(0x48, 0).WriteTemporaryDword(0x44, 0).WriteTemporaryDword(0x40, 0);
        uint address = NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, index, Word(frame.Arena, slot + 0x9C));
        (byte[] bytes, bool[] known) = frame.Core.ReadSnapshot(address, 32);
        byte[] rotation = frame.Core.Read(address, 18);
        NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(rotation.AsSpan(lane * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, address + 20)), unchecked((int)CoreDword(frame, address + 24)), unchecked((int)CoreDword(frame, address + 28)), 4096);
        NativeBattleVector global = new(unchecked((short)CoreWord(frame, 0x27971E4)), unchecked((short)CoreWord(frame, 0x27971E6)), unchecked((short)CoreWord(frame, 0x27971E8)));
        NativeBattleFullMacVertex translated = NativeBattleGfMatrices.TransformMac460840(basis, global);
        frame = frame.WriteTemporary(0xE0, bytes);
        frame.TemporaryFrame.SetKnownness(0xE0, known);
        int sx = SignedWord(frame.Arena, slot + 0x8C) << 4;
        int sy = SignedWord(frame.Arena, slot + 0x8E) << 4;
        int sz = SignedWord(frame.Arena, slot + 0x90) << 4;
        frame = frame.WriteTemporaryDword(0xD4, unchecked((uint)sx)).WriteTemporaryDword(0xD8, unchecked((uint)sy)).WriteTemporaryDword(0xDC, unchecked((uint)sz));
        NativeBattleActionMatrix scaled = NativeBattlePoseMatrices.Scale56BEF0(basis, sx, sy, sz);
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(0xE0 + lane * 2, unchecked((ushort)scaled.Rotation[lane]));
        }
        NativeBattleVector clip = new(SignedWord(frame.Arena, slot + 0x94), SignedWord(frame.Arena, slot + 0x96), SignedWord(frame.Arena, slot + 0x98));
        ushort flags = Word(frame.Arena, slot + 0xC2);
        frame = frame.WriteTemporary(0x20, frame.Arena.Read(slot + 0x94, 6)).WriteTemporaryWord(0x26, flags);
        return new(frame, scaled with { X = translated.X, Y = translated.Y, Z = translated.Z }, globalFocal) { LocalClip = new(clip, flags) };
    }

    private static NativeBattleWaterMeshPrepared PrepareRotatedUniform(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, int index, short globalFocal, int rotationMode = 0)
    {
        int slot = bank.SlotOffset(frame.Arena, index);
        NativeBattleVector angles = new(SignedWord(frame.Arena, slot + 0x8C), SignedWord(frame.Arena, slot + 0x8E), 0);
        frame = frame.WriteTemporaryWord(0x90, unchecked((ushort)angles.X)).WriteTemporaryWord(0x92, unchecked((ushort)angles.Y)).WriteTemporaryWord(0x94, 0);
        NativeBattleActionMatrix euler;
        (frame, euler) = NativeBattleWaterMatrixCommands.BuildRotationB65590(frame, angles, rotationMode);
        uint address = NativeBattleWaterMatrixCommands.ResolveB65520(frame, bank, index, Word(frame.Arena, slot + 0x9C));
        byte[] raw = frame.Core.Read(address, 18);
        NativeBattleActionMatrix basis = new(Enumerable.Range(0, 9).Select(lane => BinaryPrimitives.ReadInt16LittleEndian(raw.AsSpan(lane * 2))).ToImmutableArray(), unchecked((int)CoreDword(frame, address + 20)), unchecked((int)CoreDword(frame, address + 24)), unchecked((int)CoreDword(frame, address + 28)), 4096);
        NativeBattleVector point = new(SignedWord(frame.Arena, slot + 0x94), SignedWord(frame.Arena, slot + 0x96), SignedWord(frame.Arena, slot + 0x98));
        NativeBattleFullMacVertex position = NativeBattleGfMatrices.TransformMac460840(basis, point);
        frame = frame.WriteTemporaryDword(0xF4, unchecked((uint)position.X)).WriteTemporaryDword(0xF8, unchecked((uint)position.Y)).WriteTemporaryDword(0xFC, unchecked((uint)position.Z)).WriteTemporaryDword(0xC0, position.Flags);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Multiply56C090(basis, euler) with { X = position.X, Y = position.Y, Z = position.Z };
        int scale = SignedWord(frame.Arena, slot + 0x90) << 4;
        frame = frame.WriteTemporaryDword(0xC8, unchecked((uint)scale)).WriteTemporaryDword(0xC4, unchecked((uint)scale)).WriteTemporaryDword(0xC0, unchecked((uint)scale));
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale, scale, scale);
        for (int lane = 0; lane < 9; lane++)
        {
            frame = frame.WriteTemporaryWord(0xE0 + lane * 2, unchecked((ushort)matrix.Rotation[lane]));
        }
        // Composition copies its retained CPU stack padding WORD together with the nine rotation WORDs.
        frame.TemporaryFrame.MarkUnknown(0xF2, 2);
        frame = frame.WriteTemporaryDword(0x48, 0).WriteTemporaryDword(0x44, 0).WriteTemporaryDword(0x40, 0).WriteTemporaryDword(0x4C, Dword(frame.Arena, slot + 0xBC));
        return new(frame, matrix, globalFocal);
    }

    public static NativeBattleWaterMeshProjected MorphProject(NativeBattleWaterMeshPrepared prepared, NativeBattleWaterSlotBank bank, int index, NativeBattleWater222Resources resources, NativeBattleEffectHeapAllocation shapeResourceMap, int actualShapeExtent, NativeBattleCameraProjection projection, short dqa, int dqb, NativeBattleEffectArena depthOwner)
    {
        NativeBattleWaterExecutionFrame frame = prepared.Frame;
        resources.Validate();
        ValidateDepth(depthOwner);
        int slot = bank.SlotOffset(frame.Arena, index);
        uint manager = CoreDword(frame, 0x27973EC);
        uint shape = Dword(frame.Arena, slot + 0xD8);
        if (actualShapeExtent is not (0x218 or 0x2A48 or 0x5E4))
        {
            throw new NotSupportedException("Native Water current mesh requires its bounded selected shape extent.");
        }
        RequireResource(frame, resources, shape, actualShapeExtent);
        int shapeOffset = ArenaOffset(shape, actualShapeExtent);
        int count = checked((int)Dword(frame.Arena, shapeOffset + 0x18));
        if (count is not (24 or 170 or 49))
        {
            throw new NotSupportedException("Native Water current mesh requires its selected positive vertex count.");
        }
        uint vertices = unchecked(shape + Dword(frame.Arena, shapeOffset + 0x14));
        RequireShape(shape, actualShapeExtent, vertices, checked(count * 8));
        uint work = CoreDword(frame, manager + 0x74);
        _ = ArenaOffset(work, checked(count * 8));
        uint normalCount = Dword(frame.Arena, shapeOffset + 0x20);
        uint projectionEnd = unchecked(work + 8u * (uint)count);
        frame = frame.WriteTemporaryDword(0x60, CoreDword(frame, manager + 0x7C))
            .WriteTemporaryDword(0x50, unchecked((uint)(int)SignedWord(frame.Arena, slot + 0x92)) | 0xE1000200)
            .WriteTemporaryDword(0x54, unchecked((uint)(int)SignedWord(frame.Arena, slot + 0x9A)))
            .WriteTemporaryDword(0x84, Dword(frame.Arena, slot + 0xCC) & 0x02FFFFFF);
        frame = frame.WriteTemporaryDword(0x8C, frame.ReadTemporaryDword(0x84) & 0x02000000)
            .WriteTemporaryDword(0x5C, frame.ReadTemporaryDword(0x14C))
            .WriteTemporaryDword(0x90, frame.Arena.Read(slot + 0xDE, 1)[0])
            .WriteTemporaryDword(0x94, unchecked((uint)(int)SignedWord(frame.Arena, slot + 0xC0)))
            .WriteTemporaryDword(0x98, Word(frame.Arena, slot + 0x9E))
            .WriteTemporaryDword(0x68, shape)
            .WriteTemporaryDword(0x6C, unchecked(shape + Dword(frame.Arena, shapeOffset + 8)))
            .WriteTemporaryDword(0x74, vertices)
            .WriteTemporaryDword(0x78, unchecked(shape + Dword(frame.Arena, shapeOffset + 0x1C)))
            .WriteTemporaryDword(0x7C, work)
            .WriteTemporaryDword(0x80, projectionEnd)
            .WriteTemporaryDword(0x88, unchecked(projectionEnd + 8u * normalCount));
        frame = MorphSelected(frame, bank, index, resources, shapeResourceMap, count);
        if ((frame.Arena.Read(slot + 0x4C, 1)[0] & 2) != 0 || frame.Arena.Read(slot + 0xE1, 1)[0] != 0)
        {
            throw new NotSupportedException("Native Water alternate or per-normal mesh preparation is unsupported.");
        }
        if ((frame.ReadTemporaryDword(0x40) | frame.ReadTemporaryDword(0x44) | frame.ReadTemporaryDword(0x48)) != 0)
        {
            throw new NotSupportedException("Native Water extra floating mesh scale is outside the selected wrapper.");
        }
        projection = projection with { FocalLength = unchecked((ushort)prepared.Focal) };
        uint source = frame.ReadTemporaryDword(0x74);
        int workOffset = ArenaOffset(work, checked(count * 8));
        for (int vertex = 0; vertex < count; vertex++)
        {
            int sourceOffset = ArenaOffset(unchecked(source + (uint)(vertex * 8)), 6);
            NativeBattleVector point = new(SignedWord(frame.Arena, sourceOffset), SignedWord(frame.Arena, sourceOffset + 2), SignedWord(frame.Arena, sourceOffset + 4));
            NativeBattleModuleProjectedVertex projected = NativeBattleModuleProjection.Project45FAA0(prepared.Matrix, point, projection, dqa, dqb);
            uint mode = frame.ReadTemporaryDword(0x90) & 0x60;
            int bias = unchecked((int)frame.ReadTemporaryDword(0x94));
            int bucket = mode == 0 ? projected.Depth >> 2 : (mode & 0x20) != 0 ? bias : unchecked((projected.Depth >> 2) + bias);
            byte[] record = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(record, projected.Screen);
            BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), unchecked(projected.Flags << 4));
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), unchecked((ushort)bucket));
            frame = frame with { Arena = frame.Arena.Write(workOffset + vertex * 8, record) };
            byte[] depth = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(depth, projected.Depth);
            depthOwner = depthOwner.Write(vertex * 4, depth);
        }
        return new(frame, depthOwner, 1d);
    }

    public static NativeBattleWaterExecutionFrame MorphSelected(NativeBattleWaterExecutionFrame frame, NativeBattleWaterSlotBank bank, int index, NativeBattleWater222Resources resources, NativeBattleEffectHeapAllocation shapeResourceMap, int currentVertexCount)
    {
        return NativeBattleScriptedMeshMorph.Morph(frame, new(
            (current, selector) =>
            {
                ImmutableArray<byte> order = current.Core.Read(0x2797454, 128).ToImmutableArray();
                int reference = NativeBattleWaterSlots.LookupB65370(current.Arena, bank, index, order, selector);
                return unchecked(NativeBattleEffectNodeMemory.ArenaAddress + (uint)bank.SlotOffset(current.Arena, reference));
            },
            (current, shape) => NativeBattleWaterShapeSelection.ResolveB657E0(current, resources, shapeResourceMap, shape)));
    }

    public static NativeBattleWaterMeshSubmission Submit(NativeBattleWaterMeshProjected projected, int actualShapeExtent, Func<uint, int> resolveActualOtBucket, Func<uint, uint>? shadeMaterial = null)
    {
        if (projected.ProxyScale != 1d)
        {
            throw new NotSupportedException("Native Water selected mesh packets require the proved ordinary unit proxy scale.");
        }
        NativeBattleWaterExecutionFrame frame = projected.Frame;
        uint shape = frame.ReadTemporaryDword(0x68);
        int shapeOffset = ArenaOffset(shape, actualShapeExtent);
        int count = checked((int)Dword(frame.Arena, shapeOffset + 0x18));
        uint work = frame.ReadTemporaryDword(0x7C);
        ImmutableArray<byte> records = frame.Arena.Read(ArenaOffset(work, checked(count * 8)), count * 8).ToImmutableArray();
        ImmutableArray<uint> depths = Enumerable.Range(0, count).Select(vertex => BinaryPrimitives.ReadUInt32LittleEndian(projected.DepthOwner.Read(vertex * 4, 4))).ToImmutableArray();
        uint cursor = frame.ReadTemporaryDword(0x6C);
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        while (true)
        {
            RequireShape(shape, actualShapeExtent, cursor, 4);
            int list = ArenaOffset(cursor, 4);
            short faces = SignedWord(frame.Arena, list + 2);
            if (faces == -1)
            {
                break;
            }
            short type = SignedWord(frame.Arena, list);
            int stride = type switch
            {
                7 => 20,
                9 => 28,
                17 => 24,
                _ => throw new NotSupportedException("Native Water selected group writer is unsupported.")
            };
            if (faces <= 0)
            {
                throw new NotSupportedException("Native Water selected mesh list requires its actual positive count.");
            }
            cursor = unchecked(cursor + 4);
            uint nextGroup = unchecked(cursor + (uint)(faces * stride));
            frame = frame.WriteTemporaryDword(0x70, unchecked((uint)(int)faces)).WriteTemporaryDword(0x6C, cursor).WriteTemporaryDword(0x58, nextGroup);
            int slot = ArenaOffset(CoreDword(frame, 0x27973E8), 0x100);
            if ((frame.Arena.Read(slot + 0x4C, 1)[0] & 4) != 0)
            {
                throw new NotSupportedException("Native Water alternate selected group dispatch is unsupported.");
            }
            RequireShape(shape, actualShapeExtent, cursor, checked(faces * stride));
            NativeBattleWaterMeshGroup group = new(type, faces, frame.Arena.Read(ArenaOffset(cursor, faces * stride), faces * stride).ToImmutableArray());
            NativeBattleWaterMeshResult drawn = NativeBattleWaterMeshPackets.EmitSelected(frame, group, records, depths, frame.ReadTemporaryDword(0x60), resolveActualOtBucket, shadeMaterial);
            foreach (NativeBattleGfGeometryAttempt attempt in drawn.Packets.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) });
            }
            allocated = checked(allocated + drawn.Packets.AllocationBytes);
            frame = drawn.Frame;
            cursor = frame.ReadTemporaryDword(0x58);
            frame = frame.WriteTemporaryDword(0x6C, cursor);
        }
        frame = WriteCoreDword(frame, CoreDword(frame, 0x27973EC) + 0x7C, frame.ReadTemporaryDword(0x60));
        return new(frame, new(attempts.ToImmutable(), allocated));
    }

    private static void RequireResource(NativeBattleWaterExecutionFrame frame, NativeBattleWater222Resources resources, uint address, int length)
    {
        for (int index = 0; index < 2; index++)
        {
            uint basis = CoreDword(frame, 0x2798A68 + 4u * (uint)index);
            long offset = (long)address - basis;
            if (offset >= 0 && offset <= resources.Files[index].Data.Length - length)
            {
                _ = ArenaOffset(address, length);
                return;
            }
        }
        throw new NotSupportedException("Native Water shape leaves its supplied current resource owners.");
    }

    private static void RequireShape(uint shape, int extent, uint address, int length)
    {
        long offset = (long)address - shape;
        if (offset < 0 || length < 0 || offset > extent - length)
        {
            throw new InvalidDataException("Native Water face or vertex read leaves its actual selected shape.");
        }
    }

    private static void ValidateDepth(NativeBattleEffectArena depths)
    {
        depths.Validate();
        depths.ValidateExtent(DepthLength);
    }

    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static ushort Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(arena.Read(offset, 2));
    private static short SignedWord(NativeBattleEffectArena arena, int offset) => unchecked((short)Word(arena, offset));
}
