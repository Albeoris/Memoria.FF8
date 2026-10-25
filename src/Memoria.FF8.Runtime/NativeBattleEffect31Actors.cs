using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect31Actors
{
    public static NativeBattleEffect31Step Advance(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint callback = task.Callback;
        switch (callback)
        {
            case 0x871F60:
                frame = AdvanceFirst(frame, address, resources, inputs);
                break;
            case 0x872170:
                frame = AdvanceWaves(frame, address, inputs);
                break;
            case 0x872920:
                frame = AdvanceSecond(frame, address, resources, inputs);
                break;
            case 0x872B10:
                frame = AdvanceFlight(frame, address, resources, inputs);
                break;
            case 0x8731D0:
                frame = AdvancePersistent(frame, address, resources, inputs);
                break;
            case 0x8723E0:
            case 0x873010:
            case 0x8730C0:
                frame = AdvanceParticle(frame, address);
                frame = DrawSprite(frame, address, resources, inputs);
                break;
            default:
                throw new NotSupportedException("Effect31 actor callback is outside its selected dispatch table.");
        }
        return Finish(frame, address);
    }

    private static NativeBattleEffect31Frame AdvanceParticle(NativeBattleEffect31Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        int phase = NativePhase(task, task.Callback == 0x8723E0 ? 1 : 2);
        if (task.Callback == 0x8723E0)
        {
            if (phase == 0)
            {
                task = Move(task, 0, false);
                task = Move(task, 1, true);
                task = Move(task, 2, false);
                frame = AdvanceSpriteFrame(Write(frame, address, task), address);
            }
            return frame;
        }
        if (phase == 0)
        {
            bool falling = task.Callback == 0x8730C0;
            task = task.WriteDword(0x4C, falling ? 0x15F944Cu : 0x15F9250u).WriteWord(0x52, falling ? (ushort)14 : (ushort)18);
            if (falling)
            {
                (frame, int random) = Random(frame);
                task = task.WriteWord(0x7A, unchecked((ushort)-(random & 0x1FF)));
            }
            return IncrementPhase(Write(frame, address, task), address);
        }
        if (phase == 1)
        {
            if (task.Callback == 0x8730C0)
            {
                frame = Write(frame, address, Move(task, 1, true));
            }
            return AdvanceSpriteFrame(frame, address);
        }
        return frame;
    }

    private static NativeBattleModulePoolTask Move(NativeBattleModulePoolTask task, int axis, bool gravity)
    {
        int velocityOffset = 0x78 + axis * 2;
        short velocity = unchecked((short)task.ReadWord(velocityOffset));
        if (gravity)
        {
            velocity = unchecked((short)(velocity + 128));
        }
        velocity = unchecked((short)(velocity - unchecked(velocity * 3 << 8) / 4096));
        int positionOffset = 0x1C + axis * 2;
        return task.WriteWord(velocityOffset, unchecked((ushort)velocity)).WriteWord(positionOffset, unchecked((ushort)(task.ReadWord(positionOffset) + velocity / 16)));
    }

    private static NativeBattleEffect31Frame AdvanceSpriteFrame(NativeBattleEffect31Frame frame, uint address)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short next = unchecked((short)(task.ReadWord(0x50) + 1));
        task = task.WriteWord(0x50, unchecked((ushort)next));
        if (next > unchecked((short)task.ReadWord(0x52)))
        {
            task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
            return IncrementPhase(Write(frame, address, task), address);
        }
        return Write(frame, address, task);
    }

    private static NativeBattleEffect31Frame AdvanceWaves(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Inputs inputs)
    {
        int phase = NativePhase(Read(frame, address), 2);
        if (phase == 2)
        {
            return frame;
        }
        (frame, _) = CasterMatrix(frame, address, phase == 0 ? 0x8721D0u : 0x8725B0u, 0x1C, 32, inputs);
        int[] counts = phase == 0 ? [4, 16, 6] : [2, 8, 3];
        for (int group = 0; group < counts.Length; group++)
        {
            for (int index = 0; index < counts[group]; index++)
            {
                (frame, int random) = Random(frame);
                int speed = group switch
                {
                    0 => random % 0xC00 + 0x400,
                    1 => random % 0x1800 + 0x800,
                    _ => random % 4096 + 0x800
                };
                frame = BirthWave(frame, address, group == 1 ? 0x15F96BCu : 0x15F95E0u, group == 1 ? (ushort)8 : (ushort)7, unchecked((short)speed), inputs);
            }
        }
        if (phase == 1)
        {
            frame = MarkComplete(frame, address);
        }
        return IncrementPhase(frame, address);
    }

    private static NativeBattleEffect31Frame BirthWave(NativeBattleEffect31Frame frame, uint parent, uint sprite, ushort finalFrame, short radius, NativeBattleEffect31Inputs inputs)
    {
        (frame, uint child) = AppendHierarchy(frame, 5, 0x8723E0, 0x80, parent, inputs);
        NativeBattleModulePoolTask source = Read(frame, parent);
        NativeBattleModulePoolTask task = Read(frame, child).WriteDword(0x1C, source.ReadDword(0x1C)).WriteDword(0x20, source.ReadDword(0x20)).WriteDword(0x4C, sprite).WriteWord(0x52, finalFrame);
        (frame, int angle) = Random(frame);
        (frame, int parity) = Random(frame);
        short x = unchecked((short)(NativeBattleCameraMath.Cosine(angle & 0xFFF) * radius / 4096));
        short y = unchecked((short)(NativeBattleCameraMath.Sine(angle & 0xFFF) * radius / 4096));
        short z = unchecked((short)(radius / 2));
        if ((parity & 1) != 0)
        {
            z = unchecked((short)-z);
        }
        NativeBattleVector velocity = RotateShort(Packed(ReadBytes(frame, parent + 0x58, 32)), new(x, y, z));
        return Write(frame, child, task.WriteWord(0x78, unchecked((ushort)velocity.X)).WriteWord(0x7A, unchecked((ushort)velocity.Y)).WriteWord(0x7C, unchecked((ushort)velocity.Z)));
    }

    private static NativeBattleEffect31Frame AdvanceFlight(NativeBattleEffect31Frame frame, uint address, NativeBattleEffect31Resources resources, NativeBattleEffect31Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        switch (NativePhase(task, 3))
        {
            case 0:
                return unchecked((short)task.ReadWord(0x24)) >= 15 ? IncrementPhase(frame, address) : frame;
            case 1:
                frame = InitializeProgramme(frame, address, resources);
                (frame, _) = CasterMatrix(frame, address, 0x872B80, 0x19C, 32, inputs);
                frame = UpdatePoint(frame, address + 0x1A4, Read(frame, address).ReadByte(0x2D), inputs);
                return IncrementPhase(WriteBytes(frame, address + 0x1C, ReadBytes(frame, address + 0x19C, 8), 8), address);
            case 2:
                break;
            default:
                return frame;
        }
        frame = WriteBytes(frame, address + 0x194, ReadBytes(frame, address + 0x1C, 8), 8);
        task = Read(frame, address);
        int index = unchecked((short)task.ReadWord(0x1AE));
        short factor = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(unchecked(0x160D3B4u + (uint)(index * 2)), 2));
        short lift = BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(unchecked(0x160D3C8u + (uint)(index * 2)), 2));
        task = task.WriteWord(0x1AC, unchecked((ushort)factor));
        if (factor >= 4096)
        {
            frame = IncrementPhase(MarkComplete(Write(frame, address, task), address), address);
            task = Read(frame, address);
        }
        for (int axis = 0; axis < 3; axis++)
        {
            int start = unchecked((short)task.ReadWord(0x19C + axis * 2));
            int end = unchecked((short)task.ReadWord(0x1A4 + axis * 2));
            int value = start + unchecked((end - start) * factor) / 4096 - (axis == 1 ? lift : 0);
            task = task.WriteWord(0x1C + axis * 2, unchecked((ushort)value));
        }
        frame = Write(frame, address, task.WriteWord(0x1AE, unchecked((ushort)(index + 1))));
        NativeBattlePackedMatrix32 world = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0);
        world = Translation(world, Point(ReadBytes(frame, address + 0x1C, 8)));
        short yaw = Word31(inputs.ReadPhysical(frame, ModelAddress(Caster(frame)), 0x10), 0x0E);
        (frame, world) = Rotate(frame, world, 'Y', yaw);
        frame = Observe(frame, address, 0x872CB0, 0x872DF3, world, inputs);
        if ((Read(frame, address).ReadByte(0x26) & 1) == 0)
        {
            (frame, _) = AdvanceProgramme(frame, address, world, resources, inputs);
        }
        for (int childIndex = 0; childIndex < 4; childIndex++)
        {
            (frame, uint child) = AppendHierarchy(frame, 5, childIndex < 2 ? 0x873010u : 0x8730C0u, 0x80, address, inputs);
            NativeBattleEffectArena position = ReadBytes(frame, address + 0x1C, 8);
            if ((childIndex & 1) == 0)
            {
                frame = WriteBytes(frame, child + 0x1C, position, 8);
            }
            else
            {
                NativeBattleEffectArena old = ReadBytes(frame, address + 0x194, 8);
                NativeBattleEffectArena midpoint = NativeBattleEffectArena.Clear();
                for (int axis = 0; axis < 3; axis++)
                {
                    midpoint = Word(midpoint, axis * 2, unchecked((short)((Word31(position, axis * 2) + Word31(old, axis * 2)) / 2)));
                }
                frame = WriteBytes(frame, child + 0x1C, midpoint, 6);
            }
            if (childIndex < 2)
            {
                (frame, int random) = Random(frame);
                NativeBattleModulePoolTask particle = Read(frame, child);
                frame = Write(frame, child, particle.WriteWord(0x1E, unchecked((ushort)(particle.ReadWord(0x1E) + (random & 255) - 128))));
            }
            else
            {
                frame = Jitter(frame, child, 128);
            }
        }
        return frame;
    }

    private static NativeBattleEffect31Frame Jitter(NativeBattleEffect31Frame frame, uint address, short radius)
    {
        if (radius == 0)
        {
            radius = 1;
        }
        (frame, int yaw) = Random(frame);
        (frame, int pitch) = Random(frame);
        NativeBattlePackedMatrix32 matrix = NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0);
        (frame, matrix) = Rotate(frame, matrix, 'Y', unchecked((short)(yaw & 0xFFF)));
        (frame, matrix) = Rotate(frame, matrix, 'X', unchecked((short)(pitch & 0xFFF)));
        (frame, int random) = Random(frame);
        NativeBattleVector offset = RotateShort(matrix, new(0, 0, unchecked((short)(random % radius))));
        return AddPoint(frame, address + 0x1C, offset);
    }

    private static (NativeBattleEffect31Frame Frame, int Value) Random(NativeBattleEffect31Frame frame)
    {
        NativeCrtRandomState random = frame.Random.Draw(out ushort value);
        return (frame with { Random = random }, value);
    }

    private static int Caster(NativeBattleEffect31Frame frame) => Parameters(frame).Actor78;
    private static uint ModelAddress(int actor) => checked(0x1D972C0u + (uint)actor * 0x9C);
    private static NativeBattleVector Point(NativeBattleEffectArena point) => new(Word31(point, 0), Word31(point, 2), Word31(point, 4));
    private static NativeBattleEffectArena Word(NativeBattleEffectArena arena, int at, short value) => arena.Write(at, BitConverter.GetBytes(value));
    private static NativeBattleEffectArena Dword(NativeBattleEffectArena arena, int at, uint value) => arena.Write(at, BitConverter.GetBytes(value));
    private static NativeBattleEffect31Frame AddPoint(NativeBattleEffect31Frame frame, uint address, NativeBattleVector offset)
    {
        NativeBattleEffectArena point = ReadBytes(frame, address, 8);
        point = Word(Word(Word(point, 0, unchecked((short)(Word31(point, 0) + offset.X))), 2, unchecked((short)(Word31(point, 2) + offset.Y))), 4, unchecked((short)(Word31(point, 4) + offset.Z)));
        return WriteBytes(frame, address, point, 6);
    }
}
