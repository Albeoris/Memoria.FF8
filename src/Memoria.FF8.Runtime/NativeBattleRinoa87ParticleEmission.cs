using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa87ParticleEmission(NativeBattleEffectHierarchy Hierarchy, NativeCrtRandomState Random, bool Completed = false);
public static class NativeBattleRinoa87Emitters
{
    public static NativeBattleRinoa87ParticleEmission Emit775510(NativeBattleEffectHierarchy hierarchy, uint parent, int mode, NativeCrtRandomState random, Func<byte, NativeBattleVector> readBone502170, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        if (mode is < 0 or > 5)
        {
            throw new InvalidDataException("Native775510 requires its original emission mode0..5.");
        }
        byte bone = mode switch { 0 => 30, 1 => 33, 2 or 4 => 28, _ => 29 };
        for (int index = 0; index < (mode > 3 ? 2 : 4); index++)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(5, 0x775720, 144, parent, selectActors);
            hierarchy = appended.Hierarchy;
            NativeBattleModulePoolTask task = Position(hierarchy.Read(appended.Address), readBone502170(bone));
            short yaw;
            short speed;
            if (mode > 3)
            {
                task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + (Draw() & 127) - 64)));
                task = task.WriteWord(32, unchecked((ushort)(task.ReadWord(32) + (Draw() & 127) - 64)));
                task = task.WriteWord(30, unchecked((ushort)(((Draw() & 63) + 192) * unchecked((short)task.ReadWord(30)) / 256)));
                yaw = (short)(((Draw() & 1023) - 512) & 4095);
                task = task.WriteWord(120, 0).WriteWord(122, 0).WriteWord(124, 0).WriteWord(130, unchecked((ushort)-(Draw() % 96)));
                speed = 0;
            }
            else
            {
                task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + unchecked((byte)Draw()) - 128)));
                byte jitter = unchecked((byte)Draw());
                task = task.WriteWord(30, 0).WriteWord(32, unchecked((ushort)(task.ReadWord(32) + jitter - 128)));
                yaw = (short)(((Draw() & 1023) - 512) & 4095);
                task = task.WriteWord(120, 0).WriteWord(122, 0).WriteWord(130, unchecked((ushort)-(Draw() % 96)));
                speed = (short)((Draw() & 4095) + 512);
                task = task.WriteWord(124, unchecked((ushort)speed));
            }
            NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw);
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, speed));
            task = task.WriteWord(120, unchecked((ushort)velocity.X)).WriteWord(122, unchecked((ushort)velocity.Y)).WriteWord(124, unchecked((ushort)velocity.Z));
            hierarchy = hierarchy.Write(appended.Address, task);
        }
        return new(hierarchy, random);
        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleRinoa87ParticleEmission Emit775F40(NativeBattleEffectHierarchy hierarchy, uint parent, NativeCrtRandomState random, Func<byte, NativeBattleVector> readBone502170, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(5, 0x775720, 144, parent, selectActors);
        hierarchy = appended.Hierarchy;
        NativeBattleModulePoolTask task = Position(hierarchy.Read(appended.Address), readBone502170(241)).WriteWord(30, 0);
        task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + unchecked((byte)Draw()) - 128)));
        task = task.WriteWord(32, unchecked((ushort)(task.ReadWord(32) + unchecked((byte)Draw()) - 128)));
        short yaw = (short)(Draw() & 4095);
        task = task.WriteWord(120, 0).WriteWord(122, 0).WriteWord(130, unchecked((ushort)-(Draw() % 96)));
        short speed = (short)((Draw() & 2047) + 512);
        NativeBattleActionMatrix rotation = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw);
        NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(rotation, new(0, 0, speed));
        task = task.WriteWord(120, unchecked((ushort)velocity.X)).WriteWord(122, unchecked((ushort)velocity.Y)).WriteWord(124, unchecked((ushort)velocity.Z));
        return new(hierarchy.Write(appended.Address, task), random);
        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    public static NativeBattleRinoa87ParticleEmission Advance77E800(NativeBattleEffectHierarchy hierarchy, uint address, NativeCrtRandomState random, ImmutableArray<short> firstCounts, ImmutableArray<short> secondCounts, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        if (firstCounts.IsDefault || firstCounts.Length != 6 || secondCounts.IsDefault || secondCounts.Length != 6)
        {
            throw new InvalidDataException("Native77E850 requires its two original six-WORD emission tables.");
        }
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        int phase = unchecked((sbyte)task.ReadByte(41));
        if (task.Callback != 0x77E800 || phase is < 0 or > 1)
        {
            throw new InvalidDataException("Native77E800 requires its actual two-state impact emitter.");
        }
        if (phase == 0)
        {
            short tick = unchecked((short)task.ReadWord(36));
            if (tick < 6)
            {
                if (tick < 0)
                {
                    throw new InvalidDataException("Native77E850 negative frame leaves its original emission tables.");
                }
                for (int index = 0; index < firstCounts[tick]; index++)
                {
                    Spawn(false);
                }
                for (int index = 0; index < secondCounts[tick]; index++)
                {
                    Spawn(true);
                }
            }
            else
            {
                task = task.WriteByte(38, (byte)(task.ReadByte(38) | 1)).WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
            }
        }
        task = task.WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(hierarchy, random, complete);

        void Spawn(bool heavy)
        {
            short yaw = (short)(Draw() & 4095);
            short radius = (short)((Draw() & 1023) + 256);
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(6, heavy ? 0x77EC00u : 0x77EAB0u, 152, address, selectActors);
            hierarchy = appended.Hierarchy;
            NativeBattleModulePoolTask child = hierarchy.Read(appended.Address).WriteDword(28, task.ReadDword(28)).WriteDword(32, task.ReadDword(32));
            child = child.WriteWord(28, unchecked((ushort)(child.ReadWord(28) + radius * NativeBattleCameraMath.Sine(yaw) / 4096))).WriteWord(32, unchecked((ushort)(child.ReadWord(32) + radius * NativeBattleCameraMath.Cosine(yaw) / 4096)));
            short speed = (short)((Draw() & 1023) + 768);
            child = child.WriteWord(126, unchecked((ushort)((heavy ? -4096 : -3584) - (Draw() & 4095))));
            child = child.WriteWord(124, unchecked((ushort)(speed * NativeBattleCameraMath.Sine(yaw) / 4096))).WriteWord(128, unchecked((ushort)(speed * NativeBattleCameraMath.Cosine(yaw) / 4096))).WriteWord(70, unchecked((ushort)yaw));
            hierarchy = hierarchy.Write(appended.Address, child);
            task = hierarchy.Read(address);
        }
        ushort Draw()
        {
            random = random.Draw(out ushort value);
            return value;
        }
    }

    private static NativeBattleModulePoolTask Position(NativeBattleModulePoolTask task, NativeBattleVector point) => task.WriteWord(28, unchecked((ushort)point.X)).WriteWord(30, unchecked((ushort)point.Y)).WriteWord(32, unchecked((ushort)point.Z));
}
