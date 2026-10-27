using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleShockwavePulsar260Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleShockwavePulsar260Controller
{
    public static NativeBattleModulePoolTask Read(NativeBattleShockwavePulsar260Frame frame, uint address)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                return pool.Slots[(int)(offset / pool.Stride)];
            }
        }
        throw new InvalidDataException("Shockwave Pulsar task leaves its original task pools.");
    }

    public static NativeBattleShockwavePulsar260Frame Write(NativeBattleShockwavePulsar260Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            uint offset = unchecked(address - pool.BankAddress);
            if (offset < pool.Stride * pool.Slots.Length && offset % pool.Stride == 0)
            {
                int slot = (int)(offset / pool.Stride);
                return WithPool(frame, index, pool.WriteSlot(slot, task.UpdateHeader(task.Flags, task.State, pool.Slots[slot].Next, task.Callback)));
            }
        }
        throw new InvalidDataException("Shockwave Pulsar task leaves its original task pools.");
    }

    public static (NativeBattleShockwavePulsar260Frame Frame, uint Address) Append(NativeBattleShockwavePulsar260Frame frame, int poolIndex, uint callback)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[poolIndex];
        (pool, int slot) = pool.Append(callback);
        return (WithPool(frame, poolIndex, pool), checked(pool.BankAddress + (uint)(slot * pool.Stride)));
    }

    public static NativeBattleEffectArena ReadView(NativeBattleShockwavePulsar260Frame frame, uint address, int length)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint relative = unchecked(address - pool.BankAddress);
            if (relative < pool.Stride * pool.Slots.Length)
            {
                int offset = (int)(relative % pool.Stride);
                if (offset < 12 || length < 0 || length > pool.Stride - offset)
                {
                    throw new InvalidDataException("Shockwave Pulsar physical view crosses a task payload boundary.");
                }
                NativeBattleModulePoolTask task = pool.Slots[(int)(relative / pool.Stride)];
                NativeBattleEffectArena view = NativeBattleEffectArena.Clear().Write(0, task.ReadRawPayloadSpan().Slice(offset - 12, length));
                if (task.UnknownPayloadRanges is { } ranges)
                {
                    foreach (NativeBattleEffectUnknownRegion range in ranges)
                    {
                        int first = Math.Max(offset, range.Offset);
                        int end = Math.Min(offset + length, range.Offset + range.Length);
                        if (first < end)
                        {
                            view = view.MarkUnknown(first - offset, end - first);
                        }
                    }
                }
                return view;
            }
        }
        if (address >= NativeBattleShockwavePulsar260State.GlobalsAddress && address < NativeBattleShockwavePulsar260State.GlobalsAddress + NativeBattleShockwavePulsar260State.GlobalsLength)
        {
            return frame.State.ReadView(address, length);
        }
        return NativeBattleEffectNodeContextMemory.Copy(Arena(frame), Offset(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public static NativeBattleShockwavePulsar260Frame WriteView(NativeBattleShockwavePulsar260Frame frame, uint address, NativeBattleEffectArena view, int length)
    {
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            uint relative = unchecked(address - pool.BankAddress);
            if (relative < pool.Stride * pool.Slots.Length)
            {
                int offset = (int)(relative % pool.Stride);
                if (offset < 12 || length < 0 || length > pool.Stride - offset)
                {
                    throw new InvalidDataException("Shockwave Pulsar physical store crosses a task payload boundary.");
                }
                uint taskAddress = address - (uint)offset;
                NativeBattleModulePoolTask task = Read(frame, taskAddress);
                (byte[] bytes, bool[] known) = view.ReadSnapshot(0, length);
                task = task.WriteBytes(offset, bytes);
                for (int index = 0; index < known.Length; index++)
                {
                    if (!known[index])
                    {
                        task = task.MarkPayloadUnknown(offset + index, 1);
                    }
                }
                return Write(frame, taskAddress, task);
            }
        }
        if (address >= NativeBattleShockwavePulsar260State.GlobalsAddress && address < NativeBattleShockwavePulsar260State.GlobalsAddress + NativeBattleShockwavePulsar260State.GlobalsLength)
        {
            return frame with { State = frame.State.WriteView(address, view, length) };
        }
        return frame with { Memory = frame.Memory with { Arena = NativeBattleEffectNodeContextMemory.Copy(view, 0, Arena(frame), Offset(address, length), length) } };
    }

    public static ushort ReadWord(NativeBattleShockwavePulsar260Frame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(ReadView(frame, address, 2).Read(0, 2));
    public static uint ReadDword(NativeBattleShockwavePulsar260Frame frame, uint address) => BinaryPrimitives.ReadUInt32LittleEndian(ReadView(frame, address, 4).Read(0, 4));
    public static NativeBattleShockwavePulsar260Frame WriteWord(NativeBattleShockwavePulsar260Frame frame, uint address, ushort value) => WriteView(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 2);
    public static NativeBattleShockwavePulsar260Frame WriteDword(NativeBattleShockwavePulsar260Frame frame, uint address, uint value) => WriteView(frame, address, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(value)), 4);

    public static NativeBattleShockwavePulsar260Frame InitializeAct(NativeBattleShockwavePulsar260Frame frame, uint address, int offset, int size, uint sourceAddress, NativeBattleShockwavePulsar260Resources resources)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadRawPayloadSpan().Slice(offset - 12, size).ToArray().ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.Act(sourceAddress), size);
        return Write(frame, address, task.WriteBytes(offset, workspace.Data.AsSpan()));
    }

    public static NativeBattleShockwavePulsar260TaskStep AdvanceAct(NativeBattleShockwavePulsar260Frame frame, uint address, int offset, int size, uint sourceAddress, NativeBattleEffectArena context20, NativeBattleShockwavePulsar260Resources resources, NativeBattleShockwavePulsar260Inputs inputs)
    {
        NativeBattleModulePoolTask task = Read(frame, address);
        NativeBattleGfAnimationWorkspace workspace = new(task.ReadBytes(offset, size).ToImmutableArray());
        NativeBattleShockwavePulsar260Frame current = frame;
        NativeBattleGfAnimationAdvance advanced = workspace.Advance701970(resources.Act(sourceAddress), 0, (unchanged, node) =>
        {
            current = inputs.Execute(current, new NativeBattleShockwavePulsar260ActNode(sourceAddress, node, context20));
            return unchanged;
        });
        current = Write(current, address, Read(current, address).WriteBytes(offset, advanced.Workspace.Data.AsSpan()));
        return new(current, advanced.Remaining);
    }

    public static (NativeBattleShockwavePulsar260Frame Frame, uint Address) AllocatePuff(NativeBattleShockwavePulsar260Frame frame) => AllocateRow(frame, 0x2508614, PuffsOffset, PuffsCount, 15, 1);
    public static (NativeBattleShockwavePulsar260Frame Frame, uint Address) AllocateSecondary(NativeBattleShockwavePulsar260Frame frame) => AllocateRow(frame, 0x2508618, SecondaryParticlesOffset, SecondaryParticlesCount, 0, 2);
    private static (NativeBattleShockwavePulsar260Frame Frame, uint Address) AllocateRow(NativeBattleShockwavePulsar260Frame frame, uint cursorGlobal, int offset, int count, int activeOffset, int activeBytes)
    {
        uint first = checked(frame.State.ReadDword(WorkingGlobal) + (uint)offset);
        uint cursor = frame.State.ReadDword(cursorGlobal);
        if (cursor < first || cursor >= first + count * 16 || (cursor - first) % 16 != 0)
        {
            throw new InvalidDataException("Shockwave Pulsar free-row cache leaves its original particle bank.");
        }
        bool Active(uint address) => activeBytes == 1 ? ReadView(frame, address + (uint)activeOffset, 1).Read(0, 1)[0] != 0 : ReadWord(frame, address) != 0;
        if (Active(cursor))
        {
            cursor = first;
            int index = 0;
            while (index < count && Active(cursor))
            {
                index++;
                cursor += 16;
            }
            if (index == count)
            {
                return (frame, 0);
            }
        }
        uint next = cursor >= first + (count - 1) * 16 ? first : cursor + 16;
        return (frame with { State = frame.State.WriteDword(cursorGlobal, next) }, cursor);
    }

    public static int Actor(uint address)
    {
        uint relative = unchecked(address - 0x1D972C0);
        if (relative % 156 != 0 || relative / 156 >= 7)
        {
            throw new InvalidDataException("Shockwave Pulsar selected a model outside the original actor bank.");
        }
        return (int)(relative / 156);
    }
    private static short Signed(NativeBattleModulePoolTask task, int offset) => unchecked((short)task.ReadWord(offset));
    private static NativeBattleShockwavePulsar260Frame DrawRandom(NativeBattleShockwavePulsar260Frame frame, out ushort value) => frame with { Random = frame.Random.Draw(out value) };
    private static NativeBattleShockwavePulsar260Frame WithPool(NativeBattleShockwavePulsar260Frame frame, int index, NativeBattleEffectTaskPool pool) => frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
    private static NativeBattleEffectArena Arena(NativeBattleShockwavePulsar260Frame frame) => frame.Memory.Arena ?? throw new NotSupportedException("Shockwave Pulsar requires its current physical arena.");
    private static int Offset(uint address, int length) => address < ArenaAddress || length < 0 || (ulong)address + (uint)length > (ulong)ArenaAddress + NativeBattleEffectArena.Length ? throw new InvalidDataException("Shockwave Pulsar physical access leaves its canonical arena.") : checked((int)(address - ArenaAddress));
}
