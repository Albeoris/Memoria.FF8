using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect65Programme
{
    public static NativeBattleGfAnimationSource ReadSource(NativeBattleEffect65Frame frame, uint address, NativeBattleEffect65Resources resources)
    {
        uint start = checked(frame.State.ReadDword(ArenaGlobal) + FileOffset);
        int offset = checked((int)(address - start));
        short[] files = BundleProgrammeOffsets.Where(pair => pair.Value.Contains(offset)).Select(pair => pair.Key).ToArray();
        if (files.Length != 1)
        {
            throw new InvalidDataException("Effect65 programme pointer leaves its admitted current loaded source offsets.");
        }
        short file = files[0];
        int length = checked(BundleLengths[file] - offset);
        return new(address, ReadBytes(frame, address, length).Read(0, length).ToImmutableArray());
    }

    public static NativeBattleEffect65Step Advance(NativeBattleEffect65Frame frame, uint workspaceAddress, int workspaceLength, NativeBattleEffectArena caller48, int frozen, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs, bool scaleNodes = false, uint cursorGlobal = CursorGlobal)
    {
        NativeBattleGfAnimationWorkspace Workspace() => new(ReadBytes(frame, workspaceAddress, workspaceLength).Read(0, workspaceLength).ToImmutableArray());
        NativeBattleGfAnimationSource source = ReadSource(frame, Workspace().ResourceAddress, resources);
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, frozen, (workspace, node) =>
        {
            frame = WriteBytes(frame, workspaceAddress, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), workspaceLength);
            if (scaleNodes)
            {
                int factor = unchecked((int)frame.State.ReadDword(ScaleGlobal));
                byte[] output = node.Data.ToArray();
                for (int axis = 0; axis < 3; axis++)
                {
                    int at = 24 + axis * 2;
                    short value = BinaryPrimitives.ReadInt16LittleEndian(output.AsSpan(at));
                    BinaryPrimitives.WriteInt16LittleEndian(output.AsSpan(at), unchecked((short)(unchecked(value * factor) >> 12)));
                }
                node = node with { Data = output.ToImmutableArray() };
            }
            frame = DrawNode(frame, source, node, caller48, resources, inputs, cursorGlobal);
            return Workspace();
        });
        frame = WriteBytes(frame, workspaceAddress, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan()), workspaceLength);
        return new(frame, advanced.Remaining);
    }

    public static NativeBattleEffect65Frame DrawNode(NativeBattleEffect65Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller48, NativeBattleEffect65Resources resources, NativeBattleEffect65Inputs inputs, uint cursorGlobal = CursorGlobal)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        uint geometry = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = NativeBattleEffect65Resources.ReadLoadedGeometry(source, geometry);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (count is < 1 or > 306 || node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("Effect65 ACT geometry leaves its current selected shapes.");
        }
        (frame, uint temporaryAddress) = Allocate(frame, 108);
        uint vertices = checked(geometry + 12 + (uint)((node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40) * count * 8));
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        if (morph)
        {
            vertices = Dword(caller48, 0x2C);
            if (vertices != checked(frame.State.ReadDword(ArenaGlobal) + 0x6CB8))
            {
                throw new InvalidDataException("Effect65 morph requires its actual shared row owner.");
            }
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            for (int vertex = 0; vertex < count; vertex++)
            {
                NativeBattleVector first = Shape(node.Word40, vertex);
                NativeBattleVector second = Shape(node.Word42, vertex);
                registers = Weight(registers, first, 4096 - node.Scalar38, false);
                registers = Weight(registers, second, node.Scalar38, true);
                byte[] xyz = new byte[6];
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(xyz.AsSpan(axis * 2), unchecked((short)registers.Colors.Ir[axis]!.Value));
                }
                frame = WriteBytes(frame, checked(vertices + (uint)(vertex * 8)), NativeBattleEffectArena.Clear().Write(0, xyz), 6);
            }
            frame = Publish(frame, registers);
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x20000) != 0 ? NativeBattleZombieDrawing.Euler701530(node.Vector16) : (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector point = node.Vector8;
        if ((node.Header & 0x800) != 0)
        {
            point = NativeBattleGfMatrices.RotateShort56C4F0(rotation, point);
        }
        (byte[] matrixBytes, bool[] matrixKnown) = caller48.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 callerMatrix = new(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray());
        NativeBattleActionMatrix basis = callerMatrix.ReadMatrix();
        int x = point.X;
        int y = point.Y;
        int z = point.Z;
        if ((node.Header & 0x200) == 0)
        {
            frame = LoadMatrix(frame, callerMatrix, true, false);
            NativeBattleGeometryRegisterStep registers = NativeBattleGeometry.LoadVector(Registers(frame), 0, point, null);
            registers = NativeBattleGeometry.Transform(registers, false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = Publish(frame, registers);
            rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
        }
        rotation = rotation with { X = unchecked(basis.X + x), Y = unchecked(basis.Y + y), Z = unchecked(basis.Z + z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        flags |= (node.Header & 0x2000) != 0 ? 0xCu : 0;
        flags |= node.Scalar36 != 0 ? 0xC0u : 0;
        NativeBattleEffectArena context = Context(frame, temporaryAddress, 108).Write(0, BitConverter.GetBytes(geometry)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(0x18, BitConverter.GetBytes((int)Word(caller48, 0x28))).Write(0x1C, BitConverter.GetBytes(flags)).Write(0x20, new byte[8]).Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        byte selector = ReadStatic(frame, checked(Dword(caller48, 0x20) + node.GeometryIndex), 1, resources)[0];
        if (selector is (byte)'1' or (byte)'2' or (byte)'3')
        {
            short phase = Word(caller48, 0x2A);
            context = context.Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes((ushort)(selector == '3' ? 64 : 256))).Write(0x32, BitConverter.GetBytes((ushort)(selector == '3' ? 256 : 128)));
            if (selector == '1')
            {
                context = context.Write(0x2E, BitConverter.GetBytes((ushort)128));
            }
            context = context.Write(selector == '3' ? 0x20 : 0x22, BitConverter.GetBytes(unchecked((ushort)(phase & (selector == '3' ? 63 : 127)))));
        }
        NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, 306, offset =>
        {
            NativeBattleEffectArena row = morph ? ReadBytes(frame, checked(vertices + (uint)offset), 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
            (byte[] bytes, bool[] known) = row.ReadSnapshot(0, 8);
            if (!known.Take(6).All(value => value))
            {
                throw new NotSupportedException("Effect65 ACT requires its reached current vertex XYZ.");
            }
            return (new NativeBattleVector(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(4))), known[6] && known[7] ? BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(6)) : (short?)null);
        });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporaryAddress, drawn.Context108, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect65DeliverBatch(drawn.Batch, cursorGlobal));
        return Release(frame, 108);

        NativeBattleVector Shape(int shape, int vertex)
        {
            int offset = checked((int)(geometry - source.Address) + 12 + (shape * count + vertex) * 8);
            ReadOnlySpan<byte> bytes = source.Data.AsSpan().Slice(offset, 6);
            return new(BinaryPrimitives.ReadInt16LittleEndian(bytes), BinaryPrimitives.ReadInt16LittleEndian(bytes[2..]), BinaryPrimitives.ReadInt16LittleEndian(bytes[4..]));
        }
        NativeBattleGeometryRegisterStep Weight(NativeBattleGeometryRegisterStep registers, NativeBattleVector vector, int factor, bool accumulate)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)factor) } }, unchecked((ushort)vector.X), unchecked((ushort)vector.Y), unchecked((ushort)vector.Z));
            return NativeBattleRaldo19BallRendering.Weighted(registers, accumulate, inputs.Colors);
        }
    }

    public static byte[] ReadStatic(NativeBattleEffect65Frame frame, uint address, int length, NativeBattleEffect65Resources resources)
    {
        if (LiteralRanges.Any(range => address >= range.Address && (ulong)address + (uint)length <= (ulong)range.Address + (uint)range.Length))
        {
            return frame.State.Read(address, length);
        }
        return resources.ReadStatic(address, length).ToArray();
    }
    private static uint Dword(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    private static short Word(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
}
