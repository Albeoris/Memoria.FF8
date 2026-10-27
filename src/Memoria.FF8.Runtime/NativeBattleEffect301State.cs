using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect301Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect301State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] NativeBattleEffectArena ScriptCursor4, [property: JsonRequired] bool? ScriptSourcePending)
{
    public static NativeBattleEffect301State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), NativeBattleEffectArena.Clear().MarkUnknown(0, 4), true);

    public static NativeBattleEffect301State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), NativeBattleEffectArena.Clear().MarkUnknown(0, 4), null);
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect301State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (IsScriptRange(address, bytes.Length))
        {
            ScriptCursor4.Write(checked((int)(address - ScriptPointerAddress)), bytes);
            return this;
        }
        int offset = Require(address, bytes.Length);
        Storage.Write(offset, bytes);
        return this;
    }
    public NativeBattleEffect301State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect301State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect301State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (IsScriptRange(address, length))
        {
            return NativeBattleEffectNodeContextMemory.Copy(ScriptCursor4, checked((int)(address - ScriptPointerAddress)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
        }
        int offset = Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect301State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (IsScriptRange(address, length))
        {
            NativeBattleEffectNodeContextMemory.Copy(source, 0, ScriptCursor4, checked((int)(address - ScriptPointerAddress)), length);
            return this;
        }
        int offset = Require(address, length);
        NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, offset, length);
        return this;
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect301State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect301State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect301 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect301State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect301State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect301 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect301 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        Storage.Validate();
        ValidateBank(ScriptCursor4, 4);
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateDataRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        foreach (NativeBattleEffectStorageRange region in Storage.EnumerateUnknownRanges())
        {
            _ = Require(checked(BaseAddress + (uint)region.Offset), region.Length);
        }
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect301 requires its exact physical banks.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect301 is missing a physical bank owner.");
            }
            if (bank is not null)
            {
                ValidateBank(bank, shape.Stride * shape.Capacity);
            }
        }
    }

    public const uint ScriptPointerAddress = 0x103DC9C;

    public NativeBattleEffect301State BindSources(NativeBattleEffect301Resources resources)
    {
        if (ScriptSourcePending is null)
        {
            throw new NotSupportedException("Effect301 cannot import a cold script cursor into unknown process history.");
        }
        if (ScriptSourcePending == false)
        {
            return this;
        }
        if (ScriptCursor4.ReadSnapshot(0, 4).Known.Any(value => value))
        {
            throw new InvalidDataException("Effect301 cannot replace an already observed current script cursor with its cold input.");
        }
        if (resources.OriginalScriptCursor4.IsDefault || resources.OriginalScriptCursor4.Length != 4)
        {
            throw new InvalidDataException("Effect301 requires its original four-byte cold script input.");
        }
        return this with { ScriptCursor4 = ScriptCursor4.Write(0, resources.OriginalScriptCursor4.AsSpan()), ScriptSourcePending = false };
    }

    private static bool IsScriptRange(uint address, int length) => length >= 0 && address >= ScriptPointerAddress && (ulong)address + (uint)length <= ScriptPointerAddress + 4UL;

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
        throw new InvalidDataException("Effect301 storage excludes fixed pools, descriptors, canonical model owners, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect301 pool index leaves its original fixed bank.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
