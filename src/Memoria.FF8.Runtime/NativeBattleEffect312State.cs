using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect312Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect312State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] bool ColdSourceOrigin, [property: JsonRequired] bool StaticPacketBound, [property: JsonRequired] NativeBattleEffectArena? CapturePacket24, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedQueues)
{
    public const int StorageLength = 16960;

    [JsonIgnore]
    public NativeBattleEffectArena? RootBank => FixedBanks[NativeBattleEffect312Profile.RootBank];

    [JsonIgnore]
    public NativeBattleEffectArena? ChildBank => FixedBanks[NativeBattleEffect312Profile.ChildBank];

    [JsonIgnore]
    public NativeBattleEffectArena? CapturePacket => CapturePacket24;

    public static NativeBattleEffect312State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), true, false, null, Pools.ToImmutableDictionary(pool => pool.Descriptor, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()));

    public static NativeBattleEffect312State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), false, false, null, Pools.ToImmutableDictionary(pool => pool.Descriptor, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, 8)));
    }

    public NativeBattleEffect312State BindCapturePacket(NativeBattleEffect312Resources resources)
    {
        if (StaticPacketBound)
        {
            _ = ReadCapturePacket();
            return this;
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect312 cannot initialize its physical capture packet from unknown process history.");
        }
        if (CapturePacket24 is not null)
        {
            throw new InvalidDataException("Effect312 capture packet has no matching publication state.");
        }
        return this with { StaticPacketBound = true, CapturePacket24 = NativeBattleEffectArena.Clear().Write(0, resources.CapturePacket.AsSpan()) };
    }

    public NativeBattleEffectArena ReadCapturePacket() => StaticPacketBound && CapturePacket24 is { } packet ? packet : throw new NotSupportedException("Effect312 requires its currently bound physical capture packet.");

    public NativeBattleEffect312State WriteCapturePacket(NativeBattleEffectArena packet)
    {
        _ = ReadCapturePacket();
        ValidateBank(packet, CapturePacketLength);
        return this with { CapturePacket24 = packet };
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect312State Write(uint address, ReadOnlySpan<byte> bytes) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    public NativeBattleEffect312State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect312State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect312State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        int offset = Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect312State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        int offset = Require(address, length);
        return this with { Storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, offset, length) };
    }

    public NativeBattleEffectArena ReadQueue(uint descriptor) => FixedQueues.TryGetValue(descriptor, out NativeBattleEffectArena? queue) && queue is not null ? queue : throw new InvalidDataException("Effect312 inactive queue is owned exclusively by its active hierarchy.");

    public NativeBattleEffect312State DetachQueue(uint descriptor)
    {
        _ = ReadQueue(descriptor);
        return this with { FixedQueues = FixedQueues.SetItem(descriptor, null) };
    }

    public NativeBattleEffect312State AttachQueue(uint descriptor, NativeBattleEffectArena queue)
    {
        if (!FixedQueues.TryGetValue(descriptor, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect312 queue already has an inactive owner.");
        }
        ValidateBank(queue, 8);
        return this with { FixedQueues = FixedQueues.SetItem(descriptor, queue) };
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect312State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect312State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect312 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect312State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect312State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect312 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect312 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        Storage.Validate();
        if (StaticPacketBound != (CapturePacket24 is not null))
        {
            throw new InvalidDataException("Effect312 capture packet publication and current owner disagree.");
        }
        if (CapturePacket24 is { } packet)
        {
            ValidateBank(packet, CapturePacketLength);
        }
        foreach (NativeBattleEffectMemoryRegion region in Storage.Regions)
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Storage.UnknownRegions)
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect312 requires its exact physical banks.");
        }
        if (FixedQueues is null || FixedQueues.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect312 requires its exact physical queue owners.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedQueues.TryGetValue(shape.Descriptor, out NativeBattleEffectArena? queue))
            {
                throw new InvalidDataException("Effect312 is missing a physical queue owner.");
            }
            if (queue is not null)
            {
                ValidateBank(queue, 8);
            }
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect312 is missing a physical bank owner.");
            }
            if (bank is not null)
            {
                ValidateBank(bank, shape.Stride * shape.Capacity);
            }
        }
    }

    private static int Require(uint address, int length)
    {
        if (length >= 0)
        {
            ulong cursor = address;
            ulong end = (ulong)address + (uint)length;
            foreach ((uint start, int size) in OwnedRanges)
            {
                if (cursor < start)
                {
                    break;
                }
                if (cursor >= start && cursor <= (ulong)start + (uint)size)
                {
                    cursor = Math.Max(cursor, (ulong)start + (uint)size);
                    if (cursor >= end)
                    {
                        return checked((int)(address - BaseAddress));
                    }
                }
            }
        }
        throw new InvalidDataException("Effect312 storage excludes fixed pools, separately owned descriptors, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect312 pool index leaves its two original fixed banks.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect312 retained bytes leave their physical bank allocation.");
        }
    }
}
