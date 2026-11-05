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
        return ReadNativeRequestFiberScript(fiber.ObjectId, fiber.ScriptId, fiber.Priority, fiber.Initialization, fiber.NativeActorSlot, fiber.NativePartyRequestPointer, scene);
    }

    private EventScript ReadNativeRequestFiberScript(int objectId, int scriptId, int priority, bool initialization, byte? actorSlot, NativePartyRequestPointer? owner, SceneEventState scene)
    {
        if (program is null || (uint)objectId >= program.Objects.Count)
        {
            throw new InvalidDataException("Saved request fiber refers to an absent physical object.");
        }
        if (owner is not { } receipt)
        {
            return program!.Objects[objectId].Scripts.SingleOrDefault(script => script.Id == scriptId) ?? throw new InvalidDataException("Saved fiber references an unknown declared script.");
        }
        if (receipt.Version != 1 || initialization || actorSlot is not byte slot || slot != priority || slot >= 8)
        {
            throw new InvalidDataException("Saved physical request pointer lost its native actor-slot owner.");
        }
        NativeScriptPointer pointer;
        try
        {
            pointer = ReadNativePartyRequestPointer(objectId, receipt.LocalSelector, scene);
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException("Saved physical request pointer has no current original allocation.", exception);
        }
        if (pointer.Index != receipt.PointerIndex || pointer.Script.Id != scriptId)
        {
            throw new InvalidDataException("Saved physical request pointer differs from the original table entry.");
        }
        return pointer.Script;
    }
}
