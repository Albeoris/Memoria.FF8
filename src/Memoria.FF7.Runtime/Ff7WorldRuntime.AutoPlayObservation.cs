using Memoria.FF7.Events;

namespace Memoria.FF7.Runtime;

internal sealed record Ff7WorldMeshDispatchObservation(int ChunkX, int ChunkZ, int Event, int Entry, int CallDepth, bool Accepted, long Tick);

public sealed partial class Ff7WorldRuntime
{
    internal event Action<Ff7WorldMeshDispatchObservation>? NativeMeshDispatched;
    internal event Action<WorldHostRequest>? NativeHostOperationObserved;

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
}
