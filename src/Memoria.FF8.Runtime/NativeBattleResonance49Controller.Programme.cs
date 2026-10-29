using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleResonance49Controller
{
    public static NativeBattleResonance49Frame InitializeProgramme(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 0x24) < 20)
        {
            return frame;
        }
        NativeBattleGfAnimationSource source = resources.ReadProgramme(task.ReadDword(0x74));
        NativeBattleGfAnimationWorkspace workspace = ProgrammeWorkspace(task, false).Initialize7016B0(source, unchecked((int)task.ReadDword(0x78)));
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, workspace);
        NativeBattleResonance49PointStep bone = inputs.ReadBone502170(frame, task.ReadByte(0x2C), 0xF0, 4096);
        frame = WriteBytes(bone.Frame, address + 0x1C, bone.Point8, 6);
        NativeBattleGraphicsModel model = inputs.ReadModel(frame, task.ReadByte(0x2C));
        short yaw = model.Rotation14 ?? throw new NotSupportedException("Resonance programme requires its current model yaw.");
        task = Read(frame, address).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, 0xFF80).WriteWord(0x62, unchecked((ushort)yaw));
        frame = Write(frame, address, task);
        (frame, _) = DrawProgramme(frame, address, resources, inputs);
        task = Read(frame, address);
        return Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    public static (NativeBattleResonance49Frame Frame, int Remaining) DrawProgramme(NativeBattleResonance49Frame frame, uint address, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        foreach ((int offset, char axis) in new[] { (0x62, 'Y'), (0x60, 'X'), (0x64, 'Z') })
        {
            short angle = Signed(task, offset);
            if (angle != 0)
            {
                (frame, matrix) = RotateProgramme49(frame, matrix, axis, angle);
            }
        }
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58)));
        matrix = matrix with { X = Signed(task, 0x1C), Y = Signed(task, 0x1E), Z = Signed(task, 0x20) };
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix), null);
        NativeBattleGfAnimationSource source = resources.ReadProgramme(task.ReadDword(0x74));
        NativeBattleGfAnimationAdvance advanced = ProgrammeWorkspace(task).Advance701970(source, 0, (current, node) =>
        {
            NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, current);
            frame = DrawProgrammeNode49(frame, source, node, composed, Signed(Read(frame, address), 0x88), resources, inputs, NativeBattlePilotVisualLifetimes.Identity(49, Read(frame, address)));
            return ProgrammeWorkspace(Read(frame, address));
        });
        NativeBattleEffectTaskMemory.PublishAnimationWorkspace(frame.Memory, frame.Effect.Pools, address + 0x94, advanced.Workspace);
        return (frame, advanced.Remaining);
    }

    public static NativeBattleResonance49Frame DrawProgrammeNode49(NativeBattleResonance49Frame frame, NativeBattleGfAnimationSource source, NativeBattleGfAnimationNode node, NativeBattlePackedMatrix32 caller, int depthBias, NativeBattleResonance49Resources resources, NativeBattleResonance49Inputs inputs, NativeBattlePilotVisualIdentity? pilotIdentity = null)
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
            throw new InvalidDataException("Resonance programme shape leaves its original geometry.");
        }
        (frame, uint temporary) = Allocate(frame, 104);
        uint vertices;
        if (first == second || ratio == 0 || ratio == 4096)
        {
            int selected = ratio == 4096 && first != second ? second : first;
            vertices = checked(geometryAddress + 12 + (uint)(selected * count * 8));
        }
        else
        {
            frame = MorphProgramme49(frame, mesh, first, second, ratio);
            vertices = 0x2647D20;
        }
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(node.Vector16);
        frame = LoadMatrix(frame, caller, true, false);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters!, frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Resonance node transform requires its arithmetic owner."));
        registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, node.Vector8, null), false);
        NativeBattleActionMatrix basis = caller.ReadMatrix();
        if ((node.Header & 0x8000) == 0)
        {
            rotation = NativeBattleGfMatrices.Multiply56C090(basis, rotation);
        }
        rotation = rotation with { X = unchecked(basis.X + registers.Colors.Mac[0]!.Value), Y = unchecked(basis.Y + registers.Colors.Mac[1]!.Value), Z = unchecked(basis.Z + registers.Colors.Mac[2]!.Value) };
        if (scale is not { X: 4096, Y: 4096, Z: 4096 })
        {
            rotation = NativeBattlePoseMatrices.Scale56BEF0(rotation, scale.X, scale.Y, scale.Z);
        }
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        NativeBattleEffectArena context = Context(frame, temporary, 104).Write(0, BitConverter.GetBytes(geometryAddress)).Write(4, BitConverter.GetBytes(vertices)).Write(12, BitConverter.GetBytes((int)node.Scalar36)).Write(16, BitConverter.GetBytes(depthBias)).Write(20, BitConverter.GetBytes(node.Scalar36 == 0 ? 0x2030u : 0x20F0u));
        if (node.Scalar36 != 0)
        {
            context = context.Write(8, BitConverter.GetBytes(node.Color32));
        }
        for (int offset = 0x18; offset <= 0x2A; offset += 2)
        {
            context = context.Write(offset, BitConverter.GetBytes((ushort)(offset is 0x20 or 0x22 or 0x28 or 0x2A ? 256 : 0)));
        }
        frame = PublishContext(frame, temporary, context, 104);
        frame = inputs.Execute(frame, new NativeBattleResonance49DrawMesh104(temporary, geometryAddress, vertices) { PilotIdentity = pilotIdentity is { } identity ? identity with { Primitive = unchecked(identity.Primitive * 397 + node.ResourceOffset), ResourceGeneration = geometryAddress } : null });
        return Release(frame, 104);
    }

    private static NativeBattleResonance49Frame MorphProgramme49(NativeBattleResonance49Frame frame, NativeBattleStaticMeshResource mesh, int first, int second, int ratio)
    {
        int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4));
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Resonance morph requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Resonance morph requires its arithmetic owner."));
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
            frame = frame with { State = frame.State.WriteView(checked(0x2647D20 + (uint)(vertex * 8)), result, 6) };
        }
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };

        ushort ShapeWord(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(mesh.Bytes.AsSpan().Slice(offset));
    }

    private static NativeBattleGfAnimationWorkspace ProgrammeWorkspace(NativeBattleModulePoolTask task, bool requireKnown = true)
    {
        if (requireKnown)
        {
            task.RequireKnownPayload(0x94, checked((int)task.ReadDword(0x78)));
        }
        return NativeBattleGfAnimationWorkspace.Borrow(task, 0x94, checked((int)task.ReadDword(0x78)));
    }

    private static (NativeBattleResonance49Frame Frame, NativeBattleActionMatrix Matrix) RotateProgramme49(NativeBattleResonance49Frame frame, NativeBattleActionMatrix matrix, char axis, short angle)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix operand = axis switch
        {
            'X' => NativeBattleCuragaMatrices.RotateX8DD7E0(identity, angle),
            'Y' => NativeBattleCuragaMatrices.RotateY8DD8A0(identity, angle),
            'Z' => NativeBattleCuragaMatrices.RotateZ8DD960(identity, angle),
            _ => throw new InvalidDataException("Resonance matrix axis is unsupported.")
        };
        (frame, uint temporary) = Allocate(frame, 36);
        NativeBattleEffectArena context = Context(frame, temporary, 36);
        for (int index = 0; index < 9; index++)
        {
            context = context.Write(index * 2, BitConverter.GetBytes(operand.Rotation[index]));
        }
        context = context.Write(32, BitConverter.GetBytes(unchecked((short)NativeBattleCameraMath.Sine(angle)))).Write(34, BitConverter.GetBytes(unchecked((short)NativeBattleCuragaMatrices.Cosine8DDRotation(angle))));
        frame = PublishContext(frame, temporary, context, 36);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Resonance rotation requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Resonance rotation requires its arithmetic owner."));
        registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, matrix, true, false, null) };
        for (int column = 0; column < 3; column++)
        {
            registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)operand.Rotation[column]), unchecked((ushort)operand.Rotation[column + 3]), unchecked((ushort)operand.Rotation[column + 6]));
            registers = NativeBattleGeometry.Transform(registers, false, true);
        }
        frame = Release(frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } }, 36);
        return (frame, NativeBattlePoseMatrices.MultiplyGteRotation45E470(matrix, operand));
    }
}
