using System.Buffers.Binary;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect70State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage)
{
    public static NativeBattleEffect70State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear());

    public static NativeBattleEffect70State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength));
    }

    public byte[] Read(uint address, int length)
    {
        return Storage.Read(Require(address, length), length);
    }

    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect70State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        return this with { Storage = Storage.Write(Require(address, bytes.Length), bytes) };
    }

    public NativeBattleEffect70State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect70State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect70State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        return NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect70State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        return this with { Storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length) };
    }

    public NativeBattleEffectArena ReadRootBank() => RootBankStorage ?? throw new InvalidDataException("Effect70 root bank is owned exclusively by the active pool.");

    public NativeBattleEffect70State DetachRootBank()
    {
        _ = ReadRootBank();
        return this with { RootBankStorage = null };
    }

    public NativeBattleEffect70State AttachRootBank(NativeBattleEffectArena bank)
    {
        if (RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect70 cannot duplicate its retained physical root bank.");
        }
        ValidateBank(bank, RootBankLength);
        return this with { RootBankStorage = bank };
    }

    public void Validate()
    {
        Storage.Validate();
        foreach (NativeBattleEffectMemoryRegion region in Storage.Regions)
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Data.Length);
        }
        foreach (NativeBattleEffectUnknownRegion region in Storage.UnknownRegions)
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (RootBankStorage is { } bank)
        {
            ValidateBank(bank, RootBankLength);
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
        throw new InvalidDataException("Effect70 storage excludes fixed root bank, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect70 retained bytes leave their physical bank allocation.");
        }
    }
}
