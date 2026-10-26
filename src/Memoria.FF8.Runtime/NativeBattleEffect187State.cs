using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect187Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect187State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks)
{
    [JsonRequired]
    public NativeBattleCactuarRawModelState? Model { get; init; }

    [JsonRequired]
    public ImmutableDictionary<uint, NativeBattleGuardianCameraTask> CameraTasks { get; init; } = ImmutableDictionary<uint, NativeBattleGuardianCameraTask>.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CommonPacketStart { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CommonPacketEnd { get; init; }

    public static NativeBattleEffect187State FromColdProcess() => new(NativeBattleEffectArena.Clear().MarkUnknown(Require(0xE41E50, 12), 12).MarkUnknown(Require(0xE41E98, 4), 4).MarkUnknown(Require(0xFA94A8, 0x4000), 0x4000), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear())) { Model = NativeBattleCactuarRawModelState.FromColdProcess() };

    public static NativeBattleEffect187State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(Require(address, length), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))));
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect187State Write(uint address, ReadOnlySpan<byte> bytes) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    public NativeBattleEffect187State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect187State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect187State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        if (InRange(address, length, NativeBattleEffect187Model.Address, 0x90))
        {
            NativeBattleCactuarRawModelState model = Model ?? throw new NotSupportedException("Effect187 lacks its actual retained model owner.");
            return NativeBattleEffectArena.Clear().Write(0, model.Model.AsSpan(checked((int)(address - NativeBattleEffect187Model.Address)), length));
        }
        if (InRange(address, length, NativeBattleEffect187Model.ControlAddress, 0x34))
        {
            NativeBattleCactuarRawModelState model = Model ?? throw new NotSupportedException("Effect187 lacks its actual retained model control owner.");
            return NativeBattleEffectArena.Clear().Write(0, model.Control.AsSpan(checked((int)(address - NativeBattleEffect187Model.ControlAddress)), length));
        }
        int offset = Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect187State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        if (InRange(address, length, NativeBattleEffect187Model.Address, 0x90))
        {
            NativeBattleCactuarRawModelState model = Model ?? throw new NotSupportedException("Effect187 lacks its actual retained model owner.");
            byte[] bytes = model.Model.ToArray();
            source.Read(0, length).CopyTo(bytes, checked((int)(address - NativeBattleEffect187Model.Address)));
            return this with { Model = model with { Model = bytes.ToImmutableArray() } };
        }
        if (InRange(address, length, NativeBattleEffect187Model.ControlAddress, 0x34))
        {
            NativeBattleCactuarRawModelState model = Model ?? throw new NotSupportedException("Effect187 lacks its actual retained model control owner.");
            byte[] bytes = model.Control.ToArray();
            source.Read(0, length).CopyTo(bytes, checked((int)(address - NativeBattleEffect187Model.ControlAddress)));
            return this with { Model = model with { Control = bytes.ToImmutableArray() } };
        }
        int offset = Require(address, length);
        return this with { Storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, offset, length) };
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect187State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect187State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect187 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect187State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect187State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect187 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect187 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        if (CameraTasks is null)
        {
            throw new InvalidDataException("Effect187 requires its explicit retained camera tasks.");
        }
        foreach ((uint address, NativeBattleGuardianCameraTask camera) in CameraTasks)
        {
            if (address < ChildBank || address >= ChildBank + 3600 || (address - ChildBank) % 36 != 0 || camera.Program.IsDefaultOrEmpty || camera.Offset < 0 || camera.Offset > camera.Program.Length)
            {
                throw new InvalidDataException("Effect187 camera state leaves its current task and program owners.");
            }
        }
        if (Model is { } model && (model.Model.IsDefault || model.Model.Length != 0x90 || model.Control.IsDefault || model.Control.Length != 0x34 || model.DecodedPose is { } pose && pose.Bones.IsDefault))
        {
            throw new InvalidDataException("Effect187 retained model/control or pose owner is incomplete.");
        }
        if ((CommonPacketStart is null) != (CommonPacketEnd is null) || CommonPacketStart is < 0 || CommonPacketEnd < CommonPacketStart)
        {
            throw new InvalidDataException("Effect187 COMMON output requires its actual ordered callback interval.");
        }
        Storage.Validate();
        int storageLength = OwnedRanges.Sum(range => range.Length);
        foreach (NativeBattleEffectMemoryRegion region in Storage.Regions)
        {
            if (region.Offset < 0 || region.Offset > storageLength - region.Data.Length)
            {
                throw new InvalidDataException("Effect187 stored bytes leave their mapped physical owners.");
            }
        }
        foreach (NativeBattleEffectUnknownRegion region in Storage.UnknownRegions)
        {
            if (region.Offset < 0 || region.Offset > storageLength - region.Length)
            {
                throw new InvalidDataException("Effect187 unknown bytes leave their mapped physical owners.");
            }
        }
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect187 requires its exact physical banks.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect187 is missing a physical bank owner.");
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
            ulong end = (ulong)address + (uint)length;
            int storageOffset = 0;
            foreach ((uint start, int size) in OwnedRanges)
            {
                if (address >= start && end <= (ulong)start + (uint)size)
                {
                    return checked(storageOffset + (int)(address - start));
                }
                storageOffset = checked(storageOffset + size);
            }
        }
        throw new InvalidDataException("Effect187 storage excludes fixed pools, descriptors, model owners, COMMON and unobserved gaps.");
    }

    private static bool InRange(uint address, int length, uint start, int size) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size;

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect187 pool index leaves its original fixed bank.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect187 retained bytes leave their physical bank allocation.");
        }
    }
}
