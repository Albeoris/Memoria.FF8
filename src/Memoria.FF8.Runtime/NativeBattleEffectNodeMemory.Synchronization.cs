namespace Memoria.FF8.Runtime;

internal partial class NativeBattleEffectNodeMemory
{
    internal void SynchronizePublishedStorage(NativeBattleEffectMemory published)
    {
        NativeBattleEffectArena arena = published.Arena ?? throw new NotSupportedException("Native node synchronization requires its current physical auxiliary owner.");
        ObjectDisposedException.ThrowIf(disposed, this);
        Dictionary<int, BattleExecutionReference.TraversalPosition> nextPositions = CaptureTraversalPositions(arena);
        NativeBattleReadWindow next = arena.OpenReadWindow(first, length);
        original.Dispose();
        original = next;
        originalTraversalPositions = nextPositions;
        cells.Clear();
        dirtyCells.Clear();
        traversalPositions.Clear();
        memory = published;
        scratch = published.NodeScratch2793E30;
    }
}
