using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Applies or restores the opposite side's tint.</summary>
public static class NativeBattleApocalypse221ActorTint69
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, Func<NativeBattleWaterExecutionFrame, BattleSide, int, uint, NativeBattleWaterExecutionFrame> tintOpposingActors)
    {
        BattleModelOwner firstActor = NativeBattleCinematicActorAccess.ReadCurrentActor(frame, 0);
        BattleSide opposing = firstActor.Side == BattleSide.Allies ? BattleSide.Enemies : BattleSide.Allies;
        int red = unchecked((short)SlotWord(frame, 0x94));
        int green = unchecked((short)SlotWord(frame, 0x96));
        int blue = unchecked((short)SlotWord(frame, 0x98));
        int combined = red | green | blue;
        uint color = 0;
        if (combined > 0)
        {
            color = 0x02000000u | (uint)ReadSlot(frame, 0x98, 1)[0] << 16 | (uint)ReadSlot(frame, 0x96, 1)[0] << 8 | ReadSlot(frame, 0x94, 1)[0];
        }
        return tintOpposingActors(frame, opposing, combined, color);
    }

    internal static NativeBattleGraphicsModel ApplyTint(NativeBattleGraphicsModel actor, int combined, uint color, Func<ushort> readOriginalFlags)
    {
        if (combined == 0)
        {
            return actor with { Flags0 = (ushort)(actor.Flags0 | 0xC) };
        }
        ushort original = readOriginalFlags();
        return combined < 0 ? actor with { Flags0 = original } : actor with { Flags0 = (ushort)(original | 0x800), Color40 = color };
    }
}
