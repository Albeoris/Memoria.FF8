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
        BattleModelOwner actor = NativeBattleZantetsukenTasks.ReadActor(task);
        short age = unchecked((short)task.ReadWord(12));
        if (age <= 40)
        {
            NativeBattleGilgameshProcess process = frame.Memory.Gilgamesh ?? throw new NotSupportedException("Zantetsuken requires its current process actor owner.");
            NativeBattleGilgameshZantetsukenActors actors = process.ZantetsukenActors ?? NativeBattleGilgameshZantetsukenActors.FromUnknown();
            bool original = inputs.Aliases.TryEncodeExact(actor, out byte slot);
            NativeBattleEffectArena row = original ? actors.ReadRow(slot) : frame.Effect.ZantetsukenTransforms.GetValueOrDefault(actor) ?? NativeBattleEffectArena.Clear().MarkUnknown(0, NativeBattleGilgameshZantetsukenActors.RecordStride);
            if (age == 0)
            {
                row = NativeBattleGilgameshZantetsukenActors.WithWord(row, 0, 0);
                row = NativeBattleGilgameshZantetsukenActors.WithWord(row, 2, 0);
                row = NativeBattleGilgameshZantetsukenActors.WithWord(row, 4, 0);
            }
            short angle = unchecked((short)task.ReadWord(0x18));
            row = NativeBattleGilgameshZantetsukenActors.WithWord(row, 0, unchecked((short)(age * angle / 40)));
            row = NativeBattleGilgameshZantetsukenActors.WithWord(row, 2, unchecked((short)(age * (angle / 2) / 40)));
            NativeBattleActionMatrix local = NativeBattleGfMatrices.Euler56CE30(new(NativeBattleGilgameshZantetsukenActors.ReadWord(row, 0), NativeBattleGilgameshZantetsukenActors.ReadWord(row, 2), NativeBattleGilgameshZantetsukenActors.ReadWord(row, 4)));
            NativeBattleVector anchor = new(unchecked((short)task.ReadWord(0x10)), unchecked((short)task.ReadWord(0x12)), unchecked((short)task.ReadWord(0x14)));
            NativeBattleVector rotated = NativeBattleGfMatrices.RotateShort56C4F0(local, anchor);
            local = local with { X = anchor.X - rotated.X, Y = anchor.Y + unchecked((short)task.ReadWord(0x20)) - rotated.Y, Z = anchor.Z - rotated.Z };
            NativeBattleActionMatrix composed = NativeBattleGfMatrices.Compose56C2F0(inputs.ReadWorld32(actor).ReadMatrix(), local);
            row = NativeBattleGilgameshZantetsukenActors.WithMatrix(row, NativeBattlePackedMatrix32.FromMatrix(composed, null));
            if (original)
            {
                frame = frame with { Memory = frame.Memory with { Gilgamesh = process with { ZantetsukenActors = actors.WithRow(slot, row) } } };
            }
            else
            {
                frame = frame with { Effect = frame.Effect with { ZantetsukenTransforms = frame.Effect.ZantetsukenTransforms.SetItem(actor, row) } };
            }
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
            BattleModelOwner actor = NativeBattleZantetsukenTasks.ReadActor(task);
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
        frame = inputs.Execute(frame, new NativeBattleGilgameshZantetsukenContact(inputs.ResolveContact(task.ReadExecutionReference<BattleExecutionReference.ContactOffset>(0x1A, 2).Offset ?? throw new InvalidDataException("Zantetsuken child has no contact offset."))));
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

}
