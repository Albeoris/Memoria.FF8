using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87TargetMotion(NativeBattleModulePoolTask Task, ImmutableArray<NativeBattleGraphicsModel> Models);

public static class NativeBattleRinoa87Motion
{
    public static NativeBattleModulePoolTask Move7762D0(NativeBattleModulePoolTask task)
    {
        task = Accelerate(task);
        return Interpolate(task);
    }

    public static NativeBattleRinoa87TargetMotion MoveTarget77E260(NativeBattleModulePoolTask task, ImmutableArray<NativeBattleGraphicsModel> models, int selectedMinimum, bool useCurve, ImmutableArray<short> actualCurve)
    {
        if ((uint)selectedMinimum >= models.Length || models.Length != 7)
        {
            throw new InvalidDataException("Native77E260 requires its selected501FF0 model in the physical bank.");
        }
        if (useCurve)
        {
            short cursor = Word(372);
            if (actualCurve.IsDefault || cursor < 0 || cursor >= actualCurve.Length)
            {
                throw new NotSupportedException("Native77E260 curve cursor leaves its actual retained source.");
            }
            task = task.WriteWord(360, unchecked((ushort)actualCurve[cursor]));
            if (actualCurve[cursor] != 4096)
            {
                task = task.WriteWord(372, unchecked((ushort)(cursor + 1)));
            }
        }
        else
        {
            task = Accelerate(task);
        }
        task = Interpolate(task);
        NativeBattleGraphicsModel source = models[selectedMinimum];
        NativeBattleModelTransform transform = source.Transform ?? throw new NotSupportedException("Native77E260 requires its actual model position and fourth WORD.");
        short pitch = (short)((-1024 - task.ReadWord(60)) & 4095);
        short yaw = source.Rotation14 ?? throw new NotSupportedException("Native77E260 requires the selected model's actual yaw.");
        short roll = source.Rotation16 ?? throw new NotSupportedException("Native77E260 requires the selected model's actual roll.");
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), Word(62));
        local = NativeBattleCuragaMatrices.RotateX8DD7E0(local, unchecked((short)(Word(60) + 1024)));
        NativeBattleVector delta = NativeBattleGfMatrices.RotateShort56C4F0(local, new(Word(352), Word(354), Word(356)));
        int x = delta.X + Word(76);
        int y = delta.Y + Word(78);
        int z = delta.Z + Word(80);
        NativeBattleActionMatrix world = NativeBattleGfMatrices.Euler56CE30(new(pitch, yaw, roll)) with { X = x, Y = y, Z = z };
        NativeBattleModelTransform moved = transform with { Position28 = new(unchecked((short)x), unchecked((short)y), unchecked((short)z)) };
        source = source with { Rotation12 = pitch, Transform = moved, GeometryWorldMatrix = world };
        models = models.SetItem(selectedMinimum, source);
        foreach (int index in NativeBattleModelRings.Walk(models, selectedMinimum))
        {
            if ((models[index].Flags0 & 2) != 0)
            {
                NativeBattleModelTransform target = models[index].Transform ?? throw new NotSupportedException("Native77E260 loaded ring member lacks its actual transform.");
                models = models.SetItem(index, models[index] with { Rotation12 = source.Rotation12, Rotation14 = source.Rotation14, Rotation16 = source.Rotation16, RotationPadding18 = source.RotationPadding18, Transform = target with { Position28 = moved.Position28, Vertical34 = moved.Vertical34 }, GeometryWorldMatrix = world });
            }
        }
        return new(task, models);

        short Word(int offset) => unchecked((short)task.ReadWord(offset));
    }

    public static NativeBattleModulePoolTask TurnTowardZero77F070(NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(62, (ushort)(task.ReadWord(62) & 4095));
        short yaw = unchecked((short)task.ReadWord(62));
        if (yaw > 2048)
        {
            short next = (short)((yaw + 16) & 4095);
            task = task.WriteWord(62, next < 2048 ? (ushort)0 : unchecked((ushort)next));
        }
        else if (yaw < 2048)
        {
            short next = (short)((yaw - 16) & 4095);
            task = task.WriteWord(62, next > 2048 ? (ushort)0 : unchecked((ushort)next));
        }
        return task;
    }

    public static NativeBattleModulePoolTask Velocity77EFD0(NativeBattleModulePoolTask task, short speed)
    {
        NativeBattleActionMatrix local = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), unchecked((short)task.ReadWord(62)));
        NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(local, new(0, 0, speed));
        return task.WriteWord(292, unchecked((ushort)velocity.X)).WriteWord(294, unchecked((ushort)velocity.Y)).WriteWord(296, unchecked((ushort)velocity.Z));
    }

    private static NativeBattleModulePoolTask Accelerate(NativeBattleModulePoolTask task)
    {
        task = task.WriteWord(362, unchecked((ushort)(task.ReadWord(362) + task.ReadWord(364))));
        return task.WriteWord(360, unchecked((ushort)(task.ReadWord(360) + task.ReadWord(362))));
    }

    private static NativeBattleModulePoolTask Interpolate(NativeBattleModulePoolTask task)
    {
        short progress = unchecked((short)task.ReadWord(360));
        if (progress < 4096)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                short from = unchecked((short)task.ReadWord(320 + axis * 2));
                short to = unchecked((short)task.ReadWord(328 + axis * 2));
                task = task.WriteWord(76 + axis * 2, unchecked((ushort)(from + progress * (to - from) / 4096)));
            }
        }
        else
        {
            task = task.WriteDword(76, task.ReadDword(328)).WriteDword(80, task.ReadDword(332)).WriteWord(360, 4096);
        }
        return task.WriteWord(78, unchecked((ushort)(task.ReadWord(78) + task.ReadWord(366))));
    }
}
