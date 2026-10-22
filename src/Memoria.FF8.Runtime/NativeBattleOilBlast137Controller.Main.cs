using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleOilBlast137Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleOilBlast137Controller
{
    public static (NativeBattleOilBlast137Frame Frame, int Result) AdvanceMain(NativeBattleOilBlast137Frame frame, uint address, NativeBattleOilBlast137Resources resources, NativeBattleOilBlast137Inputs inputs)
    {
        int age = Word(Read(frame, address), 12);
        uint caster = frame.State.ReadDword(CasterGlobal);
        uint morph = checked(frame.State.ReadDword(WorkingGlobal) + MorphOffset);
        if (unchecked((uint)(age - 18)) < 9)
        {
            if (age == 18)
            {
                frame = CaptureBone(frame, address, 0x2C, caster, 0x1E, inputs);
                frame = Write(frame, address, Read(frame, address).WriteWord(0x2E, 0));
                frame = CaptureBone(frame, address, 0x34, caster, 0x1C, inputs);
                frame = Write(frame, address, Read(frame, address).WriteWord(0x36, 0));
                frame = inputs.Execute(frame, new NativeBattleOilBlast137InitializeAct(address, 0x84, 0x40, resources.OpeningAct));
                frame = inputs.Execute(frame, new NativeBattleOilBlast137InitializeAct(address, 0xC4, 0x40, resources.OpeningAct));
            }
            frame = Opening(frame, address, 0x2C, 0x84, morph, resources, inputs);
            frame = Opening(frame, address, 0x34, 0xC4, morph, resources, inputs);
        }
        if (unchecked((uint)(age - 21)) < 20)
        {
            if (age == 21)
            {
                frame = CaptureBone(frame, address, 0x3C, caster, 0x13, inputs);
                frame = Write(frame, address, Read(frame, address).WriteWord(0x3E, 0));
                frame = inputs.Execute(frame, new NativeBattleOilBlast137InitializeAct(address, 0x104, 0x1A0, resources.MainAct));
            }
            NativeBattleVector point = Vector(Read(frame, address), 0x3C);
            NativeBattleActionMatrix yaw = NativeBattlePandemona291Restoration.Rotation701270(inputs.ReadModelYaw(caster)) with { X = point.X, Y = point.Y, Z = point.Z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), yaw);
            frame = inputs.AdvanceAct(frame, address, 0x104, 0x1A0, resources.MainAct, Context(NativeBattlePackedMatrix32.FromMatrix(composed, null), null, 0, morph)).Frame;
        }
        if (unchecked((uint)(age - 21)) < 8)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            if (age == 21)
            {
                NativeBattleVector target = Vector(task, 0x1C);
                NativeBattleVector start = Vector(task, 0x3C);
                NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(target.X - start.X, 0, start.Z - target.Z).Matrix;
                task = PutRotation(task, 0x44, basis);
                task = PutVector(task, 0x14, new(unchecked((short)(start.X + (basis.Rotation[2] >> 4))), 0, unchecked((short)(start.Z + (basis.Rotation[8] >> 4)))));
                frame = Write(frame, address, task);
            }
            int factor = (age - 21) << 9;
            ushort scale = unchecked((ushort)(NativeBattleCameraMath.Sine(factor >> 2) + 1024));
            frame = frame with { State = frame.State.WriteWord(MatrixAddress, scale).WriteWord(MatrixAddress + 8, scale).WriteWord(MatrixAddress + 16, scale) };
            NativeBattleEffectArena matrixSource = frame.State.ReadView(MatrixAddress, 32);
            (byte[] matrixBytes, bool[] matrixKnown) = matrixSource.ReadSnapshot(0, 32);
            NativeBattleActionMatrix diagonal = new NativeBattlePackedMatrix32(matrixBytes.ToImmutableArray(), matrixKnown.ToImmutableArray()).ReadMatrix();
            NativeBattleActionMatrix scaled = NativeBattleGfMatrices.Multiply56C090(Matrix(Read(frame, address), 0x44), diagonal);
            NativeBattleWeightedPointsStep weighted = NativeBattleWeightedPoints56CB90.Blend(frame.Memory, Vector(Read(frame, address), 0x14), Vector(Read(frame, address), 0x1C) with { Y = 0 }, unchecked((short)(4096 - factor)), unchecked((short)factor), inputs.Colors);
            frame = frame with { Memory = weighted.Memory };
            NativeBattleActionMatrix local = scaled with { X = weighted.Position.X, Y = weighted.Position.Y, Z = weighted.Position.Z };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local), null));
            (frame, uint temporary) = Allocate(frame, 84);
            NativeBattleFileStaging staging = frame.Staging!.Write(temporary, BitConverter.GetBytes(CustomMeshAddress)).Write(temporary + 4, BitConverter.GetBytes(unchecked((ushort)(((age - 21) & 3) << 5))));
            ushort[] windows = [0, 0, 256, 256, 0, 0, 128, 256];
            for (int index = 0; index < windows.Length; index++)
            {
                staging = staging.Write(temporary + 8u + (uint)(index * 2), BitConverter.GetBytes(windows[index]));
            }
            frame = inputs.Execute(frame with { Staging = staging }, new NativeBattleOilBlast137DrawCustom84(temporary));
            frame = Release(frame, 84);
            for (int index = 0; index < 2; index++)
            {
                frame = SpawnSecondary(frame, address, weighted.Position, inputs);
            }
        }
        if (unchecked((uint)(age - 28)) < 26)
        {
            NativeBattleModulePoolTask task = Read(frame, address);
            if (age == 28)
            {
                NativeBattleVector target = Vector(task, 0x1C);
                NativeBattleVector start = Vector(task, 0x3C);
                frame = Write(frame, address, PutRotation(task, 0x64, NativeBattleFunguarLaser277.Basis50CBA0(start.X - target.X, 0, target.Z - start.Z).Matrix));
                frame = inputs.Execute(frame, new NativeBattleOilBlast137InitializeAct(address, 0x2A4, 0x2FC, resources.ImpactAct));
            }
            task = Read(frame, address);
            NativeBattleActionMatrix local = Matrix(task, 0x64);
            NativeBattleVector targetPoint = Vector(task, 0x1C);
            local = local with { X = targetPoint.X - (local.Rotation[2] >> 2), Y = 0, Z = targetPoint.Z - (local.Rotation[8] >> 2) };
            task = task.WriteDword(0x78, unchecked((uint)local.X)).WriteDword(0x7C, 0).WriteDword(0x80, unchecked((uint)local.Z));
            frame = Write(frame, address, task);
            if (age - 28 < 6)
            {
                for (int index = 0; index < 10; index++)
                {
                    frame = SpawnParticle(frame, address, new(unchecked((short)local.X), 0, unchecked((short)local.Z)), inputs);
                }
            }
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadCamera(frame).ReadMatrix(), local);
            frame = inputs.AdvanceAct(frame, address, 0x2A4, 0x2FC, resources.ImpactAct, Context(NativeBattlePackedMatrix32.FromMatrix(composed, null), -256, 1, morph)).Frame;
        }
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleOilBlast137Sound(resources.Sound));
        }
        if (age == 45)
        {
            frame = inputs.Execute(frame, new NativeBattleOilBlast137Contact(Header(frame, inputs).FirstContactIndex ?? throw new NotSupportedException("Oil Blast requires its first captured contact.")));
        }
        if (age >= 60)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    private static NativeBattleOilBlast137Frame CaptureBone(NativeBattleOilBlast137Frame frame, uint taskAddress, int offset, uint model, byte selector, NativeBattleOilBlast137Inputs inputs)
    {
        NativeBattleOilBlast137PointStep result = inputs.ReadBone502170(frame, model, selector, 0);
        return Write(result.Frame, taskAddress, PutVector(Read(result.Frame, taskAddress), offset, result.Point));
    }

    private static NativeBattleOilBlast137Frame Opening(NativeBattleOilBlast137Frame frame, uint taskAddress, int pointOffset, int workspaceOffset, uint morph, NativeBattleOilBlast137Resources resources, NativeBattleOilBlast137Inputs inputs)
    {
        NativeBattlePackedMatrix32 packed = inputs.ReadCamera(frame);
        NativeBattleActionMatrix camera = packed.ReadMatrix();
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = registers with { Geometry = registers.Geometry with { Rotation20 = Enumerable.Range(0, 20).Select(index => packed.Known[index] ? (byte?)packed.Bytes[index] : null).ToImmutableArray() } };
        registers = NativeBattleGeometry.LoadVector(registers, 0, Vector(Read(frame, taskAddress), pointOffset), OptionalWord(Read(frame, taskAddress), pointOffset + 6));
        registers = NativeBattleGeometry.Transform(registers, false);
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
        NativeBattleActionMatrix combined = camera with { X = unchecked(camera.X + registers.Colors.Mac[0]!.Value), Y = unchecked(camera.Y + registers.Colors.Mac[1]!.Value), Z = unchecked(camera.Z + registers.Colors.Mac[2]!.Value) };
        return inputs.AdvanceAct(frame, taskAddress, workspaceOffset, 0x40, resources.OpeningAct, Context(NativeBattlePackedMatrix32.FromMatrix(combined, null), null, 0, morph)).Frame;
    }

    private static NativeBattleModulePoolTask PutRotation(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task;
    }

    private static short? OptionalWord(NativeBattleModulePoolTask task, int offset)
    {
        if (task.UnknownPayloadRanges is { } ranges && ranges.Any(range => range.Offset < offset + 2 && range.Offset + range.Length > offset))
        {
            return null;
        }
        return Word(task, offset);
    }

    private static NativeBattleOilBlast137Frame Velocity(NativeBattleOilBlast137Frame frame, uint parentAddress, int minimum, int mask, NativeBattleOilBlast137Inputs inputs, out NativeBattleVector velocity)
    {
        frame = Random(frame, out ushort first);
        frame = Random(frame, out ushort second);
        NativeBattleVector direction = NativeBattleRinoa89Controller.Direction6A6020(unchecked((short)((second * 800) >> 15)), unchecked((short)(((first * 2000) >> 15) - 1000)));
        direction = NativeBattleGfMatrices.RotateShort56C4F0(Matrix(Read(frame, parentAddress), 0x44), direction);
        frame = Random(frame, out ushort scale);
        NativeBattleGeometryRegisterStep registers = new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
        registers = registers with { Colors = registers.Colors with { Ir0 = (uint)((scale & mask) + minimum) } };
        registers = NativeBattleGeometry.LoadIrColumn(registers, unchecked((ushort)direction.X), unchecked((ushort)direction.Y), unchecked((ushort)direction.Z));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
        velocity = new(unchecked((short)registers.Colors.Ir[0]!.Value), unchecked((short)registers.Colors.Ir[1]!.Value), unchecked((short)registers.Colors.Ir[2]!.Value));
        return frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    }

    private static NativeBattleOilBlast137Frame SpawnSecondary(NativeBattleOilBlast137Frame frame, uint parentAddress, NativeBattleVector position, NativeBattleOilBlast137Inputs inputs)
    {
        if (frame.Effect.Pools[2].Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (frame, uint child) = Append(frame, 2, SecondaryCallback);
        frame = Write(frame, child, PutVector(Read(frame, child), 12, position).WriteWord(0x12, 0));
        frame = Velocity(frame, parentAddress, 128, 127, inputs, out NativeBattleVector velocity);
        NativeBattleModulePoolTask task = PutVector(Read(frame, child), 0x14, velocity);
        for (int index = 0; index < 3; index++)
        {
            frame = Random(frame, out ushort value);
            task = task.WriteWord(0x1C + index * 2, value);
        }
        for (int index = 0; index < 3; index++)
        {
            frame = Random(frame, out ushort value);
            task = task.WriteWord(0x24 + index * 2, unchecked((ushort)((value & 511) - 256)));
        }
        return Write(frame, child, task);
    }

    private static NativeBattleOilBlast137Frame SpawnParticle(NativeBattleOilBlast137Frame frame, uint parentAddress, NativeBattleVector position, NativeBattleOilBlast137Inputs inputs)
    {
        (frame, uint child) = Append(frame, 3, ParticleCallback);
        frame = Write(frame, child, PutVector(Read(frame, child), 12, position).WriteWord(0x12, 0));
        frame = Velocity(frame, parentAddress, 256, 255, inputs, out NativeBattleVector velocity);
        frame = Random(frame, out ushort size);
        NativeBattleModulePoolTask task = PutVector(Read(frame, child), 0x14, velocity).WriteWord(0x1A, unchecked((ushort)((size & 2047) + 2048)));
        task = PutVector(task, 0x1C, new(unchecked((short)(-velocity.X >> 4)), unchecked((short)(-velocity.Y >> 4)), unchecked((short)(-velocity.Z >> 4))));
        return Write(frame, child, task);
    }
}
