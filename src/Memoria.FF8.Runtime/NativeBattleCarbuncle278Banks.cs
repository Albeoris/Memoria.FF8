using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

internal static class NativeBattleCarbuncle278Banks
{
    private const uint BaseAddress = NativeBattleEffectNodeMemory.ArenaAddress;
    internal const uint Debris = 0x2508128;
    internal const uint Rays = 0x2508138;
    internal const uint Rings = 0x2508148;
    internal const uint Recipients = 0x2508158;
    internal const uint ReturnParticles = 0x2508168;
    internal const uint Sparkles = 0x2508178;
    internal const uint Creature = 0x2508188;

    private static readonly ImmutableArray<NativeBattleCureFamilyPool> Profiles =
    [
        new(Debris, BaseAddress + 4, 0x1C, 256),
        new(Rays, BaseAddress + 0x1C04, 0x1C, 24),
        new(Rings, BaseAddress + 0x1EA4, 0x1C, 9),
        new(Recipients, BaseAddress + 0x1FA0, 0x10C, 3),
        new(ReturnParticles, BaseAddress + 0x22C4, 0x1C, 8),
        new(Sparkles, BaseAddress + 0x23A4, 0x14, 64),
        new(Creature, BaseAddress + 0x28A4, 0x5E0, 1)
    ];

    internal static NativeBattleCarbuncle278Frame Initialize(NativeBattleCarbuncle278Frame frame, BattleModelRoster roster, int count)
    {
        NativeBattleEffectMemory memory = frame.Memory;
        uint working = NativeBattleCarbuncle278Entry.RequireState(memory).ModelBuffer81F8;
        var layouts = new (uint Address, ushort Capacity)[Profiles.Length];
        for (int index = 0; index < Profiles.Length; index++)
        {
            NativeBattleCureFamilyPool profile = Profiles[index];
            ushort capacity = Capacity(profile, count);
            uint address = unchecked(working + (profile.Bank - BaseAddress));
            if (capacity > profile.Capacity)
            {
                (memory, address) = NativeBattlePresentationTaskStorage.Reserve(memory, frame.Effect, roster, profile.Stride, capacity);
            }
            layouts[index] = (address, capacity);
        }
        NativeBattleEffectArena arena = memory.Arena ?? throw new NotSupportedException("Native Carbuncle requires its current effect arena.");
        ImmutableArray<NativeBattleEffectTaskPool> pools = frame.Effect.Pools;
        for (int index = 0; index < Profiles.Length; index++)
        {
            NativeBattleCureFamilyPool profile = Profiles[index];
            (uint address, ushort capacity) = layouts[index];
            NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(profile.Descriptor, address, profile.Stride, capacity);
            pools = pools.Where(previous => previous.DescriptorAddress != profile.Descriptor).Append(pool).ToImmutableArray();
            arena = arena.Write(checked((int)(profile.Bank - BaseAddress)), new byte[profile.Stride * profile.Capacity]);
        }
        return frame with { Memory = memory with { Arena = arena }, Effect = frame.Effect with { Pools = pools } };
    }

    internal static void ValidateExpanded(NativeBattleCarbuncle278Frame frame, int count)
    {
        List<NativeBattleEffectTaskPool> hosted = [];
        foreach (NativeBattleCureFamilyPool profile in Profiles)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == profile.Descriptor);
            ushort capacity = Capacity(profile, count);
            bool located = capacity == profile.Capacity ? pool.BankAddress == profile.Bank : IsHosted(pool);
            if (!located || pool.Stride != profile.Stride || pool.Slots.Length != capacity)
            {
                throw new InvalidDataException("Native Carbuncle lost its actual reserved task bank.");
            }
            if (capacity > profile.Capacity)
            {
                ulong end = pool.BankAddress + (ulong)pool.Stride * capacity;
                if (hosted.Any(other => pool.BankAddress < other.BankAddress + (ulong)other.Stride * (uint)other.Slots.Length && other.BankAddress < end))
                {
                    throw new InvalidDataException("Native Carbuncle hosted task banks overlap.");
                }
                hosted.Add(pool);
            }
        }
    }

    internal static bool IsHosted(NativeBattleEffectTaskPool pool) => NativeBattlePresentationTaskStorage.IsHostedBank(pool.BankAddress, pool.Stride, pool.Slots.Length);

    internal static bool CanAppend(NativeBattleCarbuncle278Frame frame, NativeBattleEffectTaskPool pool, bool required = false)
    {
        if (pool.Slots.Any(task => (task.Flags & 1) == 0))
        {
            return true;
        }
        if (frame.ExpandedStorage && required)
        {
            throw new InvalidDataException("Native Carbuncle exhausted a reserved task bank.");
        }
        return false;
    }

    internal static NativeBattleCarbuncle278Frame MirrorPayload(NativeBattleCarbuncle278Frame frame, NativeBattleEffectTaskPool pool, int slot, NativeBattleModulePoolTask task, bool recipientCache = false)
    {
        if (IsHosted(pool))
        {
            if (recipientCache)
            {
                task.MarkPayloadUnknown(0x46, 2);
            }
            return frame;
        }
        int offset = checked((int)(pool.BankAddress - BaseAddress) + slot * pool.Stride + 12);
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        if (frame.ExpandedStorage)
        {
            (byte[] bytes, bool[] known) = task.BorrowPayloadArena(12, task.PayloadLength).ReadSnapshot(0, task.PayloadLength);
            arena = arena.WriteWithKnownness(offset, bytes, known);
        }
        else
        {
            arena = arena.Write(offset, task.ReadRawPayloadSpan());
        }
        if (recipientCache)
        {
            arena = arena.MarkUnknown(offset + 0x46 - 12, 2);
        }
        return frame with { Memory = frame.Memory with { Arena = arena } };
    }

    internal static bool IsHostedRecipient(NativeBattleCarbuncle278Frame frame, uint address)
    {
        if (!frame.ExpandedStorage)
        {
            return false;
        }
        NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(child => child.DescriptorAddress == Recipients);
        uint offset = unchecked(address - pool.BankAddress);
        if (offset >= (uint)(pool.Stride * pool.Slots.Length) || offset % pool.Stride != 0)
        {
            throw new InvalidDataException("Native Carbuncle child lost its actual addressed recipient parent.");
        }
        return IsHosted(pool);
    }

    internal static NativeBattleCarbuncle278Frame RetireHosted(NativeBattleCarbuncle278Frame frame)
    {
        NativeBattleEffectArena arena = frame.Memory.Arena ?? throw new NotSupportedException("Native Carbuncle requires its original task bank destination.");
        var retained = frame.Effect.Pools.ToBuilder();
        foreach (NativeBattleCureFamilyPool profile in Profiles)
        {
            NativeBattleEffectTaskPool pool = frame.Effect.Pools.Single(pool => pool.DescriptorAddress == profile.Descriptor);
            if (!IsHosted(pool))
            {
                continue;
            }
            if (pool.Head is not null)
            {
                throw new InvalidDataException("Native Carbuncle cannot retire a live hosted task bank.");
            }
            int length = profile.Stride * profile.Capacity;
            NativeBattleTaskBankOwner owner = pool.RequireOwner();
            var source = NativeBattleEffectArena.BorrowWorking(owner.Storage, owner.StorageOffset, length);
            arena = NativeBattleEffectNodeContextMemory.Copy(source, 0, arena, checked((int)(profile.Bank - BaseAddress)), length);
            retained.Remove(pool);
        }
        return frame with { Memory = frame.Memory with { Arena = arena }, Effect = frame.Effect with { Pools = retained.ToImmutable() } };
    }

    private static ushort Capacity(NativeBattleCureFamilyPool profile, int recipients)
    {
        int required = profile.Descriptor switch
        {
            Debris => checked(128 * recipients),
            Rays => checked(8 * recipients),
            Rings => checked(3 * recipients),
            Recipients => recipients,
            Sparkles => checked(44 + 11 * recipients),
            _ => 0
        };
        return checked((ushort)Math.Max(profile.Capacity, required));
    }
}
