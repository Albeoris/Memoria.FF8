using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect69Controller
{
    public static NativeBattleEffect69Frame AdvanceProgramme(NativeBattleEffect69Frame frame, uint address, bool initialize, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleGfAnimationWorkspace workspace = new(task.Payload.AsSpan(0x1C - 12, NativeBattleEffect69Profile.ProgrammeWorkspaceLength).ToArray().ToImmutableArray());
        if (initialize)
        {
            workspace = workspace.Initialize7016B0(resources.Programme, NativeBattleEffect69Profile.ProgrammeWorkspaceLength);
            frame = PublishProgramme69(frame, address, workspace);
        }
        NativeBattleModelTransform caster = inputs.ReadModel(frame, Caster(frame)).Transform ?? throw new NotSupportedException("Effect69 requires its actual current caster transform.");
        NativeBattleEffectArena caller = NativeBattleEffectArena.Clear().MarkUnknown(0, 20).Write(0, BitConverter.GetBytes(caster.Position28.X)).Write(2, BitConverter.GetBytes((short)0)).Write(4, BitConverter.GetBytes(caster.Position28.Z)).Write(8, BitConverter.GetBytes(NativeBattleEffect69Profile.SelectorsAddress)).Write(12, BitConverter.GetBytes((short)-128)).Write(14, BitConverter.GetBytes(unchecked((ushort)(task.ReadWord(0xC) << 4)))).Write(16, BitConverter.GetBytes(checked(frame.State.ReadDword(NativeBattleEffect69Profile.ArenaGlobal) + 0x6000)));
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Programme, 0, (current, node) =>
        {
            frame = PublishProgramme69(frame, address, current);
            frame = DrawProgrammeNode6FE9D0(frame, resources.Programme, node, caller, resources, inputs);
            return current;
        });
        return PublishProgramme69(frame, address, advanced.Workspace);
    }

    private static NativeBattleEffect69Frame PublishProgramme69(NativeBattleEffect69Frame frame, uint address, NativeBattleGfAnimationWorkspace workspace)
    {
        return Write(frame, address, CopyTask(Read(frame, address), 0x1C, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), NativeBattleEffect69Profile.ProgrammeWorkspaceLength));
    }

    private static NativeBattleVector Vector69(NativeBattleEffectArena point) => Vector(point);
    private static short? W69(NativeBattleEffectArena point)
    {
        (byte[] bytes, bool[] known) = point.ReadSnapshot(6, 2);
        return known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
    }

    public static NativeBattleEffect69Frame DrawProgrammeNode6FE9D0(NativeBattleEffect69Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller20, NativeBattleEffect69Resources resources, NativeBattleEffect69Inputs inputs)
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
        if (first < 0 || second < 0 || first >= shapes || second >= shapes)
        {
            throw new InvalidDataException("Effect69 programme shape leaves its original geometry.");
        }
        (frame, uint temporary) = Allocate(frame, 108);
        uint vertices;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int selected = ratio == 4096 && first != second ? second : first;
            vertices = checked(geometryAddress + 12 + (uint)(selected * count * 8));
        }
        else
        {
            frame = MorphProgramme69(frame, mesh, first, second, ratio, BinaryPrimitives.ReadUInt32LittleEndian(caller20.Read(16, 4)));
            vertices = BinaryPrimitives.ReadUInt32LittleEndian(caller20.Read(16, 4));
        }
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(node.Vector16);
        NativeBattleVector local = node.Vector8;
        NativeBattlePackedMatrix32 view = inputs.ReadView(frame);
        NativeBattleActionMatrix camera = view.ReadMatrix();
        NativeBattleVector anchor = Vector69(caller20);
        NativeBattlePackedMatrix32 basis = view;
        frame = LoadMatrix(frame, view, true, false);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        bool separate = (node.Header & 0x1000) != 0;
        NativeBattleVector point = separate ? anchor : new(unchecked((short)(anchor.X + local.X)), unchecked((short)(anchor.Y + local.Y)), unchecked((short)(anchor.Z + local.Z)));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, point, separate ? W69(caller20) : null), false);
        int x = registers.Colors.Mac[0]!.Value;
        int y = registers.Colors.Mac[1]!.Value;
        int z = registers.Colors.Mac[2]!.Value;
        frame = Publish(frame, registers);
        if (separate)
        {
            (byte[] bytes, bool[] known) = ReadBytes(frame, NativeBattleEffect69Profile.ViewAddress, 32).ReadSnapshot(0, 32);
            basis = new(bytes.ToImmutableArray(), known.ToImmutableArray());
            frame = LoadMatrix(frame, basis, true, false);
            registers = Registers(frame);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, local, null), false);
            x = unchecked(x + registers.Colors.Mac[0]!.Value);
            y = unchecked(y + registers.Colors.Mac[1]!.Value);
            z = unchecked(z + registers.Colors.Mac[2]!.Value);
            frame = Publish(frame, registers);
        }
        rotation = NativeBattleGfMatrices.Multiply56C090(basis.ReadMatrix(), rotation);
        rotation = rotation with { X = unchecked(camera.X + x), Y = unchecked(camera.Y + y), Z = unchecked(camera.Z + z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        int depthBias = BinaryPrimitives.ReadInt16LittleEndian(caller20.Read(12, 2));
        NativeBattleEffectArena context = Context(frame, temporary, 108).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, BitConverter.GetBytes(depthBias)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u)).Write(0x20, BitConverter.GetBytes((ushort)0));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        context = context.Write(0x22, BitConverter.GetBytes((ushort)0));
        byte selector = resources.Selectors[node.GeometryIndex];
        byte clock = caller20.Read(14, 1)[0];
        if (selector is (byte)'1' or (byte)'2')
        {
            ushort window = selector == (byte)'1' ? (ushort)((clock & 63) * 2) : (ushort)(clock & 127);
            context = context.Write(0x22, BitConverter.GetBytes(window)).Write(0x24, BitConverter.GetBytes((ushort)0)).Write(0x26, BitConverter.GetBytes((ushort)0)).Write(0x28, BitConverter.GetBytes((ushort)256)).Write(0x2A, BitConverter.GetBytes((ushort)256)).Write(0x2C, BitConverter.GetBytes((ushort)0)).Write(0x2E, BitConverter.GetBytes((ushort)128)).Write(0x30, BitConverter.GetBytes((ushort)256)).Write(0x32, BitConverter.GetBytes((ushort)128));
        }
        frame = PublishContext(frame, temporary, context, 108);
        frame = inputs.Execute(frame, new NativeBattleEffect69DrawWindow108(temporary, geometryAddress, vertices));
        return Release(frame, 108);
    }

    private static NativeBattleEffect69Frame MorphProgramme69(NativeBattleEffect69Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        if (destination != frame.State.ReadDword(NativeBattleEffect69Profile.ArenaGlobal) + 0x6000)
        {
            throw new InvalidDataException("Effect69 morph requires its current programme scratch.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (count < 1 || count * 8 > 0x3C0)
        {
            throw new InvalidDataException("Effect69 morph leaves its actual current scratch extent.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect69 morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect69 morph requires its arithmetic owner."));
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
