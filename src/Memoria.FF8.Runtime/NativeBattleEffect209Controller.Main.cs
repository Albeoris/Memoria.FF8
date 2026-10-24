using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect209Controller
{
    public static NativeBattleEffect209Step AdvanceMain6375D0(NativeBattleEffect209Frame frame, uint address, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs) => AdvanceMain(frame, address, false, inputs);
    public static NativeBattleEffect209Step AdvanceAlternate638BD0(NativeBattleEffect209Frame frame, uint address, NativeBattleEffect209Resources resources, NativeBattleEffect209Inputs inputs) => AdvanceMain(frame, address, true, inputs);

    private static NativeBattleEffect209Step AdvanceMain(NativeBattleEffect209Frame frame, uint address, bool alternate, NativeBattleEffect209Inputs inputs)
    {
        uint flags = inputs.ReadFlags(frame);
        if ((flags & 0x201) != 0)
        {
            if ((flags & 1) != 0)
            {
                return new(frame, 0);
            }
            NativeBattleEffect209Step ready = inputs.ReadCurrentStep508500(frame);
            frame = ready.Frame;
            if (ready.Result < 0)
            {
                return new(frame, 0);
            }
        }
        int age = Signed(Read(frame, address), 12);
        int caster = checked((int)frame.State.ReadDword(CasterGlobal));
        if (age == 0)
        {
            NativeBattleEffect209PointStep anchor = inputs.Anchor571400(frame, caster, TaskView(Read(frame, address), 16, 8));
            frame = anchor.Frame;
            frame = WriteBytes(frame, address + 16, anchor.Output8, 8);
            frame = Write(frame, address, Read(frame, address).WriteWord(18, unchecked((ushort)(Read(frame, address).ReadWord(18) - 1800))));
            frame = frame with { State = frame.State.WriteDword(SavedColor40Global, inputs.ReadColor(frame, 0xB8B7D8)).WriteDword(SavedColor44Global, inputs.ReadColor(frame, 0xB8B9A8)) };
            if (alternate)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect209Vibration4A29A0(VibrationAddress, 1, 128, VibrationGlobal));
            }
        }
        if (age == 2)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209QueueVariant50E3A0(frame.State.ReadDword(GuardianGlobal)));
        }
        if (age == 52)
        {
            NativeBattleEffect209Step ready = inputs.ReadCurrentStep508500(frame);
            frame = ready.Frame;
            if (ready.Result < 0)
            {
                return new(frame, 0);
            }
            frame = inputs.Execute(frame, new NativeBattleEffect209RebuildVariant50E3C0(frame.State.ReadDword(GuardianGlobal)));
            frame = frame with { State = frame.State.WriteDword(StageAfterColor40Global, inputs.ReadColor(frame, 0xB8B7D8)).WriteDword(StageAfterColor44Global, inputs.ReadColor(frame, 0xB8B9A8)) };
        }
        if (age == 16)
        {
            (frame, uint colorTask) = RequireAppend(frame, 0x637A40);
            frame = Write(frame, colorTask, Read(frame, colorTask).WriteWord(12, 0));
        }
        if (age == 0)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209ModelFlags(caster, 0xFF, 8, false));
        }
        if (age == (alternate ? 65 : 20))
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209ModelFlags(caster, 0xFFF7, 0, true));
        }
        if (age == 1)
        {
            frame = BirthOpening(frame, address, alternate);
        }
        bool sprite = alternate ? age >= 70 && age <= 90 && age % 6 == 1 : age >= 30 && age <= 100 && age % 10 == 1;
        if (sprite)
        {
            frame = BirthSprite(frame, caster, inputs);
        }
        bool burst = alternate ? age >= 93 && age <= 110 && age % 6 == 0 : age == 115;
        if (burst)
        {
            frame = BirthBurst(frame, caster, alternate, inputs);
        }
        if (alternate)
        {
            if (age is 22 or 28 or 50)
            {
                frame = inputs.Execute(frame, new NativeBattleEffect209Flash5712C0(0, 2, age == 50 ? 8 : 4, 255));
            }
            if (age >= 93 && age < 101)
            {
                int shift = unchecked(NativeBattleCameraMath.Sine(((age - 48) * 1024) / 8 + 1024) * 60) >> 12;
                if ((age & 1) == 0)
                {
                    shift = -shift;
                }
                frame = inputs.Execute(frame, new NativeBattleEffect209SetViewTranslationY(unchecked((short)shift)));
            }
        }
        if (age < 16)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209Stage5713E0(unchecked(age * 96), 0));
        }
        else if (age >= 122)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209Stage5713E0(unchecked((130 - age) * 192), 0));
        }
        if (age == 124)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Effect209 requires its current header.");
            NativeBattleActionHeaderSlot header = inputs.ReadPackets(frame).Headers[parameters.HeaderIndex7C];
            frame = inputs.Execute(frame, new NativeBattleEffect209Contact506690(header.FirstContactIndex ?? throw new NotSupportedException("Effect209 requires its current first contact.")));
        }
        if (age == 1)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209Sound501330(alternate ? AlternateSoundAddress : SoundAddress, 0, 128));
        }
        if (alternate && age == 127)
        {
            int handle = unchecked((int)frame.State.ReadDword(VibrationGlobal));
            if (inputs.ReadVibrationPlaying4A2900(frame, handle))
            {
                frame = inputs.Execute(frame, new NativeBattleEffect209StopVibration4A2940(handle));
            }
        }
        short next = unchecked((short)(Read(frame, address).ReadWord(12) + 1));
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)next)));
        if (next > 130)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect209Stage5713E0(0, 0));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    public static NativeBattleEffect209Frame BirthOpening(NativeBattleEffect209Frame frame, uint main, bool alternate)
    {
        (frame, uint ring) = RequireAppend(frame, 0x638490);
        frame = WriteBytes(frame, ring + 16, TaskView(Read(frame, main), 16, 8), 8);
        frame = Write(frame, ring, Read(frame, ring).WriteWord(12, 0).WriteWord(28, 0x3AA).WriteWord(30, 0x3AA));
        int angle = Random(ref frame) % 4096;
        for (int index = 0; index < 12; index++)
        {
            (frame, uint star) = RequireAppend(frame, 0x6385E0);
            frame = WriteBytes(frame, star + 16, TaskView(Read(frame, main), 16, 8), 8);
            NativeBattleModulePoolTask task = Read(frame, star).WriteWord(12, 0).WriteWord(14, 0).WriteWord(22, alternate ? (ushort)1 : (ushort)0).WriteWord(24, (ushort)angle).WriteWord(26, 30).WriteWord(28, 256).WriteWord(30, 256);
            int scale = (Random(ref frame) % 1024 + 3072) / 6;
            frame = Write(frame, star, task.WriteWord(32, (ushort)scale).WriteWord(34, (ushort)scale));
        }
        if (!alternate)
        {
            (frame, uint mesh) = RequireAppend(frame, 0x638770);
            frame = WriteBytes(frame, mesh + 16, TaskView(Read(frame, main), 16, 8), 8);
            frame = Write(frame, mesh, Read(frame, mesh).WriteWord(12, 0).WriteWord(28, 0x1800));
        }
        return frame;
    }

    public static NativeBattleEffect209Frame BirthSprite(NativeBattleEffect209Frame frame, int caster, NativeBattleEffect209Inputs inputs)
    {
        (frame, uint address) = RequireAppend(frame, 0x638900);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0));
        NativeBattleEffect209PointStep point = inputs.Point502170(frame, caster, 0xF1, 0, TaskView(Read(frame, address), 16, 8));
        frame = WriteBytes(point.Frame, address + 16, point.Output8, 8);
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(18, unchecked((ushort)(Read(frame, address).ReadWord(18) + Random(ref frame) % 600)));
        task = task.WriteWord(24, (ushort)(Random(ref frame) % 2048));
        task = task.WriteWord(26, unchecked((ushort)(-40 - Random(ref frame) % 120)));
        int first = (Random(ref frame) % 2048 + 3584) / 8;
        int second = (Random(ref frame) % 2048 + 1536) / 8;
        task = task.WriteWord(28, (ushort)first).WriteWord(30, (ushort)first).WriteWord(32, (ushort)second).WriteWord(34, (ushort)second);
        return Write(frame, address, task);
    }

    public static NativeBattleEffect209Frame BirthBurst(NativeBattleEffect209Frame frame, int caster, bool alternate, NativeBattleEffect209Inputs inputs)
    {
        (frame, uint address) = RequireAppend(frame, 0x638A80);
        frame = Write(frame, address, Read(frame, address).WriteWord(12, 0));
        NativeBattleEffect209PointStep point = inputs.Point502170(frame, caster, 0xF1, 0, TaskView(Read(frame, address), 16, 8));
        frame = WriteBytes(point.Frame, address + 16, point.Output8, 8);
        NativeBattleModulePoolTask task = Read(frame, address).WriteWord(18, 0).WriteWord(24, (ushort)(Random(ref frame) % 4096));
        int speed = Random(ref frame) % 40;
        task = task.WriteWord(26, (ushort)(alternate ? 100 + speed * 2 : 50 + speed));
        int scale = Random(ref frame) % 1024 + 8192;
        scale = alternate ? scale * 3 / 12 : scale / 12;
        return Write(frame, address, task.WriteWord(28, (ushort)scale).WriteWord(30, (ushort)scale));
    }

    private static (NativeBattleEffect209Frame Frame, uint Address) RequireAppend(NativeBattleEffect209Frame frame, uint callback)
    {
        (frame, uint address) = Append(frame, ChildDescriptor, callback);
        return address == 0 ? throw new InvalidDataException("Effect209 required child allocation failed.") : (frame, address);
    }

    private static int Random(ref NativeBattleEffect209Frame frame)
    {
        frame = frame with { Random = frame.Random.Draw(out ushort value) };
        return value;
    }
}
