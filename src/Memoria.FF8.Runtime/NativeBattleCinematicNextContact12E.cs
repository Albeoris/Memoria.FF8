using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleWaterExecutionFields;

namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicNextContact12E
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services)
    {
        uint manager = CoreDword(frame, 0x27973EC);
        uint action = CoreDword(frame, manager + 0xCC);
        uint owner = CoreDword(frame, 0x279744C);
        bool hosted = services.ActionPackets?.PresentationRoster is not null;
        NativeBattleCinematicActorContext? context = NativeBattleCinematicActorAccess.ReadContext(frame);
        if (hosted && context is null)
        {
            throw new InvalidDataException("Hosted cinematic next contact requires its actor context cursor.");
        }
        int index = context?.ContactCursor ?? frame.Core.Read(owner + 0x3A, 1)[0];
        int count = NativeBattleCinematicContacts.ReadCount(frame, action, services.ReadActionBytes, services.ActionPackets, signedNative: true);
        if (count > index)
        {
            uint contacts = BinaryPrimitives.ReadUInt32LittleEndian(NativeBattleCinematicContacts.ReadBytes(frame, action + 8, 4, services.ReadActionBytes));
            frame = services.ApplyContact506690(frame, NativeBattleCinematicContacts.Offset(contacts, index, services.ActionPackets));
            owner = CoreDword(frame, 0x279744C);
            if (context is not null)
            {
                context = NativeBattleCinematicActorAccess.ReadContext(frame) ?? throw new InvalidDataException("Hosted cinematic contact lost its actor context.");
                frame = frame with { Core = frame.Core with { PresentationActors = context.WithContactCursor(checked(context.ContactCursor + 1)) } };
            }
            else
            {
                byte current = frame.Core.Read(owner + 0x3A, 1)[0];
                frame = frame with { Core = frame.Core.Write(owner + 0x3A, [unchecked((byte)(current + 1))]) };
            }
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }
}
