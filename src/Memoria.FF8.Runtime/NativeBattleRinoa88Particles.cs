using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88ParticleEmission(NativeBattleEffectHierarchy Hierarchy, NativeCrtRandomState Random);

public static class NativeBattleRinoa88Particles
{
    public static NativeBattleRinoa88ParticleEmission Emit(NativeBattleEffectHierarchy hierarchy, uint parent, uint callback, NativeCrtRandomState random, Func<byte, NativeBattleVector> readBone502170, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        if (callback is not (0x771F50 or 0x772880))
        {
            throw new InvalidDataException("Native Rinoa88 requires its original four-particle or three-particle body emitter.");
        }
        bool wide = callback == 0x772880;
        for (int index = 0; index < (wide ? 3 : 4); index++)
        {
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(5, wide ? 0x772990u : 0x772060u, 144, parent, selectActors);
            hierarchy = appended.Hierarchy;
            NativeBattleVector anchor = readBone502170(241);
            NativeBattleModulePoolTask task = hierarchy.Read(appended.Address).WriteWord(28, unchecked((ushort)anchor.X)).WriteWord(30, 0).WriteWord(32, unchecked((ushort)anchor.Z));
            short yaw = (short)(Draw() & 4095);
            short radius = (short)(unchecked((byte)Draw()) + 64);
            NativeBattleActionMatrix basis = NativeBattleCuragaMatrices.RotateY8DD8A0(NativeBattleCuragaMatrices.Identity8DD770(), yaw);
            NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, 0, radius));
            task = task.WriteWord(28, unchecked((ushort)(task.ReadWord(28) + offset.X))).WriteWord(32, unchecked((ushort)(task.ReadWord(32) + offset.Z))).WriteWord(120, 0).WriteWord(122, 0).WriteWord(130, unchecked((ushort)-(Draw() % 96)));
            short speed = (short)((Draw() & (wide ? 2047 : 1023)) + 512);
            NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(basis, new(0, 0, speed));
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

    public static NativeBattleRinoa88LeafStep Advance(NativeBattleRinoa88Frame frame, uint address, Func<NativeBattleRinoa88Frame, uint, NativeBattleRinoa88Frame> draw7720C0)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback is not (0x772060 or 0x772990))
        {
            throw new InvalidDataException("Native Rinoa88 body particle requires its actual144-byte callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                task = task.WriteDword(76, task.Callback == 0x772060 ? 0x154ECECu : 0x154EE30u).WriteWord(86, unchecked((ushort)-64)).WriteWord(82, 7);
                Next();
                break;
            case 1:
                Add(80, 1);
                if (Signed(80) > Signed(82))
                {
                    task = task.WriteWord(80, task.ReadWord(82)).WriteByte(38, (byte)(task.ReadByte(38) | 5));
                    Next();
                }
                Add(122, Signed(130));
                int x = Signed(120);
                int z = Signed(124);
                task = task.WriteWord(120, unchecked((ushort)(x - x / 8))).WriteWord(124, unchecked((ushort)(z - z / 8)));
                Add(28, Signed(120) / 16);
                Add(30, Signed(122) / 16);
                Add(32, Signed(124) / 16);
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Native Rinoa88 body particle signed phase exceeds its three callbacks.");
        }
        Store();
        frame = draw7720C0(frame, address);
        hierarchy = new(frame.Effect.Pools);
        task = hierarchy.Read(address).WriteWord(36, unchecked((ushort)(task.ReadWord(36) + 1)));
        Store();
        bool complete = (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0;
        if (complete)
        {
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Complete8DC530(address).Pools } };
        }
        return new(frame, complete ? 2 : 0);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Add(int offset, int value) => task = task.WriteWord(offset, unchecked((ushort)(Signed(offset) + value)));
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void Store()
        {
            hierarchy = new NativeBattleEffectHierarchy(frame.Effect.Pools).Write(address, task);
            frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        }
    }
}
