using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWildCannonBlow156Controller;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleWildCannonBlow156Children
{
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance605F40(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs) => AdvanceParticles(frame, address, 0, resources, inputs);
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance6063E0(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs) => AdvanceParticles(frame, address, 1, resources, inputs);
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance6065A0(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs) => AdvanceParticles(frame, address, 2, resources, inputs);
    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance606A60(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs) => AdvanceParticles(frame, address, 3, resources, inputs);

    public static (NativeBattleWildCannonBlow156Frame Frame, int Result) Advance606320(NativeBattleWildCannonBlow156Frame frame, uint address, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        uint point = SampleAddress(task, Signed(task, 0x18));
        short size = Signed(task, 0x1C);
        frame = SetupSprite(frame, inputs, point, size, unchecked((short)-(size >> 3)));
        (frame, uint temporary) = Allocate(frame, 180);
        NativeBattleEffectArena context = Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(0xDB528Cu)).Write(4, BitConverter.GetBytes((ushort)(task.ReadWord(12) & 3))).Write(0x24, new byte[2]);
        frame = PublishContext(frame, temporary, context, 180);
        frame = inputs.Execute(frame, new NativeBattleWildCannonBlow156DrawSprite180(temporary, 0xDB528C) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(156, task) with { ResourceGeneration = 0xDB528C } });
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

    private static (NativeBattleWildCannonBlow156Frame Frame, int Result) AdvanceParticles(NativeBattleWildCannonBlow156Frame frame, uint address, int kind, NativeBattleWildCannonBlow156Resources resources, NativeBattleWildCannonBlow156Inputs inputs)
    {
        uint bank = kind switch { 1 => 0x23ECE78, 2 => 0x23EDC88, _ => 0x23EEA98 };
        int count = kind == 1 || kind == 2 ? 150 : 100;
        uint source = kind switch { 0 => 0xDB5300, 1 => 0xDB4F34, 2 => 0xDB50E0, _ => 0xDB4D88 };
        bool paused = (inputs.GraphicsFlags & 0x201) != 0;
        int survivors = 0;
        (frame, uint temporary) = Allocate(frame, 180);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 180).Write(0, BitConverter.GetBytes(source)).Write(0x24, new byte[2]), 180);
        for (int index = 0; index < count; index++)
        {
            uint record = checked(bank + (uint)(index * 24));
            NativeBattleModulePoolTask task = Read(frame, address);
            uint mask = unchecked((uint)(int)Signed(task, 0x18));
            if ((frame.State.ReadDword(record) & mask) == 0)
            {
                continue;
            }
            short size = Word(frame, record + 6);
            NativeBattleEffectArena context = Context(frame, temporary, 180).Write(4, BitConverter.GetBytes(frame.State.ReadWord(record + 4)));
            frame = PublishContext(frame, temporary, context, 180);
            if (kind == 0)
            {
                NativeBattleActionMatrix matrix = ReadRetainedMatrix(frame, MatrixAddress(task)).ReadMatrix();
                NativeBattleVector point = NativeBattleGfMatrices.RotateShort56C4F0(matrix, Point(frame, record + 8));
                point = new(unchecked((short)(point.X + matrix.X)), unchecked((short)(point.Y + matrix.Y)), unchecked((short)(point.Z + matrix.Z)));
                frame = frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), point, null, size, unchecked((short)-(size >> 4))) };
            }
            else
            {
                frame = SetupSprite(frame, inputs, record + 8, size, unchecked((short)-(size >> 4)));
            }
            frame = inputs.Execute(frame, new NativeBattleWildCannonBlow156DrawSprite180(temporary, source) { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(156, task, kind * 150 + index) with { ResourceGeneration = NativeBattlePilotVisualLifetimes.Identity(156, frame.State.Globals, record, 0).Generation } });
            if (paused)
            {
                continue;
            }
            frame = PutWord(frame, record + 4, Word(frame, record + 4) + 1);
            if (BinaryPrimitives.ReadInt16LittleEndian(RequireStaging(frame).Read(temporary + 0x28, 2)) < 0)
            {
                frame = PutDword(frame, record, 0);
                continue;
            }
            if (kind == 0)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    uint velocity = record + 16 + (uint)(axis * 2);
                    frame = PutWord(frame, record + 8 + (uint)(axis * 2), Word(frame, record + 8 + (uint)(axis * 2)) + Word(frame, velocity));
                    int speed = Word(frame, velocity);
                    frame = PutWord(frame, velocity, speed - (speed >> (axis == 2 ? 4 : 2)));
                }
            }
            else if (kind == 1 || kind == 2)
            {
                frame = PutWord(frame, record + 6, size + (size >> 5));
            }
            survivors++;
        }
        frame = Release(frame, 180);
        if (paused)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask current = Read(frame, address);
        if (kind == 1 || kind == 2)
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
                    uint record = FindFree(frame, bank, count);
                    if (record == 0)
                    {
                        break;
                    }
                    frame = BirthHeader(frame, record, current);
                    frame = DrawRandom(frame, out ushort size);
                    frame = PutWord(frame, record + 6, size % 0x700 + 0xB00);
                    frame = frame with { State = frame.State.WriteView(record + 8, frame.State.ReadView(SampleAddress(current, sample), 8), 8) };
                    if (kind == 2)
                    {
                        for (int axis = 0; axis < 3; axis++)
                        {
                            frame = DrawRandom(frame, out ushort jitter);
                            uint component = record + 8 + (uint)(axis * 2);
                            frame = PutWord(frame, component, Word(frame, component) + jitter % 200 - 100);
                        }
                    }
                }
            }
        }
        else if (kind == 0 ? Signed(current, 12) < 2 : Signed(current, 12) >= 0 && Signed(current, 12) <= 2)
        {
            int births = kind == 0 ? 12 : 1;
            for (int birth = 0; birth < births; birth++)
            {
                uint record = FindFree(frame, bank, count);
                if (record == 0)
                {
                    break;
                }
                frame = BirthHeader(frame, record, current);
                frame = DrawRandom(frame, out ushort size);
                frame = PutWord(frame, record + 6, kind == 0 ? (size & 2047) + 1024 : size % 1280 + 1280);
                if (kind == 0)
                {
                    frame = DrawRandom(frame, out ushort x);
                    frame = PutWord(frame, record + 8, x % 200 - 100);
                    frame = DrawRandom(frame, out ushort y);
                    frame = PutWord(frame, record + 10, y % 200 - 100);
                    frame = DrawRandom(frame, out ushort z);
                    frame = PutWord(frame, record + 12, z % 100);
                    frame = DrawRandom(frame, out ushort angle);
                    frame = DrawRandom(frame, out ushort speed);
                    int radius = speed % 110 + 90;
                    frame = PutWord(frame, record + 16, unchecked(NativeBattleCameraMath.Cosine(angle & 4095) * radius) >> 12);
                    frame = PutWord(frame, record + 18, unchecked(NativeBattleCameraMath.Sine(angle & 4095) * radius) >> 12);
                    frame = DrawRandom(frame, out ushort velocityZ);
                    frame = PutWord(frame, record + 20, velocityZ % 40 + 20);
                }
                else
                {
                    frame = frame with { State = frame.State.WriteView(record + 8, TaskView(current, 0x10, 8), 8) };
                    for (int axis = 0; axis < 3; axis++)
                    {
                        frame = DrawRandom(frame, out ushort jitter);
                        uint component = record + 8 + (uint)(axis * 2);
                        frame = PutWord(frame, component, Word(frame, component) + jitter % 600 - 300);
                    }
                }
            }
        }
        current = Read(frame, address).WriteWord(12, unchecked((ushort)(Read(frame, address).ReadWord(12) + 1)));
        return (Write(frame, address, current), Signed(current, 12) >= 4 && survivors == 0 ? 2 : 0);
    }

    private static NativeBattleWildCannonBlow156Frame SetupSprite(NativeBattleWildCannonBlow156Frame frame, NativeBattleWildCannonBlow156Inputs inputs, uint point, short size, short depth)
    {
        NativeBattleEffectArena view = frame.State.ReadView(point, 8);
        (byte[] bytes, bool[] known) = view.ReadSnapshot(6, 2);
        short? w = known.All(value => value) ? BinaryPrimitives.ReadInt16LittleEndian(bytes) : null;
        return frame with { Memory = NativeBattleModuleSpriteSetup.Setup571BC0(frame.Memory, inputs.ReadView(frame), Point(frame, point), w, size, depth) };
    }
    private static uint SampleAddress(NativeBattleModulePoolTask task, int sample) => checked((uint)(0x23F03E8 + (Signed(task, 14) * 32 + sample) * 8));
    private static short Word(NativeBattleWildCannonBlow156Frame frame, uint address) => unchecked((short)frame.State.ReadWord(address));
    private static NativeBattleVector Point(NativeBattleWildCannonBlow156Frame frame, uint address) => new(Word(frame, address), Word(frame, address + 2), Word(frame, address + 4));
    private static NativeBattleWildCannonBlow156Frame PutWord(NativeBattleWildCannonBlow156Frame frame, uint address, int value) => frame with { State = frame.State.WriteWord(address, unchecked((ushort)value)) };
    private static NativeBattleWildCannonBlow156Frame PutDword(NativeBattleWildCannonBlow156Frame frame, uint address, uint value) => frame with { State = frame.State.WriteDword(address, value) };
    private static NativeBattleWildCannonBlow156Frame BirthHeader(NativeBattleWildCannonBlow156Frame frame, uint record, NativeBattleModulePoolTask task)
    {
        frame = PutWord(PutDword(frame, record, unchecked((uint)(int)Signed(task, 0x18))), record + 4, 0);
        NativeBattlePilotVisualLifetimes.Allocated(frame.State.Globals, record, 0);
        return frame;
    }
    private static uint FindFree(NativeBattleWildCannonBlow156Frame frame, uint bank, int count)
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
