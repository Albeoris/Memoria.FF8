using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleDuel212WorkspaceStep(NativeBattleDuel212Frame Frame, NativeBattleLogicalOrdering Ordering, int Remaining);

public static class NativeBattleDuel212Workspaces
{
    public static NativeBattleDuel212WorkspaceStep Advance701970(NativeBattleDuel212Frame frame, NativeBattleDuel212Program operation, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, NativeBattleLogicalOrdering ordering)
    {
        NativeBattleDuel212Resources resources = frame.Memory.Duel212?.Resources ?? throw new NotSupportedException("Native Burning Rave workspace requires original entered resources.");
        (int offset, int size, uint address) = Contract(operation);
        NativeBattleGfAnimationSource source = new(address, resources.Read(address, checked((int)(NativeBattleDuel212Resources.EndAddress - address))));
        NativeBattleEffectTaskPool target = Target(frame);
        NativeBattleModulePoolTask task = target.Slots[0];
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(offset, size).ToImmutableArray());
        if (operation.Initialize)
        {
            workspace = workspace.Initialize7016B0(source, size);
        }
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            NativeBattleDuel212HierarchyStep drawn = NativeBattleDuel212Hierarchy.Draw6A2B80(frame, source, node, operation, current.Frame, projection, colors);
            frame = drawn.Frame;
            int cursor = frame.Effect.DrawArenaOffset ?? throw new NotSupportedException("Native Burning Rave requires its actual double-buffered packet cursor.");
            NativeBattleEffectPacketWrite written = NativeBattleEffectPacketArena.Append(frame.Memory, cursor, ordering, drawn.Batch);
            frame = frame with { Memory = written.Memory, Effect = frame.Effect with { DrawArenaOffset = written.Cursor }, Staging = frame.Staging.Write(0x2510258, BitConverter.GetBytes(checked(0x20DFAB8u + (uint)written.Cursor))) };
            ordering = written.Ordering;
            return current;
        });
        target = Target(frame);
        task = advanced.Workspace.CopyTo(target.Slots[0], offset, retainUnknown: false);
        target = target.WriteSlot(0, task);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(pool => pool.DescriptorAddress == target.DescriptorAddress ? target : pool).ToImmutableArray() } };
        return new(frame, ordering, advanced.Remaining);
    }

    private static NativeBattleEffectTaskPool Target(NativeBattleDuel212Frame frame) => frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattleDuel212Controller.Targets);

    private static (int Offset, int Size, uint Address) Contract(NativeBattleDuel212Program operation) => operation switch
    {
        { Address: 0x11883AC, Offset: 1496, Size: 400 } => (1496, 400, 0x11883AC),
        { Address: 0x118BA10, Offset: 1496, Size: 1156 } => (1496, 1156, 0x118BA10),
        { Address: 0x1187F2C, Offset: 2652, Size: 68 } => (2652, 68, 0x1187F2C),
        { Address: 0x1184ACC, Offset: 2720, Size: 560 } => (2720, 560, 0x1184ACC),
        _ => throw new InvalidDataException("Native Burning Rave selected an unsupported caller-owned program workspace.")
    };
}
