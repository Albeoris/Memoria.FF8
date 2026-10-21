namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect20Particles
{
    public static NativeBattleEffect20Frame Spawn(NativeBattleEffect20Frame frame, uint parentAddress, short count, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeCrtRandomState random = frame.Random;
        for (int index = 0; index < count; index++)
        {
            NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(3, NativeBattleEffect20Profile.ParticleCallback, 152, parentAddress, selectActors);
            hierarchy = append.Hierarchy;
            NativeBattleModulePoolTask child = hierarchy.Read(append.Address);
            NativeBattleModulePoolTask parent = hierarchy.Read(parentAddress);
            for (int point = 0; point < 2; point++)
            {
                int position = 0x54 + point * 8;
                int velocity = 0x74 + point * 8;
                child = child.WriteDword(position, parent.ReadDword(0x1C)).WriteDword(position + 4, parent.ReadDword(0x20));
                random = random.Draw(out ushort x);
                random = random.Draw(out ushort z);
                random = random.Draw(out ushort y);
                child = child.WriteWord(velocity, unchecked((ushort)((x & 0x3FFF) - 0x2000))).WriteWord(velocity + 4, unchecked((ushort)((z & 0x3FFF) - 0x2000))).WriteWord(velocity + 2, unchecked((ushort)(-256 - (y & 0xFFF))));
            }
            hierarchy = hierarchy.Write(append.Address, child);
        }
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Pools }, Random = random };
    }

    public static NativeBattleEffect20LeafStep AdvanceSprite(NativeBattleEffect20Frame frame, uint address, NativeBattleEffect20Inputs inputs, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != NativeBattleEffect20Profile.SpriteCallback)
        {
            throw new InvalidDataException("Effect20 sprite requires its original callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x4C, 0x1629C54).WriteWord(0x56, unchecked((ushort)-512)).WriteWord(0x52, 15);
                task = Next(task);
                break;
            case 1:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                if (Word(task, 0x50) > Word(task, 0x52))
                {
                    task = task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5));
                    task = Next(task);
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect20 sprite signed phase leaves its three original handlers.");
        }
        frame = With(frame, hierarchy.Write(address, task));
        if (task.ReadWord(0x24) is 0 or 1)
        {
            frame = Spawn(frame, address, 1, selectActors);
        }
        frame = inputs.Execute(frame, new NativeBattleEffect20SpriteDraw(address));
        return Finish(frame, address);
    }

    public static NativeBattleEffect20LeafStep AdvanceParticle(NativeBattleEffect20Frame frame, uint address, NativeBattleEffect20Inputs inputs)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != NativeBattleEffect20Profile.ParticleCallback)
        {
            throw new InvalidDataException("Effect20 particle requires its original callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteDword(0x4C, 0x1629E00).WriteDword(0x30, 768).WriteDword(0x34, 768).WriteDword(0x38, 768);
                NativeCrtRandomState random = frame.Random.Draw(out ushort angle);
                frame = frame with { Random = random };
                task = Next(task.WriteWord(0x48, (ushort)(angle & 0xFFF)));
                break;
            case 1:
                task = task.WriteWord(0x48, unchecked((ushort)(task.ReadWord(0x48) + 32)));
                if (Word(task, 0x24) >= 8)
                {
                    int scale = unchecked((int)task.ReadDword(0x38) - 192);
                    task = task.WriteDword(0x38, unchecked((uint)scale));
                    if (scale <= 0)
                    {
                        task = Next(task.WriteDword(0x38, 16).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                    }
                    task = task.WriteDword(0x30, task.ReadDword(0x38)).WriteDword(0x34, task.ReadDword(0x38));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect20 particle signed phase leaves its three original handlers.");
        }
        for (int point = 0; point < 2; point++)
        {
            int velocity = 0x74 + point * 8;
            int position = 0x54 + point * 8;
            task = task.WriteWord(velocity + 2, unchecked((ushort)(task.ReadWord(velocity + 2) + 256)));
            for (int axis = 0; axis < 3; axis++)
            {
                int field = velocity + axis * 2;
                short speed = Word(task, field);
                speed = unchecked((short)(speed - speed / 8));
                task = task.WriteWord(field, unchecked((ushort)speed));
                task = task.WriteWord(position + axis * 2, unchecked((ushort)(Word(task, position + axis * 2) + speed / 16)));
            }
        }
        frame = With(frame, hierarchy.Write(address, task));
        for (int point = 0; point < 2; point++)
        {
            hierarchy = new(frame.Effect.Pools);
            task = hierarchy.Read(address);
            task = task.WriteDword(0x1C, task.ReadDword(0x54 + point * 8)).WriteDword(0x20, task.ReadDword(0x58 + point * 8));
            frame = inputs.Execute(With(frame, hierarchy.Write(address, task)), new NativeBattleEffect20ParticleDraw(address));
        }
        return Finish(frame, address);
    }

    internal static NativeBattleEffect20LeafStep Finish(NativeBattleEffect20Frame frame, uint address)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        return new(With(frame, hierarchy), complete ? 2 : 0);
    }

    private static NativeBattleEffect20Frame With(NativeBattleEffect20Frame frame, NativeBattleEffectHierarchy hierarchy) => frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
    private static NativeBattleModulePoolTask Next(NativeBattleModulePoolTask task) => task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
    private static short Word(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
}
