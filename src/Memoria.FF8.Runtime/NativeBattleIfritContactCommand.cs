using System.Buffers.Binary;
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
        uint manager = CoreDword(frame, 0x27973EC);
        uint action = CoreDword(frame, unchecked(manager + 0xCC));
        uint contacts = BinaryPrimitives.ReadUInt32LittleEndian(ReadAction(unchecked(action + 8), 4));
        int count = NativeBattleCinematicContacts.ReadCount(frame, action, services.ReadActionBytes, services.ActionPackets, signedNative: true);
        if ((instruction & 0xF000) == 0x8000)
        {
            for (int index = 0; index < count; index++)
            {
                frame = services.ApplyContact506690(frame, NativeBattleCinematicContacts.Offset(contacts, index, services.ActionPackets));
            }
        }
        else if (savedActorSelection && instruction >> 12 == 1)
        {
            uint header = CoreDword(frame, 0x279744C);
            int saved = NativeBattleCinematicActorAccess.ReadSavedCount(frame, signedNative: true, headerAddress: header);
            if (saved == 0)
            {
                ApplyFirst(contacts, ReadSlot(frame, 0x1B, 1)[0]);
            }
            else
            {
                for (int index = saved; index > 0; index--)
                {
                    byte wanted = NativeBattleCinematicActorAccess.ReadSavedActor(frame, index - 1, header);
                    uint currentContacts = BinaryPrimitives.ReadUInt32LittleEndian(ReadAction(unchecked(action + 8), 4));
                    ApplyFirst(currentContacts, wanted);
                }
            }
        }
        else
        {
            ApplyFirst(contacts, ReadSlot(frame, 0x1B, 1)[0]);
        }
        return WriteCoreDword(frame, 0x2797450, unchecked(CoreDword(frame, 0x2797450) + 2));

        void ApplyFirst(uint first, byte wanted)
        {
            for (int index = 0; index < count; index++)
            {
                uint contact = NativeBattleCinematicContacts.Offset(first, index, services.ActionPackets);
                if (ReadAction(contact, 1)[0] == wanted)
                {
                    frame = services.ApplyContact506690(frame, contact);
                    return;
                }
            }
        }

        byte[] ReadAction(uint address, int length) => NativeBattleCinematicContacts.ReadBytes(frame, address, length, services.ReadActionBytes);
    }
}
