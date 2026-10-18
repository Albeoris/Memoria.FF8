using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleBio22StageFadeStep(NativeBattleModulePoolTask Task, NativeBattleStageState Stage, uint Result);

/// <summary>The six native stage-depth phases over four physical stage rows.</summary>
public static class NativeBattleBio22StageFade
{
    public static NativeBattleBio22StageFadeStep Advance89B8A0(NativeBattleModulePoolTask task, NativeBattleModulePoolTask parent, NativeBattleStageState stage)
    {
        switch (unchecked((sbyte)task.ReadByte(0x29)))
        {
            case 0:
                task = task.WriteWord(0x1C, 0);
                Publish(0, true);
                Next();
                break;
            case 1:
                short rising = unchecked((short)(task.ReadWord(0x1C) + 0x80));
                if (rising >= 0x400)
                {
                    rising = 0x400;
                    Next();
                }
                task = task.WriteWord(0x1C, unchecked((ushort)rising));
                Publish(rising, false);
                break;
            case 2:
                if (parent.ReadWord(0x80) != 0)
                {
                    task = task.WriteWord(0x1E, 0);
                    Next();
                }
                break;
            case 3:
                short elapsed = unchecked((short)(task.ReadWord(0x1E) + 1));
                task = task.WriteWord(0x1E, unchecked((ushort)elapsed));
                if (elapsed >= 4)
                {
                    Next();
                }
                break;
            case 4:
                short falling = unchecked((short)(task.ReadWord(0x1C) - 0x80));
                if (falling <= 0)
                {
                    falling = 0;
                    task = task.WriteByte(0x26, (byte)(task.ReadByte(0x26) | 1));
                    Next();
                }
                task = task.WriteWord(0x1C, unchecked((ushort)falling));
                Publish(falling, false);
                break;
            case 5:
                break;
            default:
                throw new NotSupportedException("Native Bio stage fade has an unsupported signed phase selector.");
        }
        byte flags = task.ReadByte(0x26);
        task = task.WriteWord(0x24, unchecked((ushort)(task.ReadWord(0x24) + 1)));
        uint result = (flags & 1) != 0 && task.ReadByte(0x28) == 0 ? 2u : 0u;
        return new(task, stage, result);

        void Next() => task = task.WriteByte(0x29, unchecked((byte)(task.ReadByte(0x29) + 1)));
        void Publish(short depth, bool clearColor)
        {
            stage.Validate();
            stage = stage with { Groups = stage.Groups.Select(group => group with { ColorDepth = depth, ColorFlags = clearColor ? group.ColorFlags & 0xFF000000u : group.ColorFlags }).ToImmutableArray() };
        }
    }
}
