namespace Memoria.FF8.Runtime;

public static class NativeBattleCinematicNextContact12E
{
    public static NativeBattleWaterExecutionFrame Execute(NativeBattleWaterExecutionFrame frame, NativeBattleWaterContextServices services)
    {
        BattleActionHeaderReference header = services.RequireCombatHeader(frame);
        NativeBattleActionPacketsState packets = services.ReadPackets();
        NativeBattleCinematicActorContext context = NativeBattleCinematicActorAccess.ReadContext(frame);
        if (packets.ReadHeader(header).ReadContactCount() > context.ContactsProcessed)
        {
            frame = services.ApplyContact506690(frame, packets.ResolveContact(header, context.ContactsProcessed));
            context = NativeBattleCinematicActorAccess.ReadContext(frame);
            int processed = checked(context.ContactsProcessed + 1);
            uint address = NativeBattleScriptedEffectMemory.Address + 0x3A;
            NativeBattleScriptedEffectMemory core = processed <= byte.MaxValue ? frame.Core.Write(address, [(byte)processed]) : frame.Core.MarkUnknown(address, 1);
            frame = frame with { Core = core with { PresentationActors = context.WithContactsProcessed(processed) } };
        }
        return NativeBattleScriptedCommandReads.Advance(frame, 2);
    }
}
