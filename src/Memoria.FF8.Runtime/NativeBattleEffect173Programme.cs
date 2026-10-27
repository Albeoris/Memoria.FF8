using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect173Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect173Programme
{
    public static NativeBattleEffect173Frame Initialize8FB0(NativeBattleEffect173Frame frame, uint workspaceAddress, uint sourceAddress, int clearBytes, NativeBattleEffect173Resources resources)
    {
        NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, clearBytes);
        workspace = workspace.Initialize7016B0(resources.ReadProgramme(sourceAddress), clearBytes);
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, workspace);
        return frame;
    }

    public static NativeBattleEffect173Step Advance90B0(NativeBattleEffect173Frame frame, uint address, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleEffectArena caller = CopyBytes(TaskView(task, 16, 8), 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 40), 0, 8).Write(8, BitConverter.GetBytes(ProgrammeFlagsAddress)).Write(12, BitConverter.GetBytes(checked(frame.State.ReadDword(ArenaGlobal) + 0x1C700u))).Write(16, new byte[4]).Write(20, BitConverter.GetBytes(task.ReadWord(26))).Write(22, BitConverter.GetBytes((ushort)1)).Write(24, BitConverter.GetBytes((int)Signed(task, 24))).Write(28, BitConverter.GetBytes((int)Signed(task, 24))).Write(32, BitConverter.GetBytes((int)Signed(task, 24))).Write(36, BitConverter.GetBytes(1));
        NativeBattleEffect173Step advanced = Advance95B0(frame, address + ProgrammeWorkspaceOffset, caller, resources, inputs);
        return new(advanced.Frame, advanced.Result == 0 ? 2 : 0);
    }

    public static NativeBattleEffect173Step Advance95B0(NativeBattleEffect173Frame frame, uint workspaceAddress, NativeBattleEffectArena caller40, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace Workspace() => NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, ProgrammeWorkspaceLength);
        NativeBattleGfAnimationSource source = resources.ReadProgramme(Workspace().ResourceAddress);
        NativeBattleGfAnimationAdvance result = Workspace().Advance701970(source, (int)(inputs.ReadFlags(frame) & 1), (workspace, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, workspace);
            frame = DrawNode9130(frame, source, node, caller40, resources, inputs);
            return Workspace();
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, workspaceAddress, result.Workspace);
        return new(frame, result.Remaining);
    }

    public static NativeBattleEffect173Frame DrawNode9130(NativeBattleEffect173Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller40, NativeBattleEffect173Resources resources, NativeBattleEffect173Inputs inputs)
    {
        if (!NativeBattleActMeshRendering.Visible(node))
        {
            return frame;
        }
        (frame, uint temporary) = Allocate(frame, 88);
        uint geometry = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4)));
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometry);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        if (node.Word40 < 0 || node.Word42 < 0 || node.Word40 >= shapes || node.Word42 >= shapes)
        {
            throw new InvalidDataException("Effect173 ACT shape leaves the current geometry.");
        }
        bool morph = node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096);
        uint vertices = morph ? Dword(caller40, 12) : checked(geometry + 12 + (uint)((node.Word40 != node.Word42 && node.Scalar38 == 4096 ? node.Word42 : node.Word40) * count * 8));
        if (morph)
        {
            frame = Morph9510(frame, source, geometry, node.Word40, node.Word42, node.Scalar38, vertices, inputs);
        }
        NativeBattleVector angles = node.Vector16;
        if (Word(caller40, 22) != 0)
        {
            angles = new(unchecked((short)(angles.X + Word(caller40, 16))), unchecked((short)(angles.Y + Word(caller40, 18))), unchecked((short)(angles.Z + Word(caller40, 20))));
        }
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Euler701310(angles) with { X = node.Vector8.X + Word(caller40, 0), Y = node.Vector8.Y + Word(caller40, 2), Z = node.Vector8.Z + Word(caller40, 4) };
        uint sourceFlags = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(checked(Dword(caller40, 8) + (uint)(node.NodeIndex * 4)), 4));
        NativeBattleActionMatrix view = inputs.ReadView(frame).ReadMatrix();
        if ((sourceFlags & 0x10000000) != 0)
        {
            var translated = NativeBattleGfMatrices.RotateLong56C600(view, matrix.X, matrix.Y, matrix.Z);
            matrix = matrix with { X = unchecked(translated.X + view.X), Y = unchecked(translated.Y + view.Y), Z = unchecked(translated.Z + view.Z) };
        }
        else
        {
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(view, matrix);
            matrix = composed;
        }
        NativeBattleVector scale = node.Vector24;
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        if (Dword(caller40, 36) != 0)
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)Dword(caller40, 24)), unchecked((int)Dword(caller40, 28)), unchecked((int)Dword(caller40, 32)));
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        uint flags = (sourceFlags & 0xDFFF) | 0x2000;
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometry)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
            flags |= 0xC0;
        }
        context = context.Write(28, BitConverter.GetBytes(flags));
        NativeBattleStaticMeshContext88Step drawn = NativeBattleStaticMeshContext88.DrawLoaded(frame.Memory, mesh, inputs.ReadProjection(frame), context, inputs.Colors, 17, 2, vertices, offset =>
        {
            NativeBattleEffectArena row = morph ? ReadBytes(frame, vertices + (uint)offset, 8) : NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(checked((int)(vertices - source.Address)) + offset, 8));
            return (Vector(row), (short?)Word(row, 6));
        });
        frame = PublishContext(frame with { Memory = drawn.Memory }, temporary, drawn.Context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect173DrawBatch(drawn.Batch, CursorGlobal));
        return Release(frame, 88);
    }

    public static NativeBattleEffect173Frame Morph9510(NativeBattleEffect173Frame frame, NativeBattleGfAnimationSource source, uint geometry, int first, int second, int factor, uint destination, NativeBattleEffect173Inputs inputs)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(checked((int)(geometry - source.Address)) + 4));
        for (int index = 0; index < count; index++)
        {
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            for (int part = 0; part < 2; part++)
            {
                int shape = part == 0 ? first : second;
                int offset = checked((int)(geometry - source.Address) + 12 + (shape * count + index) * 8);
                NativeBattleVector vector = Vector(NativeBattleEffectArena.Clear().Write(0, source.Data.AsSpan().Slice(offset, 8)));
                registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(part == 0 ? 4096 - factor : factor)) } }, unchecked((ushort)vector.X), unchecked((ushort)vector.Y), unchecked((ushort)vector.Z));
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, part != 0, inputs.Colors);
            }
            frame = Publish(frame, registers);
            NativeBattleEffectArena row = ReadBytes(frame, destination + (uint)(index * 8), 8);
            row = PutPoint(row, 0, new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value)));
            frame = WriteBytes(frame, destination + (uint)(index * 8), row, 6);
        }
        return frame;
    }

    private static uint Dword(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
    private static short Word(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
}
