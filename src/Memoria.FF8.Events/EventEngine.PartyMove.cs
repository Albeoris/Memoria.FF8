using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private bool IsNativePartyMove(int operation) => (uint)operation < program!.Operations.Count && program.Operations[operation].Opcode == Jsm.Opcode.PMOVEA;

    private bool IsNativeAsyncPartyMove(int operation) => (uint)operation < program!.Operations.Count && program.Operations[operation].Opcode == Jsm.Opcode.RPMOVEA;

    private (int NativeObjectId, FieldFixedPosition Position)? ReadNativePartyMoveTarget(int slot)
    {
        //523880/524310 index saved BYTE194 with the full DWORD. Only the three
        //owned party cells are admitted; neither BYTE wrapping nor FF bodies.
        if ((uint)slot >= 3 || services.NativeVariableValues?.Any(pair => pair.Key <= 194 + slot && (long)pair.Key + pair.Value.Width > 194 + slot) == true)
        {
            return null;
        }
        try
        {
            ActorState? target = ResolveNativeIndexedPartyBody(slot, out _, out _);
            FieldFixedPosition? raw = target is null ? null : ReadNativeScriptPosition(target);
            if (target is null || raw is null)
            {
                return null;
            }
            for (int index = 0; index < program!.NativeObjects.Count; index++)
            {
                if (program.NativeObjects[index] == target.ObjectId)
                {
                    return (index, raw);
                }
            }
        }
        catch (NotSupportedException)
        {
            return null;
        }
        return null;
    }
}
