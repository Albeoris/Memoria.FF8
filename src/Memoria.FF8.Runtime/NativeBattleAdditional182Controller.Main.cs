namespace Memoria.FF8.Runtime;

public static partial class NativeBattleAdditional182Controller
{
    public static (NativeBattleAdditional182Frame Frame, int Result) AdvanceMain(NativeBattleAdditional182Frame frame, uint address, NativeBattleAdditional182Resources resources, NativeBattleAdditional182Inputs inputs)
    {
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return (frame, 0);
        }
        NativeBattleModulePoolTask main = Read(frame, address);
        bool set = main.Callback switch
        {
            0x5CA0E0 => true,
            0x5CA970 => false,
            _ => throw new NotSupportedException("Additional requires its actual director callback.")
        };
        int age = Signed(main, 12);
        int actor = checked((int)frame.State.ReadDword(0x2304220));
        if (age == 0)
        {
            ushort saved = (ushort)(inputs.ReadModel(frame, actor).Flags0 & 8);
            frame = Write(frame, address, main.WriteWord(14, saved));
            frame = inputs.Execute(frame, new NativeBattleAdditional182ActorFlags(actor, 8, 0));
            NativeBattleAdditional182PointStep point = inputs.ReadAnchor571400(frame, actor);
            frame = point.Frame;
            NativeBattleGraphicsModel model = inputs.ReadModel(frame, actor);
            short maximumY = (model.GeometryBounds52 ?? throw new NotSupportedException("Additional requires its current recipient geometry bounds.")).Maximum.Y;
            short originalScale = (model.Transform ?? throw new NotSupportedException("Additional requires its current recipient transform.")).Scale38;
            int scale = Math.Min((int)originalScale, 0x1100);
            main = CopyTask(Read(frame, address), 16, point.Point8, 8).WriteWord(22, unchecked((ushort)maximumY)).WriteWord(28, unchecked((ushort)Math.Max(scale, 0x600))).WriteWord(30, unchecked((ushort)(scale * 2200 >> 12)));
            frame = Write(frame, address, main);
        }
        if (age == 1)
        {
            frame = SpawnOpening(frame, address);
        }
        if (!set && age == 24)
        {
            frame = SpawnProducers(frame, address);
        }
        if (!set && age == 26)
        {
            (frame, uint child) = RequireAppend(frame, 0x5CB130);
            main = Read(frame, address);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(16, main.ReadWord(16)).WriteWord(18, unchecked((ushort)(Signed(main, 22) - 500))).WriteWord(20, main.ReadWord(20));
            frame = DrawRandom(frame, out ushort angle);
            task = task.WriteWord(24, (ushort)(angle & 0xFFF));
            frame = DrawRandom(frame, out ushort speed);
            task = task.WriteWord(28, 0x800).WriteWord(26, (ushort)(speed % 200 + 1000));
            frame = DrawRandom(frame, out ushort growth);
            frame = Write(frame, child, task.WriteWord(30, (ushort)(growth % 35 + 65)));
            for (int index = 0; index < 5; index++)
            {
                frame = Dword(frame, 0x2306518u + (uint)(index * 36), 0);
            }
        }
        if (!set && age == 20)
        {
            (frame, uint child) = RequireAppend(frame, 0x5CB390);
            frame = Write(frame, child, Read(frame, child).WriteWord(12, 0).WriteWord(14, checked((ushort)actor)).WriteWord(24, 6).WriteWord(26, (ushort)(inputs.ReadModel(frame, actor).Flags0 & 0x800)));
        }
        if (age == (set ? 35 : 64))
        {
            frame = inputs.Execute(frame, new NativeBattleAdditional182ActorFlags(actor, 0, Read(frame, address).ReadWord(14)));
            frame = inputs.Execute(frame, new NativeBattleAdditional182ContactsAll(Parameters(frame).HeaderIndex7C));
        }
        int terminal = set ? 40 : 65;
        if (age <= 8)
        {
            frame = inputs.Execute(frame, new NativeBattleAdditional182Stage5713E0(age << 8, 0));
        }
        else if (age >= (set ? 32 : 57))
        {
            frame = inputs.Execute(frame, new NativeBattleAdditional182Stage5713E0((terminal - age) << 8, 0));
        }
        if (age == 1)
        {
            NativeBattleAdditional182PointStep point = inputs.ReadAnchor571400(frame, actor);
            frame = inputs.Execute(point.Frame, new NativeBattleAdditional182Sound5013A0(resources.Sound(set), 0, point.Point8));
        }
        main = Read(frame, address).WriteWord(12, unchecked((ushort)(age + 1)));
        frame = Write(frame, address, main);
        return Signed(main, 12) > terminal ? (inputs.Execute(frame, new NativeBattleAdditional182Stage5713E0(0, 0)), 2) : (frame, 0);
    }

    private static NativeBattleAdditional182Frame SpawnOpening(NativeBattleAdditional182Frame frame, uint address)
    {
        NativeBattleModulePoolTask main = Read(frame, address);
        NativeBattleEffectArena point = TaskView(main, 16, 8);
        (frame, uint child) = RequireAppend(frame, 0x5CA490);
        frame = Write(frame, child, CopyTask(Read(frame, child), 16, point, 8).WriteWord(28, main.ReadWord(28)).WriteWord(12, 0).WriteWord(30, main.ReadWord(30)));
        (frame, uint firstRing) = RequireAppend(frame, 0x5CA6B0);
        NativeBattleModulePoolTask ring = CopyTask(Read(frame, firstRing), 16, point, 8).WriteWord(12, 0);
        frame = DrawRandom(frame, out ushort angle);
        ring = ring.WriteWord(24, (ushort)(angle & 0xFFF));
        frame = DrawRandom(frame, out ushort speedRandom);
        int speed = speedRandom % 40 + 20;
        int scale = (Signed(main, 28) * 2560 >> 12) / 16;
        ring = ring.WriteWord(26, (ushort)speed).WriteWord(30, unchecked((ushort)scale)).WriteWord(28, unchecked((ushort)scale));
        frame = Write(frame, firstRing, ring);
        (frame, uint secondRing) = RequireAppend(frame, 0x5CA6B0);
        ring = CopyTask(Read(frame, secondRing), 16, point, 8).WriteWord(12, 0);
        frame = DrawRandom(frame, out angle);
        ring = ring.WriteWord(26, unchecked((ushort)-speed)).WriteWord(24, (ushort)(angle & 0xFFF)).WriteWord(28, unchecked((ushort)scale));
        frame = Write(frame, secondRing, ring);
        for (int index = 0; index < 12; index++)
        {
            (frame, child) = RequireAppend(frame, 0x5CA810);
            NativeBattleModulePoolTask task = CopyTask(Read(frame, child), 16, point, 8).WriteWord(12, 0).WriteWord(14, (ushort)index);
            frame = DrawRandom(frame, out ushort delay);
            task = task.WriteWord(22, (ushort)(delay % 6 + index));
            frame = DrawRandom(frame, out angle);
            task = task.WriteWord(24, (ushort)(angle & 0xFFF));
            frame = DrawRandom(frame, out speedRandom);
            speed = speedRandom % 60 + 25;
            task = task.WriteWord(26, unchecked((ushort)((speed & 1) != 0 ? -speed : speed)));
            frame = DrawRandom(frame, out ushort scaleRandom);
            short childScale = unchecked((short)(((scaleRandom & 0x3FF) + 0x900) * Signed(main, 28) >> 12));
            task = task.WriteWord(28, unchecked((ushort)childScale)).WriteWord(30, unchecked((ushort)(childScale / 10)));
            frame = DrawRandom(frame, out ushort sizeRandom);
            ushort size = (ushort)((sizeRandom % 0x500 + 0xA00) / 10);
            frame = Write(frame, child, task.WriteWord(34, size).WriteWord(32, size));
        }
        return frame;
    }

    private static NativeBattleAdditional182Frame SpawnProducers(NativeBattleAdditional182Frame frame, uint address)
    {
        NativeBattleModulePoolTask main = Read(frame, address);
        for (int index = 0; index < 12; index++)
        {
            (frame, uint child) = RequireAppend(frame, 0x5CAED0);
            NativeBattleModulePoolTask task = Read(frame, child).WriteWord(12, 0).WriteWord(14, (ushort)(1 << index));
            frame = DrawRandom(frame, out ushort x);
            task = task.WriteWord(16, unchecked((ushort)(Signed(main, 16) + x % 900 - 450)));
            frame = DrawRandom(frame, out ushort y);
            task = task.WriteWord(18, unchecked((ushort)(Signed(main, 22) - y % 450 - 100)));
            frame = DrawRandom(frame, out ushort z);
            task = task.WriteWord(20, unchecked((ushort)(Signed(main, 20) + z % 900 - 450)));
            frame = DrawRandom(frame, out ushort delay);
            task = task.WriteWord(26, (ushort)((delay & 7) + index)).WriteWord(24, (ushort)((delay & 7) + index));
            frame = DrawRandom(frame, out ushort scale);
            task = task.WriteWord(28, (ushort)(scale % 0x700 + 0xA00));
            frame = DrawRandom(frame, out ushort growth);
            frame = Write(frame, child, task.WriteWord(30, (ushort)(growth % 160 + 60)));
        }
        return frame;
    }
}
