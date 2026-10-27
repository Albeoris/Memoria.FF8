using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect65State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] NativeBattleEffectArena LiteralStorage, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] NativeBattleEffectArena? ChildQueueStorage)
{
    public static NativeBattleEffect65State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());

    public static NativeBattleEffect65State FromUnknown()
    {
        NativeBattleEffectArena globals = NativeBattleEffectArena.Clear();
        NativeBattleEffectArena literals = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            globals = globals.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        foreach ((uint address, int length) in LiteralRanges)
        {
            literals = literals.MarkUnknown(checked((int)(address - LiteralBase)), length);
        }
        return new(globals, literals, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), NativeBattleEffectArena.Clear().MarkUnknown(0, ChildQueueLength));
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect65State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (Within(address, bytes.Length, ChildDescriptor, ChildQueueLength))
        {
            ReadChildQueue().Write(checked((int)(address - ChildDescriptor)), bytes);
            return this;
        }
        bool literal = address < BaseAddress;
        int offset = Require(address, bytes.Length, literal ? LiteralBase : BaseAddress, literal ? LiteralRanges : OwnedRanges);
        (literal ? LiteralStorage : Storage).Write(offset, bytes);
        return this;
    }
    public NativeBattleEffect65State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect65State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect65State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (Within(address, length, ChildDescriptor, ChildQueueLength))
        {
            return Copy(ReadChildQueue(), checked((int)(address - ChildDescriptor)), length);
        }
        bool literal = address < BaseAddress;
        int offset = Require(address, length, literal ? LiteralBase : BaseAddress, literal ? LiteralRanges : OwnedRanges);
        return Copy(literal ? LiteralStorage : Storage, offset, length);
    }

    public NativeBattleEffect65State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (Within(address, length, ChildDescriptor, ChildQueueLength))
        {
            NativeBattleEffectNodeContextMemory.Copy(source, 0, ReadChildQueue(), checked((int)(address - ChildDescriptor)), length);
            return this;
        }
        bool literal = address < BaseAddress;
        int offset = Require(address, length, literal ? LiteralBase : BaseAddress, literal ? LiteralRanges : OwnedRanges);
        NativeBattleEffectNodeContextMemory.Copy(source, 0, literal ? LiteralStorage : Storage, offset, length);
        return this;
    }

    public NativeBattleEffectArena ReadChildQueue() => ChildQueueStorage ?? throw new InvalidDataException("Effect65 child queue is exclusively owned by its active pool.");

    public NativeBattleEffect65State DetachChildQueue()
    {
        _ = ReadChildQueue();
        return this with { ChildQueueStorage = null };
    }

    public NativeBattleEffect65State AttachChildQueue(NativeBattleEffectArena queue)
    {
        if (ChildQueueStorage is not null)
        {
            throw new InvalidDataException("Effect65 cannot duplicate its retained child queue.");
        }
        ValidateBank(queue, ChildQueueLength);
        return this with { ChildQueueStorage = queue };
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect65State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect65State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);
    public NativeBattleEffectArena ReadBank(uint address) => address == RootBank && RootBankStorage is { } bank ? bank : throw new InvalidDataException("Effect65 fixed bank is absent or exclusively owned by its active pool.");

    public NativeBattleEffect65State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { RootBankStorage = null };
    }

    public NativeBattleEffect65State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        if (address != RootBank || RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect65 cannot duplicate its retained root bank.");
        }
        ValidateBank(bank, RootBankLength);
        return this with { RootBankStorage = bank };
    }

    public void Validate()
    {
        ValidateStorage(Storage, BaseAddress, OwnedRanges);
        ValidateStorage(LiteralStorage, LiteralBase, LiteralRanges);
        if (RootBankStorage is { } root)
        {
            ValidateBank(root, RootBankLength);
        }
        if (ChildQueueStorage is { } queue)
        {
            ValidateBank(queue, ChildQueueLength);
        }
    }

    private static NativeBattleEffectArena Copy(NativeBattleEffectArena source, int offset, int length) => NativeBattleEffectNodeContextMemory.Copy(source, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    private static bool Within(uint address, int length, uint start, int size) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size;
    private static uint BankAddress(int index) => index == 0 ? RootBank : throw new InvalidDataException("Effect65 has only one fixed root bank.");

    private static int Require(uint address, int length, uint origin, ImmutableArray<(uint Address, int Length)> ranges)
    {
        if (length >= 0)
        {
            ulong cursor = address;
            ulong end = (ulong)address + (uint)length;
            foreach ((uint start, int size) in ranges)
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
                        return checked((int)(address - origin));
                    }
                }
            }
        }
        throw new InvalidDataException("Effect65 storage excludes unobserved gaps, readonly descriptor lanes, active pools and COMMON.");
    }

    private static void ValidateStorage(NativeBattleEffectArena storage, uint origin, ImmutableArray<(uint Address, int Length)> ranges)
    {
        storage.Validate();
        foreach (NativeBattleEffectStorageRange region in storage.EnumerateDataRanges())
        {
            _ = Require(checked(origin + (uint)region.Offset), region.Length, origin, ranges);
        }
        foreach (NativeBattleEffectStorageRange region in storage.EnumerateUnknownRanges())
        {
            _ = Require(checked(origin + (uint)region.Offset), region.Length, origin, ranges);
        }
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
