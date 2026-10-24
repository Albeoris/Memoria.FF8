using static Memoria.FF8.Runtime.NativeBattleEffect316Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect316Controller
{
    public static NativeBattleEffect316Step AdvanceRoot661190(NativeBattleEffect316Frame frame, uint address, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        bool toggle = task.ReadDword(0x10) != 0;
        uint work = frame.State.ReadDword(WorkGlobal);
        frame = frame with { State = frame.State.WriteDword(CursorGlobal, checked(work + (uint)(toggle ? FirstPacketOffset : SecondPacketOffset))).WriteDword(AmplitudeGlobal, 0) };
        frame = Write(frame, address, task.WriteDword(0x10, toggle ? 0u : 1u));
        task = Read(frame, address);
        if (task.ReadWord(0xC) == 2 && task.ReadByte(0xF) == 0)
        {
            frame = Write(frame, address, task.WriteByte(0xF, 1));
            frame = inputs.Execute(frame, new NativeBattleEffect316Music47E3C0(0xF0));
            frame = InitializeMainBank(frame);
            uint main;
            (frame, main) = Append(frame, MainDescriptor, MainCallback);
            if (main == 0)
            {
                throw new InvalidDataException("Effect316 exhausted its mandatory main task.");
            }
            frame = ClearDwords6DA380(frame, main + 0xC, 0x204);
            frame = inputs.Execute(frame, new NativeBattleEffect316Vibration4A29A0(VibrationAddress, 1, 0x80, main + 0x10));
            uint model = frame.State.ReadDword(CasterGlobal);
            uint color = inputs.ReadModel(frame, model).Color44 ?? throw new NotSupportedException("Effect316 requires its current raw caster Color44.");
            task = Read(frame, main).WriteDword(0x14, color).WriteDword(RecordHintOffset, main + RecordOffset);
            frame = Write(frame, main, task);
            frame = inputs.Execute(frame, new NativeBattleEffect316ClearGeometryMarkers6612C0(model));
        }
        int survivors = 1;
        if (Read(frame, address).ReadByte(0xF) != 0)
        {
            (frame, survivors) = AdvancePool(frame, 1, resources, inputs);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect316Stage5713E0(unchecked((int)frame.State.ReadDword(AmplitudeGlobal)), 0));
        if (Read(frame, address).ReadByte(0xF) != 0 && survivors == 0)
        {
            return new(frame, 2);
        }
        task = Read(frame, address);
        return new(Write(frame, address, task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)))), 0);
    }

    public static NativeBattleEffect316Step AdvanceMain661350(NativeBattleEffect316Frame frame, uint address, NativeBattleEffect316Resources resources, NativeBattleEffect316Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        short age = Signed(task, 0xC);
        uint model = frame.State.ReadDword(CasterGlobal);
        if (age >= 0x138)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect316ModelFlags(model, 2, 0xFFF7, 0));
            frame = inputs.Execute(frame, new NativeBattleEffect316WriteModel(model, 0x2C, TaskView(task, 0x14, 4), 4));
            for (int physical = 0; physical < 3; physical++)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect316ModelFlags(ModelAddress(physical), 2, 0xFFFB, 0));
            }
            return new(frame, 2);
        }
        frame = frame with { State = frame.State.WriteDword(AmplitudeGlobal, unchecked((uint)Amplitude(age))) };
        uint firstPhase = unchecked((uint)(age - 20));
        if (firstPhase < 130)
        {
            int scale = firstPhase < 100 ? FirstScale(firstPhase) : -3488;
            frame = inputs.Execute(frame, new NativeBattleEffect316Draw661550(model, scale, 11, 0x414));
            frame = inputs.Execute(frame, new NativeBattleEffect316ModelFlags(model, 1, byte.MaxValue, 4));
        }
        uint secondPhase = unchecked((uint)(age - 150));
        if (secondPhase < 150)
        {
            uint projected = checked(frame.State.ReadDword(WorkGlobal) + ProjectedRowsOffset);
            frame = inputs.Execute(frame, new NativeBattleEffect316Draw661CC0(address, model, projected, SecondScale(secondPhase), 11, 0x414));
            frame = inputs.Execute(frame, new NativeBattleEffect316ModelFlags(model, 1, byte.MaxValue, 4));
        }
        if (secondPhase < 140)
        {
            int fraction = ColorFraction(secondPhase);
            frame = inputs.Execute(frame, new NativeBattleEffect316Blend56CC00(address + 0x14, ColorGlobal, 0x1000 - fraction, fraction, model + 0x2C));
            frame = inputs.Execute(frame, new NativeBattleEffect316WriteModel(model, 0x2F, NativeBattleEffectArena.Clear().Write(0, [0x32]), 1));
        }
        if (age >= 60)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect316ModelFlags(model, 1, byte.MaxValue, 8));
        }
        frame = inputs.Execute(frame, new NativeBattleEffect316AdvanceRecords662720(address));
        if (age == 45)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect316Sound501330(SoundAddress, 0, 0x80));
        }
        else if (age == 0x134)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect316StopVibration4A2940(unchecked((int)Read(frame, address).ReadDword(0x10))));
        }
        return new(Write(frame, address, Read(frame, address).WriteWord(0xC, unchecked((ushort)(age + 1)))), 0);
    }

    public static int Amplitude(short age)
    {
        int value = age < 8 ? age : age > 0x130 ? 0x138 - age : 8;
        int product = unchecked(value * 125);
        return unchecked(product << 3) >> 3;
    }

    public static int FirstScale(uint phase) => unchecked(512 - (int)(MultiplyHigh(unchecked(phase * 4000), 0x51EB851F) >> 5));
    public static int SecondScale(uint phase) => unchecked(512 - (int)(MultiplyHigh(unchecked(0x927C0u - phase * 4000), 0x1B4E81B5) >> 4));

    public static int ColorFraction(uint phase)
    {
        uint value = unchecked(phase << 12);
        uint high = MultiplyHigh(value, 0xD41D41D5);
        return unchecked((int)((((value - high) >> 1) + high) >> 7));
    }

    private static uint MultiplyHigh(uint left, uint right) => (uint)(((ulong)left * right) >> 32);
}
