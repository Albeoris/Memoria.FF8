using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

/// <summary>Cinematic contact selection over current action packets, with explicit saved-actor selection.</summary>
public static class NativeBattleIfritContactCommand
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services)
    {
        return Execute(frame, services, false);
    }

    public static NativeBattleWaterExecutionFrame ExecuteSavedActors(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services)
    {
        return Execute(frame, services, true);
    }

    private static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services, bool savedActorSelection)
    {
        ushort instruction = frame.ReadTemporaryWord(0x14A);
        if ((instruction & 0x01FF) != 0x022)
        {
            throw new NotSupportedException("Native cinematic contact command requires its selected instruction.");
        }
        BattleActionHeaderReference header = services.RequireCombatHeader(frame);
        int count = services.ReadPackets().ReadHeader(header).ReadContactCount();
        if ((instruction & 0xF000) == 0x8000)
        {
            for (int index = 0; index < count; index++)
            {
                frame = services.ApplyContact506690(frame, services.ReadPackets().ResolveContact(header, index));
            }
        }
        else if (savedActorSelection && instruction >> 12 == 1)
        {
            int saved = NativeBattleCinematicActorAccess.ReadSavedCount(frame);
            if (saved == 0)
            {
                ApplyFirst(NativeBattleCinematicActorAccess.ReadSlotActor(frame));
            }
            else
            {
                for (int index = saved; index > 0; index--)
                {
                    ApplyFirst(NativeBattleCinematicActorAccess.ReadSavedActor(frame, index - 1));
                }
            }
        }
        else
        {
            ApplyFirst(NativeBattleCinematicActorAccess.ReadSlotActor(frame));
        }
        return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + 2));

        void ApplyFirst(BattleModelOwner wanted)
        {
            NativeBattleActionPacketsState packets = services.ReadPackets();
            for (int index = 0; index < count; index++)
            {
                BattleContactOccurrence occurrence = packets.ResolveContact(header, index);
                if (packets.ReadContact(occurrence).Primary.Recipient == wanted)
                {
                    frame = services.ApplyContact506690(frame, occurrence);
                    return;
                }
            }
        }
    }
}
