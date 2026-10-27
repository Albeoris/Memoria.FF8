using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel112WorkspaceStep(NativeBattleDuel112Frame Frame, NativeBattleLogicalOrdering Ordering, int Remaining);

public static class NativeBattleDuel112Workspaces
{
    public static NativeBattleDuel112Frame Initialize7016B0(NativeBattleDuel112Frame frame, int slot, int offset)
    {
        (uint sourceAddress, int size) = Contract(offset);
        NativeBattleDuel112Resources resources = Resources(frame);
        NativeBattleModulePoolTask task = frame.Effect.Pools[1].Slots[slot];
        NativeBattleGfAnimationWorkspace workspace = Read(task, offset, size, false);
        return Store(frame, slot, offset, workspace.Initialize7016B0(resources.Hierarchy(sourceAddress), size));
    }

    public static NativeBattleDuel112WorkspaceStep Advance701970(NativeBattleDuel112Frame frame, int slot, int offset, NativeBattleDuel112HierarchyContext context, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleLogicalOrdering ordering)
    {
        (uint sourceAddress, int size) = Contract(offset);
        NativeBattleGfAnimationSource source = Resources(frame).Hierarchy(sourceAddress);
        NativeBattleGfAnimationWorkspace workspace = Read(frame.Effect.Pools[1].Slots[slot], offset, size);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            NativeBattleDuel112HierarchyStep drawn = NativeBattleDuel112Hierarchy.Draw6CD280(frame, source, node, context, view, projection, colors);
            frame = drawn.Frame;
            uint physicalCursor = BinaryPrimitives.ReadUInt32LittleEndian(frame.Staging.Read(0x2521914, 4));
            int cursor = checked((int)((long)physicalCursor - 0x20DFAB8));
            if (cursor != frame.Effect.DrawArenaOffset)
            {
                throw new InvalidDataException("Native Duel draw cursor differs from its physical process DWORD.");
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, drawn.Batch);
            frame = frame with { Memory = written.Memory, Effect = frame.Effect with { DrawArenaOffset = written.Cursor }, Staging = frame.Staging.Write(0x2521914, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)written.Cursor))) };
            ordering = written.Ordering;
            return current;
        });
        return new(Store(frame, slot, offset, advanced.Workspace), ordering, advanced.Remaining);
    }

    private static NativeBattleDuel112Resources Resources(NativeBattleDuel112Frame frame) => frame.Memory.Duel112?.Resources ?? throw new NotSupportedException("Native Duel workspace requires its original executable resource owner.");

    private static NativeBattleGfAnimationWorkspace Read(NativeBattleModulePoolTask task, int offset, int size, bool requireKnown = true)
    {
        if (task.Callback != 0x6CBF90 || offset < 12 || size < 0 || offset - 12 > task.PayloadLength - size)
        {
            throw new InvalidDataException("Native Duel workspace leaves its actual target task.");
        }
        return new((requireKnown ? task.ReadBytes(offset, size) : task.ReadRawPayloadSpan().Slice(offset - 12, size).ToArray()).ToImmutableArray());
    }

    private static NativeBattleDuel112Frame Store(NativeBattleDuel112Frame frame, int slot, int offset, NativeBattleGfAnimationWorkspace workspace)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        if (pool.DescriptorAddress != 0x2521950)
        {
            throw new InvalidDataException("Native Duel workspace requires its physical target bank.");
        }
        NativeBattleModulePoolTask task = pool.Slots[slot];
        task = task.WriteBytes(offset, workspace.Data.AsSpan());
        pool = pool.WriteSlot(slot, task);
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
    }

    private static (uint Address, int Size) Contract(int offset) => offset switch
    {
        180 => (0x12C7420, 192),
        372 => (0x12C8020, 272),
        644 => (0x12CA7B4, 824),
        1468 => (0x12CB40C, 848),
        2316 => (0x12D19F4, 268),
        2584 => (0x12D4684, 896),
        3480 => (0x12D8D28, 224),
        3704 => (0x12D9DC0, 208),
        _ => throw new NotSupportedException("Native Duel selected an unsupported physical target workspace.")
    };
}
