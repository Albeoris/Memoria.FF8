using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleOilShot136Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleOilShot136Controller
{
    public static (NativeBattleOilShot136Frame Frame, int Result) AdvanceMain(NativeBattleOilShot136Frame frame, uint address, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        int age = Signed(Read(frame, address), 12);
        if (age >= 30)
        {
            int depth = age < 34 ? (age - 30) * 1800 / 4 : age < 70 ? 1800 : (82 - age) * 1800 / 12;
            frame = inputs.Execute(frame, new NativeBattleOilShot136Stage(depth, 0xF0F0F0));
        }
        int phase = age - 30;
        if ((uint)phase < 8)
        {
            NativeBattleOilShot136PointStep mouth = inputs.ReadMouth(frame);
            frame = mouth.Frame;
            short yaw = inputs.ReadModelYaw(frame.State.ReadDword(CasterGlobal));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler701310(new(-512, yaw, 0));
            (frame, uint child) = Append(frame, 2, MovingSpriteCallback);
            if (child != 0)
            {
                frame = DrawRandom(frame, out ushort spin);
                frame = DrawRandom(frame, out ushort turn);
                NativeBattleModulePoolTask task = Vector(Read(frame, child).WriteWord(12, spin).WriteWord(14, unchecked((ushort)((turn & 511) - 256))), 16, mouth.Point);
                frame = DrawRandom(frame, out ushort scale);
                task = task.WriteWord(22, (ushort)((scale & 2047) + 2048));
                frame = DrawRandom(frame, out ushort directionYaw);
                frame = DrawRandom(frame, out ushort pitch);
                NativeBattleVector velocity = NativeBattleOilShot136Mesh.Direction6ADFA0((pitch & 1023) - 512, directionYaw);
                velocity = NativeBattleGfMatrices.RotateShort56C4F0(local, velocity with { Z = unchecked((short)-velocity.Z) });
                frame = DrawRandom(frame, out ushort strength);
                NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
                registers = registers with { Colors = registers.Colors with { Ir0 = (uint)((strength & 127) + 128) } };
                registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)velocity.X), unchecked((ushort)velocity.Y), unchecked((ushort)velocity.Z));
                registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
                frame = frame with { Memory = frame.Memory with { ScriptedColorRegisters = registers.Colors } };
                velocity = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
                frame = Write(frame, child, Vector(task, 24, velocity).WriteWord(30, 0));
            }
            local = local with { X = mouth.Point.X, Y = mouth.Point.Y, Z = mouth.Point.Z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local);
            frame = inputs.AdvanceAct(frame, address, resources.MainAct, 0x2C, 0xC8, ActContext(frame, NativeBattlePackedMatrix32.FromMatrix(composed, null), -128, 0)).Frame;
        }
        if ((uint)phase <= 12)
        {
            if (phase == 0)
            {
                NativeBattleOilShot136PointStep mouth = inputs.ReadMouth(frame);
                frame = mouth.Frame;
                NativeBattleModulePoolTask task = Vector(Read(frame, address), 0x14, mouth.Point);
                frame = Write(frame, address, CopyTask(task, 0x24, TaskView(task, 0x14, 8), 8));
            }
            else
            {
                frame = AdvanceArc(frame, address, phase, resources, inputs);
            }
        }
        int impactPhase = age - 38;
        if ((uint)impactPhase < 26)
        {
            NativeBattleModulePoolTask main = Read(frame, address);
            (frame, NativeBattlePackedMatrix32 matrix) = CameraPoint(frame, inputs, Vector(main, 28), Signed(main, 34));
            frame = inputs.AdvanceAct(frame, address, resources.SecondaryAct, 0xF4, 0x1F4, ActContext(frame, matrix, -128, 1)).Frame;
            if (impactPhase < 16 && (impactPhase & 1) == 0)
            {
                (frame, uint child) = Append(frame, 5, LargeParticleCallback);
                if (child != 0)
                {
                    NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 12, TaskView(Read(frame, address), 28, 8), 8).WriteWord(18, 0);
                    frame = DrawRandom(frame, out ushort angle);
                    frame = DrawRandom(frame, out ushort speed);
                    int strength = (speed & 31) + 32;
                    task = task.WriteWord(20, unchecked((ushort)(unchecked(NativeBattleCameraMath.Sine(angle) * strength) >> 12))).WriteWord(24, unchecked((ushort)(unchecked(NativeBattleCameraMath.Cosine(angle) * strength) >> 12)));
                    frame = DrawRandom(frame, out ushort vertical);
                    task = task.WriteWord(22, unchecked((ushort)(-64 - (vertical & 127))));
                    frame = Write(frame, child, InitializeWorkspace(task, 0x1C, 0x50, resources.LargeParticleAct));
                }
            }
            if (impactPhase >= 4 && impactPhase < 20)
            {
                (frame, uint child) = Append(frame, 4, SimpleSpriteCallback);
                if (child != 0)
                {
                    NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 12, TaskView(Read(frame, address), 28, 8), 8);
                    frame = DrawRandom(frame, out ushort x);
                    frame = DrawRandom(frame, out ushort y);
                    frame = DrawRandom(frame, out ushort z);
                    task = Vector(task, 12, new(unchecked((short)(Signed(task, 12) + (x & 511) - 256)), unchecked((short)(Signed(task, 14) + (y & 1023) - 512)), unchecked((short)(Signed(task, 16) + (z & 511) - 256))));
                    frame = Write(frame, child, task.WriteWord(18, 0));
                }
            }
        }
        if ((uint)(age - 44) < 38)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            NativeBattleVector point = Vector(task, 28) with { Y = unchecked((short)(Signed(task, 30) - 250)) };
            (frame, NativeBattlePackedMatrix32 matrix) = CameraPoint(frame, inputs, point, null);
            frame = inputs.AdvanceAct(frame, address, resources.ImpactAct, 0x2E8, 0x184, ActContext(frame, matrix, 0, 1)).Frame;
        }
        if (age == 30)
        {
            frame = inputs.Execute(frame, new NativeBattleOilShot136Sound(resources.Sound, 0, 128));
        }
        if (age == 62)
        {
            frame = inputs.Execute(frame, new NativeBattleOilShot136Contact(0));
        }
        if (age >= 82)
        {
            return (inputs.Execute(frame, new NativeBattleOilShot136Stage(0, 0)), 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleOilShot136Frame AdvanceArc(NativeBattleOilShot136Frame frame, uint address, int phase, NativeBattleOilShot136Resources resources, NativeBattleOilShot136Inputs inputs)
    {
        int parameter = phase * 4096 / 12;
        int sine = NativeBattleCameraMath.Sine(parameter >> 1);
        NativeBattleActionMatrix rotation = NativeBattleGfMatrices.Euler701310(new(0, 0, unchecked((short)((byte)phase << 8))));
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, null));
        frame = frame with { Memory = NativeBattleModuleRenderCommands.ClearTranslation704070(frame.Memory) };
        frame = NativeBattleOilShot136Mesh.Deform6ADFF0(frame, sine, resources);
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleWeightedPointsStep blended = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, Vector(task, 0x14), Vector(task, 0x1C), unchecked((short)(4096 - parameter)), unchecked((short)parameter), inputs.Colors);
        frame = frame with { Memory = blended.Memory };
        NativeBattleVector point = blended.Position with { Y = unchecked((short)(blended.Position.Y - (sine >> 3))) };
        NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(point.X - Signed(task, 0x24), Signed(task, 0x26) - point.Y, Signed(task, 0x28) - point.Z - 100).Matrix;
        (frame, uint child) = Append(frame, 3, EmitterCallback);
        task = Vector(Read(frame, address), 0x24, point).MarkPayloadUnknown(0x2A, 2);
        frame = Write(frame, address, task);
        if (child != 0)
        {
            frame = DrawRandom(frame, out ushort angle);
            NativeBattleVector displacement = new(unchecked((short)(NativeBattleCameraMath.Cosine(angle) >> 6)), unchecked((short)(NativeBattleCameraMath.Sine(angle) >> 6)), 255);
            displacement = NativeBattleGfMatrices.RotateShort56C4F0(basis, displacement);
            NativeBattleVector position = new(unchecked((short)(displacement.X + point.X)), unchecked((short)(displacement.Y + point.Y)), unchecked((short)(displacement.Z + point.Z)));
            NativeBattleVector velocity = new(unchecked((short)(NativeBattleCameraMath.Cosine(angle) >> 8)), unchecked((short)(NativeBattleCameraMath.Sine(angle) >> 8)), 50);
            velocity = NativeBattleGfMatrices.RotateShort56C4F0(basis, velocity);
            NativeBattleModulePoolTask emitter = Vector(Vector(Read(frame, child), 12, position).WriteWord(18, 0), 20, velocity);
            frame = Write(frame, child, InitializeWorkspace(emitter, 0x1C, 0xA8, resources.EmitterAct));
        }
        int scale = sine - NativeBattleCameraMath.Sine(1024 - parameter / 4) / 2 + 2048;
        frame = frame with { State = frame.State.WriteWord(0x11F81E0, unchecked((ushort)scale)) };
        byte[] scaleBytes = frame.State.ReadView(0x11F81D0, 18).Read(0, 18);
        NativeBattleActionMatrix scaleMatrix = new(Enumerable.Range(0, 9).Select(index => BinaryPrimitives.ReadInt16LittleEndian(scaleBytes.AsSpan(index * 2))).ToImmutableArray(), 0, 0, 0, 4096);
        NativeBattleActionMatrix local = NativeBattleGfMatrices.Multiply56C090(basis, scaleMatrix) with { X = point.X, Y = point.Y, Z = point.Z };
        NativeBattleActionMatrix viewed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local);
        frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(viewed, null));
        (frame, uint temporary) = Allocate(frame, 92);
        NativeBattleEffectArena context = Context(frame, temporary, 92).Write(0, BitConverter.GetBytes(0x11F2684u)).Write(4, BitConverter.GetBytes(checked(Working(frame) + MorphOffset)));
        context = context.Write(0x10, BitConverter.GetBytes(unchecked((ushort)((phase & 7) << 4)))).Write(0x14, new byte[4]).Write(0x18, BitConverter.GetBytes(0x01000100u)).Write(0x1C, new byte[4]).Write(0x20, BitConverter.GetBytes(0x01000080u));
        frame = PublishContext(frame, temporary, context, 92);
        frame = inputs.Execute(frame, new NativeBattleOilShot136DrawCustom92(temporary));
        return Release(frame, 92);
    }

    private static (NativeBattleOilShot136Frame Frame, NativeBattlePackedMatrix32 Matrix) CameraPoint(NativeBattleOilShot136Frame frame, NativeBattleOilShot136Inputs inputs, NativeBattleVector point, short? w)
    {
        NativeBattlePackedMatrix32 camera = inputs.ReadCamera(frame);
        NativeBattleActionMatrix matrix = camera.ReadMatrix();
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), matrix, true, false, null);
        geometry = geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => camera.Known[index] ? (byte?)camera.Bytes[index] : null).ToImmutableArray() };
        NativeBattleGeometryRegisterStep registers = new(geometry, frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = NativeBattleGeometry.LoadVector(registers, 0, point, w);
        registers = NativeBattleGeometry.Transform(registers, false);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        matrix = matrix with { X = unchecked(matrix.X + registers.Colors.Mac[0]!.Value), Y = unchecked(matrix.Y + registers.Colors.Mac[1]!.Value), Z = unchecked(matrix.Z + registers.Colors.Mac[2]!.Value) };
        return (frame, NativeBattlePackedMatrix32.FromMatrix(matrix, null));
    }

    private static NativeBattleModulePoolTask CopyTask(NativeBattleModulePoolTask task, int offset, NativeBattleEffectArena source, int length)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, length);
        task = task.WriteBytes(offset, bytes);
        for (int index = 0; index < length; index++)
        {
            if (!known[index])
            {
                task = task.MarkPayloadUnknown(offset + index, 1);
            }
        }
        return task;
    }
}
