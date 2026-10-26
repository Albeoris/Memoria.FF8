using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json.Serialization;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleEffect326State([property: JsonRequired] NativeBattleEffectArena Storage, [property: JsonRequired] ImmutableDictionary<uint, NativeBattleEffectArena?> FixedBanks)
{
    [JsonRequired]
    public ImmutableDictionary<uint, NativeBattleCactuarRawModelState?> Models { get; init; } = ImmutableDictionary<uint, NativeBattleCactuarRawModelState?>.Empty;

    [JsonRequired]
    public bool ColdSourceOrigin { get; init; }

    [JsonRequired]
    public bool StaticSourcesBound { get; init; }

    [JsonRequired]
    public ImmutableDictionary<uint, NativeBattleGuardianCameraTask> CameraTasks { get; init; } = ImmutableDictionary<uint, NativeBattleGuardianCameraTask>.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CommonPacketStart { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CommonPacketEnd { get; init; }

    public static NativeBattleEffect326State FromColdProcess() => new(NativeBattleEffectArena.Clear().MarkUnknown(Require(0xE19668, 12), 12).MarkUnknown(Require(0xE196B0, 4), 4), Pools.ToImmutableDictionary(pool => pool.Bank, _ => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear()))
    {
        Models = ModelBindings.ToImmutableDictionary(binding => binding.Model, _ => (NativeBattleCactuarRawModelState?)NativeBattleCactuarRawModelState.FromColdProcess()),
        ColdSourceOrigin = true
    };

    public static NativeBattleEffect326State FromUnknown()
    {
        NativeBattleEffectArena storage = NativeBattleEffectArena.Clear();
        foreach ((uint address, int length) in OwnedRanges)
        {
            storage = storage.MarkUnknown(Require(address, length), length);
        }
        return new(storage, Pools.ToImmutableDictionary(pool => pool.Bank, pool => (NativeBattleEffectArena?)NativeBattleEffectArena.Clear().MarkUnknown(0, checked(pool.Stride * pool.Capacity))))
        {
            Models = ModelBindings.ToImmutableDictionary(binding => binding.Model, _ => (NativeBattleCactuarRawModelState?)null)
        };
    }

    public (NativeBattleEffect326State State, NativeBattleFileStaging Staging) BindSources(NativeBattleFileStaging staging, NativeBattleEffect326Resources resources)
    {
        if (StaticSourcesBound)
        {
            _ = staging.Read(StreamAddress, 4);
            _ = staging.Read(PixelSource, 24);
            _ = staging.Read(EmbeddedModelAddress, 24);
            return (this, staging);
        }
        if (!ColdSourceOrigin)
        {
            throw new NotSupportedException("Effect326 cannot replace mutable source owners from unknown process history.");
        }
        (uint Address, int Length)[] sources = [(StreamAddress, 4), (PixelSource, 0x4000), (EmbeddedModelAddress, EmbeddedModelLength)];
        int present = sources.Count(source => staging.ReadSnapshot(source.Address, source.Length).Known.Any(value => value));
        if (present != 0 && present != sources.Length)
        {
            throw new InvalidDataException("Effect326 source binding requires its three physical source owners atomically.");
        }
        if (present == 0)
        {
            staging = staging.Write(StreamAddress, BitConverter.GetBytes(resources.StreamNumber)).Write(PixelSource, resources.ScrollPixels.AsSpan()).Write(EmbeddedModelAddress, resources.EmbeddedModel.Bytes.AsSpan());
        }
        else
        {
            _ = staging.Read(StreamAddress, 4);
            _ = staging.Read(PixelSource, 24);
            _ = staging.Read(EmbeddedModelAddress, 24);
        }
        return (this with { StaticSourcesBound = true }, staging);
    }

    public byte[] Read(uint address, int length) => ReadView(address, length).Read(0, length);
    public byte[] ReadBytes(uint address, int length) => Read(address, length);
    public ushort ReadWord(uint address) => BinaryPrimitives.ReadUInt16LittleEndian(Read(address, 2));
    public uint ReadDword(uint address) => BinaryPrimitives.ReadUInt32LittleEndian(Read(address, 4));
    public NativeBattleEffect326State Write(uint address, ReadOnlySpan<byte> bytes) => WriteView(address, NativeBattleEffectArena.Clear().Write(0, bytes), bytes.Length);
    public NativeBattleEffect326State WriteBytes(uint address, ReadOnlySpan<byte> bytes) => Write(address, bytes);
    public NativeBattleEffect326State WriteWord(uint address, ushort value) => Write(address, BitConverter.GetBytes(value));
    public NativeBattleEffect326State WriteDword(uint address, uint value) => Write(address, BitConverter.GetBytes(value));

    public NativeBattleEffectArena ReadView(uint address, int length)
    {
        foreach ((uint modelAddress, uint controlAddress) in ModelBindings)
        {
            bool control = InRange(address, length, controlAddress, 0x34);
            if (control || InRange(address, length, modelAddress, 0x90))
            {
                NativeBattleCactuarRawModelState model = Models.TryGetValue(modelAddress, out NativeBattleCactuarRawModelState? current) && current is not null ? current : throw new NotSupportedException("Effect326 lacks its current raw model owner.");
                ImmutableArray<byte> bytes = control ? model.Control : model.Model;
                return NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan(checked((int)(address - (control ? controlAddress : modelAddress))), length));
            }
        }
        int offset = Require(address, length);
        return NativeBattleEffectNodeContextMemory.Copy(Storage, offset, NativeBattleEffectArena.Clear().MarkUnknown(0, length), 0, length);
    }

    public NativeBattleEffect326State WriteView(uint address, NativeBattleEffectArena source, int length)
    {
        foreach ((uint modelAddress, uint controlAddress) in ModelBindings)
        {
            bool control = InRange(address, length, controlAddress, 0x34);
            if (control || InRange(address, length, modelAddress, 0x90))
            {
                NativeBattleCactuarRawModelState model = Models.TryGetValue(modelAddress, out NativeBattleCactuarRawModelState? current) && current is not null ? current : throw new NotSupportedException("Effect326 lacks its current raw model owner.");
                byte[] bytes = (control ? model.Control : model.Model).ToArray();
                source.Read(0, length).CopyTo(bytes, checked((int)(address - (control ? controlAddress : modelAddress))));
                return this with { Models = Models.SetItem(modelAddress, control ? model with { Control = bytes.ToImmutableArray() } : model with { Model = bytes.ToImmutableArray() }) };
            }
        }
        int offset = Require(address, length);
        return this with { Storage = NativeBattleEffectNodeContextMemory.Copy(source, 0, Storage, offset, length) };
    }

    public NativeBattleEffectArena ReadBank(int index) => ReadBank(BankAddress(index));
    public NativeBattleEffect326State DetachBank(int index) => DetachBank(BankAddress(index));
    public NativeBattleEffect326State AttachBank(int index, NativeBattleEffectArena bank) => AttachBank(BankAddress(index), bank);

    public NativeBattleEffectArena ReadBank(uint address) => FixedBanks.TryGetValue(address, out NativeBattleEffectArena? bank) && bank is not null ? bank : throw new InvalidDataException("Effect326 inactive bank is missing or owned exclusively by the hierarchy.");

    public NativeBattleEffect326State DetachBank(uint address)
    {
        _ = ReadBank(address);
        return this with { FixedBanks = FixedBanks.SetItem(address, null) };
    }

    public NativeBattleEffect326State AttachBank(uint address, NativeBattleEffectArena bank)
    {
        NativeBattleCureFamilyPool shape = Pools.SingleOrDefault(pool => pool.Bank == address) ?? throw new InvalidDataException("Effect326 bank address leaves its original physical pools.");
        if (!FixedBanks.TryGetValue(address, out NativeBattleEffectArena? current) || current is not null)
        {
            throw new InvalidDataException("Effect326 cannot duplicate an already retained physical bank.");
        }
        ValidateBank(bank, shape.Stride * shape.Capacity);
        return this with { FixedBanks = FixedBanks.SetItem(address, bank) };
    }

    public void Validate()
    {
        if (CameraTasks is null)
        {
            throw new InvalidDataException("Effect326 requires its explicit retained camera tasks.");
        }
        foreach ((uint address, NativeBattleGuardianCameraTask camera) in CameraTasks)
        {
            if (address < ChildBank || address >= ChildBank + 3600 || (address - ChildBank) % 36 != 0 || camera.Program.IsDefaultOrEmpty || camera.Offset < 0 || camera.Offset > camera.Program.Length)
            {
                throw new InvalidDataException("Effect326 camera state leaves its current task and program owners.");
            }
        }
        if (Models is null || Models.Count != ModelBindings.Length || ModelBindings.Any(binding => !Models.ContainsKey(binding.Model)))
        {
            throw new InvalidDataException("Effect326 requires its three distinct retained raw model owners.");
        }
        foreach (NativeBattleCactuarRawModelState? model in Models.Values)
        {
            if (model is not null && (model.Model.IsDefault || model.Model.Length != 0x90 || model.Control.IsDefault || model.Control.Length != 0x34 || model.DecodedPose is { } pose && pose.Bones.IsDefault))
            {
                throw new InvalidDataException("Effect326 retained model/control or decoded pose owner is incomplete.");
            }
        }
        if ((CommonPacketStart is null) != (CommonPacketEnd is null) || CommonPacketStart is < 0 || CommonPacketEnd < CommonPacketStart)
        {
            throw new InvalidDataException("Effect326 COMMON output requires its actual ordered callback interval.");
        }
        Storage.Validate();
        int storageLength = OwnedRanges.Sum(range => range.Length);
        foreach (NativeBattleEffectMemoryRegion region in Storage.Regions)
        {
            if (region.Offset < 0 || region.Offset > storageLength - region.Data.Length)
            {
                throw new InvalidDataException("Effect326 stored bytes leave their mapped physical owners.");
            }
        }
        foreach (NativeBattleEffectUnknownRegion region in Storage.UnknownRegions)
        {
            if (region.Offset < 0 || region.Offset > storageLength - region.Length)
            {
                throw new InvalidDataException("Effect326 unknown bytes leave their mapped physical owners.");
            }
        }
        if (FixedBanks is null || FixedBanks.Count != Pools.Length)
        {
            throw new InvalidDataException("Effect326 requires its exact physical banks.");
        }
        foreach (NativeBattleCureFamilyPool shape in Pools)
        {
            if (!FixedBanks.TryGetValue(shape.Bank, out NativeBattleEffectArena? bank))
            {
                throw new InvalidDataException("Effect326 is missing a physical bank owner.");
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
        throw new InvalidDataException("Effect326 storage excludes fixed pools, descriptors, model owners, COMMON and unobserved gaps.");
    }

    private static bool InRange(uint address, int length, uint start, int size) => length >= 0 && address >= start && (ulong)address + (uint)length <= (ulong)start + (uint)size;

    private static uint BankAddress(int index)
    {
        if ((uint)index >= Pools.Length)
        {
            throw new InvalidDataException("Effect326 pool index leaves its original fixed bank.");
        }
        return Pools[index].Bank;
    }

    private static void ValidateBank(NativeBattleEffectArena bank, int length)
    {
        bank.Validate();
        if (bank.Regions.Any(region => region.Offset < 0 || region.Offset > length - region.Data.Length) || bank.UnknownRegions.Any(region => region.Offset < 0 || region.Offset > length - region.Length))
        {
            throw new InvalidDataException("Effect326 retained bytes leave their physical bank allocation.");
        }
    }
}
