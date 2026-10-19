using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScriptedMeshProjected(NativeBattleWaterExecutionFrame Frame, NativeBattleEffectArena DepthOwner, NativeBattleScriptedLightingMemory Lighting, NativeBattleScriptedColorRegisters Registers)
{
    public uint DepthScaleBits1877DA8 { get; init; }
}
public sealed record NativeBattleScriptedMeshSubmission(NativeBattleWaterExecutionFrame Frame, NativeBattleGfGeometryBatch Packets, NativeBattleScriptedLightingMemory Lighting, NativeBattleScriptedColorRegisters Registers);

/// <summary>The ordinary cinematic mesh path over actual current shape, work, depth and lighting owners.</summary>
public static class NativeBattleScriptedMeshPreparation
{
    public static NativeBattleScriptedMeshProjected Project(NativeBattleWaterMeshPrepared prepared, int shapeExtent, NativeBattleCameraProjection projection, short dqa, int dqb, NativeBattleEffectArena depthOwner, NativeBattleScriptedLightingMemory lighting, NativeBattleScriptedColorRegisters registers, NativeBattleScriptedMeshMorphServices? morphServices = null, uint? retainedDepthScaleBits = null)
    {
        NativeBattleWaterExecutionFrame frame = prepared.Frame;
        depthOwner.Validate();
        uint manager = CoreDword(frame, 0x27973EC);
        uint shape = SlotDword(frame, 0xD8);
        RequireShape(shape, shapeExtent, shape, 0x24);
        int vertices = unchecked((int)Dword(shape + 0x18));
        int normals = unchecked((int)Dword(shape + 0x20));
        if (vertices <= 0 || normals < 0)
        {
            throw new NotSupportedException("Native cinematic mesh requires its actual positive vertex count and nonnegative normal extent.");
        }
        bool scaled = (frame.ReadTemporaryDword(0x40) | frame.ReadTemporaryDword(0x44) | frame.ReadTemporaryDword(0x48)) != 0;
        bool alternate = !scaled && (ReadSlot(frame, 0x4C, 1)[0] & 2) != 0;
        uint depthScaleBits = scaled ? BitConverter.SingleToUInt32Bits((float)(4096d / unchecked((int)frame.ReadTemporaryDword(0xC0)))) : alternate ? retainedDepthScaleBits ?? throw new NotSupportedException("Native alternate mesh projection requires its retained depth scale FLOAT1877DA8.") : 0x3F800000u;
        int scaleX = scaled ? unchecked((int)frame.ReadTemporaryDword(0x40)) : 0;
        int scaleY = scaled ? unchecked((int)frame.ReadTemporaryDword(0x44)) : 0;
        int scaleZ = scaled ? unchecked((int)frame.ReadTemporaryDword(0x48)) : 0;
        uint source = unchecked(shape + Dword(shape + 0x14));
        uint normalSource = unchecked(shape + Dword(shape + 0x1C));
        RequireShape(shape, shapeExtent, source, checked(vertices * 8));
        uint work = CoreDword(frame, manager + 0x74);
        uint normalWork = unchecked(work + (uint)(vertices * 8));
        uint cueWork = unchecked(normalWork + (uint)(normals * 8));
        _ = ArenaOffset(work, checked(vertices * 8));
        _ = ArenaOffset(cueWork, checked(vertices * 8));
        frame = frame.WriteTemporaryDword(0x60, CoreDword(frame, manager + 0x7C))
            .WriteTemporaryDword(0x50, unchecked((uint)(int)(short)SlotWord(frame, 0x92)) | 0xE1000200)
            .WriteTemporaryDword(0x54, unchecked((uint)(int)(short)SlotWord(frame, 0x9A)))
            .WriteTemporaryDword(0x84, SlotDword(frame, 0xCC) & 0x02FFFFFF);
        frame = frame.WriteTemporaryDword(0x8C, frame.ReadTemporaryDword(0x84) & 0x02000000)
            .WriteTemporaryDword(0x5C, frame.ReadTemporaryDword(0x14C))
            .WriteTemporaryDword(0x90, ReadSlot(frame, 0xDE, 1)[0])
            .WriteTemporaryDword(0x94, unchecked((uint)(int)(short)SlotWord(frame, 0xC0)))
            .WriteTemporaryDword(0x98, SlotWord(frame, 0x9E))
            .WriteTemporaryDword(0x68, shape)
            .WriteTemporaryDword(0x6C, unchecked(shape + Dword(shape + 8)))
            .WriteTemporaryDword(0x74, source)
            .WriteTemporaryDword(0x78, normalSource)
            .WriteTemporaryDword(0x7C, work)
            .WriteTemporaryDword(0x80, normalWork)
            .WriteTemporaryDword(0x88, cueWork);
        if (frame.ReadTemporaryDword(0x4C) != 0)
        {
            frame = NativeBattleScriptedMeshMorph.Morph(frame, morphServices ?? throw new NotSupportedException("Native cinematic morph preparation requires its actual shape and reference resolvers."));
            source = frame.ReadTemporaryDword(0x74);
            normalSource = frame.ReadTemporaryDword(0x78);
        }
        if (prepared.LocalClip is { } clip)
        {
            frame = ApplyLocalClip(frame, clip, vertices);
            source = frame.ReadTemporaryDword(0x74);
        }
        projection = projection with { FocalLength = unchecked((ushort)prepared.Focal) };
        for (int index = 0; index < vertices; index++)
        {
            byte[] raw = frame.Arena.Read(ArenaOffset(unchecked(source + (uint)(index * 8)), 6), 6);
            NativeBattleVector point = Vector(raw);
            if (scaled)
            {
                point = new(unchecked((short)(unchecked(point.X * scaleX) >> 12)), unchecked((short)(unchecked(point.Y * scaleY) >> 12)), unchecked((short)(unchecked(point.Z * scaleZ) >> 12)));
            }
            NativeBattleModuleDepthCueVertex result = NativeBattleModuleProjection.ProjectWithCue45FAA0(prepared.Matrix, point, projection, dqa, dqb);
            NativeBattleModuleProjectedVertex projected = result.Projected;
            uint mode = frame.ReadTemporaryDword(0x90) & 0x60;
            int bias = unchecked((int)frame.ReadTemporaryDword(0x94));
            int bucket = mode == 0 ? projected.Depth >> 2 : (mode & 0x20) != 0 ? bias : unchecked((projected.Depth >> 2) + bias);
            byte[] record = new byte[8];
            BinaryPrimitives.WriteUInt32LittleEndian(record, projected.Screen);
            uint shiftedFlags = unchecked(projected.Flags << 4);
            if (alternate)
            {
                int x = unchecked((short)projected.Screen);
                int y = unchecked((short)(projected.Screen >> 16));
                ushort clipBits = (ushort)((projected.Flags & 0x20000) != 0 ? 0x10 : 0);
                clipBits |= (ushort)(x < 0 ? 8 : x >= 2560 ? 2 : 0);
                clipBits |= (ushort)(y < 0 ? 4 : y >= 1792 ? 1 : 0);
                BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(6), clipBits);
            }
            else
            {
                BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(4), shiftedFlags);
            }
            if (scaled)
            {
                scaleX = unchecked((int)shiftedFlags);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), unchecked((ushort)bucket));
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(work + (uint)(index * 8)), 8), record).Write(ArenaOffset(unchecked(cueWork + (uint)(index * 8)), 4), BitConverter.GetBytes(result.Cue)) };
            depthOwner = depthOwner.Write(index * 4, BitConverter.GetBytes((uint)projected.Depth));
        }
        if (ReadSlot(frame, 0xE1, 1)[0] != 0)
        {
            if (normals == 0)
            {
                throw new NotSupportedException("Native cinematic normal shading reaches its unconditional first normal without a positive declared normal extent.");
            }
            if (normalSource != normalWork)
            {
                RequireShape(shape, shapeExtent, normalSource, checked(normals * 8));
            }
            uint material = (frame.ReadTemporaryDword(0x84) & 0xFF808080) | 0x00808080;
            frame = frame.WriteTemporaryDword(0xF0, material);
            for (int index = 0; index < normals; index++)
            {
                byte[] raw = frame.Arena.Read(ArenaOffset(unchecked(normalSource + (uint)(index * 8)), 8), 8);
                registers = registers with { NormalInput = raw.ToImmutableArray(), NormalInputKnown = null, Material = material };
                registers = NativeBattleScriptedNormalColor.Transform4601B0(lighting, Vector(raw), material, registers);
                uint color = registers.Rgb[2] ?? throw new NotSupportedException("Native cinematic normal shader did not publish RGB2.");
                frame = frame with { Arena = frame.Arena.Write(ArenaOffset(unchecked(normalWork + (uint)(index * 8)), 4), BitConverter.GetBytes(color)) };
            }
        }
        return new(frame, depthOwner, lighting, registers) { DepthScaleBits1877DA8 = depthScaleBits };

        uint Dword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(address, 4), 4));
    }

    internal static NativeBattleWaterExecutionFrame ApplyLocalClip(NativeBattleWaterExecutionFrame frame, NativeBattleMeshLocalClip clip, int vertices)
    {
        if ((clip.Flags & 0xC) == 0)
        {
            return frame;
        }
        uint source = frame.ReadTemporaryDword(0x74);
        uint destination = frame.ReadTemporaryDword(0x7C);
        frame = frame.WriteTemporaryDword(0x74, destination);
        short limit = unchecked((short)(clip.Position.Y << 3));
        for (int index = 0; index < vertices; index++)
        {
            uint input = unchecked(source + (uint)(index * 8));
            uint output = unchecked(destination + (uint)(index * 8));
            short y = BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(ArenaOffset(input + 2, 2), 2));
            y = (clip.Flags & 8) != 0 ? Math.Min(y, limit) : Math.Max(y, limit);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(output + 2, 2), BitConverter.GetBytes(y)) };
            byte[] x = frame.Arena.Read(ArenaOffset(input, 2), 2);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(output, 2), x) };
            byte[] z = frame.Arena.Read(ArenaOffset(input + 4, 2), 2);
            frame = frame with { Arena = frame.Arena.Write(ArenaOffset(output + 4, 2), z) };
        }
        return frame;
    }

    public static NativeBattleScriptedMeshSubmission Submit(NativeBattleScriptedMeshProjected projected, int shapeExtent, Func<int, uint> resolveActualHandler, Func<int, byte> resolveActualStride, Func<uint, int> resolveActualOtBucket)
    {
        NativeBattleWaterExecutionFrame frame = projected.Frame;
        NativeBattleScriptedLightingMemory lighting = projected.Lighting;
        NativeBattleScriptedColorRegisters registers = projected.Registers;
        uint shape = frame.ReadTemporaryDword(0x68);
        int count = unchecked((int)BinaryPrimitives.ReadUInt32LittleEndian(frame.Arena.Read(ArenaOffset(shape + 0x18, 4), 4)));
        uint work = frame.ReadTemporaryDword(0x7C);
        ImmutableArray<byte> records = frame.Arena.Read(ArenaOffset(work, checked(count * 8)), count * 8).ToImmutableArray();
        ImmutableArray<uint> depths = Enumerable.Range(0, count).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(projected.DepthOwner.Read(index * 4, 4))).ToImmutableArray();
        ImmutableArray<NativeBattleGfGeometryAttempt>.Builder attempts = ImmutableArray.CreateBuilder<NativeBattleGfGeometryAttempt>();
        int allocated = 0;
        uint cursor = frame.ReadTemporaryDword(0x6C);
        while (true)
        {
            RequireShape(shape, shapeExtent, cursor, 4);
            byte[] header = frame.Arena.Read(ArenaOffset(cursor, 4), 4);
            short type = BinaryPrimitives.ReadInt16LittleEndian(header);
            short faces = BinaryPrimitives.ReadInt16LittleEndian(header.AsSpan(2));
            if (faces == -1)
            {
                break;
            }
            if (type < 0 || faces <= 0)
            {
                throw new NotSupportedException("Native cinematic primitive stream requires its actual selected type and positive count.");
            }
            int expectedStride = type switch
            {
                16 => 12,
                2 or 7 or 8 => 20,
                9 or 12 => 28,
                17 or 18 => 24,
                19 => 36,
                _ => throw new NotSupportedException($"Native cinematic primitive type {type:X} is unsupported.")
            };
            byte stride = resolveActualStride(type);
            if (stride != expectedStride)
            {
                throw new InvalidDataException("Native cinematic selected primitive record size disagrees with its actual handler contract.");
            }
            int dispatch = type;
            ushort flags = SlotWord(frame, 0x4C);
            if ((flags & 2) != 0)
            {
                dispatch += 20;
            }
            if ((flags & 4) != 0)
            {
                dispatch += 40;
            }
            uint handler = resolveActualHandler(dispatch);
            cursor = unchecked(cursor + 4);
            uint next = unchecked(cursor + (uint)(faces * stride));
            RequireShape(shape, shapeExtent, cursor, checked(faces * stride));
            frame = frame.WriteTemporaryDword(0x70, unchecked((uint)(int)faces)).WriteTemporaryDword(0x6C, cursor).WriteTemporaryDword(0x58, next);
            NativeBattleWaterMeshGroup group = new(type, faces, frame.Arena.Read(ArenaOffset(cursor, faces * stride), faces * stride).ToImmutableArray());
            NativeBattleScriptedMeshSubmission result = NativeBattleScriptedMeshPackets.EmitSelected(frame, group, handler, shapeExtent, records, depths, frame.ReadTemporaryDword(0x60), lighting, registers, resolveActualOtBucket, BitConverter.UInt32BitsToSingle(projected.DepthScaleBits1877DA8));
            foreach (NativeBattleGfGeometryAttempt attempt in result.Packets.Attempts)
            {
                attempts.Add(attempt with { ByteOffsetRelative = checked(attempt.ByteOffsetRelative + allocated) });
            }
            allocated = checked(allocated + result.Packets.AllocationBytes);
            frame = result.Frame;
            lighting = result.Lighting;
            registers = result.Registers;
            cursor = frame.ReadTemporaryDword(0x58);
            frame = frame.WriteTemporaryDword(0x6C, cursor);
        }
        frame = WriteCoreDword(frame, CoreDword(frame, 0x27973EC) + 0x7C, frame.ReadTemporaryDword(0x60));
        return new(frame, new(attempts.ToImmutable(), allocated), lighting, registers);
    }

    internal static NativeBattleVector Vector(ReadOnlySpan<byte> raw) => new(BinaryPrimitives.ReadInt16LittleEndian(raw), BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(2)), BinaryPrimitives.ReadInt16LittleEndian(raw.Slice(4)));

    internal static void RequireShape(uint shape, int extent, uint address, int length)
    {
        long relative = (long)address - shape;
        if (extent < 0 || length < 0 || relative < 0 || relative > extent - length)
        {
            throw new InvalidDataException("Native cinematic mesh read leaves its actual selected shape extent.");
        }
        _ = ArenaOffset(address, length);
    }

    private static uint SlotDword(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(ReadSlot(frame, offset, 4));
}
