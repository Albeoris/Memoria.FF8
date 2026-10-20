using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleRinoa89Programme
{
    public static NativeBattleRinoa89Frame Execute701970(NativeBattleRinoa89Frame frame, uint descriptor, int slot, int offset, int capacity, int clear, NativeBattleGfAnimationSource source, bool initialize, bool advance, NativeBattleRinoa89ProgramContext context, NativeBattleCameraProjection projection, NativeBattleSummonColors colors, bool common, Func<NativeBattleRinoa89Frame, NativeBattleGfGeometryBatch, bool, NativeBattleRinoa89Frame> publish)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == descriptor);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        NativeBattleGfAnimationWorkspace workspace = Workspace();
        if (initialize)
        {
            workspace = workspace.Initialize7016B0(source, clear);
            Store(workspace);
        }
        if (!advance)
        {
            return frame;
        }
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(source, 0, (current, node) =>
        {
            Store(current);
            NativeBattleRinoa89ProgramDraw drawn = NativeBattleRinoa89ProgramDrawing.Draw6FBB80(frame, source, node, context, projection, colors);
            frame = publish(drawn.Frame, drawn.Packets, common);
            Refresh();
            return Workspace();
        });
        Store(advanced.Workspace);
        return frame;

        NativeBattleGfAnimationWorkspace Workspace() => new(task.ReadBytes(offset, capacity).ToImmutableArray());
        void Store(NativeBattleGfAnimationWorkspace current)
        {
            task = task.WriteBytes(offset, current.Data.AsSpan());
            pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(bank => bank.DescriptorAddress == descriptor ? pool : bank).ToImmutableArray() } };
        }
        void Refresh()
        {
            pool = frame.Effect.Pools.Single(bank => bank.DescriptorAddress == descriptor);
            task = pool.Slots[slot];
        }
    }
}
