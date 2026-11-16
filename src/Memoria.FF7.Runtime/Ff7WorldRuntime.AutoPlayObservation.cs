using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7WorldMeshDispatchObservation(int ChunkX, int ChunkZ, int Event, int Entry, int CallDepth, bool Accepted, long Tick);

internal sealed record Ff7WorldModelDispatchObservation(int Model, int Event, int Entry, int CallDepth, bool Accepted, long Tick);

public sealed partial class Ff7WorldRuntime
{
    internal event Action<Ff7WorldMeshDispatchObservation>? NativeMeshDispatched;
    internal event Action<WorldHostRequest>? NativeHostOperationObserved;
    internal event Action<Ff7WorldModelDispatchObservation>? NativeModelDispatched;

    private void StartObservedTerrainScript(int chunkX, int chunkZ, int eventId)
    {
        if (NativeMeshDispatched is null)
        {
            Machine.StartMesh(chunkX, chunkZ, eventId);
            return;
        }
        WorldEntitySnapshot before = Machine.Capture().System;
        Machine.StartMesh(chunkX, chunkZ, eventId);
        WorldEntitySnapshot after = Machine.Capture().System;
        bool accepted = after.Priority == 3 && (after.ProgramCounter != before.ProgramCounter || after.Calls.Length > before.Calls.Length);
        NativeMeshDispatched.Invoke(new(chunkX, chunkZ, eventId, after.ProgramCounter, after.Calls.Length, accepted, PresentationTick));
    }

    public void ModelScriptCompleted(int model, int? freeModelIndex = null)
    {
        if (model < 0 || !movementEnabled || Machine.IsUnloading)
        {
            return;
        }
        Ff7WorldEntity source = ResolveHostEntity(model, freeModelIndex);
        if (source.ContactModel is int contact && entities.ContainsKey(contact))
        {
            StartObservedModelScript(contact, (input.Held & 32) != 0 ? 4 : 3);
        }
    }

    private void StartObservedModelScript(int model, int eventId)
    {
        if (NativeModelDispatched is null)
        {
            Machine.StartModel(model, eventId);
            return;
        }
        WorldEntitySnapshot? before = Machine.Capture().Models.SingleOrDefault(entity => entity.Model == model);
        Machine.StartModel(model, eventId);
        WorldEntitySnapshot? after = Machine.Capture().Models.SingleOrDefault(entity => entity.Model == model);
        bool accepted = before is not null && after is not null && after.Priority == Math.Clamp(eventId - 2, 0, 3) && (after.ProgramCounter != before.ProgramCounter || after.Calls.Length > before.Calls.Length);
        NativeModelDispatched.Invoke(new(model, eventId, after?.ProgramCounter ?? 0, after?.Calls.Length ?? 0, accepted, PresentationTick));
    }

}
