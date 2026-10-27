using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattlePandemona291ChildStep(NativeBattlePandemona291Frame Frame, NativeBattleModulePoolTask Task, int Result);
public sealed record NativeBattlePandemona291MasterInputs(NativeBattlePandemona291ConstructionInputs Construction, Func<NativeBattlePandemona291Frame, uint, NativeBattleModulePoolTask, NativeBattlePandemona291ChildStep> AdvanceLeaf);

public static class NativeBattlePandemona291Controller
{
    public static NativeBattlePandemona291Frame Advance6ED350(NativeBattlePandemona291Frame frame, Action<uint, int> stage5713E0, NativeBattlePandemona291MasterInputs? inputs = null)
    {
        NativeBattlePandemona291State state = frame.Memory.Pandemona291 ?? throw new NotSupportedException("Native Pandemona requires its retained module owner.");
        uint buffer = state.ModelBuffer25562F4 ?? throw new NotSupportedException("Native Pandemona requires its actual model-buffer publication.");
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == NativeBattlePandemona291Entry.MainDescriptor);
        NativeBattleEffectTaskStep step = pool.Advance((current, index) =>
        {
            NativeBattleModulePoolTask task = current.Slots[index];
            uint cursor;
            if (task.ReadDword(0x10) != 0)
            {
                cursor = unchecked(buffer + 0x71D4);
                task = task.WriteDword(0x10, 0);
            }
            else
            {
                cursor = unchecked(buffer + 0x151D4);
                task = task.WriteDword(0x10, 1);
            }
            state = state with { PacketCursor25562DC = cursor, Flash25562F0 = 0 };
            uint pause = state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona requires its current pause DWORD.");
            if (task.ReadWord(0xC) == 2 && pause == 0 && task.ReadByte(0xF) == 0)
            {
                NativeBattlePandemona291MasterInputs services = inputs ?? throw new NotSupportedException("Native Pandemona6ED350 requires its full target-record constructor6ED770 and timeline6ED900.");
                task = task.WriteByte(0xF, 1);
                frame = frame with { Memory = frame.Memory with { Pandemona291 = state }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(active => active.DescriptorAddress == current.DescriptorAddress ? current with { Slots = current.Slots.SetItem(index, task) } : active).ToImmutableArray() } };
                frame = NativeBattlePandemona291Construction.Initialize6ED3B6(frame, services.Construction);
                state = frame.Memory.Pandemona291!;
            }
            frame = frame with { Memory = frame.Memory with { Pandemona291 = state }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(active => active.DescriptorAddress == current.DescriptorAddress ? current with { Slots = current.Slots.SetItem(index, task) } : active).ToImmutableArray() } };
            int creatures = 0;
            if (task.ReadByte(0xF) != 0)
            {
                NativeBattlePandemona291MasterInputs services = inputs ?? throw new NotSupportedException("Native Pandemona requires its five ordered spawned task banks.");
                foreach (uint descriptor in new uint[] { 0x25562A8, 0x2556298, 0x2556288, 0x2556278, 0x2556268 })
                {
                    NativeBattleEffectTaskPool children = frame.Effect.Pools.Single(active => active.DescriptorAddress == descriptor);
                    NativeBattleEffectTaskStep advanced = children.Advance((active, childIndex) =>
                    {
                        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(selected => selected.DescriptorAddress == descriptor ? active : selected).ToImmutableArray() } };
                        uint address = unchecked(active.BankAddress + (uint)(childIndex * active.Stride));
                        NativeBattlePandemona291ChildStep child = services.AdvanceLeaf(frame, address, active.Slots[childIndex]);
                        frame = child.Frame;
                        NativeBattleEffectTaskPool live = frame.Effect.Pools.Single(selected => selected.DescriptorAddress == descriptor);
                        return new(live.WriteSlot(childIndex, child.Task), child.Result);
                    });
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(active => active.DescriptorAddress == descriptor ? advanced.Pool : active).ToImmutableArray() } };
                    if (descriptor == 0x25562A8)
                    {
                        creatures = advanced.Survivors;
                    }
                }
                state = frame.Memory.Pandemona291!;
                current = frame.Effect.Pools.Single(active => active.DescriptorAddress == current.DescriptorAddress);
                task = current.Slots[index];
            }
            stage5713E0(state.Flash25562F0 ?? throw new NotSupportedException("Native Pandemona requires its current flash DWORD."), 0);
            pause = state.Pause2556258 ?? throw new NotSupportedException("Native Pandemona requires its current pause DWORD.");
            if (pause == 0)
            {
                if (task.ReadByte(0xF) != 0 && creatures == 0)
                {
                    return new(current with { Slots = current.Slots.SetItem(index, task) }, 2);
                }
                task = task.WriteWord(0xC, unchecked((ushort)(task.ReadWord(0xC) + 1)));
            }
            return new(current with { Slots = current.Slots.SetItem(index, task) }, 0);
        });
        return frame with { Memory = frame.Memory with { Pandemona291 = state }, Effect = frame.Effect with { Pools = frame.Effect.Pools.Select(active => active.DescriptorAddress == pool.DescriptorAddress ? step.Pool : active).ToImmutableArray(), ReturnedDescriptor = step.Survivors == 0 ? 0u : NativeBattlePandemona291Entry.MainDescriptor } };
    }
}
