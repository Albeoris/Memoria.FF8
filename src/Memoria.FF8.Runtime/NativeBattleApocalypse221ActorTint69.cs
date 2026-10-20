using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleApocalypse221ActorTint69Services(Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGraphicsModel> ReadActor, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGraphicsModel, NativeBattleWaterExecutionFrame> WriteActor);

/// <summary>Applies or restores the current side's physical actor tint and original header flags.</summary>
public static class NativeBattleApocalypse221ActorTint69
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleApocalypse221ActorTint69Services services)
    {
        uint header = CoreDword(frame, 0x279744C);
        bool enemies = unchecked((sbyte)frame.Core.Read(header + 0x48, 1)[0]) <= 2;
        int first = enemies ? 3 : 0;
        int count = enemies ? 4 : 3;
        int red = unchecked((short)SlotWord(frame, 0x94));
        int green = unchecked((short)SlotWord(frame, 0x96));
        int blue = unchecked((short)SlotWord(frame, 0x98));
        int combined = red | green | blue;
        uint color = 0;
        if (combined > 0)
        {
            color = 0x02000000u | (uint)ReadSlot(frame, 0x98, 1)[0] << 16 | (uint)ReadSlot(frame, 0x96, 1)[0] << 8 | ReadSlot(frame, 0x94, 1)[0];
        }
        for (int index = first; index < first + count; index++)
        {
            uint pointer = CoreDword(frame, unchecked(header + 0x60u + (uint)(index * 4)));
            if (pointer == 0)
            {
                continue;
            }
            NativeBattleGraphicsModel actor = services.ReadActor(frame, pointer);
            if (combined == 0)
            {
                actor = actor with { Flags0 = (ushort)(actor.Flags0 | 0xC) };
            }
            else
            {
                ushort original = CoreWord(frame, unchecked(header + 0x4Eu + (uint)(index * 2)));
                actor = combined < 0 ? actor with { Flags0 = original } : actor with { Flags0 = (ushort)(original | 0x800), Color40 = color };
            }
            frame = services.WriteActor(frame, pointer, actor);
        }
        return frame;
    }
}
