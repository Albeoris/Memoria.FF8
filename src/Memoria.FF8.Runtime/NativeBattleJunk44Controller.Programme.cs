using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleJunk44Controller
{
    public static (NativeBattleJunk44Frame Frame, int Result) AdvanceProgramme6E8810(NativeBattleJunk44Frame frame, uint address, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 0x12) <= 0)
        {
            NativeBattleActionMatrix local = Rotation6E8A70(Signed(task, 0x14)) with { X = Signed(task, 0x0C), Y = Signed(task, 0x0E), Z = Signed(task, 0x10) };
            NativeBattlePackedMatrix32 caller = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local), 0);
            NativeBattleGfAnimationSource source = resources.ReadProgramme(0x1354CE0);
            NativeBattleGfAnimationWorkspace workspace = NativeBattleEffectTaskMemory.BorrowAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x18, 64);
            NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
            {
                NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x18, current);
                frame = DrawProgrammeNode6E88B0(frame, source, node, caller, resources, inputs);
                return new(TaskView(Read(frame, address), 0x18, 64).Read(0, 64).ToImmutableArray());
            });
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x18, advanced.Workspace);
            if (advanced.Remaining == 0)
            {
                return (frame, 2);
            }
        }
        task = Read(frame, address);
        task = task.WriteWord(0x14, unchecked((ushort)(task.ReadWord(0x14) + task.ReadWord(0x16)))).WriteWord(0x12, unchecked((ushort)(task.ReadWord(0x12) - 1)));
        return (Write(frame, address, task), 0);
    }

    public static NativeBattleJunk44Frame DrawProgrammeNode6E88B0(NativeBattleJunk44Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 caller, NativeBattleJunk44Resources resources, NativeBattleJunk44Inputs inputs)
    {
        NativeBattleVector scale = node.Vector24;
        if (scale is { X: 0, Y: 0, Z: 0 } || node.Scalar36 >= 4096 && node.Color32 == 0)
        {
            return frame;
        }
        int geometryOffset = BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
        uint geometryAddress = checked(source.Address + (uint)geometryOffset);
        NativeBattleStaticMeshResource mesh = resources.ReadGeometry(geometryAddress);
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        int shapes = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(8));
        int first = node.Word40;
        int second = node.Word42;
        int ratio = node.Scalar38;
        if (first < 0 || second < 0 || first >= shapes || second >= shapes || count != 9)
        {
            throw new InvalidDataException("Junk programme shape leaves its actual nine-vertex geometry.");
        }
        (frame, uint temporary) = Allocate(frame, 88);
        uint vertices;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int selected = ratio == 4096 && first != second ? second : first;
            vertices = checked(geometryAddress + 12 + (uint)(selected * count * 8));
        }
        else
        {
            frame = MorphProgramme44(frame, mesh, first, second, ratio);
            vertices = checked(frame.State.ReadDword(0x2545180) + 0x212D4);
        }
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(node.Vector16) with { X = node.Vector8.X, Y = node.Vector8.Y, Z = node.Vector8.Z };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(caller.ReadMatrix(), local);
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(0x1C, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2000u : 0x20C0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        frame = inputs.Execute(PublishContext(frame, temporary, context, 88), new NativeBattleJunk44DrawStatic88(temporary, geometryAddress));
        return Release(frame, 88);
    }

    public static NativeBattleActionMatrix Rotation6E8A70(short angle)
    {
        int signed = -(int)angle;
        short sine = unchecked((short)NativeBattleCameraMath.Sine(signed));
        short cosine = unchecked((short)NativeBattleCameraMath.Cosine(signed));
        return new([cosine, 0, unchecked((short)-sine), 0, 4096, 0, sine, 0, cosine], 0, 0, 0, 4096);
    }

    private static NativeBattleJunk44Frame MorphProgramme44(NativeBattleJunk44Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        uint destination = checked(frame.State.ReadDword(0x2545180) + 0x212D4);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Junk morph requires its current geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Junk morph requires its current arithmetic owner."));
        for (int vertex = 0; vertex < count; vertex++)
        {
            int a = 12 + (first * count + vertex) * 8;
            int b = 12 + (second * count + vertex) * 8;
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)(4096 - ratio)) } }, ShapeWord(a), ShapeWord(a + 2), ShapeWord(a + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, NativeBattleGteColorInterpolation.CodeConstants45F270);
            registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = unchecked((uint)ratio) } }, ShapeWord(b), ShapeWord(b + 2), ShapeWord(b + 4));
            registers = NativeBattleRaldo19BallRendering.Weighted(registers, true, NativeBattleGteColorInterpolation.CodeConstants45F270);
            NativeBattleEffectArena result = NativeBattleEffectArena.Clear();
            for (int axis = 0; axis < 3; axis++)
            {
                result = result.Write(axis * 2, BitConverter.GetBytes(unchecked((short)registers.Colors.Ir[axis]!.Value)));
            }
            frame = WriteBytes(frame, checked(destination + (uint)(vertex * 8)), result, 6);
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

        ushort ShapeWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset));
    }
}
