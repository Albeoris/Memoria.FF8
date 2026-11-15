using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Authenticated party CPU allocation; saved bounds are derived from the image identity.</summary>
public sealed record NativeBattlePartyCpuLayout([property: JsonRequired] string ExecutableHash)
{
    public static NativeBattlePartyCpuLayout Classic { get; } = new(NativeFieldRuntimeProfile.FF8EnExecutableHash);
    public static NativeBattlePartyCpuLayout Remastered { get; } = new(NativeFieldRuntimeProfile.RemasteredEnImageHash);

    public static NativeBattlePartyCpuLayout ForImage(string executableHash)
    {
        NativeBattlePartyCpuLayout layout = new(executableHash);
        layout.Validate();
        return layout;
    }

    public void Validate()
    {
        if (!NativeFieldRuntimeProfile.IsSupportedImage(ExecutableHash))
        {
            throw new InvalidDataException("Native party CPU allocation requires an authenticated supported image.");
        }
    }

    [JsonIgnore]
    public uint BaseAddress => ExecutableHash switch
    {
        NativeFieldRuntimeProfile.FF8EnExecutableHash => 0x0196B018u,
        NativeFieldRuntimeProfile.RemasteredEnImageHash => 0x0A100000u,
        _ => throw new InvalidDataException("Unknown native party CPU allocation.")
    };

    [JsonIgnore]
    public int BankLength => ExecutableHash switch
    {
        NativeFieldRuntimeProfile.FF8EnExecutableHash => 0x10000,
        NativeFieldRuntimeProfile.RemasteredEnImageHash => 0x20000,
        _ => throw new InvalidDataException("Unknown native party CPU allocation.")
    };
}

public sealed partial record NativeBattleModelCpuReference(int Bank, int Offset)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePartyCpuLayout? Layout { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ResourceOnly { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EnemyResourceId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ResourceOffset { get; init; }

    [JsonIgnore]
    public bool IsOwnedResource => Bank is >= 0 and < 3 || EnemyResourceId is not null || IsPresentationResource;

    [JsonIgnore]
    internal bool IsPresentationResource => NativeBattlePresentationCpuBanks.IsExtension(Bank) && ResourceOnly && EnemyResourceId is null && Offset >= 0;

    [JsonIgnore]
    public int ResourceStorageOffset => EnemyResourceId is not null ? ResourceOffset : Offset;

    public NativeBattleModelCpuReference Advance(int relativeOffset)
    {
        return this with { Offset = checked(Offset + relativeOffset), ResourceOffset = EnemyResourceId is not null ? checked(ResourceOffset + relativeOffset) : ResourceOffset };
    }

    [JsonIgnore]
    public NativeBattlePartyCpuLayout EffectiveLayout => Layout ?? NativeBattlePartyCpuLayout.Classic;

    [JsonIgnore]
    public uint PhysicalAddress
    {
        get => TryPhysicalAddress() ?? throw new NotSupportedException("This model section is resource-owned and requires a section view rather than a numeric guest address.");
    }

    public uint? TryPhysicalAddress()
    {
        NativeBattlePartyCpuLayout layout = EffectiveLayout;
        layout.Validate();
        if (Bank < 0 || ResourceOnly || EnemyResourceId is not null || NativeBattlePresentationCpuBanks.IsExtension(Bank))
        {
            return null;
        }
        return Bank < 3 ? checked(layout.BaseAddress + (uint)layout.BankLength * (uint)Bank + (uint)Offset) : SharedAddress;
    }
}

/// <summary>One bounded sparse CPU bank. Unwritten heap bytes retain their unknown owner.</summary>
public sealed record NativeBattleModelCpuBank(ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges)
{
    private readonly NativeBattleModelResourceStorage storage = NativeBattleModelResourceStorage.FromKnownRanges(KnownRanges);
    private readonly NativeBattleModelResourceStorage? resourceStorage;

    /// <summary>Compatible diagnostic materialization; ordinary resource reads do not construct or retain this image.</summary>
    public ImmutableArray<NativeBattleEffectMemoryRegion> KnownRanges
    {
        get => storage.CaptureKnownRanges(Capacity);
        init => storage = NativeBattleModelResourceStorage.FromKnownRanges(value);
    }

    /// <summary>Exact logical resource contents when they exceed the independently bounded guest window.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableArray<NativeBattleEffectMemoryRegion>? ResourceRanges
    {
        get => resourceStorage?.CaptureKnownRanges(ResourceCapacity);
        init => resourceStorage = value is { } ranges ? NativeBattleModelResourceStorage.FromKnownRanges(ranges) : null;
    }

    public const int Length = 0x10000;
    public int Capacity { get; init; } = Length;
    internal bool HasResourceStorage => resourceStorage is not null;
    internal int ResourceCapacity => resourceStorage is { Segments.IsDefaultOrEmpty: false } logical ? Math.Max(Capacity, checked(logical.Segments[^1].Offset + logical.Segments[^1].Length)) : Capacity;
    internal ImmutableArray<NativeBattleModelResourceSegment> ResourceSegments => resourceStorage?.Segments ?? storage.Segments;
    internal ImmutableArray<NativeBattleModelResourceSegment> GuestSegments => storage.Segments;
    public static NativeBattleModelCpuBank Unknown(int capacity = Length) => new([]) { Capacity = capacity };

    // Diagnostic restoration may coalesce slice boundaries; immutable equivalence follows bytes and knownness, never a host address.
    internal static bool SamePublication(NativeBattleModelCpuBank? first, NativeBattleModelCpuBank? second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }
        if (first is null || second is null || first.Capacity != second.Capacity || first.HasResourceStorage != second.HasResourceStorage)
        {
            return false;
        }
        return Equal(first.GuestSegments, second.GuestSegments) && (!first.HasResourceStorage || Equal(first.ResourceSegments, second.ResourceSegments));

        static bool Equal(ImmutableArray<NativeBattleModelResourceSegment> left, ImmutableArray<NativeBattleModelResourceSegment> right)
        {
            int a = 0;
            int b = 0;
            int aOffset = 0;
            int bOffset = 0;
            while (a < left.Length && b < right.Length)
            {
                NativeBattleModelResourceSegment x = left[a];
                NativeBattleModelResourceSegment y = right[b];
                if (x.Offset + aOffset != y.Offset + bOffset)
                {
                    return false;
                }
                int count = Math.Min(x.Length - aOffset, y.Length - bOffset);
                if (!x.Source.AsSpan(x.SourceOffset + aOffset, count).SequenceEqual(y.Source.AsSpan(y.SourceOffset + bOffset, count)))
                {
                    return false;
                }
                aOffset += count;
                bOffset += count;
                if (aOffset == x.Length)
                {
                    a++;
                    aOffset = 0;
                }
                if (bOffset == y.Length)
                {
                    b++;
                    bOffset = 0;
                }
            }
            return a == left.Length && b == right.Length;
        }
    }

    public byte[] Read(int offset, int length)
    {
        return (resourceStorage ?? storage).Read(offset, length, ResourceCapacity);
    }

    internal byte[] ReadGuest(int offset, int length)
    {
        return storage.Read(offset, length, Capacity);
    }

    public NativeBattleModelCpuBank Write(int offset, ReadOnlySpan<byte> bytes)
    {
        NativeBattleModelResourceStorage current = resourceStorage ?? storage;
        NativeBattleModelResourceStorage updated = current.Write(offset, bytes, ResourceCapacity);
        return ReferenceEquals(updated, current) ? this : resourceStorage is null ? new(updated, Capacity) : new(storage, Capacity, updated);
    }

    internal NativeBattleModelCpuBank WriteGuest(int offset, ReadOnlySpan<byte> bytes)
    {
        NativeBattleModelResourceStorage updated = storage.Write(offset, bytes, Capacity);
        return ReferenceEquals(updated, storage) ? this : new(updated, Capacity, resourceStorage);
    }

    internal NativeBattleModelCpuBank BindResource(int offset, ImmutableArray<byte> source, int sourceOffset, int length)
    {
        NativeBattleModelResourceStorage updated = storage.Bind(offset, source, sourceOffset, length, Capacity);
        return ReferenceEquals(updated, storage) ? this : new(updated, Capacity);
    }

    internal NativeBattleModelCpuBank PromoteResourceStorage()
    {
        return resourceStorage is null ? new(storage, Capacity, storage) : this;
    }

    internal NativeBattleModelCpuBank BindPartyResource(int offset, ImmutableArray<byte> source, int sourceOffset, int length)
    {
        int extent = checked(offset + length);
        if (extent <= Capacity && resourceStorage is null)
        {
            return new(storage.Bind(offset, source, sourceOffset, length, Capacity), Capacity);
        }
        NativeBattleModelResourceStorage current = resourceStorage ?? storage;
        return new(storage, Capacity, current.Bind(offset, source, sourceOffset, length, Math.Max(ResourceCapacity, extent)));
    }

    private NativeBattleModelCpuBank(NativeBattleModelResourceStorage storage, int capacity, NativeBattleModelResourceStorage? resourceStorage = null) : this([])
    {
        this.storage = storage;
        this.resourceStorage = resourceStorage;
        Capacity = capacity;
    }
}

public sealed record NativeBattleModelCpuBanks(ImmutableArray<NativeBattleModelCpuBank> Banks)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattlePartyCpuLayout? Layout { get; init; }

    [JsonIgnore]
    public BattleModelRoster? PresentationRoster { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ImmutableDictionary<int, NativeBattleModelCpuBank>? PresentationBanks { get; init; }

    [JsonIgnore]
    public ImmutableDictionary<BattlePartyModelOwner, int> PartyBankOwners { get; init; } = ImmutableDictionary<BattlePartyModelOwner, int>.Empty;

    [JsonIgnore]
    public NativeBattlePartyCpuLayout EffectiveLayout => Layout ?? NativeBattlePartyCpuLayout.Classic;

    public static NativeBattleModelCpuBanks Unknown(NativeBattlePartyCpuLayout? layout = null)
    {
        NativeBattlePartyCpuLayout effective = layout ?? NativeBattlePartyCpuLayout.Classic;
        effective.Validate();
        return new([NativeBattleModelCpuBank.Unknown(effective.BankLength), NativeBattleModelCpuBank.Unknown(effective.BankLength), NativeBattleModelCpuBank.Unknown(effective.BankLength)]) { Layout = layout };
    }

    public void Validate()
    {
        NativeBattlePartyCpuLayout layout = EffectiveLayout;
        layout.Validate();
        if (Banks.IsDefault || Banks.Length != 3 || Banks.Any(bank => bank is null || bank.Capacity != layout.BankLength))
        {
            throw new InvalidDataException("Native party CPU banks disagree with their authenticated allocation.");
        }
        foreach (NativeBattleModelCpuBank bank in Banks)
        {
            _ = bank.Read(0, 0);
        }
        if (PartyBankOwners.Values.Distinct().Count() != PartyBankOwners.Count ||
            PartyBankOwners.Any(pair => pair.Value < 0 || pair.Value is 3 or 4 ||
                NativeBattlePresentationCpuBanks.IsExtension(pair.Value) && PresentationBanks?.ContainsKey(pair.Value) != true))
        {
            throw new InvalidDataException("Party CPU bank bindings require distinct owned resource IDs.");
        }
        if (PresentationBanks is { } banks)
        {
            foreach ((int bankId, NativeBattleModelCpuBank bank) in banks)
            {
                ValidatePresentationBank(bankId, bank);
                _ = bank.Read(0, 0);
            }
        }
    }

    public NativeBattleModelCpuBanks WithPresentationRoster(BattleModelRoster roster, NativeBattleModelAliases aliases)
    {
        ArgumentNullException.ThrowIfNull(roster);
        NativeBattlePartyLoadPlan.RequireMatchingRoster(roster, aliases.Roster);
        Validate();
        if (PresentationRoster is { } existing)
        {
            NativeBattlePartyLoadPlan.RequireMatchingRoster(existing, roster);
        }
        NativeBattleModelCpuBanks result = this with { PresentationRoster = roster };
        foreach (BattlePartyModelOwner owner in roster.Party)
        {
            NativeBattleModelSlot? slot = aliases.TryEncodeExact(owner, out byte operand) ? NativeBattleModelSlot.Decode(operand) : null;
            result = result.BindPartyOwner(owner, slot);
        }
        return result;
    }

    internal NativeBattleModelCpuBanks RebindPresentationEnemies(BattleModelRoster expected, BattleModelRoster replacement, IEnumerable<BattleEnemyModelOwner> retired, NativeBattleModelAliases replacementAliases)
    {
        ArgumentNullException.ThrowIfNull(replacementAliases);
        Validate();
        _ = ValidatePresentationEnemyRebind(PresentationRoster, expected, replacement, retired);
        NativeBattlePartyLoadPlan.RequireMatchingRoster(replacement, replacementAliases.Roster);
        foreach (BattlePartyModelOwner owner in replacement.Party)
        {
            int existing = PartyBankIndex(owner);
            if (replacementAliases.TryEncodeExact(owner, out byte operand) ? existing != operand : !NativeBattlePresentationCpuBanks.IsExtension(existing))
            {
                throw new InvalidDataException("An enemy roster rebind cannot change a party CPU bank placement.");
            }
        }
        return this with { PresentationRoster = replacement };
    }

    internal static HashSet<BattleEnemyModelOwner> ValidatePresentationEnemyRebind(BattleModelRoster? current, BattleModelRoster expected, BattleModelRoster replacement, IEnumerable<BattleEnemyModelOwner> retired)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(replacement);
        ArgumentNullException.ThrowIfNull(retired);
        NativeBattlePartyLoadPlan.RequireMatchingRoster(current, expected);
        if (!expected.Party.SequenceEqual(replacement.Party))
        {
            throw new InvalidDataException("An enemy roster rebind must preserve the ordered party owners.");
        }
        HashSet<BattleEnemyModelOwner> retiredOwners = retired.ToHashSet();
        if (retiredOwners.Any(owner => owner is null || !expected.Contains(owner) || replacement.Contains(owner)) ||
            expected.Enemies.Any(owner => !replacement.Contains(owner) && !retiredOwners.Contains(owner)))
        {
            throw new InvalidDataException("An enemy roster rebind requires exactly the removed enemy owners.");
        }
        return retiredOwners;
    }

    internal NativeBattleModelCpuBanks BindPartyOwner(BattlePartyModelOwner owner, NativeBattleModelSlot? slot)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (slot is { Side: not BattleSide.Allies })
        {
            throw new InvalidDataException("A party CPU owner cannot bind an enemy native slot.");
        }
        if (PartyBankOwners.TryGetValue(owner, out int existing))
        {
            if (slot is { } original ? existing != original.Offset : !NativeBattlePresentationCpuBanks.IsExtension(existing))
            {
                throw new InvalidDataException("A party CPU owner cannot change its resource placement.");
            }
            return this;
        }
        if (slot is { } bound)
        {
            ImmutableDictionary<BattlePartyModelOwner, int> owners = PartyBankOwners.RemoveRange(PartyBankOwners.Where(pair => pair.Value == bound.Offset).Select(pair => pair.Key));
            return this with { PartyBankOwners = owners.Add(owner, bound.Offset) };
        }
        ImmutableDictionary<int, NativeBattleModelCpuBank> banks = PresentationBanks ?? ImmutableDictionary<int, NativeBattleModelCpuBank>.Empty;
        int bank = banks.IsEmpty ? NativeBattlePresentationCpuBanks.FirstResourceBank : checked(banks.Keys.Max() + 1);
        NativeBattleModelCpuBank storage = NativeBattleModelCpuBank.Unknown(EffectiveLayout.BankLength).PromoteResourceStorage();
        return this with { PartyBankOwners = PartyBankOwners.Add(owner, bank), PresentationBanks = banks.Add(bank, storage) };
    }

    public int PartyBankIndex(BattlePartyModelOwner owner)
    {
        return PartyBankOwners.TryGetValue(owner, out int bank) ? bank : throw new InvalidDataException("Party CPU storage requires a bound model owner.");
    }

    public NativeBattleModelCpuBank GetBank(int bankId)
    {
        if ((uint)bankId < NativeBattleSlots.AllyCount && !Banks.IsDefault && bankId < Banks.Length && Banks[bankId] is { } native && native.Capacity == EffectiveLayout.BankLength)
        {
            return native;
        }
        if (PresentationBanks is not { } banks || !banks.TryGetValue(bankId, out NativeBattleModelCpuBank? bank))
        {
            throw new InvalidDataException("The CPU bank has no retained party resource owner.");
        }
        ValidatePresentationBank(bankId, bank);
        return bank!;
    }

    public NativeBattleModelCpuBanks WithBank(int bankId, NativeBattleModelCpuBank bank)
    {
        ArgumentNullException.ThrowIfNull(bank);
        _ = GetBank(bankId);
        if (bank.Capacity != EffectiveLayout.BankLength)
        {
            throw new InvalidDataException("Party CPU bank capacity differs from its authenticated layout.");
        }
        _ = bank.Read(0, 0);
        if ((uint)bankId < NativeBattleSlots.AllyCount)
        {
            return this with { Banks = Banks.SetItem(bankId, bank) };
        }
        ValidatePresentationBank(bankId, bank);
        return this with { PresentationBanks = PresentationBanks!.SetItem(bankId, bank) };
    }

    private void ValidatePresentationBank(int bankId, NativeBattleModelCpuBank? bank)
    {
        if (!NativeBattlePresentationCpuBanks.IsExtension(bankId) || bank is null || bank.Capacity != EffectiveLayout.BankLength || !bank.HasResourceStorage || !bank.GuestSegments.IsEmpty)
        {
            throw new InvalidDataException("Hosted CPU bank requires a bound extension party actor and resource-only storage.");
        }
    }

    public NativeBattleModelCpuBanks CopyBank50B2A0(byte source, byte destination)
    {
        Validate();
        if (source >= 3 || destination >= 3)
        {
            throw new InvalidDataException("Native50B2A0 CPU copy requires its three physical party banks.");
        }
        // memcpy transports unknown retained bytes as unknown too, erasing prior destination knowledge.
        return this with { Banks = Banks.SetItem(destination, Banks[source]) };
    }
}
