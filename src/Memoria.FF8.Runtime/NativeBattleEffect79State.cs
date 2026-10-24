using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect79Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect79State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] ImmutableDictionary<uint, ImmutableArray<byte>> NodeBanks, [property: JsonRequired] ImmutableArray<byte>? ProgramData, [property: JsonRequired] bool ColdSourceOrigin)
{
    public static NativeBattleEffect79State FromColdProcess() => new(NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, null, true);

    public static NativeBattleEffect79State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), ImmutableDictionary<uint, ImmutableArray<byte>>.Empty, null, false);
    }

    public NativeBattleEffect79State BindProgram(NativeBattleEffect79Resources resources)
    {
        if (ProgramData is not null)
        {
            return this;
        }
        RequireColdOrigin();
        return this with { ProgramData = resources.IoScript.Data };
    }

    public NativeBattleEffect79State BindNodeSource(uint address, NativeBattleEffect79Resources resources)
    {
        if (NodeBanks.ContainsKey(address))
        {
            return this;
        }
        RequireColdOrigin();
        return this with { NodeBanks = NodeBanks.Add(address, resources.ReadNodeBank(address)) };
    }

    public NativeBattleEffect79State BindNodeSource(NativeBattleEffect79Resources resources, uint address) => BindNodeSource(address, resources);

    public ImmutableArray<byte> ReadNodeBank(uint address) => NodeBanks.TryGetValue(address, out ImmutableArray<byte> data) ? data : throw new NotSupportedException("Effect79 requires its selected current node source to be explicitly bound.");

    public NativeBattleEffect79State ReplaceNodeBank(uint address, ImmutableArray<byte> bytes)
    {
        if (!NodeBanks.ContainsKey(address) || !NodeSourceLengths.TryGetValue(address, out int length) || bytes.IsDefault || bytes.Length != length)
        {
            throw new InvalidDataException("Effect79 current node writes require the same already bound physical resource.");
        }
        return this with { NodeBanks = NodeBanks.SetItem(address, bytes) };
    }

    private void RequireColdOrigin()
    {
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect79 unknown imported source bytes require an explicit original-owner migration.");
        }
    }

    public byte[] Read(uint address, int length)
    {
        if (InSource(ScriptAddress, ScriptLength, address, length))
        {
            return (ProgramData ?? throw new NotSupportedException("Effect79 requires its current mutable IO programme.")).AsSpan().Slice(checked((int)(address - ScriptAddress)), length).ToArray();
        }
        foreach ((uint source, int sourceLength) in NodeSourceLengths)
        {
            if (InSource(source, sourceLength, address, length))
            {
                return ReadNodeBank(source).AsSpan().Slice(checked((int)(address - source)), length).ToArray();
            }
        }
        return Storage.Read(Require(address, length), length);
    }

    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));

    public NativeBattleEffect79State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        if (InSource(ScriptAddress, ScriptLength, address, bytes.Length))
        {
            byte[] script = (ProgramData ?? throw new NotSupportedException("Effect79 requires its bound mutable IO programme before writes.")).ToArray();
            bytes.CopyTo(script.AsSpan(checked((int)(address - ScriptAddress))));
            return this with { ProgramData = script.ToImmutableArray() };
        }
        foreach ((uint source, int sourceLength) in NodeSourceLengths)
        {
            if (InSource(source, sourceLength, address, bytes.Length))
            {
                byte[] data = ReadNodeBank(source).ToArray();
                bytes.CopyTo(data.AsSpan(checked((int)(address - source))));
                return ReplaceNodeBank(source, data.ToImmutableArray());
            }
        }
        return this with { Storage = Storage.Write(Require(address, bytes.Length), bytes) };
    }

    public NativeBattleEffect79State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect79State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect79State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (InMutableSource(address, length))
        {
            return NativeBattleEffectArena.Clear().Write(0, Read(address, length));
        }
        return NativeBattleEffectNodeContextMemory.Copy(Storage, Require(address, length), NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect79State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (InMutableSource(address, length))
        {
            return Write(address, source.Read(0, length));
        }
        return this with { Storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, Require(address, length), length) };
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect79State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect79State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect79 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect79State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect79State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect79 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect79 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
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
        if (FixedBanks is null || FixedBanks.Count != Pools.Length || NodeBanks is null || ProgramData is { } script && (script.IsDefault || script.Length != ScriptLength))
        {
            throw new InvalidDataException("Effect79 storage has different physical bank or programme dimensions.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect79 is missing an original physical bank owner.");
            }
            if (bank is not null)
            {
                ValidateBank(bank, shape.Stride * shape.Capacity);
            }
        }
        foreach ((uint address, ImmutableArray<byte> bytes) in NodeBanks)
        {
            if (!NodeSourceLengths.TryGetValue(address, out int length) || bytes.IsDefault || bytes.Length != length)
            {
                throw new InvalidDataException("Effect79 mutable node data leaves its selected addressed resource.");
            }
        }
    }

    private static bool InSource(uint source, int sourceLength, uint address, int length) => length >= 0 && address >= source && (ulong)address + (uint)length <= (ulong)source + (uint)sourceLength;
    private static bool InMutableSource(uint address, int length) => InSource(ScriptAddress, ScriptLength, address, length) || NodeSourceLengths.Any(pair => InSource(pair.Key, pair.Value, address, length));

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
        throw new InvalidDataException("Effect79 storage excludes fixed pools, descriptors, shared view/matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect79 pool index leaves its ten original fixed banks.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect79 retained bytes leave their physical bank allocation.");
        }
    }
}
