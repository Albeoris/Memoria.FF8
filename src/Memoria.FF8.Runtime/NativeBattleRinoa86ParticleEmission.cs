namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa86ParticleEmission(NativeBattleEffectHierarchy Hierarchy, NativeCrtRandomState Random);

public static class NativeBattleRinoa86Emitters
{
    public static NativeBattleRinoa86ParticleEmission Emit(NativeBattleEffectHierarchy hierarchy, uint parent, int mode, NativeCrtRandomState random, Func<byte, NativeBattleVector> readBone502170, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        if (mode is < 0 or > 6)
        {
            throw new InvalidDataException("Native780310/783150 requires its original selected emission mode.");
        }
        byte bone = mode switch { 0 => 30, 1 => 33, 2 or 4 => 28, 3 or 5 => 29, _ => 241 };
        int count = mode <= 3 ? 4 : 2;
        for (int index = 0; index < count; index++)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(4, 0x782930, 144, parent, selectActors);
            hierarchy = appended.Hierarchy;
            NativeBattleModulePoolTask task = Position(hierarchy.Read(appended.Address), readBone502170(bone));
            if (mode is 4 or 5)
            {
                task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + (Draw() & 127) - 64)));
                task = task.WriteWord(32, unchecked((ushort)(task.ReadWord(32) + (Draw() & 127) - 64)));
                task = task.WriteWord(30, unchecked((ushort)(((Draw() & 63) + 192) * unchecked((short)task.ReadWord(30)) / 256)));
                short yaw = (short)(((Draw() & 1023) - 512) & 4095);
                task = task.WriteWord(120, 0).WriteWord(122, 0).WriteWord(124, 0).WriteWord(130, unchecked((ushort)-(Draw() % 96)));
                task = Velocity(task, 120, yaw, 0, new(0, 0, 0));
            }
            else
            {
                if (mode == 6)
                {
                    task = task.WriteWord(30, 0);
                }
                task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + unchecked((byte)Draw()) - 128)));
                if (mode != 6)
                {
                    ushort jitter = unchecked((byte)Draw());
                    task = task.WriteWord(30, 0).WriteWord(32, unchecked((ushort)(task.ReadWord(32) + jitter - 128)));
                }
                else
                {
                    task = task.WriteWord(32, unchecked((ushort)(task.ReadWord(32) + unchecked((byte)Draw()) - 128)));
                }
                short yaw = mode == 6 ? (short)(Draw() & 4095) : (short)(((Draw() & 1023) - 512) & 4095);
                task = task.WriteWord(120, 0).WriteWord(122, 0).WriteWord(130, unchecked((ushort)-(Draw() % 96)));
                short velocity = (short)((Draw() & 4095) + 512);
                task = Velocity(task, 120, yaw, 0, new(0, 0, velocity));
            }
            hierarchy = hierarchy.Write(appended.Address, task);
        }
        if (mode <= 3)
        {
            for (int index = 0; index < 2; index++)
            {
                NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(5, 0x780640, 152, parent, selectActors);
                hierarchy = appended.Hierarchy;
                NativeBattleModulePoolTask task = Position(hierarchy.Read(appended.Address), readBone502170(bone)).WriteWord(30, 0);
                short yaw = (short)(((Draw() & 511) - 256) & 4095);
                short pitch = (short)(((Draw() % 384) + 256) & 4095);
                short velocity = (short)(Draw() % 2048 + 512);
                task = Velocity(task, 124, yaw, pitch, new(0, 0, velocity));
                hierarchy = hierarchy.Write(appended.Address, task);
            }
        }
        return new(hierarchy, random);

        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleVector point)
    {
        return task.WriteWord(28, unchecked((ushort)point.X)).WriteWord(30, unchecked((ushort)point.Y)).WriteWord(32, unchecked((ushort)point.Z));
    }

    private static NativeBattleModulePoolTask Velocity(NativeBattleModulePoolTask task, int offset, short yaw, short pitch, NativeBattleVector point)
    {
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw);
        if (pitch != 0)
        {
            rotation = NativeBattleCuragaMatrices.RotateX8DD7E0(rotation, pitch);
        }
        NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(rotation, point);
        return task.WriteWord(offset, unchecked((ushort)velocity.X)).WriteWord(offset + 2, unchecked((ushort)velocity.Y)).WriteWord(offset + 4, unchecked((ushort)velocity.Z));
    }
}
