using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel274Controller
{
    public static NativeBattleDuel274Frame Enter6898B0(NativeBattleDuel274Frame frame, NativeBattleDuel274ActorServices actors, Func<NativeBattleDuel274Frame, NativeBattleDuel274Frame> enqueueTexture505E30)
    {
        if (frame.Effect.Binding is not { Effect: 274, Entry: 0x6897E0, Initializer: 0x6897F0 } || frame.Effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Duel opening requires its actual entry and parameter owner.");
        }
        NativeBattleDuel274State state = frame.Memory.Duel274 ?? throw new NotSupportedException("Native Duel opening requires its retained process owner.");
        if (!state.ColdGlobalsPublished)
        {
            frame = frame with { Memory = frame.Memory with { Duel274 = new(true) }, Staging = frame.Staging.Write(0x25084D8, new byte[2]) };
        }
        byte selector = parameters.Parameter80 ?? throw new NotSupportedException("Native Duel opening requires its actual selector BYTE80.");
        if (selector != 0)
        {
            frame = frame with { Staging = frame.Staging.Write(0x25084D8, [1, selector]), Effect = frame.Effect with { ReturnedDescriptor = 0 } };
            return frame;
        }
        for (int index = 0; index < 7; index++)
        {
            frame = frame with { Staging = Dword(frame.Staging, checked(0x2508414u + (uint)index * 4), actors.ReadFlags(index)) };
        }
        frame = enqueueTexture505E30(frame);
        frame = frame with { Staging = Dword(frame.Staging, 0x2508430, NativeBattleEffectNodeMemory.ArenaAddress) };
        frame = frame with { Staging = Dword(frame.Staging, 0x2508470, Address(parameters.Actor78)) };
        frame = frame with { Staging = Dword(frame.Staging, 0x2508468, 0) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25084B8, 0x25084C8, 24, 1);
        (root, int slot) = root.Append(0x689990);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteBytes(12, new byte[12]).WriteByte(16, 0).WriteByte(17, selector)) };
        return frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = root.DescriptorAddress } };
    }

    public static NativeBattleDuel274Frame Advance689990(NativeBattleDuel274Frame frame, NativeBattleDuel274ActorServices actors, Func<int, int> readTarget, Func<NativeBattleDuel274Frame, int, NativeBattleDuel274TaskStep> advanceTarget)
    {
        if (frame.Effect.Pools.IsDefaultOrEmpty || frame.Effect.Pools[0].DescriptorAddress != 0x25084B8)
        {
            throw new InvalidDataException("Native Duel opening requires its actual root bank.");
        }
        NativeBattleEffectTaskStep step = frame.Effect.Pools[0].Advance((root, slot) =>
        {
            NativeBattleModulePoolTask task = root.Slots[slot];
            if (task.Callback != 0x689990)
            {
                throw new NotSupportedException("Native Duel opening root selected an unsupported callback.");
            }
            int pointerOffset = task.ReadDword(20) != 0 ? 2348 : 2352;
            task = task.WriteDword(20, task.ReadDword(20) != 0 ? 0u : 1u);
            NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Duel opening requires its actual packet-pointer bytes.");
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(pointerOffset, 4));
            frame = frame with { Staging = Dword(frame.Staging, 0x2508438, pointer) };
            root = root with { Slots = root.Slots.SetItem(slot, task) };
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, root) } };
            if (task.ReadWord(12) == 1)
            {
                if (task.ReadByte(15) == 0)
                {
                    task = task.WriteByte(15, 1);
                    root = root with { Slots = root.Slots.SetItem(slot, task) };
                    frame = frame with { Effect = frame.Effect with { Pools = [root, NativeBattleEffectTaskPool.Initialize(0x25084A8, NativeBattleEffectNodeMemory.ArenaAddress, 116, 1)] } };
                    frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, frame.Effect.Pools) };
                }
                NativeBattleEffectTaskPool targets = frame.Effect.Pools[1];
                if (targets.Slots.Any(candidate => (candidate.Flags & 1) == 0))
                {
                    (targets, int targetSlot) = targets.Append(0x689AC0);
                    int target = actors.ResolveMinimum501FF0(readTarget(task.ReadByte(14)));
                    (short maximum, short minimum) = actors.ReadBounds6DFD90(target);
                    NativeBattleModulePoolTask child = targets.Slots[targetSlot].WriteBytes(12, new byte[104]).WriteByte(15, 0).WriteByte(14, task.ReadByte(14)).WriteByte(16, 1).WriteDword(20, Address(target)).WriteWord(92, unchecked((ushort)maximum)).WriteWord(94, unchecked((ushort)minimum));
                    targets = targets with { Slots = targets.Slots.SetItem(targetSlot, child) };
                    task = task.WriteByte(14, unchecked((byte)(task.ReadByte(14) + 1)));
                    root = root with { Slots = root.Slots.SetItem(slot, task) };
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, root).SetItem(1, targets) } };
                }
            }
            int survivors = 0;
            if (task.ReadByte(15) != 0)
            {
                NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, childSlot) =>
                {
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
                    NativeBattleDuel274TaskStep child = advanceTarget(frame, childSlot);
                    frame = child.Frame;
                    return new(child.Pool, child.Result);
                });
                frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, children.Pool) } };
                survivors = children.Survivors;
            }
            int result = task.ReadByte(15) != 0 && survivors == 0 ? 2 : 0;
            if (result == 0)
            {
                short count = unchecked((short)(task.ReadWord(12) + 1));
                task = task.WriteWord(12, count >= 10 ? (ushort)10 : unchecked((ushort)count));
            }
            root = frame.Effect.Pools[0];
            root = root with { Slots = root.Slots.SetItem(slot, task) };
            return new(root, result);
        });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, step.Pool), ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x25084B8u } };
        if (step.Survivors == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        return frame;
    }

    public static NativeBattleDuel274TaskStep Advance689AC0(NativeBattleDuel274Frame frame, int slot, NativeBattleDuel274ActorServices actors, Func<NativeBattleDuel274Frame, int, NativeBattleDuel274TaskStep> advanceSelected)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.DescriptorAddress != 0x25084A8 || task.Callback != 0x689AC0)
        {
            throw new InvalidDataException("Native Duel opening target requires its actual task bank and callback.");
        }
        byte selector = frame.Staging.Read(0x25084D9, 1)[0];
        task = task.WriteByte(15, selector);
        int first = Index(task.ReadDword(20));
        HashSet<int> visited = [];
        int current = first;
        do
        {
            if (!visited.Add(current))
            {
                throw new InvalidDataException("Native Duel opening model ring does not return to its source.");
            }
            ushort flags = actors.ReadFlags(current);
            if ((flags & 2) != 0)
            {
                ushort original = BinaryPrimitives.ReadUInt16LittleEndian(frame.Staging.Read(checked(0x2508414u + (uint)current * 4), 2));
                actors.WriteFlags(current, (ushort)(flags ^ ((flags ^ original) & 4)));
            }
            uint linked = actors.ReadLinkedModel140(current);
            if (linked == 0)
            {
                break;
            }
            current = Index(linked);
        }
        while (current != first);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        if (selector != 0)
        {
            return advanceSelected(frame, slot);
        }
        if (task.ReadWord(12) == 0)
        {
            NativeBattleEffectParameters parameters = frame.Effect.Parameters ?? throw new NotSupportedException("Native Duel opening lost its actual parameter owner.");
            frame = frame with { Effect = frame.Effect with { Parameters = parameters with { Parameter81 = 1 } } };
        }
        task = task.WriteWord(12, 1);
        pool = pool with { Slots = pool.Slots.SetItem(slot, task) };
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        return new(frame, pool, 0);
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));

    public static uint Address(int index)
    {
        if ((uint)index >= 7)
        {
            throw new InvalidDataException("Native Duel requires a physical model index in its seven-row bank.");
        }
        return checked(0x1D972C0u + (uint)index * 156);
    }

    public static int Index(uint address)
    {
        uint offset = unchecked(address - 0x1D972C0u);
        if (offset % 156 != 0 || offset / 156 >= 7)
        {
            throw new InvalidDataException("Native Duel requires its exact physical model pointer.");
        }
        return checked((int)(offset / 156));
    }
}
