namespace Memoria.FF8.Runtime;

public sealed record NativeBattleScanPrimaryFrame(NativeBattleScanFrame Scan, BattleActorModels Models, ushort Highlight269AA90, short? CameraAngle3A);
public sealed record NativeBattleScanPrimaryStep(NativeBattleScanPrimaryFrame Frame, int Result);

/// <summary>84D4B0's normal-target timeline, retaining its real child tasks and their completion counters.</summary>
public static class NativeBattleScanPrimary
{
    public static NativeBattleScanPrimaryStep Advance84D4B0(NativeBattleScanPrimaryFrame frame, uint address, NativeBattleActionPacketsState packets)
    {
        NativeBattleEffectParameters parameters = frame.Scan.Effect.Parameters ?? throw new InvalidDataException("Native Scan primary requires its current action parameters.");
        NativeBattleEffectHierarchy hierarchy = new(frame.Scan.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address);
        if (task.Callback != 0x84D4B0)
        {
            throw new InvalidDataException("Native Scan primary requires its84D4B0 callback.");
        }
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                SetModelFlags(flags => (ushort)(flags | 0x7800));
                Append(2, 0x84E810, 0x78, 0);
                frame = frame with { Highlight269AA90 = 1 };
                Append(2, 0x84D5B0, 0x78);
                task = task.WriteWord(0x66, 15);
                Next();
                break;
            case 1:
                if (CountDown())
                {
                    Append(2, 0x84ED90, 0x78);
                    task = task.WriteWord(0x66, 16);
                    Next();
                }
                break;
            case 2:
                if (CountDown())
                {
                    Append(2, 0x84EFF0, 0x78);
                    task = task.WriteWord(0x66, 32);
                    Next();
                }
                break;
            case 3:
                if (CountDown())
                {
                    frame = frame with { Highlight269AA90 = 0 };
                    Next();
                }
                break;
            case 4:
                Append(2, 0x84E810, 0x78, 1);
                task = task.WriteWord(0x66, 8);
                Next();
                break;
            case 5:
                if (CountDown())
                {
                    Append(2, 0x84F860, 0x78);
                    Append(4, 0x84F300, 0x6C);
                    Append(4, 0x84F430, 0x6C);
                    Next();
                }
                break;
            case 6:
                if (task.ReadByte(0x28) == 0)
                {
                    NativeBattleModulePoolTask parent = hierarchy.Read(task.ReadDword(0x10));
                    if (unchecked((sbyte)task.ReadByte(0x2A)) == unchecked((short)parent.ReadWord(0x58)))
                    {
                        Append(2, 0x850A30, 0x78);
                    }
                    else if ((frame.CameraAngle3A ?? throw new NotSupportedException("Native Scan exit requires its actual camera angle WORD3A.")) <= -8192)
                    {
                        Append(2, 0x84E810, 0x78, 3);
                    }
                    Append(2, 0x8507F0, 0x78);
                    SetModelFlags(flags => (ushort)(flags & 0x87FF));
                    Next();
                }
                break;
            case 7:
                if (task.ReadByte(0x28) == 0)
                {
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                break;
            case 8:
                break;
            default:
                throw new InvalidDataException("Native Scan primary signed phase exceeds its nine callbacks.");
        }
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        hierarchy = hierarchy.Write(address, task);
        bool complete = (task.ReadByte(0x26) & 1) != 0 && task.ReadByte(0x28) == 0;
        if (complete)
        {
            hierarchy = hierarchy.Complete8DC530(address);
        }
        frame = frame with { Scan = frame.Scan with { Effect = frame.Scan.Effect with { Pools = hierarchy.Pools } } };
        return new(frame, complete ? 2 : 0);

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));

        bool CountDown()
        {
            task = task.WriteWord(0x66, unchecked((ushort)(task.ReadWord(0x66) - 1)));
            return unchecked((short)task.ReadWord(0x66)) < 0;
        }

        void Append(int pool, uint callback, ushort stride, ushort? mode = null)
        {
            // Appending increments this parent's child counter in the hierarchy.
            hierarchy = hierarchy.Write(address, task);
            NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(pool, callback, stride, address, (pointer, group, target) => NativeBattleCureController.SelectActors(parameters, packets, pointer, group, target));
            hierarchy = appended.Hierarchy;
            task = hierarchy.Read(address);
            if (mode is ushort value)
            {
                hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteWord(0x68, value));
            }
        }

        void SetModelFlags(Func<ushort, ushort> change)
        {
            BattleModelOwner target = task.ReadExecutionReference<BattleExecutionReference.Model>(0x2D, 1).Owner;
            NativeBattleGraphicsModel model = frame.Models.Get(target);
            frame = frame with { Models = frame.Models.Set(target, model with { Flags0 = change(model.Flags0) }) };
        }
    }
}
