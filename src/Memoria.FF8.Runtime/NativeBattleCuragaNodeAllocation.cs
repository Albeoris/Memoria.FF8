namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCuragaNodeAllocation(NativeBattleCuragaFrame Frame, int? ArenaOffset);

/// <summary>881700/882080's exact scan/clear/link allocators and8817F0's signed emitter lifetime.</summary>
public static class NativeBattleCuragaNodes
{
    public static NativeBattleCuragaNodeAllocation AllocateEmitter881700(NativeBattleCuragaFrame frame, uint contextTask, uint parentPointer, sbyte sourceIndex, sbyte targetIndex)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        int? node = AllocateEmitter(memory, parentPointer, sourceIndex, targetIndex);
        return new(memory.Flush(), node);
    }

    public static NativeBattleCuragaNodeAllocation AllocateGeometry882080(NativeBattleCuragaFrame frame, uint contextTask, int emitter, sbyte sourceIndex)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        int? node = AllocateGeometry(memory, emitter, sourceIndex);
        return new(memory.Flush(), node);
    }

    public static NativeBattleCuragaFrame Unlink882960(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        memory.Unlink882960(node);
        return memory.Flush();
    }

    public static NativeBattleCuragaFrame AdvanceEmitterLifetime8817F0(NativeBattleCuragaFrame frame, uint contextTask, int node)
    {
        NativeBattleCuragaNodeMemory memory = new(frame, contextTask);
        AdvanceEmitterLifetime(memory, node);
        return memory.Flush();
    }

    internal static void AdvanceEmitterLifetime(NativeBattleCuragaNodeMemory memory, int node)
    {
        NativeBattleEffectNodeLifecycle.AdvanceEmitterLifetime(memory, node);
    }

    internal static int? AllocateEmitter(NativeBattleCuragaNodeMemory memory, uint parentPointer, sbyte sourceIndex, sbyte targetIndex)
    {
        int start = memory.State.Auxiliary25F8 ?? throw new NotSupportedException("Native881700 requires87DE10's emitter pool.");
        ushort cursor = memory.State.Counter26D76C0 ?? throw new NotSupportedException("Native881700 requires the actual26D76C0 scan cursor.");
        int? node = NativeBattleEffectNodeAllocation.AllocateEmitter(memory, start, memory.Profile.EmitterCapacity, ref cursor, parentPointer, sourceIndex, targetIndex);
        memory.State = memory.State with { Counter26D76C0 = cursor };
        return node;
    }

    internal static int? AllocateGeometry(NativeBattleCuragaNodeMemory memory, int emitter, sbyte sourceIndex)
    {
        int start = memory.State.Auxiliary16F80 ?? throw new NotSupportedException("Native882080 requires87DE10's geometry pool.");
        ushort cursor = memory.State.Counter26D82E8 ?? throw new NotSupportedException("Native882080 requires the actual26D82E8 scan cursor.");
        int? node = NativeBattleEffectNodeAllocation.AllocateGeometry(memory, start, memory.Profile.GeometryCapacity, ref cursor, emitter, sourceIndex);
        memory.State = memory.State with { Counter26D82E8 = cursor };
        return node;
    }
}
