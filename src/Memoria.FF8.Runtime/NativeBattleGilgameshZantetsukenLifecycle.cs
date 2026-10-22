namespace Memoria.FF8.Runtime;

public static class NativeBattleGilgameshZantetsukenLifecycle
{
    public static NativeBattleGilgameshZantetsukenStep AdvanceRotation(NativeBattleGilgameshFrame frame, uint address, NativeBattleGilgameshZantetsukenInputs inputs)
    {
        NativeBattleZantetsukenTasks.Require(frame, address, 0x58E3E0);
        NativeBattleGilgameshZantetsukenStep? delayed = Delay(frame, address, inputs.GraphicsFlags, 0x16);
        if (delayed is not null)
        {
            return delayed;
        }
        NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(frame, address);
        byte actor = Actor(task);
        short age = unchecked((short)task.ReadWord(12));
        if (age <= 40)
        {
            NativeBattleGilgameshProcess process = frame.Memory.Gilgamesh ?? throw new NotSupportedException("Zantetsuken requires its current process actor owner.");
            NativeBattleGilgameshZantetsukenActors actors = process.ZantetsukenActors ?? NativeBattleGilgameshZantetsukenActors.FromUnknown();
            if (age == 0)
            {
                actors = actors.WithWord(actor, 0, 0).WithWord(actor, 2, 0).WithWord(actor, 4, 0);
            }
            short angle = unchecked((short)task.ReadWord(0x18));
            actors = actors.WithWord(actor, 0, unchecked((short)(age * angle / 40))).WithWord(actor, 2, unchecked((short)(age * (angle / 2) / 40)));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(actors.ReadWord(actor, 0), actors.ReadWord(actor, 2), actors.ReadWord(actor, 4)));
            NativeBattleVector anchor = new(unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)), unchecked((short)task.ReadWord(0x14)));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(local, anchor);
            local = local with { X = anchor.X - rotated.X, Y = anchor.Y + unchecked((short)task.ReadWord(0x20)) - rotated.Y, Z = anchor.Z - rotated.Z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadWorld32(actor).ReadMatrix(), local);
            actors = actors.WithMatrix(actor, NativeBattlePackedMatrix32.FromMatrix(composed, null));
            frame = frame with { Memory = frame.Memory with { Gilgamesh = process with { ZantetsukenActors = actors } } };
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = NativeBattleZantetsukenTasks.Read(frame, address);
        short velocity = unchecked((short)task.ReadWord(0x22));
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) + velocity))).WriteWord(0x22, unchecked((ushort)(velocity - (velocity >> 3)))).WriteWord(12, next);
        return new(NativeBattleZantetsukenTasks.Write(frame, address, task), unchecked((short)next) >= 40 ? 2 : 0);
    }

    public static NativeBattleGilgameshZantetsukenStep AdvanceActor(NativeBattleGilgameshFrame frame, uint address, NativeBattleGilgameshZantetsukenInputs inputs)
    {
        NativeBattleZantetsukenTasks.Require(frame, address, 0x58E570);
        NativeBattleGilgameshZantetsukenStep? delayed = Delay(frame, address, inputs.GraphicsFlags, 0x16);
        if (delayed is not null)
        {
            return delayed;
        }
        NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(frame, address);
        short age = unchecked((short)task.ReadWord(12));
        if (age < 40)
        {
            byte actor = Actor(task);
            frame = inputs.Execute(frame, new NativeBattleGilgameshZantetsukenActorFlags(actor, 0x0C));
            frame = inputs.Execute(frame, new NativeBattleGilgameshZantetsukenBeginDrawActor(address));
            if (age >= 24)
            {
                int cue = 4096 - NativeBattleCameraMath.Sine((age - 24) * 64);
                frame = inputs.Execute(frame, new NativeBattleGilgameshZantetsukenFadeActor(actor, cue));
            }
            frame = inputs.Execute(frame, new NativeBattleGilgameshZantetsukenDrawActor(address));
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = NativeBattleZantetsukenTasks.Read(frame, address);
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        frame = NativeBattleZantetsukenTasks.Write(frame, address, task.WriteWord(12, next));
        if (unchecked((short)next) <= 40)
        {
            return new(frame, 0);
        }
        frame = inputs.Execute(frame, new NativeBattleGilgameshZantetsukenContact(unchecked((short)task.ReadWord(0x1A))));
        return new(frame, 2);
    }

    public static NativeBattleGilgameshZantetsukenStep AdvanceStage(NativeBattleGilgameshFrame frame, uint address, NativeBattleGilgameshZantetsukenInputs inputs)
    {
        NativeBattleZantetsukenTasks.Require(frame, address, 0x58F940);
        NativeBattleGilgameshZantetsukenStep? delayed = Delay(frame, address, inputs.GraphicsFlags, 14);
        if (delayed is not null)
        {
            return delayed;
        }
        NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(frame, address);
        if (unchecked((short)task.ReadWord(12)) < 65)
        {
            frame = inputs.Execute(frame, new NativeBattleGilgameshZantetsukenStageVisibility(false));
            frame = inputs.Execute(frame, new NativeBattleGilgameshZantetsukenDrawStage(address));
        }
        if ((inputs.GraphicsFlags & 0x201) != 0)
        {
            return new(frame, 0);
        }
        task = NativeBattleZantetsukenTasks.Read(frame, address);
        short velocity = unchecked((short)task.ReadWord(0x22));
        ushort next = unchecked((ushort)(task.ReadWord(12) + 1));
        task = task.WriteWord(0x20, unchecked((ushort)(task.ReadWord(0x20) - velocity))).WriteWord(0x22, unchecked((ushort)(velocity - (velocity >> 3)))).WriteWord(12, next);
        frame = NativeBattleZantetsukenTasks.Write(frame, address, task);
        if (unchecked((short)next) <= 65)
        {
            return new(frame, 0);
        }
        return new(inputs.Execute(frame, new NativeBattleGilgameshZantetsukenStageVisibility(true)), 2);
    }

    private static NativeBattleGilgameshZantetsukenStep? Delay(NativeBattleGilgameshFrame frame, uint address, uint flags, int offset)
    {
        NativeBattleModulePoolTask task = NativeBattleZantetsukenTasks.Read(frame, address);
        short delay = unchecked((short)task.ReadWord(offset));
        if (delay <= 0)
        {
            return null;
        }
        if ((flags & 0x201) == 0)
        {
            frame = NativeBattleZantetsukenTasks.Write(frame, address, task.WriteWord(offset, unchecked((ushort)(delay - 1))));
        }
        return new(frame, 0);
    }

    private static byte Actor(NativeBattleModulePoolTask task)
    {
        ushort actor = task.ReadWord(14);
        return actor < 7 ? (byte)actor : throw new InvalidDataException("Zantetsuken child actor leaves its seven original physical models.");
    }
}
