using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect31Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect31Controller
{
    public const uint CommonAddress = 0x20DFAB8;

    public static NativeBattleEffectParameters Parameters(NativeBattleEffect31Frame frame) => frame.Effect.Parameters ?? throw new NotSupportedException("Effect31 requires its current native parameters.");
    public static NativeBattleModulePoolTask Read(NativeBattleEffect31Frame frame, uint address) => new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);

    public static NativeBattleEffect31Frame Write(NativeBattleEffect31Frame frame, uint address, NativeBattleModulePoolTask task)
    {
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        task = task.UpdateHeader(task.Flags, task.State, hierarchy.Read(address).Next, task.Callback);
        return frame with { Effect = frame.Effect with { Pools = hierarchy.Write(address, task).Pools } };
    }

    public static NativeBattleEffectArena ReadBytes(NativeBattleEffect31Frame frame, uint address, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (Contains(NativeBattleEffectNodeContextMemory.Address, 180, address, length))
        {
            return CopyBytes(NativeBattleEffectNodeContextMemory.Capture(frame.Memory), checked((int)(address - NativeBattleEffectNodeContextMemory.Address)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        if (Contains(0x2793E58, 32, address, length))
        {
            NativeBattlePackedMatrix32 view = frame.Memory.SharedEffectView2793E58 ?? throw new NotSupportedException("Effect31 requires the current copied view.");
            return CopyBytes(PackedArena(view), checked((int)(address - 0x2793E58)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        foreach (NativeBattleEffectTaskPool pool in frame.Effect.Pools)
        {
            if (Contains(pool.BankAddress, checked(pool.Stride * pool.Slots.Length), address, length))
            {
                return CopyBytes(NativeBattleEffectTaskMemory.CaptureBank(pool), checked((int)(address - pool.BankAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            }
        }
        if (Owned(address, length))
        {
            return frame.State.ReadView(address, length);
        }
        if (Contains(CommonAddress, NativeBattleEffectArena.Length, address, length))
        {
            return CopyBytes(frame.Memory.Arena ?? throw new NotSupportedException("Effect31 requires its current common allocation."), checked((int)(address - CommonAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        return NativeBattleTemporaryMemory.ReadView(RequireStaging(frame), address, length);
    }

    public static NativeBattleEffect31Frame WriteBytes(NativeBattleEffect31Frame frame, uint address, NativeBattleEffectArena source, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (Contains(NativeBattleEffectNodeContextMemory.Address, 180, address, length))
        {
            return frame with { Memory = NativeBattleEffectNodeContextMemory.Publish(frame.Memory, CopyBytes(source, 0, NativeBattleEffectNodeContextMemory.Capture(frame.Memory), checked((int)(address - NativeBattleEffectNodeContextMemory.Address)), length)) };
        }
        if (Contains(0x2793E58, 32, address, length))
        {
            NativeBattleEffectArena prior = frame.Memory.SharedEffectView2793E58 is { } view ? PackedArena(view) : NativeBattleEffectArena.Clear().MarkUnknown(0, 32);
            return frame with { Memory = frame.Memory with { SharedEffectView2793E58 = Packed(CopyBytes(source, 0, prior, checked((int)(address - 0x2793E58)), length)) } };
        }
        for (int index = 0; index < frame.Effect.Pools.Length; index++)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools[index];
            if (Contains(pool.BankAddress, checked(pool.Stride * pool.Slots.Length), address, length))
            {
                pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, checked((int)(address - pool.BankAddress)), source, length);
                return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(index, pool) } };
            }
        }
        if (Owned(address, length))
        {
            return frame with { State = frame.State.WriteView(address, source, length) };
        }
        if (Contains(CommonAddress, NativeBattleEffectArena.Length, address, length))
        {
            return frame with { Memory = frame.Memory with { Arena = CopyBytes(source, 0, frame.Memory.Arena ?? throw new NotSupportedException("Effect31 requires its current common allocation."), checked((int)(address - CommonAddress)), length) } };
        }
        return frame with { Staging = NativeBattleTemporaryMemory.PublishView(RequireStaging(frame), address, source, length) };
    }

    public static NativeBattleEffectArena CopyBytes(NativeBattleEffectArena source, int sourceOffset, NativeBattleEffectArena destination, int destinationOffset, int length) => NativeBattleEffectNodeContextMemory.Copy(source, sourceOffset, destination, destinationOffset, length);
    public static NativeBattleEffectArena PackedArena(NativeBattlePackedMatrix32 matrix)
    {
        matrix.Validate();
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().Write(0, matrix.Bytes.AsSpan());
        for (int index = 0; index < 32; index++)
        {
            if (!matrix.Known[index])
            {
                arena = arena.MarkUnknown(index, 1);
            }
        }
        return arena;
    }

    public static NativeBattlePackedMatrix32 Packed(NativeBattleEffectArena arena)
    {
        (byte[] bytes, bool[] known) = arena.ReadSnapshot(0, 32);
        return new(bytes.ToImmutableArray(), known.ToImmutableArray());
    }
    public static short Word31(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadInt16LittleEndian(arena.Read(offset, 2));
    public static uint Dword31(NativeBattleEffectArena arena, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(arena.Read(offset, 4));
    public static NativeBattleEffect31Frame ClearBytes(NativeBattleEffect31Frame frame, uint address, int length) => WriteBytes(frame, address, NativeBattleEffectArena.Clear(), length);
    public static NativeBattleFileStaging RequireStaging(NativeBattleEffect31Frame frame) => frame.Staging ?? throw new NotSupportedException("Effect31 requires its actual retained physical staging owner.");

    private static bool Contains(uint start, int size, uint address, int length) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size;

    private static bool Owned(uint address, int length)
    {
        ulong cursor = address;
        ulong end = (ulong)address + (uint)length;
        foreach ((uint start, int size) in OwnedRanges)
        {
            if (cursor < start)
            {
                return false;
            }
            if (cursor <= (ulong)start + (uint)size)
            {
                cursor = Math.Max(cursor, (ulong)start + (uint)size);
                if (cursor >= end)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
