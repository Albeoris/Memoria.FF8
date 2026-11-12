using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF7.Formats;

namespace Memoria.FF7.Runtime;

public sealed record Ff7BattleGuestProjection(ushort MaximumHp, byte Level, byte Luck, ushort Defense, ushort MagicDefense)
{
    public byte? Dexterity { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public byte? Evasion { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<int>? OriginElementDefenses { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public ImmutableArray<byte>? OriginStatusResistances { get; init; }

    internal void Validate()
    {
        if (MaximumHp is 0 or > 9999 || Level > 99 || Defense > 999 || MagicDefense > 999 || OriginElementDefenses is { } elements && (elements.IsDefault || elements.Length != 8 || elements.Any(value => value is < 800 or > 1000)) || OriginStatusResistances is { } statuses && (statuses.IsDefault || statuses.Length != 40 || statuses.Any(value => value is < 100 or > 200)))
        {
            throw new InvalidDataException("The current guest stat projection exceeds its admitted native caps.");
        }
    }

    internal static Ff7BattleGuestProjection From(Ff7BattleGuestVitalsBinding source) => new(source.MaximumHp, source.Level, source.Luck, source.Defense, source.MagicDefense);

    public bool Equals(Ff7BattleGuestProjection? other)
        => ReferenceEquals(this, other) || other is not null && MaximumHp == other.MaximumHp && Level == other.Level && Luck == other.Luck && Defense == other.Defense && MagicDefense == other.MagicDefense && Dexterity == other.Dexterity && Evasion == other.Evasion && SameValues(OriginElementDefenses, other.OriginElementDefenses) && SameValues(OriginStatusResistances, other.OriginStatusResistances);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(MaximumHp);
        hash.Add(Level);
        hash.Add(Luck);
        hash.Add(Defense);
        hash.Add(MagicDefense);
        hash.Add(Dexterity);
        hash.Add(Evasion);
        AddValues(ref hash, OriginElementDefenses);
        AddValues(ref hash, OriginStatusResistances);
        return hash.ToHashCode();
    }

    private static bool SameValues<T>(ImmutableArray<T>? left, ImmutableArray<T>? right) where T : IEquatable<T>
        => left.HasValue == right.HasValue && left.GetValueOrDefault().AsSpan().SequenceEqual(right.GetValueOrDefault().AsSpan());

    private static void AddValues<T>(ref HashCode hash, ImmutableArray<T>? values)
    {
        hash.Add(values.HasValue);
        foreach (T value in values.GetValueOrDefault().AsSpan())
        {
            hash.Add(value);
        }
    }
}

public sealed record Ff7BattleGuestVitalsBinding(Guid EncounterId, string SourcePayloadSha256, int HostSlot, int StoryCharacter, int OriginCharacter, int OriginRow, ushort InitialHp, ushort MaximumHp, byte Level, byte Luck, ushort Defense, ushort MagicDefense)
{
    public ushort OriginStatuses { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public uint OriginBattleStatuses { get; init; }
    public string OriginName { get; init; } = "Squall";

    internal void Validate()
    {
        if (EncounterId == Guid.Empty || SourcePayloadSha256 is not { Length: 64 } || !SourcePayloadSha256.All(Uri.IsHexDigit) || HostSlot is < 0 or > 2 || StoryCharacter is < 0 or > 8 || OriginCharacter is < 0 or > 10 || OriginRow is < 0 or > 10 || OriginRow >= 8 && OriginRow != OriginCharacter || InitialHp > MaximumHp || MaximumHp == 0 || MaximumHp > 9999 || Level > 99 || string.IsNullOrWhiteSpace(OriginName))
        {
            throw new InvalidDataException("Guest vitals lack their original save and host slot binding.");
        }
    }
}

public readonly record struct Ff7BattleGuestSlotState(Ff7BattleGuestVitalsBinding Binding, Ff7BattleGuestProjection? Projection = null, byte? BaseDexterity = null, long? LimitReadyAt = null);

public sealed partial class Ff7BattleSession
{
    public Ff7BattleGuestVitalsBinding? GuestVitals => state.GuestVitals;
    public Ff7BattleGuestProjection? GuestProjection => GuestVitals is { } binding ? GetGuestProjection(binding.HostSlot) : null;
    public IReadOnlyList<Ff7BattleGuestVitalsBinding> GuestVitalsBindings => state.GetGuestSlotStates().Select(guest => guest.Binding).ToArray();

    public Ff7BattleGuestProjection? GetGuestProjection(int slot)
    {
        return GetGuestSlotState(slot) is { } guest ? guest.Projection ?? Ff7BattleGuestProjection.From(guest.Binding) : null;
    }

    public long? GetGuestLimitReadyAt(int slot) => GetGuestSlotState(slot)?.LimitReadyAt;

    public void ReprojectGuestVitals(Ff7BattleGuestProjection projection)
        => ReprojectGuestVitals((GuestVitals ?? throw new InvalidOperationException("A guest stat update requires an explicit source slot.")).HostSlot, projection);

    public void ReprojectGuestVitals(int slot, Ff7BattleGuestProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        projection.Validate();
        Ff7BattleGuestSlotState guest = GetGuestSlotState(slot) ?? throw new InvalidOperationException("A guest stat update requires its immutable source binding.");
        Ff7BattleActorState actor = Actor(slot);
        Ff7BattleAiActor current = actor.Actor with { HitPoints = Math.Min(actor.Actor.HitPoints, projection.MaximumHp), MaxHitPoints = projection.MaximumHp, Defense = projection.Defense, MagicDefense = projection.MagicDefense };
        Replace(actor with { Actor = current });
        SetGuestSlotState(guest with { Projection = projection });
    }

    public void BindGuestSpeed(byte initialDexterity)
        => BindGuestSpeed((GuestVitals ?? throw new InvalidOperationException("Guest speed requires an explicit source slot.")).HostSlot, initialDexterity);

    public void BindGuestSpeed(int slot, byte initialDexterity)
    {
        Ff7BattleGuestSlotState guest = GetGuestSlotState(slot) ?? throw new InvalidOperationException("Guest speed requires its source identity binding.");
        if (guest.BaseDexterity is byte saved && saved != initialDexterity)
        {
            throw new InvalidDataException("Guest speed disagrees with its original encounter projection.");
        }
        SetGuestSlotState(guest with { BaseDexterity = initialDexterity });
        RefreshGuestSpeedDenominator();
    }

    private void RefreshGuestSpeedDenominator()
    {
        Span<byte> dexterity = stackalloc byte[3];
        int count = 0;
        for (int index = 0; index < characters.Length; index++)
        {
            if (characters[index] is { } character)
            {
                dexterity[count++] = GetGuestSlotState(index)?.BaseDexterity ?? character.NativeRecord[6];
            }
        }
        speedDenominator = Ff7NativeAtb.SpeedDenominator(dexterity[..count]);
    }

    public void BindGuestVitals(Ff7BattleGuestVitalsBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        if (GetGuestBinding(binding.HostSlot) is { } existing)
        {
            if (existing != binding)
            {
                throw new InvalidDataException("Guest vitals disagree with the saved encounter binding.");
            }
            return;
        }
        if (Tick != 0 || !state.Actors.Any(actor => actor.Actor.Slot == binding.HostSlot && actor.Identity == binding.StoryCharacter) || GetGuestAttack(binding.HostSlot) is { } guest && guest.EncounterId != binding.EncounterId)
        {
            throw new NotSupportedException("The guest vitals profile requires an occupied story slot before the first encounter tick.");
        }
        if (state.GetGuestSlotStates().Any(current => !SharesGuestEncounter(current.Binding, binding) || current.Binding.OriginCharacter == binding.OriginCharacter || current.Binding.OriginRow == binding.OriginRow))
        {
            throw new InvalidDataException("Guest slots disagree about the source encounter or reuse an origin character.");
        }
        Ff7BattleActorState actor = Actor(binding.HostSlot);
        uint statuses = Ff7GuestBattleStatuses.ToHostStatuses(binding.OriginStatuses, binding.OriginBattleStatuses) | (binding.InitialHp == 0 ? 1u : 0u);
        Ff7BattleAiActor projected = actor.Actor with { HitPoints = binding.InitialHp, MaxHitPoints = binding.MaximumHp, MagicPoints = 0, MaxMagicPoints = 0, Statuses = statuses, Defense = binding.Defense, MagicDefense = binding.MagicDefense };
        Replace(actor with { Actor = projected, LimitGauge = 0, AllUses = null, Timers = Ff7NativeBattleStatusClock.Apply(default, 0, statuses) });
        SetGuestSlotState(new(binding));
    }

    private bool IsGuestVitalsSlot(int slot) => GetGuestBinding(slot) is not null;

    private Ff7BattleGuestVitalsBinding? GetGuestBinding(int slot) => GetGuestSlotState(slot)?.Binding;

    private Ff7BattleGuestSlotState? GetGuestSlotState(int slot)
    {
        if (state.GuestSlots is { } guests)
        {
            foreach (Ff7BattleGuestSlotState guest in guests)
            {
                if (guest.Binding.HostSlot == slot)
                {
                    return guest;
                }
            }
        }
        return state.GuestVitals is { } binding && binding.HostSlot == slot ? new(binding, state.GuestProjection, state.GuestBaseDexterity, state.GuestLimitReadyAt) : null;
    }

    private void SetGuestSlotState(Ff7BattleGuestSlotState guest)
    {
        if (state.GuestSlots is null && (state.GuestVitals is null || state.GuestVitals.HostSlot == guest.Binding.HostSlot))
        {
            state = state with { GuestVitals = guest.Binding, GuestProjection = guest.Projection, GuestBaseDexterity = guest.BaseDexterity, GuestLimitReadyAt = guest.LimitReadyAt };
            return;
        }
        IEnumerable<Ff7BattleGuestSlotState> otherGuests = state.GetGuestSlotStates().Where(current => current.Binding.HostSlot != guest.Binding.HostSlot);
        Ff7BattleGuestSlotState[] guests = otherGuests.Append(guest).OrderBy(current => current.Binding.HostSlot).ToArray();
        state = state with { GuestSlots = guests, GuestVitals = null, GuestProjection = null, GuestBaseDexterity = null, GuestLimitReadyAt = null };
    }

    private static bool SharesGuestEncounter(Ff7BattleGuestVitalsBinding first, Ff7BattleGuestVitalsBinding second)
        => first.EncounterId == second.EncounterId && string.Equals(first.SourcePayloadSha256, second.SourcePayloadSha256, StringComparison.OrdinalIgnoreCase);

    private Ff7BattleDamageActor ProjectGuestDefense(Ff7BattleActorState actor, Ff7BattleDamageActor stats)
    {
        return GetGuestProjection(actor.Actor.Slot) is { } projection
            ? stats with { Level = projection.Level, Luck = projection.Luck, Evade = projection.Evasion.GetValueOrDefault(), MagicEvade = 0, DefenseModifier = 0, EvadeModifier = 0, MagicDefenseModifier = 0 }
            : stats;
    }

    private bool GuestRecoveryCommand(Ff7BattleCommand command, ushort argument)
    {
        return command == Ff7BattleCommand.Magic && argument == 0 || command == Ff7BattleCommand.Item && ReadBattleItem(argument) is not null;
    }

    private void ValidateGuestIncoming(Ff7BattleQueuedCommand command, int targetSlot, Ff7BattleAttack? record)
    {
        if (!IsGuestVitalsSlot(targetSlot))
        {
            return;
        }
        bool recovery = GuestRecoveryCommand((Ff7BattleCommand)command.CommandId, command.Argument) && command.ActorSlot < 3;
        bool enemy = command.ActorSlot >= 4 && (command.CommandId is 0x20 or 2) || command.CommandId == 0x23;
        if (!(enemy || recovery) || record is { AdditionalEffect: not byte.MaxValue })
        {
            throw new NotSupportedException("The native action has no admitted effect on this guest vitals profile.");
        }
    }

    internal static void ValidateGuestVitalsSnapshot(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.GuestSlots is { } slots && (slots.Length is < 2 or > 3 || snapshot.GuestVitals is not null) || snapshot.GuestVitals is null && (snapshot.GuestProjection is not null || snapshot.GuestBaseDexterity is not null || snapshot.GuestLimitReadyAt is not null))
        {
            throw new InvalidDataException("The saved guest projections have inconsistent source identity bindings.");
        }
        IReadOnlyList<Ff7BattleGuestSlotState> guests = snapshot.GetGuestSlotStates();
        if (guests.Count == 0)
        {
            return;
        }
        if (snapshot.Actors is null || snapshot.Actors.Any(actor => actor?.Actor is null) || snapshot.Queue?.Ready is null || snapshot.Queue.Slots is null || snapshot.Action is { Command: null })
        {
            throw new InvalidDataException("Saved guest vitals require their host actors and action queue.");
        }
        HashSet<int> hostSlots = [];
        HashSet<int> storyCharacters = [];
        HashSet<int> originCharacters = [];
        HashSet<int> originRows = [];
        for (int index = 0; index < guests.Count; index++)
        {
            Ff7BattleGuestSlotState guest = guests[index];
            Ff7BattleGuestVitalsBinding binding = guest.Binding ?? throw new InvalidDataException("A saved guest projection is missing its source identity.");
            if (!hostSlots.Add(binding.HostSlot) || !storyCharacters.Add(binding.StoryCharacter) || !originCharacters.Add(binding.OriginCharacter) || !originRows.Add(binding.OriginRow) || index != 0 && !SharesGuestEncounter(guests[0].Binding, binding))
            {
                throw new InvalidDataException("Saved guest slots disagree about the source encounter or reuse a character.");
            }
            ValidateGuestSlotSnapshot(snapshot, guest);
        }
        if (snapshot.Action is { Command.GuestAttack: true } action && !hostSlots.Contains(action.Command.ActorSlot) || snapshot.Queue.Ready.Any(command => command is { GuestAttack: true } && !hostSlots.Contains(command.ActorSlot)) || snapshot.Queue.Slots.Any(entry => entry?.Command is { GuestAttack: true } command && !hostSlots.Contains(command.ActorSlot)))
        {
            throw new InvalidDataException("A saved guest action has no projected host slot.");
        }
    }

    private static void ValidateGuestSlotSnapshot(Ff7BattleSnapshot snapshot, Ff7BattleGuestSlotState guest)
    {
        Ff7BattleGuestVitalsBinding binding = guest.Binding;
        binding.Validate();
        if (guest.LimitReadyAt is long readyAt && (readyAt < 0 || readyAt > snapshot.Tick))
        {
            throw new InvalidDataException("The saved guest Limit readiness contradicts the host clock.");
        }
        Ff7BattleGuestProjection projection = guest.Projection ?? Ff7BattleGuestProjection.From(binding);
        projection.Validate();
        Ff7BattleActorState actor = snapshot.Actors.SingleOrDefault(candidate => candidate.Actor.Slot == binding.HostSlot && candidate.Identity == binding.StoryCharacter)
            ?? throw new InvalidDataException("Saved guest vitals do not identify the occupied story slot.");
        Ff7BattleAiActor native = actor.Actor;
        if (native.HitPoints > projection.MaximumHp || native.MaxHitPoints != projection.MaximumHp || native.Defense != projection.Defense || native.MagicDefense != projection.MagicDefense || native.MagicPoints != 0 || native.MaxMagicPoints != 0 || (native.Statuses & ~(Ff7GuestBattleStatuses.SupportedHost | Ff7NativeBattleStatuses.SupportedRecordStatuses)) != 0 || (native.HitPoints == 0) != ((native.Statuses & 1) != 0) || actor.LimitGauge != 0 || actor.AllUses is not null)
        {
            throw new InvalidDataException("Saved guest vitals contain an unsupported host projection.");
        }
    }
}
