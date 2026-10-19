using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRinoa88StageStep(NativeBattleModulePoolTask Task, NativeBattleStageState Stage, bool ReadyToComplete);

public static class NativeBattleRinoa88Stage
{
    public static NativeBattleStageState Visibility(NativeBattleStageState stage, uint callback)
    {
        stage.Validate();
        return callback switch
        {
            0x769DB0 => stage with { Groups = stage.Groups.Select(group => group with { Flags = (byte)(group.Flags & 0xFD) }).ToImmutableArray() },
            0x769E60 => stage with { Groups = stage.Groups.Select(group => group with { Flags = (byte)((group.Flags & 0xFD) | ((group.Flags >> 1) & 2)) }).ToImmutableArray() },
            _ => throw new InvalidDataException("Native Rinoa88 stage visibility requires its actual reached callback.")
        };
    }

    public static NativeBattleRinoa88StageStep Advance771510(NativeBattleModulePoolTask task, NativeBattleStageState stage)
    {
        stage.Validate();
        if (task.Callback != 0x771510)
        {
            throw new InvalidDataException("Native771510 requires its actual144-byte stage color task.");
        }
        switch (unchecked((sbyte)task.ReadByte(41)))
        {
            case 0:
                task = task.WriteWord(28, 0);
                stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = 0, ColorFlags = group.ColorFlags & 0xFF000000 }).ToImmutableArray() };
                Next();
                break;
            case 1:
                Add(28, 384);
                if (Signed(28) >= 2560)
                {
                    task = task.WriteWord(28, 2560);
                    Next();
                }
                PublishDepth();
                break;
            case 2:
                if (Signed(36) > 30)
                {
                    Next();
                }
                break;
            case 3:
                Add(28, -256);
                if (Signed(28) <= 0)
                {
                    task = task.WriteWord(28, 0).WriteByte(38, (byte)(task.ReadByte(38) | 1));
                    Next();
                }
                PublishDepth();
                break;
            case 4:
                break;
            default:
                throw new InvalidDataException("Native771510 signed phase exceeds its five callbacks.");
        }
        Add(36, 1);
        return new(task, stage, (task.ReadByte(38) & 1) != 0 && task.ReadByte(40) == 0);

        short Signed(int offset) => unchecked((short)task.ReadWord(offset));
        void Add(int offset, int value) => task = task.WriteWord(offset, unchecked((ushort)(Signed(offset) + value)));
        void Next() => task = task.WriteByte(41, unchecked((byte)(task.ReadByte(41) + 1)));
        void PublishDepth()
        {
            short depth = Signed(28);
            stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth }).ToImmutableArray() };
        }
    }
}
