using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public static class NativeBattleDuel112Controller
{
    private const uint Arena = 0x20DFAB8;

    public static NativeBattleDuel112Frame Enter6CBC10(NativeBattleEffectMemory memory, NativeBattleOriginalEffectState effect, NativeBattleFileStaging staging, NativeCrtRandomState random, NativeBattleDuel112Resources resources, Func<NativeBattleDuel112Frame, NativeBattleDuel112Frame> enqueueInitialTexture)
    {
        if (effect.Binding is not { Effect: 112, Entry: 0x6CBBE0, Initializer: 0x6CBBF0 } || effect.Parameters is not { } parameters)
        {
            throw new InvalidDataException("Native Duel entry requires its actual binding and action parameters.");
        }
        resources.Validate();
        NativeBattleDuel112State state = memory.Duel112 ?? throw new NotSupportedException("Native Duel requires its retained process globals owner.");
        state.Validate();
        if (!state.GlobalsInitialized)
        {
            staging = staging.Write(NativeBattleDuel112Resources.GlobalsAddress, resources.ColdGlobals.AsSpan());
        }
        memory = memory with { Duel112 = new(true, parameters, resources) };
        NativeBattleDuel112Frame frame = enqueueInitialTexture(new(memory, effect, staging, random));
        frame = frame with { Staging = Dword(frame.Staging, 0x25218E8, Arena) };
        frame = frame with { Staging = Dword(frame.Staging, 0x25218E0, 0x1D972C0u + 156u * parameters.Actor78) };
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(0x2521960, 0x25218F8, 28, 1);
        (root, int slot) = root.Append(0x6CBCF0);
        root = root.WriteSlot(slot, root.Slots[slot].ClearPayload(16));
        int count = BinaryPrimitives.ReadInt32LittleEndian(resources.Read(0x12C592C, 4).AsSpan());
        if (count < 2)
        {
            throw new InvalidDataException("Native Duel requires its original nonempty deformation vertex owner.");
        }
        short minimum = Word(0x12C5932);
        short maximum = minimum;
        for (int index = 1; index < count; index++)
        {
            short y = Word(checked(0x12C5932u + 8u * (uint)index));
            if (y < minimum)
            {
                minimum = y;
            }
            if (y > maximum)
            {
                maximum = y;
            }
        }
        frame = frame with { Staging = frame.Staging.Write(0x25218F0, BitConverter.GetBytes(maximum)).Write(0x25218F2, BitConverter.GetBytes(unchecked((short)(maximum - minimum)))) };
        return frame with { Effect = frame.Effect with { Pools = [root], ReturnedDescriptor = 0x2521960 } };

        short Word(uint address) => BinaryPrimitives.ReadInt16LittleEndian(resources.Read(address, 2).AsSpan());
    }

    public static NativeBattleDuel112Frame Advance6CBCF0(NativeBattleDuel112Frame frame, Func<byte> readMaximumSequence, Func<NativeBattleDuel112Frame, int, int, NativeBattleDuel112Frame> initializeTarget, Func<NativeBattleDuel112Frame, int, NativeBattleEffectTaskPool, int, NativeBattleDuel112TaskStep> advanceChild, Func<NativeBattleDuel112Frame, NativeBattleDuel112Frame> registerAnimation)
    {
        if (frame.Effect.Pools.Length is not (1 or 5) || frame.Effect.Pools[0].DescriptorAddress != 0x2521960)
        {
            throw new InvalidDataException("Native Duel requires its actual retained task banks.");
        }
        NativeBattleEffectTaskStep roots = frame.Effect.Pools[0].Advance((root, slot) =>
        {
            NativeBattleModulePoolTask task = root.Slots[slot];
            if (task.Callback != 0x6CBCF0)
            {
                throw new NotSupportedException("Native Duel root selected an unrelated callback.");
            }
            uint offset = task.ReadDword(16) != 0 ? 0x1E88u : 0x11E88u;
            task = task.WriteDword(16, task.ReadDword(16) != 0 ? 0u : 1u);
            root = root with { Slots = root.Slots.SetItem(slot, task) };
            frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, root), DrawArenaOffset = (int)offset }, Staging = Dword(frame.Staging, 0x2521914, Arena + offset) };
            if (task.ReadWord(12) == 1)
            {
                if (task.ReadByte(15) == 0)
                {
                    task = task.WriteByte(15, 1);
                    root = root with { Slots = root.Slots.SetItem(slot, task) };
                    frame = frame with { Effect = frame.Effect with { Pools = [root, NativeBattleEffectTaskPool.Initialize(0x2521950, Arena, 0xF48, 1), NativeBattleEffectTaskPool.Initialize(0x2521940, Arena + 0x1488, 28, 32), NativeBattleEffectTaskPool.Initialize(0x2521930, Arena + 0x1248, 36, 16), NativeBattleEffectTaskPool.Initialize(0x2521920, Arena + 0xF48, 48, 16)] } };
                    frame = frame with { Memory = NativeBattleEffectTaskMemory.RevokeArenaOwnership(frame.Memory, frame.Effect.Pools) };
                }
                int sequence = task.ReadByte(14);
                if (sequence <= readMaximumSequence())
                {
                    NativeBattleEffectTaskPool targets = frame.Effect.Pools[1];
                    if (targets.Slots.Any(candidate => (candidate.Flags & 1) == 0))
                    {
                        (targets, int targetSlot) = targets.Append(0x6CBF90);
                        targets = targets.WriteSlot(targetSlot, targets.Slots[targetSlot].ClearPayload(0xF48 - 12));
                        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, targets) } };
                        frame = initializeTarget(frame, targetSlot, sequence);
                        task = task.WriteByte(14, unchecked((byte)(sequence + 1)));
                        root = frame.Effect.Pools[0];
                        root = root with { Slots = root.Slots.SetItem(slot, task) };
                        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, root) } };
                        frame = registerAnimation(frame);
                    }
                }
            }
            int survivors = 0;
            if (task.ReadByte(15) != 0)
            {
                for (int index = 1; index < 5; index++)
                {
                    int selected = index;
                    NativeBattleEffectTaskStep children = frame.Effect.Pools[index].Advance((pool, childSlot) =>
                    {
                        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(selected, pool) } };
                        NativeBattleDuel112TaskStep child = advanceChild(frame, selected, pool, childSlot);
                        frame = child.Frame;
                        return new(child.Pool, child.Result);
                    });
                    frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, children.Pool) } };
                    survivors += children.Survivors;
                }
            }
            int result = task.ReadByte(15) != 0 && survivors == 0 ? 2 : 0;
            if (result == 0)
            {
                short counter = unchecked((short)(task.ReadWord(12) + 1));
                task = task.WriteWord(12, counter >= 10 ? (ushort)0 : unchecked((ushort)counter));
            }
            root = frame.Effect.Pools[0];
            root = root with { Slots = root.Slots.SetItem(slot, task) };
            return new(root, result);
        });
        frame = frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(0, roots.Pool), ReturnedDescriptor = roots.Survivors == 0 ? 0u : 0x2521960u } };
        if (roots.Survivors == 0)
        {
            NativeBattleEffectTaskTransfer transferred = NativeBattleEffectTaskMemory.TransferInactiveArenaBanks(frame.Memory, frame.Effect);
            frame = frame with { Memory = transferred.Memory, Effect = transferred.Effect };
        }
        return frame;
    }

    private static NativeBattleFileStaging Dword(NativeBattleFileStaging staging, uint address, uint value) => staging.Write(address, BitConverter.GetBytes(value));

    public static NativeBattleOriginalEffectState WriteCompletionByte(NativeBattleOriginalEffectState effect, uint address, byte value)
    {
        uint delta = unchecked(address - (Arena + 0xF48));
        if (effect.Binding.Entry != 0x6CBBE0 || effect.Pools.Length != 5 || delta >= 16 * 48 || delta % 48 != 14)
        {
            throw new InvalidDataException("Native Duel completion requires its actual inline texture-task byte.");
        }
        NativeBattleEffectTaskPool pool = effect.Pools[4];
        int slot = (int)(delta / 48);
        NativeBattleModulePoolTask task = pool.Slots[slot];
        if (pool.BankAddress != Arena + 0xF48 || pool.DescriptorAddress != 0x2521920 || task.Callback != 0x6CD870 || (task.Flags & 1) == 0)
        {
            throw new InvalidDataException("Native Duel completion lost its active physical texture task.");
        }
        return effect with { Pools = effect.Pools.SetItem(4, pool.WriteSlot(slot, task.WriteByte(14, value))) };
    }
}
