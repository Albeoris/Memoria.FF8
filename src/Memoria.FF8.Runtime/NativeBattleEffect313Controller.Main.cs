using static Memoria.FF8.Runtime.NativeBattleEffect313Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect313Controller
{
    public static NativeBattleEffect313Step AdvanceMain59B180(NativeBattleEffect313Frame frame, uint address, NativeBattleEffect313Resources resources, NativeBattleEffect313Inputs inputs)
    {
        if ((inputs.ReadFlags(frame) & 0x201) != 0)
        {
            return new(frame, 0);
        }
        short age = Signed(Read(frame, address), 12);
        if (age == 0)
        {
            uint model = ModelAddress(checked((int)frame.State.ReadDword(CasterGlobal)));
            NativeBattleEffect313PointStep point = inputs.Point502170(frame, model, 0xF0, 4096, frame.State.ReadView(CenterAddress, 8));
            frame = point.Frame with { State = point.Frame.State.WriteView(CenterAddress, point.Output8, 8) };
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(CurrentCasterMatrix313(frame, inputs), new(0, 200, 50));
            for (int axis = 0; axis < 3; axis++)
            {
                int delta = axis == 0 ? offset.X : axis == 1 ? offset.Y : offset.Z;
                uint field = CenterAddress + (uint)(axis * 2);
                frame = frame with { State = frame.State.WriteWord(field, unchecked((ushort)(frame.State.ReadWord(field) + delta))) };
            }
            ushort mask = frame.Effect.Parameters?.TargetMask7A ?? throw new NotSupportedException("Effect313 requires its current target mask.");
            point = inputs.TargetCenter5020A0(frame, mask, frame.State.ReadView(TargetPointAddress, 8));
            frame = point.Frame with { State = point.Frame.State.WriteView(TargetPointAddress, point.Output8, 8) };
        }
        if (age == 26)
        {
            for (int ordinal = 0; ordinal < 2; ordinal++)
            {
                (frame, uint child) = AppendRequired(frame, 0x59C3E0);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
                int delay = Roll313(ref frame) % 6 + ordinal;
                int scale = Roll313(ref frame) % 800 + 400;
                frame = Write(frame, child, Read(frame, child).WriteWord(14, (ushort)delay).WriteWord(0x1C, 3072).WriteWord(0x1E, 76).WriteWord(0x20, 0).WriteWord(0x18, (ushort)scale));
            }
        }
        if (age is >= 26 and <= 55)
        {
            frame = BirthRecords313(frame, resources, inputs);
        }
        if (age == 56)
        {
            (frame, uint child) = AppendRequired(frame, 0x59C2F0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(0x1C, 0x3333).WriteWord(0x1E, 468));
        }
        if (age == 74)
        {
            frame = BirthPairedEmitters313(frame);
        }
        if (age is >= 99 and <= 104)
        {
            frame = BirthGround313(frame, resources);
        }
        if (age == 121)
        {
            (frame, uint child) = AppendRequired(frame, 0x59BB60);
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child).WriteWord(12, 0), 0x10, frame.State.ReadView(TargetPointAddress, 8), 8).WriteWord(0x1C, 0x3924).WriteWord(0x20, 0).WriteWord(0x22, 0x130C);
            frame = Write(frame, child, task);
        }
        if (age is >= 121 and <= 124)
        {
            frame = BirthFalling313(frame, resources);
        }
        if (age == 99)
        {
            (frame, uint cleanup) = AppendRequired(frame, CleanupCallback);
            frame = Write(frame, cleanup, Read(frame, cleanup).WriteWord(12, 0));
        }
        if (age == 122)
        {
            frame = BirthParty313(frame, inputs);
        }
        if (age <= 16)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect313Stage5713E0(age << 7, 0));
        }
        else if (age >= 182)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect313Stage5713E0((190 - age) << 8, 0));
        }
        if (age == 10)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect313Sound501330(SoundAddress, 0, 128));
        }
        frame = Write(frame, address, Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1))));
        if (age + 1 > 190)
        {
            frame = inputs.Execute(frame, new NativeBattleEffect313Stage5713E0(0, 0));
            return new(frame, 2);
        }
        return new(frame, 0);
    }

    private static NativeBattleEffect313Frame BirthPairedEmitters313(NativeBattleEffect313Frame frame)
    {
        NativeBattleEffectArena center = frame.State.ReadView(CenterAddress, 8);
        NativeBattleEffectArena target = frame.State.ReadView(TargetPointAddress, 8);
        NativeBattleVector direction = new(unchecked((short)(Word313(target, 0) - Word313(center, 0))), 0, unchecked((short)(Word313(target, 4) - Word313(center, 4))));
        for (int ordinal = 0; ordinal < 2; ordinal++)
        {
            (frame, uint child) = AppendRequired(frame, 0x59BDF0);
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(NativeBattleGfMatrices.Euler56CD50(new(0, unchecked((short)(ordinal == 0 ? -1024 : 1024)), 0)), direction);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(0x10, unchecked((ushort)(rotated.X + Word313(center, 0)))).WriteWord(0x12, unchecked((ushort)Word313(target, 2))).WriteWord(0x14, unchecked((ushort)(rotated.Z + Word313(center, 4)))).WriteWord(0x18, unchecked((ushort)(ordinal == 0 ? 70 : -70))).WriteWord(0x1A, unchecked((ushort)(ordinal == 0 ? 160 : -160))).WriteWord(0x20, 4096);
            frame = Write(frame, child, task);
        }
        return frame;
    }

    private static NativeBattleEffect313Frame BirthGround313(NativeBattleEffect313Frame frame, NativeBattleEffect313Resources resources)
    {
        for (int group = 0; group < 5; group++)
        {
            int count = 1 + Roll313(ref frame) % 2;
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                (frame, uint child) = AppendRequired(frame, 0x59BCA0);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
                int delay = Roll313(ref frame) % 8 + count;
                NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 0x10, frame.State.ReadView(TargetPointAddress, 8), 8).WriteWord(14, (ushort)delay);
                int x = Roll313(ref frame) % 800 + Word313(Static313(resources, 0xCD8CF0 + (uint)(group * 4), 2), 0) - 400;
                int z = Roll313(ref frame) % 800 + Word313(Static313(resources, 0xCD8D18 + (uint)(group * 4), 2), 0) - 300;
                task = task.WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + x))).WriteWord(0x12, 0).WriteWord(0x14, unchecked((ushort)(task.ReadWord(0x14) + z)));
                int angle = Roll313(ref frame) % 4096;
                int scale = Roll313(ref frame) % 3584 + 2048;
                int life = Roll313(ref frame) % 4 + 4;
                frame = Write(frame, child, task.WriteWord(0x18, (ushort)angle).WriteWord(0x1C, (ushort)scale).WriteWord(0x20, (ushort)life));
            }
        }
        return frame;
    }

    private static NativeBattleEffect313Frame BirthFalling313(NativeBattleEffect313Frame frame, NativeBattleEffect313Resources resources)
    {
        for (int group = 0; group < 5; group++)
        {
            int count = 2 + Roll313(ref frame) % 2;
            for (int ordinal = 0; ordinal < count; ordinal++)
            {
                (frame, uint child) = AppendRequired(frame, 0x59B9F0);
                frame = Write(frame, child, Read(frame, child).WriteWord(12, 0));
                int delay = Roll313(ref frame) % 8 + ordinal + 11;
                NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 0x10, frame.State.ReadView(TargetPointAddress, 8), 8).WriteWord(14, (ushort)delay);
                int x = Roll313(ref frame) % 3600 + Word313(Static313(resources, 0xCD8D04 + (uint)(group * 4), 2), 0) - 1800;
                int y = -(Roll313(ref frame) % 60);
                int z = Roll313(ref frame) % 3600 + Word313(Static313(resources, 0xCD8D2C + (uint)(group * 4), 2), 0) - 1800;
                int velocity = -30 - Roll313(ref frame) % 500;
                int choice = Roll313(ref frame) % 2;
                int scale = 256 + Roll313(ref frame) % 1280;
                int life = 40 + Roll313(ref frame) % 100;
                task = task.WriteWord(0x10, unchecked((ushort)(task.ReadWord(0x10) + x))).WriteWord(0x12, unchecked((ushort)y)).WriteWord(0x14, unchecked((ushort)(task.ReadWord(0x14) + z))).WriteWord(0x16, unchecked((ushort)velocity)).WriteWord(0x18, (ushort)choice).WriteWord(0x1A, (ushort)scale).WriteWord(0x1C, (ushort)(count * 400)).WriteWord(0x1E, unchecked((ushort)velocity)).WriteWord(0x20, (ushort)life).WriteWord(0x22, (ushort)(life >> 2));
                frame = Write(frame, child, task);
            }
        }
        return frame;
    }

    private static NativeBattleEffect313Frame BirthParty313(NativeBattleEffect313Frame frame, NativeBattleEffect313Inputs inputs)
    {
        for (int actor = 0; actor < 3; actor++)
        {
            if ((inputs.ReadModelArena9C(frame, ModelAddress(actor)).Read(0, 1)[0] & 2) == 0)
            {
                continue;
            }
            (frame, uint child) = AppendRequired(frame, 0x59C4F0);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)actor));
            int scale = (Roll313(ref frame) % 3 + 1) << 12;
            int velocity = -2500 - Roll313(ref frame) % 1000;
            NativeBattleActionPacketsState packets = inputs.ReadPackets(frame);
            NativeBattleActionHeaderSlot header = CurrentHeader(frame, packets);
            int contact = -1;
            int contactCount = header.ReadByte(16);
            int first = contactCount == 0 ? 0 : header.FirstContactIndex ?? throw new NotSupportedException("Effect313 requires its current contact pointer.");
            for (int index = 0; index < contactCount; index++)
            {
                if (packets.ReadContact(first + index).Primary.ActorIndex == actor)
                {
                    contact = index;
                    break;
                }
            }
            frame = Write(frame, child, Read(frame, child).WriteWord(0x18, (ushort)scale).WriteWord(0x1A, unchecked((ushort)contact)).WriteWord(0x20, unchecked((ushort)(contact < 0 ? velocity >> 1 : velocity))));
        }
        return frame;
    }
}
