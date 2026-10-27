using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleMicroMissiles299Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleMicroMissiles299Controller
{
    private static (NativeBattleMicroMissiles299Frame Frame, int Result) Particle(NativeBattleMicroMissiles299Frame frame, uint address, NativeBattleMicroMissiles299Inputs inputs)
    {
        frame = inputs.Execute(frame, new NativeBattleMicroMissiles299DrawParticle(address));
        NativeBattleModulePoolTask task = Read(frame, address);
        for (int axis = 0; axis < 3; axis++)
        {
            ushort velocity = task.ReadWord(0x14 + axis * 2);
            task = task.WriteWord(0xC + axis * 2, unchecked((ushort)(task.ReadWord(0xC + axis * 2) + velocity)));
            task = task.WriteWord(0x14 + axis * 2, unchecked((ushort)(velocity + task.ReadWord(0x1C + axis * 2))));
        }
        short age = unchecked((short)(task.ReadWord(0x12) + 1));
        return (Write(frame, address, task.WriteWord(0x12, unchecked((ushort)age))), age < 12 ? 0 : 2);
    }

    private static (NativeBattleMicroMissiles299Frame Frame, int Result) Impact(NativeBattleMicroMissiles299Frame frame, uint address, NativeBattleMicroMissiles299Resources resources, NativeBattleMicroMissiles299Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleActionMatrix matrix = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadView(frame).ReadMatrix(), Matrix(task, 0x10, 0x24));
        uint sourceAddress = task.ReadDword(0x30);
        NativeBattleGfAnimationSource source = sourceAddress == resources.Impact.Address ? resources.Impact : sourceAddress == resources.WideImpact.Address ? resources.WideImpact : throw new InvalidDataException("Micro Missiles impact workspace selected an unsupported original programme.");
        int size = sourceAddress == resources.Impact.Address ? 0xA0 : 0x114;
        (frame, int remaining) = Play(frame, address, 0x30, size, source, Context(frame, matrix, -128), inputs);
        short age = unchecked((short)Read(frame, address).ReadWord(0xC));
        if (age < 14)
        {
            NativeBattleActionMatrix sprite = new([6144, 0, 0, 0, 6144, 0, 0, 0, 6144], matrix.X, matrix.Y, matrix.Z, 4096);
            frame = LoadMatrix(frame, sprite, null);
            NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 180);
            frame = frame with { Staging = allocation.Staging };
            frame = EmitSprite(frame, allocation.Address, resources.ImpactSprite.Address, unchecked((ushort)age), inputs);
            frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 180) };
        }
        else if (remaining == 0)
        {
            return (frame, 2);
        }
        return (Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1)))), 0);
    }

    private static (NativeBattleMicroMissiles299Frame Frame, int Result) Missile(NativeBattleMicroMissiles299Frame frame, uint address, NativeBattleMicroMissiles299Resources resources, NativeBattleMicroMissiles299Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleTemporaryAllocation allocation = NativeBattleTemporaryMemory.Allocate5082B0(RequireStaging(frame), 180);
        frame = frame with { Staging = allocation.Staging };
        int parameter = unchecked((short)task.ReadWord(0x2A)) - 0x990;
        NativeBattleActionMatrix camera = inputs.ReadView(frame).ReadMatrix();
        NativeBattleActionMatrix rotation = Matrix(task, 0xC, null);
        for (int phase = 11; parameter < 4096 && phase >= 0; phase--, parameter = unchecked(parameter + 204))
        {
            if (parameter < 0)
            {
                continue;
            }
            NativeBattleVector local = Curve(task, parameter);
            NativeBattleGeometryRegisterStep registers = Registers(frame);
            registers = registers with { Geometry = NativeBattleGeometry.LoadMatrix(registers.Geometry, rotation, true, false, unchecked((short)task.ReadWord(0x1E))) };
            registers = NativeBattleGeometry.Transform(NativeBattleGeometry.LoadVector(registers, 0, local, null), false);
            frame = Publish(frame, registers);
            int x = unchecked(registers.Colors.Mac[0]!.Value + (short)task.ReadWord(0x24));
            int y = unchecked(registers.Colors.Mac[1]!.Value + (short)task.ReadWord(0x26));
            int z = unchecked(registers.Colors.Mac[2]!.Value + (short)task.ReadWord(0x28));
            (x, y, z) = NativeBattleGfMatrices.RotateLong56C600(camera, x, y, z);
            NativeBattleActionMatrix sprite = NativeBattleCuragaMatrices.Identity8DD770() with { X = unchecked(x + camera.X), Y = unchecked(y + camera.Y), Z = unchecked(z + camera.Z) };
            frame = LoadMatrix(frame, sprite, null);
            frame = EmitSprite(frame, allocation.Address, resources.TrailSprite.Address, (ushort)phase, inputs);
        }
        frame = frame with { Staging = NativeBattleTemporaryMemory.Release5082D0(RequireStaging(frame), 180) };
        task = Read(frame, address);
        parameter = unchecked((short)task.ReadWord(0x2A));
        int dx = 0;
        int dy = 0;
        int dz = 0;
        if (parameter <= 4096)
        {
            NativeBattleVector local = Curve(task, parameter);
            (int x, int y, int z) = NativeBattleGfMatrices.Rotate56C3D0(rotation, local);
            x = unchecked(x + (short)task.ReadWord(0x24));
            y = unchecked(y + (short)task.ReadWord(0x26));
            z = unchecked(z + (short)task.ReadWord(0x28));
            dx = unchecked(x - (short)task.ReadWord(0x2C));
            dz = unchecked((short)task.ReadWord(0x30) - z);
            dy = dz <= 0 ? unchecked((short)task.ReadWord(0x2E) - y) : unchecked(y - (short)task.ReadWord(0x2E));
            task = task.WriteWord(0x2C, unchecked((ushort)x)).WriteWord(0x2E, unchecked((ushort)y)).WriteWord(0x30, unchecked((ushort)z));
            frame = Write(frame, address, task);
            NativeBattleActionMatrix localMatrix = NativeBattleFunguarLaser277.Basis50CBA0(dx, dy, dz).Matrix with { X = x, Y = y, Z = z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(camera, localMatrix);
            (frame, _) = Play(frame, address, 0x34, 0x7D8, resources.Missile, Context(frame, composed, 0), inputs);
        }
        task = Read(frame, address);
        short next = unchecked((short)(task.ReadWord(0x2A) + task.ReadWord(0x32)));
        task = task.WriteWord(0x2A, unchecked((ushort)next));
        frame = Write(frame, address, task);
        short variant = unchecked((short)task.ReadWord(0x22));
        if (next >= 4096 && variant != -1)
        {
            (frame, uint impact) = Append(frame, 2, ImpactCallback);
            if (impact != 0)
            {
                if (parameter > 4096)
                {
                    throw new NotSupportedException("Micro Missiles impact basis requires the native current curve publication.");
                }
                NativeBattleActionMatrix basis = NativeBattleFunguarLaser277.Basis50CBA0(dx, 0, dz).Matrix;
                NativeBattleModulePoolTask child = WriteRotation(Read(frame, impact).WriteWord(0xC, 0), 0x10, basis);
                for (int axis = 0; axis < 3; axis++)
                {
                    child = child.WriteDword(0x24 + axis * 4, unchecked((uint)(int)(short)task.ReadWord(0x2C + axis * 2)));
                }
                NativeBattleGfAnimationSource source = variant > 1 ? resources.WideImpact : resources.Impact;
                int size = variant > 1 ? 0x114 : 0xA0;
                child = new NativeBattleGfAnimationWorkspace(child.ReadBytes(0x30, size).ToImmutableArray()).Initialize7016B0(source, size).CopyTo(child, 0x30, retainUnknown: false);
                frame = Write(frame, impact, child);
                if (variant == 0)
                {
                    frame = inputs.Execute(frame, new NativeBattleMicroMissiles299RingSequence((byte)inputs.Packets.ReadContact(FirstContact(frame, inputs)).Primary.ActorIndex, 5));
                }
                else if (variant == 1)
                {
                    frame = inputs.Execute(frame, new NativeBattleMicroMissiles299Contact(FirstContact(frame, inputs)));
                }
            }
            for (int index = 0; index < 16; index++)
            {
                (frame, uint particle) = Append(frame, 3, ParticleCallback);
                if (particle == 0)
                {
                    continue;
                }
                frame = SpawnParticle(frame, particle, task, inputs);
            }
            frame = Write(frame, address, Read(frame, address).WriteWord(0x22, 0xFFFF));
        }
        return (frame, next < 0x1990 ? 0 : 2);
    }

    private static NativeBattleVector Curve(NativeBattleModulePoolTask task, int parameter)
    {
        int length = unchecked((short)task.ReadWord(0x20));
        int along = unchecked(length * parameter) >> 12;
        int a = unchecked(4096 - NativeBattleCameraMath.Cosine(parameter >> 2));
        int b = unchecked(-NativeBattleCameraMath.Sine(parameter >> 1) * along) >> 12;
        short x = unchecked((short)(unchecked(a * b) >> 12));
        short z = unchecked((short)(unchecked(along * parameter) >> 12));
        return new(x, 0, z);
    }

    private static NativeBattleMicroMissiles299Frame SpawnParticle(NativeBattleMicroMissiles299Frame frame, uint address, NativeBattleModulePoolTask missile, NativeBattleMicroMissiles299Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address).WriteDword(0xC, missile.ReadDword(0x2C)).WriteDword(0x10, missile.ReadDword(0x30)).WriteWord(0x12, 0);
        frame = DrawRandom(frame, out ushort yaw);
        frame = DrawRandom(frame, out ushort pitch);
        NativeBattleVector direction = NativeBattleRinoa89Controller.Direction6A6020(pitch, yaw);
        frame = DrawRandom(frame, out ushort strength);
        NativeBattleGeometryRegisterStep registers = Registers(frame);
        registers = registers with { Colors = registers.Colors with { Ir0 = (uint)((strength & 63) + 128), Ir = [(int)direction.X, (int)direction.Y, (int)direction.Z] } };
        registers = NativeBattleRaldo19BallRendering.Weighted(registers, false, inputs.Colors);
        frame = Publish(frame, registers);
        for (int axis = 0; axis < 3; axis++)
        {
            short velocity = unchecked((short)registers.Colors.Ir[axis]!.Value);
            task = task.WriteWord(0x14 + axis * 2, unchecked((ushort)velocity)).WriteWord(0x1C + axis * 2, unchecked((ushort)(-velocity >> 4)));
        }
        return Write(frame, address, task);
    }

    private static (NativeBattleMicroMissiles299Frame Frame, int Remaining) Play(NativeBattleMicroMissiles299Frame frame, uint address, int offset, int size, NativeBattleGfAnimationSource source, NativeBattleEffectArena context, NativeBattleMicroMissiles299Inputs inputs)
    {
        NativeBattleGfAnimationWorkspace workspace = new(Read(frame, address).ReadBytes(offset, size).ToImmutableArray());
        NativeBattleGfAnimationAdvance advance = workspace.Advance701970(source, 0, (current, node) =>
        {
            frame = Write(frame, address, Read(frame, address).WriteBytes(offset, current.Data.AsSpan()));
            frame = inputs.EmitNode(frame, source, context, node);
            return new(Read(frame, address).ReadBytes(offset, size).ToImmutableArray());
        });
        return (Write(frame, address, advance.Workspace.CopyTo(Read(frame, address), offset, retainUnknown: false)), advance.Remaining);
    }
    private static NativeBattleMicroMissiles299Frame EmitSprite(NativeBattleMicroMissiles299Frame frame, uint address, uint resource, ushort phase, NativeBattleMicroMissiles299Inputs inputs)
    {
        NativeBattleFileStaging staging = NativeBattleModuleSpriteTemporary.Begin571C80(RequireStaging(frame), address, resource, 0);
        byte[] word = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(word, phase);
        frame = frame with { Staging = staging.Write(address + 4, word) };
        return inputs.Execute(frame, new NativeBattleMicroMissiles299DrawSprite(address, resource, phase));
    }
    private static NativeBattleEffectArena Context(NativeBattleMicroMissiles299Frame frame, NativeBattleActionMatrix matrix, int depth)
    {
        NativeBattlePackedMatrix32 packed = NativeBattlePackedMatrix32.FromMatrix(matrix, null);
        NativeBattleEffectArena context = NativeBattleEffectArena.Clear().MarkUnknown(0, 40);
        for (int index = 0; index < 32; index++)
        {
            if (packed.Known[index])
            {
                context = context.Write(index, [packed.Bytes[index]]);
            }
        }
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, depth);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), checked(frame.State.ReadDword(WorkingGlobal) + MorphOffset));
        return context.Write(32, bytes);
    }
    private static NativeBattleActionMatrix Matrix(NativeBattleModulePoolTask task, int rotation, int? translation) => new(Enumerable.Range(0, 9).Select(lane => unchecked((short)task.ReadWord(rotation + lane * 2))).ToImmutableArray(), translation is int atX ? unchecked((int)task.ReadDword(atX)) : 0, translation is int atY ? unchecked((int)task.ReadDword(atY + 4)) : 0, translation is int atZ ? unchecked((int)task.ReadDword(atZ + 8)) : 0, 4096);
    private static NativeBattleGeometryRegisterStep Registers(NativeBattleMicroMissiles299Frame frame) => new(frame.Memory.GeometryRegisters ?? NativeBattleGeometryRegisters.Unknown(), frame.Memory.ScriptedColorRegisters ?? NativeBattleScriptedColorRegisters.Unknown());
    private static NativeBattleMicroMissiles299Frame Publish(NativeBattleMicroMissiles299Frame frame, NativeBattleGeometryRegisterStep registers) => frame with { Memory = frame.Memory with { GeometryRegisters = registers.Geometry, ScriptedColorRegisters = registers.Colors } };
    private static NativeBattleMicroMissiles299Frame LoadMatrix(NativeBattleMicroMissiles299Frame frame, NativeBattleActionMatrix matrix, short? padding) => Publish(frame, Registers(frame) with { Geometry = NativeBattleGeometry.LoadMatrix(Registers(frame).Geometry, matrix, true, true, padding) });
    private static NativeBattleFileStaging RequireStaging(NativeBattleMicroMissiles299Frame frame) => frame.Staging ?? throw new NotSupportedException("Micro Missiles requires its actual physical temporary memory.");
}
