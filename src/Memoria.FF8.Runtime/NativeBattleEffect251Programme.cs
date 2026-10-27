using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect251Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect251Lower;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect251Programme
{
    public static NativeBattleEffect251Frame Initialize(NativeBattleEffect251Frame frame, uint workspace, uint source, int clearBytes, NativeBattleEffect251Resources resources)
    {
        int index = NativeBattleEffect251Profile.ProgrammeAddresses.IndexOf(source);
        if (index < 0)
        {
            throw new NotSupportedException("Effect251 requires its selected programme source.");
        }
        int length = Math.Max(clearBytes, NativeBattleEffect251Profile.ProgrammeWorkspaceLengths[index]);
        NativeBattleGfAnimationWorkspace initialized = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, length);
        initialized = initialized.Initialize7016B0(Source(frame, resources, source), clearBytes);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, initialized);
        return frame;
    }

    public static NativeBattleEffect251Step Advance(NativeBattleEffect251Frame frame, uint workspace, uint sourceAddress, NativeBattleEffectArena caller56, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        int index = NativeBattleEffect251Profile.ProgrammeAddresses.IndexOf(sourceAddress);
        if (index < 0)
        {
            throw new NotSupportedException("Effect251 requires its selected programme workspace.");
        }
        return AdvanceWorkspace(frame, workspace, NativeBattleEffect251Profile.ProgrammeWorkspaceLengths[index], caller56, resources, inputs);
    }

    public static NativeBattleEffect251Step AdvanceWorkspace(NativeBattleEffect251Frame frame, uint workspace, int length, NativeBattleEffectArena caller56, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Current() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, length);
        NativeBattleGfAnimationSource source = Source(frame, resources, Current().ResourceAddress);
        NativeBattleGfAnimationAdvance advanced = Current().Advance701970(source, unchecked((int)ReadCurrentPause(frame)), (cache, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, cache);
            frame = DrawNode(frame, source, node, caller56, resources, inputs);
            return Current();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspace, advanced.Workspace);
        return new(frame, advanced.Remaining);
    }

    public static NativeBattleEffect251Frame DrawNode(NativeBattleEffect251Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller56, NativeBattleEffect251Resources resources, NativeBattleEffect251Inputs inputs)
    {
        if (node.Vector24 is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        (frame, uint temporary) = Allocate(frame, 108);
        int geometryOffset = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + unchecked((short)node.GeometryIndex) * 4));
        uint geometry = checked(source.Address + (uint)geometryOffset);
        NativeBattleStaticMeshResource original = resources.ReadGeometry(geometry);
        NativeBattleStaticMeshResource mesh = original with { Bytes = source.Data.Slice(geometryOffset, original.Bytes.Length) };
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (count < 1 || node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("Effect251 mesh requires its actual selected vertex shapes.");
        }
        NativeBattleEffectArena context = Put251(ReadBytes(frame, temporary, 108), 0, geometry);
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int shape = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40;
        uint vertices = checked(geometry + 12 + (uint)(shape * count * 8));
        if (morph)
        {
            vertices = Dword251(caller56, 0x34);
            RequireScratch(frame, vertices, count);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            for (int index = 0; index < count; index++)
            {
                registers = Weight(registers, Shape(node.Word40, index), 4096 - node.Scalar38, false);
                registers = Weight(registers, Shape(node.Word42, index), node.Scalar38, true);
                NativeBattleEffectArena xyz = NativeBattleEffectArena.Clear();
                for (int axis = 0; axis < 3; axis++)
                {
                    xyz = PutWord251(xyz, axis * 2, registers.Colors.Ir[axis]!.Value);
                }
                frame = WriteBytes(frame, checked(vertices + (uint)index * 8), xyz, 6);
            }
            frame = Publish(frame, registers);
        }
        context = Put251(context, 4, vertices);
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattlePackedMatrix32 basis = Packed251(caller56);
        NativeBattleVector vector = node.Vector8;
        int x = vector.X;
        int y = vector.Y;
        int z = vector.Z;
        if ((node.Header & 0x1000) != 0 || (node.Header & 0x200) == 0)
        {
            NativeBattlePackedMatrix32 selected = (node.Header & 0x1000) != 0 ? Packed251(ReadBytes(frame, 0x2556458, 32)) : basis;
            frame = LoadMatrix(frame, selected, true, false);
            NativeBattleEffectArena row = PutPoint(NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, vector);
            NativeBattleGeometryRegisterStep transformed = NativeBattleGeometry.Transform(LoadPacked251(Registers(frame), row, 0, 0), false);
            frame = Publish(frame, transformed);
            x = transformed.Colors.Mac[0]!.Value;
            y = transformed.Colors.Mac[1]!.Value;
            z = transformed.Colors.Mac[2]!.Value;
            if ((node.Header & 0x8000) == 0)
            {
                rotation = NativeBattleGfMatrices.Multiply56C090(selected.ReadMatrix(), rotation);
            }
        }
        NativeBattleActionMatrix parent = basis.ReadMatrix();
        rotation = rotation with { X = unchecked(x + parent.X), Y = unchecked(y + parent.Y), Z = unchecked(z + parent.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix diagonal = new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096);
                rotation = rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, diagonal).Rotation };
            }
            else
            {
                rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
            }
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        context = Put251(Put251(Put251(context, 12, unchecked((uint)(int)node.Scalar36)), 0x18, unchecked((uint)(int)Word251(caller56, 0x24))), 0x1C, node.Scalar36 == 0 ? 0x2030u : 0x20F0u);
        if (node.Scalar36 != 0)
        {
            context = Put251(context, 8, node.Color32);
        }
        context = context.Write(0x20, new byte[8]);
        context = PutWord251(PutWord251(context, 0x28, 256), 0x2A, 256);
        byte command = resources.ReadStatic(checked(Dword251(caller56, 0x20) + node.GeometryIndex), 1)[0];
        if (command is 0x31 or 0x41)
        {
            context = context.Write(0x2C, new byte[4]);
            context = PutWord251(PutWord251(context, 0x30, command == 0x31 ? 64 : 256), 0x32, command == 0x31 ? 256 : 128);
            context = PutWord251(context, command == 0x31 ? 0x20 : 0x22, caller56.Read(0x26, 1)[0] & (command == 0x31 ? 63 : 127));
        }
        if (command is 0x41 or 0x42)
        {
            frame = Deform6470(frame, geometry, count, Word251(caller56, 0x2A), Word251(caller56, 0x2C), Word251(caller56, 0x2E), Word251(caller56, 0x30), source, resources);
            vertices = checked(frame.State.ReadDword(0x2556450) + 0xA880);
            context = Put251(context, 4, vertices);
        }
        short overrideCue = Word251(caller56, 0x28);
        if (overrideCue != 0)
        {
            context = Put251(Put251(Put251(context, 12, unchecked((uint)(int)overrideCue)), 8, 0), 0x1C, Dword251(context, 0x1C) | 0xC0);
        }
        frame = WriteBytes(frame, temporary, context, 108);
        bool external = morph || command is 0x41 or 0x42;
        NativeBattleEffectArena Row(int offset) => external ? ReadBytes(frame, checked(vertices + (uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
        (NativeBattleVector Vertex, short? W) ReadRow(int offset)
        {
            NativeBattleEffectArena row = Row(offset);
            (byte[] data, bool[] known) = row.ReadSnapshot(0, 8);
            return (Vector(row), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(data.AsSpan(6)) : null);
        }
        NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, count, ReadRow, readSelectedPackedVertex: Row);
        frame = WriteBytes(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect251DrawBatch(drawn.Batch));
        return Release(frame, 108);

        NativeBattleVector Shape(int shapeIndex, int vertex)
        {
            ReadOnlySpan<byte> row = source.Data.AsSpan().Slice(geometryOffset + 12 + (shapeIndex * count + vertex) * 8, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(row), BinaryPrimitives.ReadInt16LittleEndian(row[2..]), BinaryPrimitives.ReadInt16LittleEndian(row[4..]));
        }
        NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep registers, NativeBattleVector value, int factor, bool accumulate)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)value.X), unchecked((ushort)value.Y), unchecked((ushort)value.Z));
            return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, inputs.Colors);
        }
    }

    public static NativeBattleEffect251Frame Deform6470(NativeBattleEffect251Frame frame, uint geometry, int count, int phase, int amplitude, int horizontalScale, int verticalScale, NativeBattleGfAnimationSource source, NativeBattleEffect251Resources resources)
    {
        uint destination = checked(frame.State.ReadDword(0x2556450) + 0xA880);
        RequireScratch(frame, destination, count);
        for (int index = 0; index < count; index++)
        {
            NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(geometry - source.Address)) + 12 + index * 8, 8));
            int angle = Word251(row, 6);
            int wave = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(checked(0x1438C90u + (uint)((angle & 0xFFE) * 2)), 2));
            int radius = unchecked(wave * amplitude) >> 12;
            byte[] pair = resources.ReadStatic(checked(0x1438C90u + (uint)(((angle + phase) & 4095) * 4)), 4);
            int dx = unchecked((short)(unchecked(BinaryPrimitives.ReadInt16LittleEndian(pair) * radius) >> 12));
            int dz = unchecked((short)(unchecked(BinaryPrimitives.ReadInt16LittleEndian(pair.AsSpan(2)) * radius) >> 12));
            NativeBattleEffectArena xyz = PutWord251(PutWord251(PutWord251(NativeBattleEffectArena.Clear(), 0, unchecked((Word251(row, 0) + dx) * horizontalScale) >> 12), 2, unchecked(Word251(row, 2) * verticalScale) >> 12), 4, unchecked((Word251(row, 4) + dz) * horizontalScale) >> 12);
            frame = WriteBytes(frame, checked(destination + (uint)index * 8), xyz, 6);
        }
        return frame;
    }

    private static void RequireScratch(NativeBattleEffect251Frame frame, uint address, int count)
    {
        if (address != checked(frame.State.ReadDword(0x2556450) + 0xA880) || count < 1 || count > 336)
        {
            throw new InvalidDataException("Effect251 vertex scratch exceeds its actual packed rows.");
        }
    }
}
