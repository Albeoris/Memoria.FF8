using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect64Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect64State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] NativeBattleEffectArena? RootBankStorage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena> ScriptSources, [property: JsonRequired] bool ColdSourceOrigin)
{
    public static NativeBattleEffect64State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), ImmutableDictionary<uint, NativeBattleEffectArena>.Empty, true);

    public static NativeBattleEffect64State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, NativeBattleEffectArena.Clear().MarkUnknown(0, RootBankLength), ImmutableDictionary<uint, NativeBattleEffectArena>.Empty, false);
    }

    public byte[] Read(uint address, int length)
    {
        return ReadView(address, length).Read(0, length);
    }

    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect64State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        return WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    }

    public NativeBattleEffect64State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect64State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect64State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        foreach ((uint start, int size) in ScriptLengths)
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size)
            {
                return NativeBattleEffectNodeContextMemory.Copy(ReadScriptSource(start), checked((int)(address - start)), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
            }
        }
        return NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect64State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        foreach ((uint start, int size) in ScriptLengths)
        {
            if (address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size)
            {
                return ReplaceScriptSource(start, NativeBattleEffectNodeContextMemory.Copy(source, 0, ReadScriptSource(start), checked((int)(address - start)), length));
            }
        }
        return this with { Storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length) };
    }

    public NativeBattleEffect64State BindScriptSource(NativeBattleEffect64Resources resources, uint address)
    {
        if (ScriptSources.ContainsKey(address))
        {
            _ = ReadScriptSource(address);
            return this;
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect64 cannot reconstruct an unknown imported mutable script.");
        }
        return ReplaceScriptSource(address, NativeBattleEffectArena.Clear().Write(0, resources.ReadOriginalScript(address).AsSpan()));
    }

    public NativeBattleEffectArena ReadScriptSource(uint address) => ScriptSources.TryGetValue(address, out NativeBattleEffectArena? source) ? source : throw new NotSupportedException("Effect64 requires its actual current mutable script source.");

    public NativeBattleEffect64State ReplaceScriptSource(uint address, NativeBattleEffectArena source)
    {
        if (!ScriptLengths.TryGetValue(address, out int length))
        {
            throw new InvalidDataException("Effect64 script address is unsupported.");
        }
        ValidateBank(source, length);
        return this with { ScriptSources = ScriptSources.SetItem(address, source) };
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect64State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect64State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);
    public NativeBattleEffectArena ReadBank(uint address) => address == RootBank && RootBankStorage is { } bank ? bank : throw new InvalidDataException("Effect64 root bank is absent or exclusively owned by its active pool.");

    public NativeBattleEffect64State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { RootBankStorage = null };
    }

    public NativeBattleEffect64State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        if (address != RootBank || RootBankStorage is not null)
        {
            throw new InvalidDataException("Effect64 cannot duplicate its retained root bank.");
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
        if (RootBankStorage is { } root)
        {
            ValidateBank(root, RootBankLength);
        }
        if (ScriptSources is null)
        {
            throw new InvalidDataException("Effect64 current script owners are missing.");
        }
        foreach ((uint address, NativeBattleEffectArena source) in ScriptSources)
        {
            if (!ScriptLengths.TryGetValue(address, out int length))
            {
                throw new InvalidDataException("Effect64 current script has an unsupported address.");
            }
            ValidateBank(source, length);
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
        throw new InvalidDataException("Effect64 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index) => index == 0 ? RootBank : throw new InvalidDataException("Effect64 has only one fixed root bank.");

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect64 retained bytes leave their physical bank allocation.");
        }
    }
}
