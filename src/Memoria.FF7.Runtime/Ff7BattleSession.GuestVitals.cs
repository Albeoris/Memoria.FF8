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
        if (EncounterId == Guid.Empty || SourcePayloadSha256 is not { Length: 64 } || !SourcePayloadSha256.All(Uri.IsHexDigit) || HostSlot is < 0 or > 2 || StoryCharacter is < 0 or > 8 || OriginCharacter is < 0 or > 10 || OriginRow is < 0 or > 7 || InitialHp > MaximumHp || MaximumHp == 0 || MaximumHp > 9999 || Level > 99 || string.IsNullOrWhiteSpace(OriginName))
        {
            throw new InvalidDataException("Guest vitals lack their original save and host slot binding.");
        }
    }
}

public sealed partial class Ff7BattleSession
{
    public Ff7BattleGuestVitalsBinding? GuestVitals => state.GuestVitals;
    public Ff7BattleGuestProjection? GuestProjection => state.GuestProjection ?? (state.GuestVitals is { } binding ? Ff7BattleGuestProjection.From(binding) : null);

    public void ReprojectGuestVitals(Ff7BattleGuestProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        projection.Validate();
        Ff7BattleGuestVitalsBinding binding = state.GuestVitals ?? throw new InvalidOperationException("A guest stat update requires its immutable source binding.");
        Ff7BattleActorState actor = Actor(binding.HostSlot);
        Ff7BattleAiActor current = actor.Actor with { HitPoints = Math.Min(actor.Actor.HitPoints, projection.MaximumHp), MaxHitPoints = projection.MaximumHp, Defense = projection.Defense, MagicDefense = projection.MagicDefense };
        Replace(actor with { Actor = current });
        state = state with { GuestProjection = projection };
    }

    public void BindGuestSpeed(byte initialDexterity)
    {
        Ff7BattleGuestVitalsBinding binding = state.GuestVitals ?? throw new InvalidOperationException("Guest speed requires its source identity binding.");
        if (state.GuestBaseDexterity is byte saved && saved != initialDexterity)
        {
            throw new InvalidDataException("Guest speed disagrees with its original encounter projection.");
        }
        RefreshGuestSpeedDenominator(binding.HostSlot, initialDexterity);
        state = state with { GuestBaseDexterity = initialDexterity };
    }

    private void RefreshGuestSpeedDenominator(int slot, byte guestDexterity)
    {
        Span<byte> dexterity = stackalloc byte[3];
        int count = 0;
        for (int index = 0; index < characters.Length; index++)
        {
            if (characters[index] is { } character)
            {
                dexterity[count++] = index == slot ? guestDexterity : character.NativeRecord[6];
            }
        }
        speedDenominator = Ff7NativeAtb.SpeedDenominator(dexterity[..count]);
    }

    public void BindGuestVitals(Ff7BattleGuestVitalsBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);
        binding.Validate();
        if (state.GuestVitals is not null)
        {
            if (state.GuestVitals != binding)
            {
                throw new InvalidDataException("Guest vitals disagree with the saved encounter binding.");
            }
            return;
        }
        if (Tick != 0 || !state.Actors.Any(actor => actor.Actor.Slot == binding.HostSlot && actor.Identity == binding.StoryCharacter) || guestAttack is { } guest && (guest.EncounterId != binding.EncounterId || guest.ActorSlot != binding.HostSlot))
        {
            throw new NotSupportedException("The guest vitals profile requires an occupied story slot before the first encounter tick.");
        }
        Ff7BattleActorState actor = Actor(binding.HostSlot);
        uint statuses = Ff7GuestBattleStatuses.ToHostStatuses(binding.OriginStatuses, binding.OriginBattleStatuses) | (binding.InitialHp == 0 ? 1u : 0u);
        Ff7BattleAiActor projected = actor.Actor with { HitPoints = binding.InitialHp, MaxHitPoints = binding.MaximumHp, MagicPoints = 0, MaxMagicPoints = 0, Statuses = statuses, Defense = binding.Defense, MagicDefense = binding.MagicDefense };
        Replace(actor with { Actor = projected, LimitGauge = 0, AllUses = null, Timers = Ff7NativeBattleStatusClock.Apply(default, 0, statuses) });
        state = state with { GuestVitals = binding };
    }

    private bool IsGuestVitalsSlot(int slot) => state.GuestVitals?.HostSlot == slot;

    private Ff7BattleDamageActor ProjectGuestDefense(Ff7BattleActorState actor, Ff7BattleDamageActor stats)
    {
        return state.GuestVitals is { } binding && actor.Actor.Slot == binding.HostSlot && GuestProjection is { } projection
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

    private void ValidateGuestVitalsSnapshot(Ff7BattleSnapshot snapshot)
    {
        if (snapshot.GuestVitals is not { } binding)
        {
            if (snapshot.GuestProjection is not null || snapshot.GuestBaseDexterity is not null)
            {
                throw new InvalidDataException("A current guest projection has no source identity binding.");
            }
            return;
        }
        binding.Validate();
        Ff7BattleGuestProjection projection = snapshot.GuestProjection ?? Ff7BattleGuestProjection.From(binding);
        projection.Validate();
        Ff7BattleActorState actor = snapshot.Actors.SingleOrDefault(candidate => candidate.Actor.Slot == binding.HostSlot && candidate.Identity == binding.StoryCharacter)
            ?? throw new InvalidDataException("Saved guest vitals do not identify the occupied story slot.");
        Ff7BattleAiActor native = actor.Actor;
        if (native.HitPoints > projection.MaximumHp || native.MaxHitPoints != projection.MaximumHp || native.Defense != projection.Defense || native.MagicDefense != projection.MagicDefense || native.MagicPoints != 0 || native.MaxMagicPoints != 0 || (native.Statuses & ~Ff7GuestBattleStatuses.SupportedHost) != 0 || (native.HitPoints == 0) != ((native.Statuses & 1) != 0) || actor.LimitGauge != 0 || actor.AllUses is not null)
        {
            throw new InvalidDataException("Saved guest vitals contain an unsupported host projection.");
        }
    }
}
