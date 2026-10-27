using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEnemyMicroMissiles284Controller
{
    public static (NativeBattleEnemyMicroMissiles284Frame Frame, int Result) AdvanceMissile67BBB0(NativeBattleEnemyMicroMissiles284Frame frame, uint address, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        (frame, uint temporary) = Allocate(frame, 180);
        int oldParameter = Signed(task, 0x2A);
        NativeBattleActionMatrix rotation = TaskMatrix284(task, 12, null);
        int parameter = oldParameter - 0x4C8;
        NativeBattleActionMatrix camera = inputs.ReadView(frame).ReadMatrix();
        for (int phase = 11; phase >= 0 && parameter < 4096; phase--, parameter = unchecked(parameter + 0x66))
        {
            if (parameter < 0)
            {
                continue;
            }
            NativeBattleVector local = Curve284(task, parameter);
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(rotation, task.ReadWord(0x1E)), true, false);
            NativeBattleGeometryRegisterStep registers = Registers284(frame);
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, local, null), false);
            frame = Publish284(frame, registers);
            int x = unchecked(registers.Colors.Mac[0]!.Value + Signed(task, 0x24));
            int y = unchecked(registers.Colors.Mac[1]!.Value + Signed(task, 0x26));
            int z = unchecked(registers.Colors.Mac[2]!.Value + Signed(task, 0x28));
            (x, y, z) = NativeBattleGfMatrices.RotateLong56C600(camera, x, y, z);
            NativeBattleActionMatrix sprite = NativeBattleCuragaMatrices.Identity8DD770() with { X = unchecked(x + camera.X), Y = unchecked(y + camera.Y), Z = unchecked(z + camera.Z) };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(sprite, null));
            frame = DrawSprite284(frame, temporary, resources.TrailSprite.Address, (ushort)phase, inputs);
        }
        frame = Release(frame, 180);
        task = Read(frame, address);
        oldParameter = Signed(task, 0x2A);
        int dx = 0;
        int dy = 0;
        int dz = 0;
        if (oldParameter <= 4096)
        {
            NativeBattleVector local = Curve284(task, oldParameter);
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(rotation, local);
            x = unchecked(x + Signed(task, 0x24));
            y = unchecked(y + Signed(task, 0x26));
            z = unchecked(z + Signed(task, 0x28));
            dx = unchecked(x - Signed(task, 0x2C));
            dz = unchecked(Signed(task, 0x30) - z);
            dy = dz > 0 ? unchecked(y - Signed(task, 0x2E)) : unchecked(Signed(task, 0x2E) - y);
            task = task.WriteWord(0x2C, unchecked((ushort)x)).WriteWord(0x2E, unchecked((ushort)y)).WriteWord(0x30, unchecked((ushort)z));
            frame = Write(frame, address, task);
            NativeBattleActionMatrix current = NativeBattleFunguarLaser277.Basis50CBA0(dx, dy, dz).Matrix with { X = x, Y = y, Z = z };
            current = NativeBattleGfMatrices.Compose56C2F0(camera, current);
            (frame, _) = PlayProgramme284(frame, address, 0x34, 2020, resources.Missile, ProgrammeCaller284(current, 0), resources, inputs);
        }
        task = Read(frame, address);
        short next = unchecked((short)(task.ReadWord(0x2A) + task.ReadWord(0x32)));
        task = task.WriteWord(0x2A, unchecked((ushort)next));
        frame = Write(frame, address, task);
        short variant = Signed(task, 0x22);
        if (next >= 4096 && variant != -1)
        {
            (frame, uint impact) = Append(frame, 0x2507DD0, 0x67C300);
            if (impact != 0)
            {
                if (oldParameter > 4096)
                {
                    throw new NotSupportedException("Enemy Micro Missiles impact requires its actual current curve basis.");
                }
                NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(dx, 0, dz).Matrix;
                NativeBattleModulePoolTask child = WriteTaskRotation284(Read(frame, impact).WriteWord(12, 0), 16, basis);
                for (int axis = 0; axis < 3; axis++)
                {
                    child = child.WriteDword(36 + axis * 4, unchecked((uint)(int)Signed(task, 0x2C + axis * 2)));
                }
                NativeBattleGfAnimationSource source = variant == 0 ? resources.Impact : resources.WideImpact;
                int bytes = variant == 0 ? 160 : 276;
                NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(child, 48, bytes);
                workspace = workspace.Initialize7016B0(source, bytes);
                child = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(child, 48, workspace);
                frame = Write(frame, impact, child);
                if (variant == 0)
                {
                    NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
                    int contact = Header(frame, packets).FirstContactIndex ?? throw new NotSupportedException("Enemy Micro Missiles reaction requires its actual first contact pointer.");
                    int actor = packets.ReadContact(contact).Primary.ActorIndex;
                    frame = inputs.Execute(frame, new NativeBattleEnemyMicroMissiles284Reaction505C00(checked(0x1D972C0u + (uint)(actor * 0x9C)), 4));
                }
            }
            for (int index = 0; index < 16; index++)
            {
                (frame, uint particle) = Append(frame, 0x2507DC0, 0x67C240);
                if (particle != 0)
                {
                    frame = SpawnParticle284(frame, particle, task, inputs);
                }
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(0x22, 0xFFFF));
        }
        return (frame, next < 0x14C8 ? 0 : 2);
    }

    public static (NativeBattleEnemyMicroMissiles284Frame Frame, int Result) AdvanceImpact67C300(NativeBattleEnemyMicroMissiles284Frame frame, uint address, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), TaskMatrix284(task, 16, 36));
        uint sourceAddress = task.ReadDword(48);
        NativeBattleGfAnimationSource source = sourceAddress == resources.Impact.Address ? resources.Impact : sourceAddress == resources.WideImpact.Address ? resources.WideImpact : throw new InvalidDataException("Enemy Micro Missiles impact requires its actual selected source.");
        int bytes = sourceAddress == resources.Impact.Address ? 160 : 276;
        (frame, int remaining) = PlayProgramme284(frame, address, 48, bytes, source, ProgrammeCaller284(matrix, -128), resources, inputs);
        task = Read(frame, address);
        short age = Signed(task, 12);
        if (age < 14)
        {
            NativeBattleActionMatrix sprite = NativeBattleCuragaMatrices.Identity8DD770() with { X = matrix.X, Y = matrix.Y, Z = matrix.Z };
            frame = LoadMatrix(frame, NativeBattlePackedMatrix32.FromMatrix(sprite, null));
            (frame, uint temporary) = Allocate(frame, 180);
            frame = DrawSprite284(frame, temporary, resources.ImpactSprite.Address, unchecked((ushort)age), inputs);
            frame = Release(frame, 180);
        }
        else if (remaining == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)))), 0);
    }

    public static (NativeBattleEnemyMicroMissiles284Frame Frame, int Result) AdvanceParticle67C240(NativeBattleEnemyMicroMissiles284Frame frame, uint address, NativeBattleEnemyMicroMissiles284Resources resources, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(Signed(task, 12), Signed(task, 14), Signed(task, 16)), Signed(task, 18), 1024, -256) };
        (frame, uint temporary) = Allocate(frame, 180);
        frame = DrawSprite284(frame, temporary, resources.ParticleSprite.Address, task.ReadWord(18), inputs);
        frame = Release(frame, 180);
        task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            ushort velocity = task.ReadWord(20 + axis * 2);
            task = task.WriteWord(12 + axis * 2, unchecked((ushort)(task.ReadWord(12 + axis * 2) + velocity))).WriteWord(20 + axis * 2, unchecked((ushort)(velocity + task.ReadWord(28 + axis * 2))));
        }
        short age = unchecked((short)(task.ReadWord(18) + 1));
        frame = Write(frame, address, task.WriteWord(18, unchecked((ushort)age)));
        return (frame, age < 12 ? 0 : 2);
    }

    private static NativeBattleEnemyMicroMissiles284Frame SpawnParticle284(NativeBattleEnemyMicroMissiles284Frame frame, uint address, NativeBattleModulePoolTask missile, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        frame = Write(frame, address, Read(frame, address).WriteDword(12, missile.ReadDword(0x2C)).WriteDword(16, missile.ReadDword(0x30)).WriteWord(18, 0));
        frame = DrawRandom(frame, out ushort yaw);
        frame = DrawRandom(frame, out ushort pitch);
        NativeBattleVector direction = NativeBattleRinoa89Controller.Direction6A6020(pitch, yaw);
        frame = DrawRandom(frame, out ushort strength);
        NativeBattleGeometryRegisterStep registers = Registers284(frame);
        registers = NativeBattleGeometry.LoadIrColumn(registers with { Colors = registers.Colors with { Ir0 = (uint)((strength & 63) + 128) } }, unchecked((ushort)direction.X), unchecked((ushort)direction.Y), unchecked((ushort)direction.Z));
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
        frame = Publish284(frame, registers);
        NativeBattleModulePoolTask task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            short velocity = unchecked((short)registers.Colors.Ir[axis]!.Value);
            task = task.WriteWord(20 + axis * 2, unchecked((ushort)velocity)).WriteWord(28 + axis * 2, unchecked((ushort)(-velocity >> 4)));
        }
        return Write(frame, address, task);
    }

    private static NativeBattleVector Curve284(NativeBattleModulePoolTask task, int parameter)
    {
        int along = unchecked(Signed(task, 0x20) * parameter) >> 12;
        int x = unchecked(-unchecked(NativeBattleCameraMath.Sine(parameter >> 1) * along)) >> 12;
        int z = unchecked(-unchecked(NativeBattleCameraMath.Cosine(parameter >> 1) * along)) >> 12;
        return new(unchecked((short)x), 0, unchecked((short)z));
    }

    private static NativeBattleEnemyMicroMissiles284Frame DrawSprite284(NativeBattleEnemyMicroMissiles284Frame frame, uint temporary, uint source, ushort phase, NativeBattleEnemyMicroMissiles284Inputs inputs)
    {
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(4, BitConverter.GetBytes(phase)).Write(36, BitConverter.GetBytes((ushort)0));
        frame = PublishContext(frame, temporary, context, 180);
        return inputs.Execute(frame, new NativeBattleEnemyMicroMissiles284DrawSprite180(temporary, source));
    }
    private static NativeBattleGeometryRegisterStep Registers284(NativeBattleEnemyMicroMissiles284Frame frame) => new(frame.Memory.GeometryRegisters ?? throw new NotSupportedException("Enemy Micro Missiles requires its current geometry owner."), frame.Memory.ScriptedColorRegisters ?? throw new NotSupportedException("Enemy Micro Missiles requires its current arithmetic owner."));
    private static NativeBattleEnemyMicroMissiles284Frame Publish284(NativeBattleEnemyMicroMissiles284Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static NativeBattleActionMatrix TaskMatrix284(NativeBattleModulePoolTask task, int rotation, int? translation) => new(Enumerable.Range(0, 9).Select(index => Signed(task, rotation + index * 2)).ToImmutableArray(), translation is int x ? unchecked((int)task.ReadDword(x)) : 0, translation is int y ? unchecked((int)task.ReadDword(y + 4)) : 0, translation is int z ? unchecked((int)task.ReadDword(z + 8)) : 0, 4096);
    private static NativeBattleModulePoolTask WriteTaskRotation284(NativeBattleModulePoolTask task, int offset, NativeBattleActionMatrix matrix)
    {
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(offset + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        return task;
    }
}
