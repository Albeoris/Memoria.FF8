using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private int? CaptureGameplayPartyRequestSelector(Fiber fiber)
    {
        if (fiber.NativePartyRequestPointer is not { } owner)
        {
            return null;
        }
        _ = ReadNativeRequestFiberScript(fiber.ObjectId, fiber.Script.Id, fiber.Priority, fiber.Initialization, fiber.NativeActorSlot, owner, Scene);
        return owner.LocalSelector;
    }

    private EventScript ReadGameplayRequestScript(GameplaySaveScriptFiber source, out NativePartyRequestPointer? owner)
    {
        owner = null;
        if (source.PartyRequestSelector is int selector)
        {
            NativeScriptPointer pointer;
            try
            {
                pointer = ReadNativePartyRequestPointer(source.ObjectId, selector, Scene);
            }
            catch (NotSupportedException exception)
            {
                throw new InvalidDataException("Gameplay indexed-party request has no current copied instruction owner.", exception);
            }
            owner = new(1, selector, checked((ushort)pointer.Index));
        }
        return ReadNativeRequestFiberScript(source.ObjectId, source.ScriptId, source.Priority, source.Initialization, source.ActorSlot, owner, Scene);
    }
}
