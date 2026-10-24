using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect66Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect66State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] NativeBattleEffectArena InitializedStorage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleGuardianCameraTask> CameraTasks)
{
    public static NativeBattleEffect66State FromColdProcess() => new(NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)255, InitializedLength).ToArray()), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()), ImmutableDictionary<uint, NativeBattleGuardianCameraTask>.Empty);

    public static NativeBattleEffect66State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(checked((int)(address - BaseAddress)), length);
        }
        return new(storage, NativeBattleEffectArena.Clear().MarkUnknown(0, InitializedLength), Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))), ImmutableDictionary<uint, NativeBattleGuardianCameraTask>.Empty);
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect66State Write(uint address, ReadOnlySpan<byte> bytes) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    public NativeBattleEffect66State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect66State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect66State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        bool initialized = address >= InitializedBase && address <= InitializedBase + InitializedLength;
        int offset = initialized ? RequireInitialized(address, length) : Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(initialized ? InitializedStorage : Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect66State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        bool initialized = address >= InitializedBase && address <= InitializedBase + InitializedLength;
        int offset = initialized ? RequireInitialized(address, length) : Require(address, length);
        NativeBattleEffectArena current = NativeBattleEffectNodeContextMemory.Copy(source, 0, initialized ? InitializedStorage : Storage, offset, length);
        return initialized ? this with { InitializedStorage = current } : this with { Storage = current };
    }

    private static int RequireInitialized(uint address, int length)
    {
        if (length < 0 || address < InitializedBase || (ulong)address + (uint)length > InitializedBase + InitializedLength)
        {
            throw new InvalidDataException("Effect66 initialized pointer access leaves its current physical owner.");
        }
        return checked((int)(address - InitializedBase));
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect66State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect66State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect66 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect66State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect66State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect66 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect66 cannot duplicate an already retained physical bank.");
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
        ValidateBank(InitializedStorage, InitializedLength);
        if (FixedBanks is null || FixedBanks.Count != Pools.Length || CameraTasks is null)
        {
            throw new InvalidDataException("Effect66 requires its exact physical banks and retained camera tasks.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect66 is missing a physical bank owner.");
            }
            if (bank is not null)
            {
                ValidateBank(bank, shape.Stride * shape.Capacity);
            }
        }
        foreach ((uint address, NativeBattleGuardianCameraTask camera) in CameraTasks)
        {
            if (address < ChildBank || address >= ChildBank + ChildStride * ChildCount || (address - ChildBank) % ChildStride != 0 || camera is null || camera.Program.IsDefault || camera.Program.Length != CameraLength || camera.Offset < 0 || camera.Offset > camera.Program.Length)
            {
                throw new InvalidDataException("Effect66 camera task leaves its real child slot or current program.");
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
        throw new InvalidDataException("Effect66 storage excludes fixed pools, descriptors, shared matrices, COMMON and unobserved gaps.");
    }

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect66 pool index leaves its two original fixed banks.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect66 retained bytes leave their physical bank allocation.");
        }
    }
}
