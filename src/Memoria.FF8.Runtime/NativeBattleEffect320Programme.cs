using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect320Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect320Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect320Programme
{
    public static NativeBattleEffect320Frame Initialize(NativeBattleEffect320Frame frame, uint workspaceAddress, uint sourceAddress, int clearBytes, NativeBattleEffect320Resources resources)
    {
        NativeBattleGfAnimationWorkspace workspace = new(ReadBytes(frame, workspaceAddress, clearBytes).ReadSnapshot(0, clearBytes).Data.ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.ReadProgramme(sourceAddress), clearBytes);
        return WriteBytes(frame, workspaceAddress, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), clearBytes);
    }

    public static NativeBattleEffect320Step Advance(NativeBattleEffect320Frame frame, uint workspaceAddress, NativeBattleEffectArena caller48, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        uint resourceAddress = Dword(ReadBytes(frame, workspaceAddress, 4), 0);
        int workspaceLength = resourceAddress switch
        {
            0xFE761C => 0x1F0,
            0xFE9294 => 0x110,
            0xFE9BCC => 0x414,
            _ => throw new NotSupportedException("Effect320 requires its selected programme workspace.")
        };
        NativeBattleGfAnimationWorkspace Workspace() => new(ReadBytes(frame, workspaceAddress, workspaceLength).Read(0, workspaceLength).ToImmutableArray());
        NativeBattleGfAnimationSource source = resources.ReadProgramme(Workspace().ResourceAddress);
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (workspace, node) =>
        {
            frame = WriteBytes(frame, workspaceAddress, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), workspaceLength);
            frame = DrawNode65C430(frame, source, node, caller48, resources, inputs);
            return Workspace();
        });
        frame = WriteBytes(frame, workspaceAddress, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan()), workspaceLength);
        return new(frame, advanced.Remaining);
    }

    public static NativeBattleEffect320Frame DrawNode65C430(NativeBattleEffect320Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller48, NativeBattleEffect320Resources resources, NativeBattleEffect320Inputs inputs)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        (frame, uint temporary) = Allocate(frame, 108);
        uint geometry = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometry);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("Effect320 ACT shape leaves its selected geometry.");
        }
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40;
        uint vertices = morph ? Dword(caller48, 0x2C) : checked(geometry + 12u + (uint)(selected * count * 8));
        if (morph)
        {
            frame = Morph701390(frame, source, geometry, node.Word40, node.Word42, node.Scalar38, vertices, inputs);
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        (byte[] matrixBytes, bool[] matrixKnown) = caller48.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 callerMatrix = new(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray());
        NativeBattleActionMatrix parent = callerMatrix.ReadMatrix();
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if ((node.Header & 0x200) == 0)
        {
            frame = LoadMatrix(frame, callerMatrix, true, false);
            NativeBattleGeometryRegisterStep translated = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, node.Vector8, BinaryPrimitives.ReadInt16LittleEndian(node.Data.AsSpan().Slice(0xE))), false);
            frame = Publish(frame, translated);
            x = translated.Colors.Mac[0]!.Value;
            y = translated.Colors.Mac[1]!.Value;
            z = translated.Colors.Mac[2]!.Value;
            rotation = NativeBattleGfMatrices.Multiply56C090(parent, rotation);
        }
        rotation = rotation with { X = unchecked(x + parent.X), Y = unchecked(y + parent.Y), Z = unchecked(z + parent.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Context(frame, temporary, 108).Write(0, BitConverter.GetBytes(geometry)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u)).Write(24, BitConverter.GetBytes((int)Word(caller48, 0x28)));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        foreach (int offset in new[] { 0x20, 0x22, 0x24, 0x26 })
        {
            context = context.Write(offset, BitConverter.GetBytes((ushort)0));
        }
        context = context.Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256));
        byte selector = resources.ReadStatic(Dword(caller48, 0x20) + (uint)node.GeometryIndex, 1)[0];
        if (selector is (byte)'1' or (byte)'2')
        {
            context = context.Write(0x2C, BitConverter.GetBytes((ushort)0)).Write(0x2E, BitConverter.GetBytes((ushort)(selector == (byte)'2' ? 128 : 0))).Write(0x30, BitConverter.GetBytes((ushort)256)).Write(0x32, BitConverter.GetBytes((ushort)128)).Write(0x22, BitConverter.GetBytes((ushort)(caller48.Read(0x2A, 1)[0] & 127)));
        }
        frame = PublishContext(frame, temporary, context, 108);
        if (resources.ReadStatic(Dword(caller48, 0x24) + (uint)node.GeometryIndex, 1)[0] != (byte)'0')
        {
            frame = NativeBattleEffect320Lower.Draw65C750(frame, temporary, resources, inputs);
        }
        else
        {
            NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, count, offset =>
            {
                NativeBattleEffectArena row = ReadRow(offset);
                var observed = row.ReadSnapshot(0, 8);
                return (Vector(row), observed.Known[6] && observed.Known[7] ? Word(row, 6) : (short?)null);
            }, readSelectedPackedVertex: ReadRow);
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, 108);
            frame = inputs.Execute(frame, new NativeBattleEffect320DrawBatch(drawn.Batch, CursorGlobal));
        }
        return Release(frame, 108);

        NativeBattleEffectArena ReadRow(int offset)
        {
            return morph ? ReadBytes(frame, vertices + (uint)offset, 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
        }
    }

    public static NativeBattleEffect320Frame Morph701390(NativeBattleEffect320Frame frame, NativeBattleGfAnimationSource source, uint geometry, int first, int second, int factor, uint destination, NativeBattleEffect320Inputs inputs)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked((int)(geometry - source.Address)) + 4));
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        for (int index = 0; index < count; index++)
        {
            for (int part = 0; part < 2; part++)
            {
                int shape = part == 0 ? first : second;
                int offset = checked((int)(geometry - source.Address) + 12 + (shape * count + index) * 8);
                NativeBattleVector vector = Vector(NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(offset, 8)));
                registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(part == 0 ? 4096 - factor : factor)) } }, unchecked((ushort)vector.X), unchecked((ushort)vector.Y), unchecked((ushort)vector.Z));
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, part != 0, inputs.Colors);
            }
            NativeBattleEffectArena row = ReadBytes(frame, destination + (uint)(index * 8), 8);
            row = PutPoint(row, 0, new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value)));
            frame = WriteBytes(frame, destination + (uint)(index * 8), row, 6);
        }
        return Publish(frame, registers);
    }

    private static uint Dword(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
    private static short Word(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
}
