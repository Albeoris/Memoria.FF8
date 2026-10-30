using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel215WorkspaceStep(NativeBattleDuel215Frame Frame, NativeBattleLogicalOrdering Ordering, int Remaining);

public static class NativeBattleDuel215Workspaces
{
    public static NativeBattleDuel215Frame Initialize69B070(NativeBattleDuel215Frame frame, int slot, int offset, uint address, int size)
    {
        Require(offset, address, size);
        NativeBattleDuel215Resources resources = frame.Memory.Duel215?.Resources ?? throw new NotSupportedException("Native Duel215 workspace requires its entered resources.");
        NativeBattleEffectTaskPool pool = Scene(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleGfAnimationSource source = new(address, resources.Read(address, checked((int)(NativeBattleDuel215Resources.EndAddress - address))));
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, offset, size);
        workspace = workspace.Initialize7016B0(source, size);
        task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, offset, workspace);
        pool = pool.WriteSlot(slot, task);
        return Store(frame, pool);
    }

    public static NativeBattleDuel215WorkspaceStep Advance69B070(NativeBattleDuel215Frame frame, int slot, int offset, int size, NativeBattleDuel215Program context, bool anchored, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleLogicalOrdering ordering, bool capturePresentation = false)
    {
        NativeBattleDuel215Resources resources = frame.Memory.Duel215?.Resources ?? throw new NotSupportedException("Native Duel215 workspace requires its entered resources.");
        NativeBattleEffectTaskPool pool = Scene(frame);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint address = task.ReadDword(offset);
        Require(offset, address, size);
        NativeBattleGfAnimationSource source = new(address, resources.Read(address, checked((int)(NativeBattleDuel215Resources.EndAddress - address))));
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, offset, size);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            NativeBattleDuel215HierarchyStep drawn = anchored ? NativeBattleDuel215Hierarchy.Draw69C6B0(frame, source, node, context, projection, colors, capturePresentation ? NativeBattleDuel28Presentation.Identity(215, task, source.Address, unchecked(offset * 65537 + node.ResourceOffset)) : null) : NativeBattleDuel215Hierarchy.Draw69C220(frame, source, node, context, projection, colors, capturePresentation ? NativeBattleDuel28Presentation.Identity(215, task, source.Address, unchecked(offset * 65537 + node.ResourceOffset)) : null);
            frame = drawn.Frame;
            int cursor = frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Duel215 workspace requires its actual private packet cursor.");
            if (capturePresentation)
            {
                frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, "20DFAB8", 0, cursor, drawn.Batch) } };
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, drawn.Batch);
            frame = frame with { Memory = written.Memory, Effect = frame.Effect with { DrawArenaOffset = written.Cursor }, Staging = frame.Staging.Write(0x2508988, BitConverter.GetBytes(checked(NativeBattleDuel215Controller.Arena + (uint)written.Cursor))) };
            ordering = written.Ordering;
            return current;
        });
        pool = Scene(frame);
        task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(pool.Slots[slot], offset, advanced.Workspace);
        pool = pool.WriteSlot(slot, task);
        return new(Store(frame, pool), ordering, advanced.Remaining);
    }

    private static NativeBattleEffectTaskPool Scene(NativeBattleDuel215Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel215Controller.Scene);
    private static NativeBattleDuel215Frame Store(NativeBattleDuel215Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(current => current.DescriptorAddress == pool.DescriptorAddress ? pool : current).ToImmutableArray() } };

    private static void Require(int offset, uint address, int size)
    {
        bool supported = (offset, address, size) switch
        {
            (1420, 0x11550A8, 880) => true,
            (784, 0x115A910, 436) => true,
            (392, 0x1168A5C, 176) => true,
            (392, 0x1160BB0, 392) => true,
            (392, 0x11625E0, 148) => true,
            (392, 0x1169844, 340) => true,
            (392, 0x116703C, 188) => true,
            (784, 0x115DCCC, 636) => true,
            (1420, 0x116AD0C, 340) => true,
            _ => false
        };
        if (!supported)
        {
            throw new NotSupportedException("Native Duel215 selected an unsupported caller-owned program workspace.");
        }
    }
}
