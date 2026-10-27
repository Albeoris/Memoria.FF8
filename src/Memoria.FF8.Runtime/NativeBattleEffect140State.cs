using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect140Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect140State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] bool ColdSourceOrigin, [property: JsonRequired] bool StaticSourcesBound)
{
    public static NativeBattleEffect140State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), true, false);

    public static NativeBattleEffect140State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), false, false);
    }

    public byte[] Read(uint address, int length)
    {
        return ReadView(address, length).Read(0, length);
    }

    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect140State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        return WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    }

    public NativeBattleEffect140State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect140State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect140State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect140State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length);
        return this;
    }

    public (NativeBattleEffect140State State, NativeBattleFileStaging Staging) BindSources(NativeBattleFileStaging staging, NativeBattleEffect140Resources resources)
    {
        if (StaticSourcesBound)
        {
            _ = staging.Read(ModelAddress, 24);
            foreach (uint address in MutableMeshAddresses)
            {
                _ = staging.Read(address, 8);
            }
            return (this, staging);
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect140 cannot initialize original mutable bytes from unknown process history.");
        }
        int present = BoundSources.Count(source => staging.ReadSnapshot(source.Address, source.Length).Known.Any(known => known));
        if (present != 0 && present != BoundSources.Length)
        {
            throw new InvalidDataException("Effect140 source binding requires all four physical owners atomically.");
        }
        if (present == 0)
        {
            staging = staging.Rebase(Math.Min(staging.BaseAddress, ModelAddress)).Write(ModelAddress, resources.Model.Bytes.AsSpan());
            foreach (uint address in MutableMeshAddresses)
            {
                staging = staging.Write(address, resources.ReadMesh(address).Bytes.AsSpan());
            }
        }
        else
        {
            _ = staging.Read(ModelAddress, 24);
            foreach (uint address in MutableMeshAddresses)
            {
                _ = staging.Read(address, 8);
            }
        }
        return (this with { StaticSourcesBound = true }, staging);
    }

    public NativeBattleEffectArena ReadBank() => ReadBank(0);
    public NativeBattleEffect140State DetachBank() => DetachBank(0);
    public NativeBattleEffect140State AttachBank(NativeBattleEffectArena bank) => AttachBank(0, bank);

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect140State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect140State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);
    public NativeBattleEffectArena ReadBank(uint address) => address == RootBank && RootBankStorage is { } bank ? bank : throw new InvalidDataException("Effect140 root bank is absent or exclusively owned by its active pool.");

    public NativeBattleEffect140State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { RootBankStorage = null };
    }

    public NativeBattleEffect140State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        if (address != RootBank || RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect140 cannot duplicate its retained root bank.");
        }
        ValidateBank(bank, RootBankLength);
        return this with { RootBankStorage = bank };
    }

    public void Validate()
    {
        Storage.Validate();
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateDataRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateUnknownRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (RootBankStorage is { } root)
        {
            ValidateBank(root, RootBankLength);
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
        throw new InvalidDataException("Effect140 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index) => index == 0 ? RootBank : throw new InvalidDataException("Effect140 has only one fixed root bank.");

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
