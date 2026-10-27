using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect207Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect207Programme
{
    public static NativeBattleEffect207Frame Initialize(NativeBattleEffect207Frame frame, uint workspaceAddress, uint sourceAddress, int clearBytes, NativeBattleEffect207Resources resources)
    {
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, clearBytes);
        workspace = workspace.Initialize7016B0(resources.ReadProgramme(sourceAddress), clearBytes);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, workspace);
        return frame;
    }

    public static NativeBattleEffect207Step Advance(NativeBattleEffect207Frame frame, uint workspaceAddress, NativeBattleEffectArena caller20, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, ProgrammeWorkspaceLength);
        NativeBattleGfAnimationSource source = resources.ReadProgramme(Workspace().ResourceAddress);
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (workspace, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, workspace);
            frame = DrawNode41E0(frame, source, node, caller20, resources, inputs);
            return Workspace();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, advanced.Workspace);
        return new(frame, advanced.Remaining);
    }

    public static NativeBattleEffect207Frame DrawNode41E0(NativeBattleEffect207Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller20, NativeBattleEffect207Resources resources, NativeBattleEffect207Inputs inputs)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        if ((node.Header & 0x1200) != 0x200)
        {
            throw new NotSupportedException("Effect207 requires its selected view-relative ACT translation.");
        }
        (frame, uint temporary) = Allocate(frame, 108);
        uint geometry = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometry);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("Effect207 ACT shape leaves its selected geometry.");
        }
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        int selected = node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40;
        uint vertices = morph ? Dword(caller20, 16) : checked(geometry + 12u + (uint)(selected * count * 8));
        if (morph)
        {
            frame = Morph701390(frame, source, geometry, node.Word40, node.Word42, node.Scalar38, vertices, inputs);
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        frame = LoadMatrix(frame, view, true, false);
        NativeBattleGeometryRegisterStep translated = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(Registers(frame), 0, Vector(caller20), Word(caller20, 6)), false);
        frame = Publish(frame, translated);
        NativeBattleActionMatrix currentView = view.ReadMatrix();
        rotation = rotation with { X = unchecked(translated.Colors.Mac[0]!.Value + node.Vector8.X + currentView.X), Y = unchecked(translated.Colors.Mac[1]!.Value + node.Vector8.Y + currentView.Y), Z = unchecked(translated.Colors.Mac[2]!.Value + node.Vector8.Z + currentView.Z) };
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = (node.Header & 0x100) != 0 ? rotation with { Rotation = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096)).Rotation } : NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Context(frame, temporary, 108).Write(0, BitConverter.GetBytes(geometry)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u)).Write(24, BitConverter.GetBytes((int)Word(caller20, 12)));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        foreach (int offset in new[] { 0x20, 0x22, 0x24, 0x26 })
        {
            context = context.Write(offset, BitConverter.GetBytes((ushort)0));
        }
        context = context.Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256));
        byte selector = resources.ReadStatic(Dword(caller20, 8) + (uint)node.GeometryIndex, 1)[0];
        if (selector == (byte)'1')
        {
            context = context.Write(0x2C, BitConverter.GetBytes((ushort)0)).Write(0x2E, BitConverter.GetBytes((ushort)0)).Write(0x30, BitConverter.GetBytes((ushort)256)).Write(0x32, BitConverter.GetBytes((ushort)128)).Write(0x22, BitConverter.GetBytes((ushort)(caller20.Read(14, 1)[0] & 127)));
        }
        frame = PublishContext(frame, temporary, context, 108);
        if (selector == (byte)'9')
        {
            frame = NativeBattleEffect207Lower.Draw45E0(frame, temporary, resources, inputs);
        }
        else
        {
            NativeBattleWindowedMeshContext108Step drawn = NativeBattleWindowedMeshContext108.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, 128, offset =>
            {
                NativeBattleEffectArena row = morph ? ReadBytes(frame, vertices + (uint)offset, 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
                var observed = row.ReadSnapshot(0, 8);
                return (Vector(row), observed.Known[6] && observed.Known[7] ? Word(row, 6) : (short?)null);
            });
            frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context108, 108);
            frame = inputs.Execute(frame, new NativeBattleEffect207DrawBatch(drawn.Batch, CursorGlobal));
        }
        return Release(frame, 108);
    }

    public static NativeBattleEffect207Frame Morph701390(NativeBattleEffect207Frame frame, NativeBattleGfAnimationSource source, uint geometry, int first, int second, int factor, uint destination, NativeBattleEffect207Inputs inputs)
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
