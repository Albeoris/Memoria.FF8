using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePsychoBlast23ActContext(NativeBattleActionMatrix Matrix, short DepthBias);
public sealed record NativeBattlePsychoBlast23NodeDraw(NativeBattleGfGeometryBatch Batch, NativeBattleEffectArena Temporary104, NativeBattleEffectArena MorphScratch, NativeBattleGeometryRegisterStep Registers, NativeBattleModelRendererRegisters FarControls);

public static class NativeBattlePsychoBlast23Rendering
{
    public static NativeBattlePsychoBlast23ActContext PrepareAct(NativeBattleModulePoolTask task, NativeBattleActionMatrix camera)
    {
        ImmutableArray<short> rotation = Enumerable.Range(0, 9).Select(lane => unchecked((short)task.ReadWord(0x30 + lane * 2))).ToImmutableArray();
        NativeBattleActionMatrix local = new(rotation, unchecked((short)task.ReadWord(0x1C)), unchecked((short)task.ReadWord(0x1E)), unchecked((short)task.ReadWord(0x20)), 4096);
        local = NativeBattlePoseMatrices.Scale56BEF0(local, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58)));
        return new(NativeBattleGfMatrices.Compose56C2F0(camera, local), unchecked((short)task.ReadWord(0x88)));
    }

    public static bool Visible(NativeBattleGfAnimationNode node) => node.Vector24 is not { X: 0, Y: 0, Z: 0 } && (node.Scalar36 < 4096 || node.Color32 != 0);

    public static NativeBattlePsychoBlast23NodeDraw DrawNode(NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattlePsychoBlast23ActContext root, NativeBattleEffectArena temporary, NativeBattleEffectArena morph, NativeBattleGeometryRegisterStep registers, NativeBattleModelRendererRegisters farControls, NativeBattleCameraProjection projection, short dqa, int dqb, short? zsf3, short? zsf4, NativeBattleSummonColors constants)
    {
        if (!Visible(node))
        {
            throw new InvalidOperationException("Psycho Blast callback allocation requires a visible ACT node.");
        }
        int geometry = Int(source.Data, checked(8 + node.GeometryIndex * 4));
        int count = Int(source.Data, checked(geometry + 4));
        if (count < 0 || count > NativeBattlePsychoBlast23Profile.MorphScratchLength / 8)
        {
            throw new InvalidDataException("Psycho Blast shape exceeds its original vertex owner.");
        }
        int first = checked(geometry + 12 + node.Word40 * count * 8);
        int second = checked(geometry + 12 + node.Word42 * count * 8);
        int ratio = node.Scalar38;
        bool interpolate = node.Word40 != node.Word42 && ratio is not (0 or 4096);
        int selected = ratio == 4096 && node.Word40 != node.Word42 ? second : first;
        uint selectedAddress;
        if (interpolate)
        {
            for (int vertex = 0; vertex < count; vertex++)
            {
                NativeBattleVector a = Vector(source.Data, checked(first + vertex * 8));
                NativeBattleVector b = Vector(source.Data, checked(second + vertex * 8));
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - ratio)), Ir = [(int)unchecked((ushort)a.X), (int)unchecked((ushort)a.Y), (int)unchecked((ushort)a.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, constants);
                registers = registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)ratio), Ir = [(int)unchecked((ushort)b.X), (int)unchecked((ushort)b.Y), (int)unchecked((ushort)b.Z)] } };
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, constants);
                for (int axis = 0; axis < 3; axis++)
                {
                    morph = Word(morph, vertex * 8 + axis * 2, unchecked((ushort)registers.Colors.Ir[axis]!.Value));
                }
            }
            selectedAddress = NativeBattlePsychoBlast23Profile.MorphScratchAddress;
        }
        else
        {
            _ = Slice(source.Data, selected, checked(count * 8));
            selectedAddress = checked(source.Address + (uint)selected);
        }
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(node.Vector16);
        int x;
        int y;
        int z;
        if ((node.Header & 0x200) != 0)
        {
            x = node.Vector8.X;
            y = node.Vector8.Y;
            z = node.Vector8.Z;
        }
        else
        {
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, root.Matrix, true, false, null) };
            registers = NativeBattleGeometry.LoadVector(registers, 0, node.Vector8, null);
            registers = NativeBattleGeometry.Transform(registers, false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            matrix = NativeBattleGfMatrices.Multiply56C090(root.Matrix, matrix);
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = (node.Header & 0x100) != 0 ? NativeBattleGfMatrices.Multiply56C090(matrix, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)) : NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        matrix = matrix with { X = unchecked(x + root.Matrix.X), Y = unchecked(y + root.Matrix.Y), Z = unchecked(z + root.Matrix.Z) };
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, true, null) };
        uint shapeAddress = checked(source.Address + (uint)geometry);
        temporary = Dword(temporary, 0, shapeAddress);
        temporary = Dword(temporary, 4, selectedAddress);
        uint flags = (node.Header & 0x4000) != 0 ? 0x2000u : 0x2030u;
        if ((node.Header & 0x2000) != 0)
        {
            flags |= 0xC;
        }
        temporary = Dword(temporary, 12, unchecked((uint)(int)node.Scalar36));
        if (node.Scalar36 != 0)
        {
            flags |= 0xC0;
            temporary = Dword(temporary, 8, node.Color32);
        }
        temporary = Dword(temporary, 0x14, flags);
        temporary = Dword(temporary, 0x10, unchecked((uint)(int)root.DepthBias));
        foreach (int offset in new[] { 0x18, 0x1A, 0x1C, 0x1E, 0x24, 0x26 })
        {
            temporary = Word(temporary, offset, 0);
        }
        foreach (int offset in new[] { 0x20, 0x22, 0x28, 0x2A })
        {
            temporary = Word(temporary, offset, 256);
        }
        int end = checked(geometry + Int(source.Data, geometry));
        int[] sizes = [12, 12, 20, 24, 20, 24, 28, 36];
        for (int group = 0; group < 8; group++)
        {
            int faces = Int(source.Data, end);
            if (faces < 0)
            {
                throw new InvalidDataException("Psycho Blast shape contains a negative group count.");
            }
            end = checked(end + 4 + faces * sizes[group]);
        }
        ImmutableArray<byte> mesh = Slice(source.Data, geometry, checked(end - geometry)).ToArray().ToImmutableArray();
        NativeBattleEffectArena currentMorph = morph;
        NativeBattleActorWaveMeshStep drawn = NativeBattleActorWaveMesh.EmitBatch(mesh, shapeAddress, matrix, projection, temporary, registers, farControls, dqa, dqb, zsf4, constants, null, zsf3, selectedVerticesAddress: selectedAddress, readSelectedVertex: ReadVertex);
        return new(drawn.Batch, drawn.Context104, morph, drawn.Registers, drawn.FarControls);

        (NativeBattleVector Vertex, short? W) ReadVertex(int offset)
        {
            if (offset < 0 || offset > count * 8 - 8)
            {
                throw new InvalidDataException("Psycho Blast mesh references a vertex outside its selected shape.");
            }
            if (!interpolate)
            {
                int at = checked(selected + offset);
                return (Vector(source.Data, at), BinaryPrimitives.ReadInt16LittleEndian(Slice(source.Data, at + 6, 2)));
            }
            byte[] xyz = currentMorph.Read(offset, 6);
            (byte[] padding, bool[] known) = currentMorph.ReadSnapshot(offset + 6, 2);
            return (new(BinaryPrimitives.ReadInt16LittleEndian(xyz), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(2)), BinaryPrimitives.ReadInt16LittleEndian(xyz.AsSpan(4))), known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(padding) : null);
        }
    }

    private static int Int(ImmutableArray<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(Slice(bytes, offset, 4));
    private static NativeBattleVector Vector(ImmutableArray<byte> bytes, int offset) => new(BinaryPrimitives.ReadInt16LittleEndian(Slice(bytes, offset, 2)), BinaryPrimitives.ReadInt16LittleEndian(Slice(bytes, offset + 2, 2)), BinaryPrimitives.ReadInt16LittleEndian(Slice(bytes, offset + 4, 2)));
    private static ReadOnlySpan<byte> Slice(ImmutableArray<byte> bytes, int offset, int length)
    {
        if (bytes.IsDefault || offset < 0 || length < 0 || offset > bytes.Length - length)
        {
            throw new InvalidDataException("Psycho Blast rendering read leaves its original ACT source.");
        }
        return bytes.AsSpan().Slice(offset, length);
    }
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int offset, ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return arena.Write(offset, bytes);
    }
}
