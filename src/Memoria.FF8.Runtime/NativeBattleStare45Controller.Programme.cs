using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleStare45Controller
{
    public static NativeBattleStare45Frame InitializeCasterProgramme836DC0(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleGfAnimationSource source = resources.ReadProgramme(task.ReadDword(0x74));
        NativeBattleGfAnimationWorkspace workspace = ProgrammeWorkspace(task, false).Initialize7016B0(source, unchecked((int)task.ReadDword(0x78)));
        frame = WriteBytes(frame, address + 0x94, NativeBattleEffectArena.Clear().Write(0, workspace.Data.AsSpan().Slice(0, checked((int)task.ReadDword(0x78)))), checked((int)task.ReadDword(0x78)));
        NativeBattleGraphicsModel model = inputs.ReadModel(frame, task.ReadByte(0x2C));
        short yaw = model.Rotation14 ?? throw new NotSupportedException("Stare programme requires its current model yaw.");
        frame = Write(frame, address, Read(frame, address).WriteWord(0x60, 0xF00).WriteWord(0x62, unchecked((ushort)yaw)));
        frame = ReadCasterPosition45(frame, address, inputs);
        task = Read(frame, address).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 4096).WriteWord(0x88, 0xFF80);
        frame = Write(frame, address, task);
        (frame, _) = DrawProgramme836EB0(frame, address, resources, inputs);
        task = Read(frame, address);
        return Write(frame, address, task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1))));
    }

    public static (NativeBattleStare45Frame Frame, int Remaining) AdvanceCasterProgramme8394C0(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        return DrawProgramme836EB0(ReadCasterPosition45(frame, address, inputs), address, resources, inputs);
    }

    private static NativeBattleStare45Frame ReadCasterPosition45(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleStare45PointStep bone = inputs.ReadBone502170(frame, task.ReadByte(0x2C), task.ReadWord(0x8A) == 0 ? 0x15 : 0x16, 0);
        frame = WriteBytes(bone.Frame, address + 0x1C, bone.Point8, 6);
        NativeBattleActionMatrix rotation;
        (frame, rotation) = RotateProgramme45(frame, NativeBattleCuragaMatrices.Identity8DD770(), 'Y', Signed(Read(frame, address), 0x62));
        NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, -64));
        task = Read(frame, address);
        task = task.WriteWord(0x1C, unchecked((ushort)(task.ReadWord(0x1C) + offset.X))).WriteWord(0x1E, unchecked((ushort)(task.ReadWord(0x1E) + offset.Y))).WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + offset.Z)));
        return Write(frame, address, task);
    }

    public static (NativeBattleStare45Frame Frame, int Remaining) DrawProgramme836EB0(NativeBattleStare45Frame frame, uint address, NativeBattleStare45Resources resources, NativeBattleStare45Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        foreach ((int offset, char axis) in new[] { (0x62, 'Y'), (0x60, 'X'), (0x64, 'Z') })
        {
            short angle = Signed(task, offset);
            if (angle != 0)
            {
                (frame, matrix) = RotateProgramme45(frame, matrix, axis, angle);
            }
        }
        matrix = NativeBattlePoseMatrices.Scale56BEF0(matrix, unchecked((int)task.ReadDword(0x50)), unchecked((int)task.ReadDword(0x54)), unchecked((int)task.ReadDword(0x58)));
        matrix = matrix with { X = Signed(task, 0x1C), Y = Signed(task, 0x1E), Z = Signed(task, 0x20) };
        NativeBattlePackedMatrix32 composed = NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), matrix), null);
        NativeBattleGfAnimationSource source = resources.ReadProgramme(task.ReadDword(0x74));
        NativeBattleGfAnimationAdvance advanced = ProgrammeWorkspace(task).Advance701970(source, 0, (current, node) =>
        {
            frame = WriteBytes(frame, address + 0x94, NativeBattleEffectArena.Clear().Write(0, current.Data.AsSpan().Slice(0, checked((int)task.ReadDword(0x78)))), checked((int)task.ReadDword(0x78)));
            frame = DrawProgrammeNode45(frame, source, node, composed, Signed(Read(frame, address), 0x88), resources, inputs);
            return ProgrammeWorkspace(Read(frame, address));
        });
        frame = WriteBytes(frame, address + 0x94, NativeBattleEffectArena.Clear().Write(0, advanced.Workspace.Data.AsSpan().Slice(0, checked((int)task.ReadDword(0x78)))), checked((int)task.ReadDword(0x78)));
        return (frame, advanced.Remaining);
    }

    private static NativeBattleGfAnimationWorkspace ProgrammeWorkspace(NativeBattleModulePoolTask task, bool requireKnown = true)
    {
        if (requireKnown)
        {
            task.RequireKnownPayload(0x94, checked((int)task.ReadDword(0x78)));
        }
        return new(task.ReadRawPayloadSpan().Slice(0x94 - 12).ToArray().ToImmutableArray());
    }

    private static (NativeBattleStare45Frame Frame, NativeBattleActionMatrix Matrix) RotateProgramme45(NativeBattleStare45Frame frame, NativeBattleActionMatrix matrix, char axis, short angle)
    {
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleActionMatrix operand = axis switch
        {
            'X' => NativeBattleCuragaMatrices.RotateX8DD7E0(identity, angle),
            'Y' => NativeBattleCuragaMatrices.RotateY8DD8A0(identity, angle),
            'Z' => NativeBattleCuragaMatrices.RotateZ8DD960(identity, angle),
            _ => throw new InvalidDataException("Stare matrix axis is unsupported.")
        };
        (frame, uint temporary) = Allocate(frame, 36);
        NativeBattleEffectArena context = Context(frame, temporary, 36);
        for (int index = 0; index < 9; index++)
        {
            context = context.Write(index * 2, BitConverter.GetBytes(operand.Rotation[index]));
        }
        context = context.Write(32, BitConverter.GetBytes(unchecked((short)NativeBattleCameraMath.Sine(angle)))).Write(34, BitConverter.GetBytes(unchecked((short)NativeBattleCuragaMatrices.Cosine8DDRotation(angle))));
        frame = PublishContext(frame, temporary, context, 36);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Stare rotation requires its geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Stare rotation requires its arithmetic owner."));
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
