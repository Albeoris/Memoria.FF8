namespace Memoria.FF8.Runtime;

public sealed record NativeBattleRestorationNodeAllocation(NativeBattleRestorationFrame Frame, int? ArenaOffset);

/// <summary>891610/891F90 and886BC0/887540 use the shared original allocator with their own40/100 and50/140 physical slots.</summary>
public static class NativeBattleRestorationNodes
{
    public static NativeBattleRestorationNodeAllocation AllocateEmitter(NativeBattleRestorationFrame frame, uint contextTask, uint parentPointer, sbyte sourceIndex, sbyte targetIndex)
    {
        NativeBattleRestorationNodeMemory memory = new(frame, contextTask);
        ushort cursor = memory.State.EmitterScanCursor ?? throw new NotSupportedException("Native restoration emitter allocation requires its actual scan WORD.");
        int? node = NativeBattleEffectNodeAllocation.AllocateEmitter(memory, memory.State.Emitters!.Value, memory.Profile.EmitterBytes / 108, ref cursor, parentPointer, sourceIndex, targetIndex);
        memory.State = memory.State with { EmitterScanCursor = cursor };
        return new(memory.Flush(), node);
    }

    public static NativeBattleRestorationNodeAllocation AllocateGeometry(NativeBattleRestorationFrame frame, uint contextTask, int emitter, sbyte sourceIndex)
    {
        NativeBattleRestorationNodeMemory memory = new(frame, contextTask);
        ushort cursor = memory.State.GeometryScanCursor ?? throw new NotSupportedException("Native restoration geometry allocation requires its actual scan WORD.");
        int? node = NativeBattleEffectNodeAllocation.AllocateGeometry(memory, memory.State.Geometry!.Value, memory.Profile.GeometryBytes / 672, ref cursor, emitter, sourceIndex);
        memory.State = memory.State with { GeometryScanCursor = cursor };
        return new(memory.Flush(), node);
    }

    public static NativeBattleRestorationFrame Unlink(NativeBattleRestorationFrame frame, uint contextTask, int node)
    {
        NativeBattleRestorationNodeMemory memory = new(frame, contextTask);
        memory.Unlink882960(node);
        return memory.Flush();
    }

    public static NativeBattleRestorationFrame AdvanceList(NativeBattleRestorationFrame frame, uint contextTask, Func<NativeBattleRestorationFrame, int, short, NativeBattleRestorationFrame> advance)
    {
        NativeBattleRestorationNodeMemory memory = new(frame, contextTask);
        NativeBattleEffectNodeAllocation.WalkList(() => memory, (node, kind) =>
        {
            if (kind is not (0 or 1))
            {
                return;
            }
            frame = advance(frame, node, kind);
            memory = new(frame, contextTask);
        });
        return frame;
    }
}
