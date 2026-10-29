using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleCannonBlow151Controller
{
    public static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceSlots(NativeBattleCannonBlow151Frame frame, uint address, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs) => AdvanceParticles151(frame, address, Read(frame, address).Callback switch { 0x60D3B0 => 0, 0x60D850 => 1, 0x60DA10 => 2, 0x60DC20 => 3, _ => throw new NotSupportedException("Cannon Blow particle callback is unsupported.") }, resources, inputs);

    public static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceSprite(NativeBattleCannonBlow151Frame frame, uint address, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (task.Callback == 0x60E1B0)
        {
            return AdvanceImpactSprite151(frame, address, inputs);
        }
        if (task.Callback != 0x60D790)
        {
            throw new NotSupportedException("Cannon Blow sprite callback is unsupported.");
        }
        uint point = SampleAddress151(task, Signed(task, 0x18));
        short size = Signed(task, 0x1C);
        frame = SetupSprite151(frame, inputs, point, size, unchecked((short)-(size >> 3)));
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xDBF2A0u)).Write(4, BitConverter.GetBytes((ushort)(task.ReadWord(12) & 3))).Write(0x24, new byte[2]);
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleCannonBlow151DrawSprite180(temporary, 0xDBF2A0) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(151, task) with { ResourceGeneration = 0xDBF2A0 } });
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        short speed = Signed(task, 0x1A);
        task = task.WriteWord(0x18, unchecked((ushort)(task.ReadWord(0x18) + speed)));
        if (speed > 2)
        {
            task = task.WriteWord(0x1A, unchecked((ushort)(speed - 1)));
        }
        task = task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1)));
        return (Write(frame, address, task), Signed(task, 0x18) >= 32 ? 2 : 0);
    }

    private static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceParticles151(NativeBattleCannonBlow151Frame frame, uint address, int kind, NativeBattleCannonBlow151Resources resources, NativeBattleCannonBlow151Inputs inputs)
    {
        uint bank = kind switch { 1 => 0x244D6A8, 2 => 0x244D6A8, _ => 0x244E008 };
        int count = 100;
        uint source = kind switch { 0 => 0xDBF314, 1 => 0xDBEF48, 2 => 0xDBF0F4, _ => 0xDBF314 };
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int survivors = 0;
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, BitConverter.GetBytes(kind == 2 ? (ushort)8 : (ushort)0)), 180);
        for (int index = 0; index < count; index++)
        {
            uint record = checked(bank + (uint)(index * 24));
            NativeBattleModulePoolTask task = Read(frame, address);
            uint mask = unchecked((uint)(int)Signed(task, 0x18));
            if ((frame.State.ReadDword(record) & mask) == 0)
            {
                continue;
            }
            short size = ParticleWord151(frame, record + 6);
            NativeBattleEffectArena context = Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(frame.State.ReadWord(record + 4)));
            frame = PublishContext(frame, temporary, context, 180);
            if (kind == 0)
            {
                NativeBattleActionMatrix matrix = ReadRetainedMatrix151(frame, MatrixAddress151(task)).ReadMatrix();
                NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(matrix, ParticlePoint151(frame, record + 8));
                point = new(unchecked((short)(point.X + matrix.X)), unchecked((short)(point.Y + matrix.Y)), unchecked((short)(point.Z + matrix.Z)));
                frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), point, null, size, unchecked((short)-(size >> 4))) };
            }
            else
            {
                frame = SetupSprite151(frame, inputs, record + 8, size, unchecked((short)-(size >> (kind == 3 ? 3 : 4))));
            }
            frame = inputs.Execute(frame, new NativeBattleCannonBlow151DrawSprite180(temporary, source) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(151, task, kind * 100 + index) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(151, frame.State.Globals, record, 0).Generation } });
            if (paused)
            {
                continue;
            }
            frame = PutWord151(frame, record + 4, ParticleWord151(frame, record + 4) + 1);
            if (BinaryPrimitives.ReadInt16LittleEndian(RequireStaging(frame).Read(temporary + 0x28, 2)) < 0)
            {
                frame = PutDword151(frame, record, 0);
                continue;
            }
            if (kind == 0)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    uint velocity = record + 16 + (uint)(axis * 2);
                    frame = PutWord151(frame, record + 8 + (uint)(axis * 2), ParticleWord151(frame, record + 8 + (uint)(axis * 2)) + ParticleWord151(frame, velocity));
                    int speed = ParticleWord151(frame, velocity);
                    frame = PutWord151(frame, velocity, speed - (speed >> (axis == 2 ? 4 : 2)));
                }
            }
            else if (kind != 0)
            {
                frame = PutWord151(frame, record + 6, size + (size >> 5));
            }
            survivors++;
        }
        frame = Release(frame, 180);
        if (paused)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        if (kind != 0)
        {
            short speed = Signed(current, 0x1E);
            current = current.WriteWord(0x1C, unchecked((ushort)(current.ReadWord(0x1C) + speed)));
            if (speed > 2)
            {
                current = current.WriteWord(0x1E, unchecked((ushort)(speed - 1)));
            }
            frame = Write(frame, address, current);
            int sample = Signed(current, 0x1C);
            int lower = sample - Signed(current, 0x1E);
            if (sample < 32)
            {
                for (; sample >= lower; sample--)
                {
                    uint record = FindFree151(frame, bank, count);
                    if (record == 0)
                    {
                        break;
                    }
                    int repetitions = kind == 3 ? 2 : 1;
                    for (int repeat = 0; repeat < repetitions; repeat++)
                    {
                        if (repeat != 0)
                        {
                            record = FindFree151(frame, bank, count);
                            if (record == 0)
                            {
                                break;
                            }
                        }
                        frame = BirthHeader151(frame, record, current);
                        frame = DrawRandom(frame, out ushort size);
                        frame = PutWord151(frame, record + 6, kind == 3 ? (size & 2047) + 1024 : size % 0x700 + 0xB00);
                        frame = frame with { State = frame.State.WriteView(record + 8, frame.State.ReadView(SampleAddress151(current, sample), 8), 8) };
                        if (kind == 2 || kind == 3)
                        {
                            for (int axis = 0; axis < 3; axis++)
                            {
                                frame = DrawRandom(frame, out ushort jitter);
                                uint component = record + 8 + (uint)(axis * 2);
                                frame = PutWord151(frame, component, ParticleWord151(frame, component) + (kind == 3 ? jitter % 300 - 150 : jitter % 200 - 100));
                            }
                        }
                    }
                }
            }
        }
        else if (Signed(current, 12) < 2)
        {
            for (int birth = 0; birth < 12; birth++)
            {
                uint record = FindFree151(frame, bank, count);
                if (record == 0)
                {
                    break;
                }
                frame = BirthHeader151(frame, record, current);
                frame = DrawRandom(frame, out ushort size);
                frame = PutWord151(frame, record + 6, (size & 2047) + 1024);
                frame = DrawRandom(frame, out ushort x);
                frame = PutWord151(frame, record + 8, x % 200 - 100);
                frame = DrawRandom(frame, out ushort y);
                frame = PutWord151(frame, record + 10, y % 200 - 100);
                frame = DrawRandom(frame, out ushort z);
                frame = PutWord151(frame, record + 12, z % 100);
                frame = DrawRandom(frame, out ushort angle);
                frame = DrawRandom(frame, out ushort speed);
                int radius = speed % 110 + 90;
                frame = PutWord151(frame, record + 16, unchecked(NativeBattleCameraMath.Cosine(angle & 4095) * radius) >> 12);
                frame = PutWord151(frame, record + 18, unchecked(NativeBattleCameraMath.Sine(angle & 4095) * radius) >> 12);
                frame = DrawRandom(frame, out ushort velocityZ);
                frame = PutWord151(frame, record + 20, velocityZ % 40 + 20);
            }
        }
        current = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, current), Signed(current, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static (NativeBattleCannonBlow151Frame Frame, int Result) AdvanceImpactSprite151(NativeBattleCannonBlow151Frame frame, uint address, NativeBattleCannonBlow151Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        if (Signed(task, 14) > 0)
        {
            if ((inputs.GraphicsFlags & 0x201) == 0)
            {
                frame = Write(frame, address, task.WriteWord(14, unchecked((ushort)(task.ReadWord(14) - 1))));
            }
            return (frame, 0);
        }
        short size = Signed(task, 0x1C);
        NativeBattleEffectArena point = TaskView(task, 0x10, 8);
        (byte[] wBytes, bool[] wKnown) = point.ReadSnapshot(6, 2);
        frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), new(BinaryPrimitives.ReadInt16LittleEndian(point.Read(0, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(2, 2)), BinaryPrimitives.ReadInt16LittleEndian(point.Read(4, 2))), wKnown.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(wBytes) : null, size, unchecked((short)-(size >> 4))) };
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xDBED9Cu)).Write(4, BitConverter.GetBytes(task.ReadWord(12))).Write(0x24, new byte[2]), 180);
        frame = inputs.Execute(frame, new NativeBattleCannonBlow151DrawSprite180(temporary, 0xDBED9C) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(151, task) with { ResourceGeneration = 0xDBED9C } });
        frame = Release(frame, 180);
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        task = Read(frame, address);
        frame = Write(frame, address, task.WriteWord(12, unchecked((ushort)(task.ReadWord(12) + 1))));
        return (frame, BinaryPrimitives.ReadInt16LittleEndian(Context(frame, temporary, 180).Read(0x28, 2)) < 0 ? 2 : 0);
    }

    private static NativeBattleCannonBlow151Frame SetupSprite151(NativeBattleCannonBlow151Frame frame, NativeBattleCannonBlow151Inputs inputs, uint point, short size, short depth)
    {
        NativeBattleEffectArena view = frame.State.ReadView(point, 8);
        (byte[] bytes, bool[] known) = view.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), ParticlePoint151(frame, point), w, size, depth) };
    }
    private static uint SampleAddress151(NativeBattleModulePoolTask task, int sample) => checked((uint)(0x244F958 + (Signed(task, 14) * 32 + sample) * 8));
    private static short ParticleWord151(NativeBattleCannonBlow151Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleVector ParticlePoint151(NativeBattleCannonBlow151Frame frame, uint address) => new(ParticleWord151(frame, address), ParticleWord151(frame, address + 2), ParticleWord151(frame, address + 4));
    private static NativeBattleCannonBlow151Frame PutWord151(NativeBattleCannonBlow151Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleCannonBlow151Frame PutDword151(NativeBattleCannonBlow151Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleCannonBlow151Frame BirthHeader151(NativeBattleCannonBlow151Frame frame, uint record, NativeBattleModulePoolTask task)
    {
        frame = PutWord151(PutDword151(frame, record, unchecked((uint)(int)Signed(task, 0x18))), record + 4, 0);
        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, record, 0);
        return frame;
    }
    private static uint FindFree151(NativeBattleCannonBlow151Frame frame, uint bank, int count)
    {
        for (int index = 0; index < count; index++)
        {
            uint address = checked(bank + (uint)(index * 24));
            if (frame.State.ReadDword(address) == 0)
            {
                return address;
            }
        }
        return 0;
    }
}
