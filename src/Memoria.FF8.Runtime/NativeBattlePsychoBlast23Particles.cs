namespace Memoria.FF8.Runtime;

public static class NativeBattlePsychoBlast23Particles
{
    public static NativeBattlePsychoBlast23LeafStep AdvanceEmitter(NativeBattlePsychoBlast23Frame frame, uint address, NativeBattlePsychoBlast23Inputs inputs, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleModulePoolTask task = NativeBattlePsychoBlast23Act.Read(frame, address);
        if (task.Callback != NativeBattlePsychoBlast23Profile.DebrisEmitterCallback)
        {
            throw new InvalidDataException("Psycho Blast debris emitter requires its original callback.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (phase == 0)
        {
            short age = NativeBattlePsychoBlast23Act.Word(task, 0x24);
            int count = age == 0 ? 8 : age == 1 ? 4 : 0;
            if (age == 1)
            {
                frame = NativeBattlePsychoBlast23Act.Put(frame, address, NativeBattlePsychoBlast23Act.Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
            }
            for (int index = 0; index < count; index++)
            {
                NativeBattleEffectHierarchyAppend append = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(5, NativeBattlePsychoBlast23Profile.DebrisCallback, 176, address, selectActors);
                frame = frame with { Effect = frame.Effect with { Pools = append.Hierarchy.Pools } };
                uint childAddress = append.Address;
                for (int point = 0; point < 4; point++)
                {
                    NativeBattleModulePoolTask child = NativeBattlePsychoBlast23Act.Read(frame, childAddress);
                    int position = 0x6C + point * 8;
                    int velocity = 0x8C + point * 8;
                    child = NativeBattlePsychoBlast23Act.Copy(frame.State.Basis56, 0x30, child, position, 8);
                    NativeCrtRandomState random = frame.Random.Draw(out ushort magnitude);
                    random = random.Draw(out ushort angle);
                    frame = frame with { Random = random };
                    child = child.WriteWord(velocity, 0).WriteWord(velocity + 2, (ushort)((magnitude & 0xFFF) + 0x400)).WriteWord(velocity + 4, 0);
                    NativeBattleActionMatrix matrix = NativeBattlePsychoBlast23Act.ReadMatrix(frame.State.Basis56, 0);
                    short rotation = (short)(angle & 0xFFF);
                    frame = inputs.Execute(NativeBattlePsychoBlast23Act.Put(frame, childAddress, child), new NativeBattlePsychoBlast23Rotate(matrix, 'Z', rotation, NativeBattlePsychoBlast23Act.Padding(frame.State.Basis56, 18)));
                    matrix = NativeBattleCuragaMatrices.RotateZ8DD960(matrix, rotation);
                    NativeBattleVector speed = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new(0, (short)((magnitude & 0xFFF) + 0x400), 0));
                    child = NativeBattlePsychoBlast23Act.Read(frame, childAddress).WriteWord(velocity, unchecked((ushort)speed.X)).WriteWord(velocity + 2, unchecked((ushort)speed.Y)).WriteWord(velocity + 4, unchecked((ushort)speed.Z));
                    frame = NativeBattlePsychoBlast23Act.Put(frame, childAddress, child);
                }
            }
        }
        else if (phase != 1)
        {
            throw new InvalidDataException("Psycho Blast debris emitter signed phase leaves its two original handlers.");
        }
        return NativeBattlePsychoBlast23Act.Finish(frame, address);
    }

    public static NativeBattlePsychoBlast23LeafStep Advance(NativeBattlePsychoBlast23Frame frame, uint address, NativeBattlePsychoBlast23Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattlePsychoBlast23Act.Read(frame, address);
        if (task.Callback != NativeBattlePsychoBlast23Profile.DebrisCallback)
        {
            throw new InvalidDataException("Psycho Blast debris requires its original callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = NativeBattlePsychoBlast23Act.Next(task.WriteDword(0x4C, 0x161428C).WriteWord(0x56, unchecked((ushort)-64)).WriteWord(0x52, 8));
                break;
            case 1:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                if (NativeBattlePsychoBlast23Act.Word(task, 0x50) > NativeBattlePsychoBlast23Act.Word(task, 0x52))
                {
                    task = NativeBattlePsychoBlast23Act.Next(task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Psycho Blast debris signed phase leaves its three original handlers.");
        }
        frame = NativeBattlePsychoBlast23Act.Put(frame, address, task);
        for (int point = 0; point < 4; point++)
        {
            task = NativeBattlePsychoBlast23Act.Read(frame, address);
            int position = 0x6C + point * 8;
            int velocity = 0x8C + point * 8;
            for (int axis = 0; axis < 3; axis++)
            {
                int offset = axis * 2;
                short previous = NativeBattlePsychoBlast23Act.Word(task, velocity + offset);
                short damped = unchecked((short)(previous - previous / 8));
                task = task.WriteWord(velocity + offset, unchecked((ushort)damped));
                task = task.WriteWord(position + offset, unchecked((ushort)(NativeBattlePsychoBlast23Act.Word(task, position + offset) + damped / 16)));
            }
            task = task.WriteDword(0x1C, task.ReadDword(position)).WriteDword(0x20, task.ReadDword(position + 4));
            frame = inputs.Execute(NativeBattlePsychoBlast23Act.Put(frame, address, task), new NativeBattlePsychoBlast23DebrisDraw(address));
        }
        return NativeBattlePsychoBlast23Act.Finish(frame, address);
    }
}
