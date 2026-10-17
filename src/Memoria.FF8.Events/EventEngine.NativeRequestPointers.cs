namespace Memoria.FF8.Events;

public sealed record NativePartyRequestPointer(int Version, int LocalSelector, ushort PointerIndex);

public sealed partial class EventEngine
{
    private NativeScriptPointer ReadNativePartyRequestPointer(int objectId, int localSelector, SceneEventState scene)
    {
        if (NativeRuntimeProfile is not { IsVerified: true } || scene.NativeActorLayoutVersion != 1)
        {
            throw new NotSupportedException("Native party request pointers require their authenticated physical allocation.");
        }
        return program!.ResolveNativeRequestPointer(objectId, localSelector, scene.NativeActorObjectIds);
    }

    private EventScript ReadSavedFiberScript(FiberCheckpoint fiber, SceneEventState scene)
    {
        if (program is null || (uint)fiber.ObjectId >= program.Objects.Count)
        {
            throw new InvalidDataException("Saved request fiber refers to an absent physical object.");
        }
        if (fiber.NativePartyRequestPointer is not { } receipt)
        {
            return program!.Objects[fiber.ObjectId].Scripts.SingleOrDefault(script => script.Id == fiber.ScriptId) ?? throw new InvalidDataException("Saved fiber references an unknown declared script.");
        }
        if (receipt.Version != 1 || fiber.Initialization || fiber.NativeActorSlot is not byte slot || slot != fiber.Priority || slot >= 8)
        {
            throw new InvalidDataException("Saved physical request pointer lost its native actor-slot owner.");
        }
        NativeScriptPointer pointer;
        try
        {
            pointer = ReadNativePartyRequestPointer(fiber.ObjectId, receipt.LocalSelector, scene);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException("Saved physical request pointer has no current original allocation.", exception);
        }
        if (pointer.Index != receipt.PointerIndex || pointer.Script.Id != fiber.ScriptId)
        {
            throw new InvalidDataException("Saved physical request pointer differs from the original table entry.");
        }
        return pointer.Script;
    }
}
