using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect68Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect68State([property: JsonRequired] NativeBattleEffectArena Core, [property: JsonRequired] NativeBattleEffectArena Header, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] bool ColdSourceOrigin, [property: JsonRequired] bool MeshSourcesBound)
{
    public static NativeBattleEffect68State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear(), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), true, false);

    public static NativeBattleEffect68State FromUnknown()
    {
        NativeBattleEffectArena core = NativeBattleEffectArena.Clear();
        NativeBattleEffectArena header = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in CoreRanges)
        {
            core = core.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        foreach ((uint address, int length) in HeaderRanges)
        {
            header = header.MarkUnknown(checked((int)(address - HeaderBase)), length);
        }
        return new(core, header, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, pool.Stride * pool.Capacity)), false, false);
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect68State Write(uint address, ReadOnlySpan<byte> bytes)
    {
        bool header = address >= HeaderBase;
        int offset = Require(address, bytes.Length, header ? HeaderBase : BaseAddress, header ? HeaderRanges : CoreRanges);
        (header ? Header : Core).Write(offset, bytes);
        return this;
    }
    public NativeBattleEffect68State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect68State WriteWord(uint address, ushort value)
    {
        Span<byte> encoded = stackalloc byte[2];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(encoded, value);
        return Write(address, encoded);
    }
    public NativeBattleEffect68State WriteDword(uint address, uint value)
    {
        Span<byte> encoded = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(encoded, value);
        return Write(address, encoded);
    }

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        bool header = address >= HeaderBase;
        int offset = Require(address, length, header ? HeaderBase : BaseAddress, header ? HeaderRanges : CoreRanges);
        return NativeBattleEffectNodeContextMemory.Copy(header ? Header : Core, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect68State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        bool header = address >= HeaderBase;
        int offset = Require(address, length, header ? HeaderBase : BaseAddress, header ? HeaderRanges : CoreRanges);
        NativeBattleEffectNodeContextMemory.Copy(source, 0, header ? Header : Core, offset, length);
        return this;
    }

    public (NativeBattleEffect68State State, NativeBattleEffectMemory Memory) BindMeshSources(NativeBattleEffectMemory memory, NativeBattleEffect68Resources resources)
    {
        NativeBattleScanProcess scan = memory.Scan ?? throw new NotSupportedException("Effect68 requires its current shared Scan process.");
        int present = MutableMeshAddresses.Count(scan.MutableMeshes.ContainsKey);
        if (MeshSourcesBound)
        {
            if (present != MutableMeshAddresses.Length)
            {
                throw new NotSupportedException("Effect68 bound mutable mesh bytes are missing; original resources cannot restore current state.");
            }
            return (this, memory);
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect68 cannot initialize mutable source bytes from unknown process history.");
        }
        if (present != 0 && present != MutableMeshAddresses.Length)
        {
            throw new InvalidDataException("Effect68 mutable source publication requires both physical owners atomically.");
        }
        if (present == 0)
        {
            foreach (uint address in MutableMeshAddresses)
            {
                ImmutableArray<byte> bytes = resources.ReadMesh(address).Bytes;
                scan = scan with { MutableMeshes = scan.MutableMeshes.Add(address, new(bytes, Enumerable.Repeat(true, bytes.Length).ToImmutableArray())) };
            }
        }
        return (this with { MeshSourcesBound = true }, memory with { Scan = scan });
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect68State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect68State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect68 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect68State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect68State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect68 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect68 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        ValidateStorage(Core, BaseAddress, CoreRanges);
        ValidateStorage(Header, HeaderBase, HeaderRanges);
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect68 requires its five physical inactive bank owners.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect68 is missing a physical inactive bank owner.");
            }
            if (bank is not null)
            {
                ValidateBank(bank, shape.Stride * shape.Capacity);
            }
        }
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
        throw new InvalidDataException("Effect68 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect68 pool index leaves its five original fixed banks.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        bank.ValidateExtent(length);
    }
}
