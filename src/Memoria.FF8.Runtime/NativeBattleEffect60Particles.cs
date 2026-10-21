namespace Memoria.FF8.Runtime;

public static class NativeBattleEffect60Particles
{
    public static NativeBattleEffect60LeafStep AdvanceEmitter(NativeBattleEffect60Frame frame, uint address, NativeBattleEffect60Inputs inputs, Func<uint, sbyte, sbyte, NativeBattleEffectTaskActors> selectActors)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect60Act.Read(frame, address);
        if (task.Callback != NativeBattleEffect60Profile.DebrisEmitterCallback)
        {
            throw new InvalidDataException("Effect60 emitter requires its original callback.");
        }
        int phase = unchecked((sbyte)task.ReadByte(0x29));
        if (phase == 0)
        {
            if (NativeBattleEffect60Act.Word(task, 0x24) >= 36)
            {
                short angle = inputs.ReadModel(task.ReadByte(0x2D)).Rotation14 ?? throw new NotSupportedException("Effect60 emitter requires its target yaw.");
                NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
                for (int index = 0; index < 9; index++)
                {
                    task = task.WriteWord(0x58 + index * 2, unchecked((ushort)identity.Rotation[index]));
                }
                task = task.WriteDword(0x6C, 0).WriteDword(0x70, 0).WriteDword(0x74, 0);
                frame = inputs.Execute(NativeBattleEffect60Act.Put(frame, address, task), new NativeBattleEffect60Rotate(identity, 'Y', angle, NativeBattleEffect60Act.Word(task, 0x6A)));
                NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.RotateY8DD8A0(identity, angle);
                task = NativeBattleEffect60Act.Read(frame, address);
                for (int index = 0; index < 9; index++)
                {
                    task = task.WriteWord(0x58 + index * 2, unchecked((ushort)matrix.Rotation[index]));
                }
                frame = NativeBattleEffect60Act.Put(frame, address, NativeBattleEffect60Act.Next(task));
            }
        }
        else if (phase == 1)
        {
            NativeBattleEffectArena position = NativeBattleEffect60Act.RequirePacked8(inputs.ReadPackedPosition8(task.ReadByte(0x2D)));
            position = NativeBattleEffect60Act.WriteWord(position, 2, 0);
            task = NativeBattleEffect60Act.Copy(position, 0, task, 0x1C, 8);
            frame = NativeBattleEffect60Act.Put(frame, address, task);
            NativeBattleActionMatrix matrix = NativeBattleEffect60Act.ReadMatrix(task, 0x58);
            for (int index = 0; index < 2; index++)
            {
                NativeBattleEffectHierarchyAppend append = new NativeBattleEffectHierarchy(frame.Effect.Pools).Append8DC540(5, NativeBattleEffect60Profile.DebrisCallback, 140, address, selectActors);
                frame = frame with { Effect = frame.Effect with { Pools = append.Hierarchy.Pools } };
                NativeBattleModulePoolTask child = NativeBattleEffect60Act.Copy(position, 0, append.Hierarchy.Read(append.Address), 0x1C, 8);
                NativeCrtRandomState random = frame.Random.Draw(out ushort gravity);
                child = child.WriteWord(0x82, unchecked((ushort)-(gravity & 0x7F)));
                random = random.Draw(out ushort x);
                random = random.Draw(out ushort z);
                NativeBattleVector offset = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new((short)((x & 0x1FF) - 0x100), 0, (short)((z & 0xFF) - 0x80)));
                child = child.WriteWord(0x1C, unchecked((ushort)(NativeBattleEffect60Act.Word(child, 0x1C) + offset.X))).WriteWord(0x1E, unchecked((ushort)(NativeBattleEffect60Act.Word(child, 0x1E) + offset.Y))).WriteWord(0x20, unchecked((ushort)(NativeBattleEffect60Act.Word(child, 0x20) + offset.Z)));
                random = random.Draw(out ushort vx);
                random = random.Draw(out ushort vz);
                NativeBattleVector velocity = NativeBattleGfMatrices.RotateShort56C4F0(matrix, new((short)((vx & 0x1FF) - 0x100), 0, (short)(-512 - (vz & 0x7FF))));
                child = child.WriteWord(0x78, unchecked((ushort)velocity.X)).WriteWord(0x7A, unchecked((ushort)velocity.Y)).WriteWord(0x7C, unchecked((ushort)velocity.Z));
                frame = NativeBattleEffect60Act.Put(frame with { Random = random }, append.Address, child);
            }
            task = NativeBattleEffect60Act.Read(frame, address);
            if (NativeBattleEffect60Act.Word(task, 0x24) >= 44)
            {
                frame = NativeBattleEffect60Act.Put(frame, address, NativeBattleEffect60Act.Next(task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1))));
            }
        }
        else if (phase != 2)
        {
            throw new InvalidDataException("Effect60 emitter signed phase leaves its original handlers.");
        }
        return NativeBattleEffect60Act.Finish(frame, address);
    }

    public static NativeBattleEffect60LeafStep Advance(NativeBattleEffect60Frame frame, uint address, NativeBattleEffect60Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect60Act.Read(frame, address);
        if (task.Callback != NativeBattleEffect60Profile.DebrisCallback)
        {
            throw new InvalidDataException("Effect60 debris requires its original callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = NativeBattleEffect60Act.Next(task.WriteDword(0x4C, 0x1597360).WriteWord(0x56, unchecked((ushort)-64)).WriteWord(0x52, 15));
                break;
            case 1:
                task = task.WriteWord(0x50, unchecked((ushort)(task.ReadWord(0x50) + 1)));
                if (NativeBattleEffect60Act.Word(task, 0x50) > NativeBattleEffect60Act.Word(task, 0x52))
                {
                    task = NativeBattleEffect60Act.Next(task.WriteWord(0x50, task.ReadWord(0x52)).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 5)));
                }
                break;
            case 2:
                break;
            default:
                throw new InvalidDataException("Effect60 debris signed phase leaves its original handlers.");
        }
        task = task.WriteWord(0x7A, unchecked((ushort)(task.ReadWord(0x7A) + task.ReadWord(0x82))));
        for (int axis = 0; axis < 3; axis++)
        {
            int offset = axis * 2;
            short previous = NativeBattleEffect60Act.Word(task, 0x78 + offset);
            short damped = unchecked((short)(previous - previous / 8));
            task = task.WriteWord(0x78 + offset, unchecked((ushort)damped));
            task = task.WriteWord(0x1C + offset, unchecked((ushort)(NativeBattleEffect60Act.Word(task, 0x1C + offset) + damped / 16)));
        }
        frame = inputs.Execute(NativeBattleEffect60Act.Put(frame, address, task), new NativeBattleEffect60DebrisDraw(address));
        return NativeBattleEffect60Act.Finish(frame, address);
    }

    public static NativeBattleEffect60LeafStep AdvanceStage(NativeBattleEffect60Frame frame, uint address, NativeBattleEffect60Inputs inputs)
    {
        NativeBattleModulePoolTask task = NativeBattleEffect60Act.Read(frame, address);
        if (task.Callback != NativeBattleEffect60Profile.StageCallback)
        {
            throw new InvalidDataException("Effect60 stage requires its original callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                if (NativeBattleEffect60Act.Word(task, 0x24) >= 5)
                {
                    task = NativeBattleEffect60Act.Next(task.WriteWord(0x1C, 0));
                    frame = inputs.Execute(NativeBattleEffect60Act.Put(frame, address, task), new NativeBattleEffect60StageInitialize());
                }
                break;
            case 1:
                short up = unchecked((short)(NativeBattleEffect60Act.Word(task, 0x1C) + 256));
                if (up >= 1536)
                {
                    task = NativeBattleEffect60Act.Next(task.WriteWord(0x1C, 1536));
                }
                else
                {
                    task = task.WriteWord(0x1C, unchecked((ushort)up));
                }
                frame = inputs.Execute(NativeBattleEffect60Act.Put(frame, address, task), new NativeBattleEffect60StageFade(NativeBattleEffect60Act.Word(task, 0x1C)));
                break;
            case 2:
                if (NativeBattleEffect60Act.Word(task, 0x24) >= 54)
                {
                    frame = NativeBattleEffect60Act.Put(frame, address, NativeBattleEffect60Act.Next(task));
                }
                break;
            case 3:
                short down = unchecked((short)(NativeBattleEffect60Act.Word(task, 0x1C) - 256));
                if (down <= 0)
                {
                    task = NativeBattleEffect60Act.Next(task.WriteWord(0x1C, 0).WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1)));
                }
                else
                {
                    task = task.WriteWord(0x1C, unchecked((ushort)down));
                }
                frame = inputs.Execute(NativeBattleEffect60Act.Put(frame, address, task), new NativeBattleEffect60StageFade(NativeBattleEffect60Act.Word(task, 0x1C)));
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Effect60 stage signed phase leaves its original handlers.");
        }
        return NativeBattleEffect60Act.Finish(frame, address);
    }
}
