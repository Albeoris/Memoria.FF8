using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel216WorkspaceStep(NativeBattleDuel216Frame Frame, NativeBattleLogicalOrdering Ordering, int Remaining);
public sealed record NativeBattleDuel216HierarchyTaskStep(NativeBattleDuel216Step Step, NativeBattleLogicalOrdering Ordering);

public static class NativeBattleDuel216Workspaces
{
    public static NativeBattleDuel216WorkspaceStep Advance701970(NativeBattleDuel216Frame frame, uint descriptor, int slot, int offset, int size, NativeBattleDuel216Program context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleLogicalOrdering ordering, bool capturePresentation = false)
    {
        NativeBattleDuel216Resources resources = frame.Memory.Duel216?.Resources ?? throw new NotSupportedException("Native Different Beat workspace requires its entered resources.");
        NativeBattleEffectTaskPool pool = Pool(frame, descriptor);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        uint address = task.ReadDword(offset);
        bool supported = (descriptor, offset, address, size) switch
        {
            (NativeBattleDuel216Controller.Main, 88, 0x1140C5C, 64) => true,
            (NativeBattleDuel216Controller.Main, 88, 0x11431D4, 1156) => true,
            (NativeBattleDuel216Controller.Main, 1244, 0x11484D0, 352) => true,
            (NativeBattleDuel216Controller.Main, 1596, 0x1141444, 168) => true,
            (NativeBattleDuel216Controller.Main, 1764, 0x114A6DC, 1668) => true,
            (NativeBattleDuel216Controller.Main, 3432, 0x11512C8, 236) => true,
            (NativeBattleDuel216Controller.Secondary, 24, 0x115049C, 236) => true,
            _ => false
        };
        if (!supported)
        {
            throw new InvalidDataException("Native Different Beat selected an unsupported physical program workspace.");
        }
        NativeBattleGfAnimationSource source = resources.Hierarchy(address);
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, offset, size);
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            NativeBattleDuel216HierarchyStep drawn = NativeBattleDuel216Hierarchy.Draw6978F0(frame, source, node, context, projection, colors, capturePresentation ? NativeBattleDuel28Presentation.Identity(216, task, source.Address, unchecked(offset * 65537 + node.ResourceOffset)) : null);
            frame = drawn.Frame;
            int cursor = frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Different Beat requires its actual private packet cursor.");
            if (capturePresentation)
            {
                frame = frame with { Effect = frame.Effect with { PresentationPackets = NativeBattleEffectPresentationPackets.Record(frame.Effect.PresentationPackets, "20DFAB8", 0, cursor, drawn.Batch) } };
            }
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, drawn.Batch);
            frame = frame with { Memory = written.Memory, Effect = frame.Effect with { DrawArenaOffset = written.Cursor }, Staging = frame.Staging.Write(0x2508874, BitConverter.GetBytes(checked(NativeBattleDuel216Controller.Arena + (uint)written.Cursor))) };
            ordering = written.Ordering;
            return current;
        });
        pool = Pool(frame, descriptor);
        task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(pool.Slots[slot], offset, advanced.Workspace);
        pool = pool.WriteSlot(slot, task);
        frame = Store(frame, pool);
        return new(frame, ordering, advanced.Remaining);
    }

    public static NativeBattleDuel216Frame Spawn69A550(NativeBattleDuel216Frame frame, NativeBattleVector origin, short x, short y, short z, short scale)
    {
        NativeBattleEffectTaskPool pool = Pool(frame, NativeBattleDuel216Controller.Secondary);
        if (pool.Slots.All(task => (task.Flags & 1) != 0))
        {
            return frame;
        }
        (pool, int slot) = pool.Append(0x69A5D0);
        NativeBattleModulePoolTask task = pool.Slots[slot].WriteWord(12, unchecked((ushort)(origin.X + x))).WriteWord(14, unchecked((ushort)(origin.Y + y))).WriteWord(16, unchecked((ushort)(origin.Z + z))).WriteWord(18, unchecked((ushort)scale)).WriteDword(20, 0);
        NativeBattleDuel216Resources resources = frame.Memory.Duel216?.Resources ?? throw new NotSupportedException("Native Different Beat child requires its entered resources.");
        NativeBattleGfAnimationWorkspace workspace = NativeBattleGfAnimationWorkspace.Borrow(task, 24, 236);
        task = NativeBattleEffectTaskMemory.PublishAnimationWorkspace(task, 24, workspace.Initialize7016B0(resources.Hierarchy(0x115049C), 236));
        return Store(frame, pool.WriteSlot(slot, task));
    }

    public static NativeBattleDuel216HierarchyTaskStep Advance69A5D0(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleActionMatrix view, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleLogicalOrdering ordering, bool capturePresentation = false)
    {
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != NativeBattleDuel216Controller.Secondary || task.Callback != 0x69A5D0)
        {
            throw new InvalidDataException("Native Different Beat secondary draw requires its original callback.");
        }
        NativeBattleActionMatrix local = NativeBattleDuel215Controller.ParticleBasis6ED1E0(unchecked((short)task.ReadWord(20)), unchecked((short)task.ReadWord(18))) with { X = unchecked((short)task.ReadWord(12)), Y = unchecked((short)task.ReadWord(14)), Z = unchecked((short)task.ReadWord(16)) };
        NativeBattleDuel216Program context = new(NativeBattleGfMatrices.Compose56C2F0(view, local), NativeBattleGfMatrices.Normalize67AAE0(view), 0x1154F44, -128, 0, 0, NativeBattleDuel216Controller.Arena + 11480);
        NativeBattleDuel216WorkspaceStep advanced = Advance701970(frame, pool.DescriptorAddress, slot, 24, 236, context, projection, colors, ordering, capturePresentation);
        pool = Pool(advanced.Frame, pool.DescriptorAddress);
        return new(new(advanced.Frame, pool, advanced.Remaining == 0 ? 2 : 0), advanced.Ordering);
    }

    private static NativeBattleEffectTaskPool Pool(NativeBattleDuel216Frame frame, uint descriptor) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == descriptor);
    private static NativeBattleDuel216Frame Store(NativeBattleDuel216Frame frame, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == pool.DescriptorAddress ? pool : bank).ToImmutableArray() } };
}
