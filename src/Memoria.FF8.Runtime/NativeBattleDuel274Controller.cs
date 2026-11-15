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
            frame = frame with { Memory = frame.Memory with { Duel274 = state with { ColdGlobalsPublished = true } }, Staging = frame.Staging.Write(0x25084D8, new byte[2]) };
        }
        byte selector = parameters.Parameter80 ?? throw new NotSupportedException("Native Duel opening requires its actual selector BYTE80.");
        if (selector != 0)
        {
            frame = WriteRootTransition(frame, 1, selector);
            frame = frame with { Effect = frame.Effect with { ReturnedDescriptor = 0 } };
            return frame;
        }
        var savedFlags = ImmutableDictionary.CreateBuilder<BattleModelOwner, ushort>();
        foreach (BattleModelOwner owner in actors.Aliases.Roster.Owners)
        {
            ushort flags = actors.ReadFlags(owner);
            savedFlags.Add(owner, flags);
            if (actors.Aliases.TryEncodeExact(owner, out byte alias))
            {
                frame = frame with { Staging = Dword(frame.Staging, checked(0x2508414u + (uint)alias * 4), flags) };
            }
        }
        frame = frame with { Effect = frame.Effect with { Duel274SavedFlags = savedFlags.ToImmutable(), Duel274SavedPartyZ = ImmutableDictionary<BattleModelOwner, short>.Empty } };
        frame = enqueueTexture505E30(frame);
        frame = frame with { Staging = Dword(frame.Staging, 0x2508430, NativeBattleEffectNodeMemory.ArenaAddress) };
        frame = frame with { Staging = frame.Staging.WriteExecutionReference(0x2508470, 4, new BattleExecutionReference.Model(parameters.Source), actors.Aliases.EncodeAddress(parameters.Source)) };
        frame = frame with { Staging = Dword(frame.Staging, 0x2508468, 0) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x25084B8, 0x25084C8, 24, 1);
        (root, int slot) = root.Append(0x689990);
        root = root.WriteSlot(slot, root.Slots[slot].WriteBytes(12, new byte[12]).WriteByte(16, 0).WriteByte(17, selector));
        frame = frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = root.DescriptorAddress } };
        return WriteRootTransition(frame, 0, selector);
    }

    public static NativeBattleDuel274Frame Advance689990(NativeBattleDuel274Frame frame, NativeBattleDuel274ActorServices actors, Func<int, BattleModelOwner> readTarget, Func<NativeBattleDuel274Frame, int, NativeBattleDuel274TaskStep> advanceTarget)
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
            root = root.WriteSlot(slot, task);
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, root) } };
            if (task.ReadWord(12) == 1)
            {
                if (task.ReadByte(15) == 0)
                {
                    task = task.WriteByte(15, 1);
                    root = root.WriteSlot(slot, task);
                    frame = frame with { Effect = frame.Effect with { Pools = [root, NativeBattleEffectTaskPool.Initialize(0x25084A8, NativeBattleEffectNodeMemory.ArenaAddress, 116, 1)] } };
                    frame = frame with { Memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(frame.Memory, frame.Effect.Pools) };
                }
                NativeBattleEffectTaskPool targets = frame.Effect.Pools[1];
                if (targets.Slots.Any(candidate => (candidate.Flags & 1) == 0))
                {
                    (targets, int targetSlot) = targets.Append(0x689AC0);
                    BattleModelOwner target = actors.ResolveMinimum501FF0(readTarget(task.ReadByte(14)));
                    (short maximum, short minimum) = actors.ReadBounds6DFD90(target);
                    NativeBattleModulePoolTask child = targets.Slots[targetSlot].WriteBytes(12, new byte[104]).WriteByte(15, 0).WriteByte(14, task.ReadByte(14)).WriteByte(16, 1);
                    child.WriteExecutionReference(20, 4, new BattleExecutionReference.Model(target), actors.Aliases.EncodeAddress(target));
                    child = child.WriteWord(92, unchecked((ushort)maximum)).WriteWord(94, unchecked((ushort)minimum));
                    targets = targets.WriteSlot(targetSlot, child);
                    task = task.WriteByte(14, unchecked((byte)(task.ReadByte(14) + 1)));
                    root = root.WriteSlot(slot, task);
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, root).SetItem(1, targets) } };
                }
            }
            int survivors = 0;
            if (task.ReadByte(15) != 0)
            {
                NativeBattleEffectTaskStep children = frame.Effect.Pools[1].Advance((pool, childSlot) =>
                {
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
                    NativeBattlePilotVisualIdentity? previousIdentity = frame.PilotIdentity;
                    if (frame.CapturePresentation)
                    {
                        frame = frame with { PilotIdentity = NativeBattlePilotVisualLifetimes.Identity(274, pool.Slots[childSlot]) };
                    }
                    NativeBattleDuel274TaskStep child = advanceTarget(frame, childSlot);
                    frame = child.Frame with { PilotIdentity = previousIdentity };
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
            task = task.WriteByte(16, root.Slots[slot].ReadByte(16)).WriteByte(17, root.Slots[slot].ReadByte(17));
            root = root.WriteSlot(slot, task);
            return new(root, result);
        });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, step.Pool), ReturnedDescriptor = step.Survivors == 0 ? 0u : 0x25084B8u } };
        if (step.Survivors == 0)
        {
            frame = NativeBattleDuel274Banks.DetachUnscheduledPrivateBanks(frame);
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
        BattleModelOwner first = ReadTarget(task);
        HashSet<BattleModelOwner> visited = [];
        BattleModelOwner current = first;
        do
        {
            if (!visited.Add(current))
            {
                throw new InvalidDataException("Native Duel opening model ring does not return to its source.");
            }
            ushort flags = actors.ReadFlags(current);
            if ((flags & 2) != 0)
            {
                ushort original = ReadSavedFlags(frame.Effect, current);
                actors.WriteFlags(current, (ushort)(flags ^ ((flags ^ original) & 4)));
            }
            BattleModelOwner? linked = actors.ReadLinkedModel140(current);
            if (linked is null)
            {
                break;
            }
            current = linked;
        }
        while (current != first);
        pool = pool.WriteSlot(slot, task);
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
        pool = pool.WriteSlot(slot, task);
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) } };
        return new(frame, pool, 0);
    }

    public static NativeBattleDuel274Frame WriteRootTransition(NativeBattleDuel274Frame frame, byte pending, byte selector)
    {
        // The transition globals alias bytes 16/17 of the physical root task at 0x25084C8.
        int index = NativeBattleDuel274Banks.Index(frame, 0x25084B8);
        NativeBattleEffectTaskPool root = frame.Effect.Pools[index];
        NativeBattleModulePoolTask task = root.Slots[0].WriteByte(16, pending).WriteByte(17, selector);
        root = root.WriteSlot(0, task);
        return frame with { Staging = frame.Staging.Write(0x25084D8, [pending, selector]), Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, root) } };
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));

    internal static BattleModelOwner ReadTarget(NativeBattleModulePoolTask task) => task.ReadExecutionReference<BattleExecutionReference.Model>(20, 4).Owner;

    internal static ushort ReadSavedFlags(NativeBattleOriginalEffectState effect, BattleModelOwner owner)
    {
        return effect.Duel274SavedFlags.TryGetValue(owner, out ushort flags) ? flags : throw new InvalidDataException("Native Duel cannot restore flags without its captured model owner.");
    }
}
