using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleApocalypse221ActorTint69Services(Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGraphicsModel> ReadActor, Func<NativeBattleWaterExecutionFrame, uint, NativeBattleGraphicsModel, NativeBattleWaterExecutionFrame> WriteActor);

/// <summary>Applies or restores the current side's physical actor tint and original header flags.</summary>
public static class NativeBattleApocalypse221ActorTint69
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleApocalypse221ActorTint69Services services)
    {
        uint header = CoreDword(frame, 0x279744C);
        NativeBattleCinematicActorContext? context = NativeBattleCinematicActorAccess.ReadContext(frame, header);
        byte firstActor = NativeBattleCinematicActorAccess.ReadCurrentActor(frame, 0, header);
        IEnumerable<int> rows;
        if (context is null)
        {
            bool enemies = unchecked((sbyte)firstActor) <= 2;
            rows = enemies ? Enumerable.Range(3, 4) : Enumerable.Range(0, 3);
        }
        else
        {
            rows = context.Roster[firstActor].IsParty ? context.Roster.EnemyRows : context.Roster.PartyRows;
        }
        int red = unchecked((short)SlotWord(frame, 0x94));
        int green = unchecked((short)SlotWord(frame, 0x96));
        int blue = unchecked((short)SlotWord(frame, 0x98));
        int combined = red | green | blue;
        uint color = 0;
        if (combined > 0)
        {
            color = 0x02000000u | (uint)ReadSlot(frame, 0x98, 1)[0] << 16 | (uint)ReadSlot(frame, 0x96, 1)[0] << 8 | ReadSlot(frame, 0x94, 1)[0];
        }
        foreach (int index in rows)
        {
            uint pointer = NativeBattleCinematicActorAccess.ReadModelPointer(frame, index, header);
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
                ushort original = NativeBattleCinematicActorAccess.ReadCachedFlags0(frame, index, header);
                actor = combined < 0 ? actor with { Flags0 = original } : actor with { Flags0 = (ushort)(original | 0x800), Color40 = color };
            }
            frame = services.WriteActor(frame, pointer, actor);
        }
        return frame;
    }
}
