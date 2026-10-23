using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect298Controller
{
    public static (NativeBattleEffect298Frame Frame, uint Next) BirthProgramme671990(NativeBattleEffect298Frame frame, uint script, uint model, uint offsets, bool forceYZero, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        short[] words = Enumerable.Range(0, 6).Select(index => ScriptWord298(frame, resources, script + (uint)(index * 2))).ToArray();
        (frame, uint address) = Append(frame, 0x25078B0, 0x671AB0);
        if (address == 0)
        {
            return (frame, script + 12);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect298ApplyOffsetPose662C00(model, offsets));
        var bone = inputs.ReadBone502170(frame, model, words[0], words[1]);
        frame = bone.Frame;
        NativeBattleEffectArena extra = ReadBytes(frame, checked(offsets + (uint)(words[0] * 16)), 6);
        NativeBattleModulePoolTask task = CopyTask(Read(frame, address), 12, bone.Point8, 6);
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + BinaryPrimitives.ReadInt16LittleEndian(extra.Read(0, 2))))).WriteWord(14, forceYZero ? (ushort)0 : unchecked((ushort)(task.ReadWord(14) + BinaryPrimitives.ReadInt16LittleEndian(extra.Read(2, 2))))).WriteWord(16, unchecked((ushort)(task.ReadWord(16) + BinaryPrimitives.ReadInt16LittleEndian(extra.Read(4, 2)))));
        task = task.WriteWord(18, unchecked((ushort)words[3])).WriteWord(20, unchecked((ushort)words[4])).WriteWord(22, unchecked((ushort)words[5]));
        frame = Write(frame, address, task);
        frame = inputs.Execute(frame, new NativeBattleEffect298RestorePose508C90(model));
        if ((uint)words[2] >= 4)
        {
            throw new InvalidDataException("Effect298 programme selector leaves its original four-entry table.");
        }
        uint sourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(resources.ProgrammeTable32.AsSpan().Slice(words[2] * 8, 4));
        int length = BinaryPrimitives.ReadInt32LittleEndian(resources.ProgrammeTable32.AsSpan().Slice(words[2] * 8 + 4, 4));
        var workspace = new NativeBattleGfAnimationWorkspace(TaskView(Read(frame, address), 24, length).ReadSnapshot(0, length).Data.ToImmutableArray()).Initialize7016B0(resources.ReadProgramme(sourceAddress), length);
        frame = WriteBytes(frame, address + 24, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan()), length);
        return (frame, script + 12);
    }

    public static (NativeBattleEffect298Frame Frame, int Result) AdvanceProgramme671AB0(NativeBattleEffect298Frame frame, uint address, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix local = NativeBattlePandemona291Restoration.Rotation701270(Signed(task, 20)) with { X = Signed(task, 12), Y = Signed(task, 14), Z = Signed(task, 16) };
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), local);
        int scale = Signed(task, 18);
        NativeBattleEffectArena caller = PackedArena(NativeBattlePackedMatrix32.FromMatrix(matrix, 0)).Write(32, BitConverter.GetBytes(scale)).Write(36, BitConverter.GetBytes(scale)).Write(40, BitConverter.GetBytes(scale)).Write(44, BitConverter.GetBytes(1)).Write(48, BitConverter.GetBytes((int)Signed(task, 22))).Write(52, BitConverter.GetBytes(NativeBattleEffect298Profile.ArenaAddress + 0x261D0));
        uint sourceAddress = BinaryPrimitives.ReadUInt32LittleEndian(TaskView(task, 24, 4).Read(0, 4));
        NativeBattleGfAnimationSource source = resources.ReadProgramme(sourceAddress);
        int length = sourceAddress switch { 0x104ED34 => 80, 0x104F7C0 => 304, 0x1050A60 or 0x105166C => 272, _ => throw new NotSupportedException("Effect298 programme task has no selected source.") };
        NativeBattleGfAnimationWorkspace Workspace()
        {
            return new(ReadBytes(frame, address + 24, length).Read(0, length).ToImmutableArray());
        }
        NativeBattleGfAnimationAdvance advanced = Workspace().Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, address + 24, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan()), length);
            frame = DrawProgrammeNode671B50(frame, source, node, caller, resources, inputs);
            return Workspace();
        });
        frame = WriteBytes(frame, address + 24, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan()), length);
        return (frame, advanced.Remaining == 0 ? 2 : 0);
    }

    private static short ScriptWord298(NativeBattleEffect298Frame frame, NativeBattleEffect298Resources resources, uint address) => BinaryPrimitives.ReadInt16LittleEndian(frame.State.ReadStatic(resources, address, 2));

    public static NativeBattleEffect298Frame DrawProgrammeNode671B50(NativeBattleEffect298Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattleEffectArena caller56, NativeBattleEffect298Resources resources, NativeBattleEffect298Inputs inputs)
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
            throw new InvalidDataException("Effect298 programme shape leaves its original geometry.");
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
            frame = MorphProgramme298(frame, mesh, first, second, ratio, BinaryPrimitives.ReadUInt32LittleEndian(caller56.Read(52, 4)));
            vertices = BinaryPrimitives.ReadUInt32LittleEndian(caller56.Read(52, 4));
        }
        NativeBattleActionMatrix rotation = (node.Header & 0x40000) != 0 ? NativeBattleGfMatrices.Euler7015B0(node.Vector16) : NativeBattleGfMatrices.Euler701310(node.Vector16);
        (byte[] callerData, bool[] callerKnown) = caller56.ReadSnapshot(0, 32);
        NativeBattlePackedMatrix32 caller = new(callerData.ToImmutableArray(), callerKnown.ToImmutableArray());
        NativeBattleActionMatrix callerMatrix = caller.ReadMatrix();
        int x = node.Vector8.X;
        int y = node.Vector8.Y;
        int z = node.Vector8.Z;
        if (BinaryPrimitives.ReadInt32LittleEndian(caller56.Read(44, 4)) != 0)
        {
            int sx = BinaryPrimitives.ReadInt32LittleEndian(caller56.Read(32, 4));
            int sy = BinaryPrimitives.ReadInt32LittleEndian(caller56.Read(36, 4));
            int sz = BinaryPrimitives.ReadInt32LittleEndian(caller56.Read(40, 4));
            x = unchecked((short)(unchecked(x * sx) >> 12));
            y = unchecked((short)(unchecked(y * sy) >> 12));
            z = unchecked((short)(unchecked(z * sz) >> 12));
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, sx, sy, sz);
        }
        if ((node.Header & 0x200) == 0)
        {
            frame = LoadMatrix(frame, caller, true, false);
            NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect298 node requires its current arithmetic owner."));
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, new(unchecked((short)x), unchecked((short)y), unchecked((short)z)), null), false);
            x = registers.Colors.Mac[0]!.Value;
            y = registers.Colors.Mac[1]!.Value;
            z = registers.Colors.Mac[2]!.Value;
            frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
            rotation = NativeBattleGfMatrices.Multiply56C090(callerMatrix, rotation);
        }
        rotation = rotation with { X = unchecked(callerMatrix.X + x), Y = unchecked(callerMatrix.Y + y), Z = unchecked(callerMatrix.Z + z) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            if ((node.Header & 0x100) != 0)
            {
                NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(rotation, new([scale.X, 0, 0, 0, scale.Y, 0, 0, 0, scale.Z], 0, 0, 0, 4096));
                rotation = rotation with { Rotation = scaled.Rotation };
            }
            else
            {
                rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
            }
        }
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        int depthBias = BinaryPrimitives.ReadInt32LittleEndian(caller56.Read(48, 4));
        NativeBattleEffectArena context = Context(frame, temporary, 88).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(24, BitConverter.GetBytes(depthBias)).Write(28, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        frame = PublishContext(frame, temporary, context, 88);
        frame = inputs.Execute(frame, new NativeBattleEffect298DrawMesh7043B0(temporary, geometryAddress, vertices));
        return Release(frame, 88);
    }

    private static NativeBattleEffect298Frame MorphProgramme298(NativeBattleEffect298Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio, uint destination)
    {
        if (destination != NativeBattleEffect298Profile.ArenaAddress + 0x261D0)
        {
            throw new InvalidDataException("Effect298 morph requires its current programme scratch.");
        }
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        if (count < 1 || count * 8 > 512)
        {
            throw new InvalidDataException("Effect298 morph leaves its actual current scratch extent.");
        }
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Effect298 morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Effect298 morph requires its arithmetic owner."));
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
