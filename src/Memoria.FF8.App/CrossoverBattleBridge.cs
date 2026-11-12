using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal enum CrossoverBattleActorKind { Character, Enemy }
internal enum CrossoverBattleAbilityKind { Attack, Spell, Draw, Card, Limit }

internal readonly record struct CrossoverBattleActorId(SessionGame Origin, CrossoverBattleActorKind Kind, int NativeId, int Occurrence);
internal readonly record struct CrossoverBattleAbilityId(SessionGame Origin, CrossoverBattleAbilityKind Kind, ushort NativeId);
internal sealed record CrossoverBattleBinding(int HostSlot, int StoryCharacter, int GuestCharacter);
internal sealed record CrossoverBattleParticipant(int HostSlot, CrossoverBattleActorId Story, CrossoverBattleActorId Presentation, CrossoverBattleActorId Combat, bool ProvidesDrawStock = false, bool ProvidesCardConversion = false);

internal sealed record CrossoverBattleRoster(int Version, Guid EncounterId, long HostRequestId, int HostFormationId, ImmutableArray<CrossoverBattleParticipant> Participants)
{
    public const int CurrentVersion = 1;

    public static CrossoverBattleRoster Create(Guid encounterId, Ff7BattleSnapshot host, IReadOnlyList<CrossoverBattleBinding> bindings, IReadOnlyList<CharacterAppearanceMapping> appearances)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(bindings);
        ArgumentNullException.ThrowIfNull(appearances);
        if (bindings.Count is < 1 or > 3 || bindings.Any(binding => binding.GuestCharacter is < 0 or > 10))
        {
            throw new NotSupportedException("A mixed roster requires one to three mapped FFVIII combatants.");
        }
        if (bindings.Select(binding => binding.HostSlot).Distinct().Count() != bindings.Count || bindings.Select(binding => binding.GuestCharacter).Distinct().Count() != bindings.Count)
        {
            throw new NotSupportedException("Each FFVIII combatant and FFVII party slot may be mapped once per encounter.");
        }
        foreach (CrossoverBattleBinding binding in bindings)
        {
            if (binding.HostSlot is < 0 or > 2 || !host.Actors.Any(actor => actor.Actor.Slot == binding.HostSlot && actor.Identity == binding.StoryCharacter))
            {
                throw new InvalidDataException("The mixed combat binding does not match an occupied FFVII story slot.");
            }
        }

        ImmutableArray<CrossoverBattleParticipant>.Builder roster = ImmutableArray.CreateBuilder<CrossoverBattleParticipant>(host.Actors.Length);
        foreach (Ff7BattleActorState actor in host.Actors)
        {
            int slot = actor.Actor.Slot;
            bool party = slot < 3;
            CrossoverBattleActorId story = new(SessionGame.Ff7, party ? CrossoverBattleActorKind.Character : CrossoverBattleActorKind.Enemy, actor.Identity, slot);
            CharacterAppearanceMapping? appearance = party ? appearances.SingleOrDefault(mapping => mapping.StoryGame == SessionGame.Ff7 && mapping.StoryCharacter == actor.Identity) : null;
            CrossoverBattleActorId presentation = appearance is null ? story : new(appearance.AppearanceGame, CrossoverBattleActorKind.Character, appearance.AppearanceCharacter, slot);
            CrossoverBattleBinding? binding = bindings.SingleOrDefault(candidate => candidate.HostSlot == slot);
            CrossoverBattleActorId combat = binding is null ? story : new(SessionGame.Ff8, CrossoverBattleActorKind.Character, binding.GuestCharacter, slot);
            roster.Add(new(slot, story, presentation, combat));
        }
        CrossoverBattleRoster result = new(CurrentVersion, encounterId, host.RequestId, host.FormationId, roster.MoveToImmutable());
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Version != CurrentVersion)
        {
            throw new NotSupportedException($"Mixed battle execution version {Version} is unsupported.");
        }
        if (EncounterId == Guid.Empty || HostRequestId < 1 || HostFormationId is < 0 or > 1023 || Participants.IsDefaultOrEmpty || Participants.Any(actor => actor is null) || Participants.Select(actor => actor.HostSlot).Distinct().Count() != Participants.Length)
        {
            throw new InvalidDataException("Mixed battle roster lacks stable encounter and actor slot identities.");
        }

        int guests = 0;
        HashSet<(SessionGame Origin, int NativeId)> activeCharacters = [];
        foreach (CrossoverBattleParticipant actor in Participants)
        {
            if (actor.HostSlot is < 0 or 3 or > 9 || actor.Story.Origin != SessionGame.Ff7 || actor.Story.Occurrence != actor.HostSlot || actor.Presentation.Occurrence != actor.HostSlot || actor.Combat.Occurrence != actor.HostSlot)
            {
                throw new InvalidDataException("Mixed battle roster has an invalid physical occurrence.");
            }
            if (actor.Presentation != actor.Story && (actor.HostSlot >= 3 || actor.Presentation.Origin != SessionGame.Ff8 || actor.Presentation.Kind != CrossoverBattleActorKind.Character || actor.Presentation.NativeId is < 0 or > 10))
            {
                throw new InvalidDataException("Mixed battle roster has an invalid presentation identity.");
            }
            if (actor.HostSlot < 3)
            {
                if (actor.Story.Kind != CrossoverBattleActorKind.Character || actor.Story.NativeId is < 0 or > 8 || actor.Combat.Kind != CrossoverBattleActorKind.Character || !activeCharacters.Add((actor.Combat.Origin, actor.Combat.NativeId)))
                {
                    throw new InvalidDataException("Mixed battle party contains duplicate or malformed character ownership.");
                }
                if (actor.Combat.Origin == SessionGame.Ff8 && actor.Combat.NativeId is >= 0 and <= 10)
                {
                    guests++;
                }
                else if (actor.Combat != actor.Story)
                {
                    throw new InvalidDataException("Mixed battle party contains an unbound combat identity.");
                }
            }
            else if (actor.Story.Kind != CrossoverBattleActorKind.Enemy || actor.Story.NativeId < 0 || actor.Combat != actor.Story || actor.ProvidesDrawStock || actor.ProvidesCardConversion)
            {
                throw new InvalidDataException("An FFVII encounter enemy requires its original combat identity and capabilities.");
            }
        }
        if (guests == 0)
        {
            throw new InvalidDataException("The mixed roster requires at least one original FFVIII combat owner.");
        }
    }

    [JsonIgnore]
    public IEnumerable<CrossoverBattleParticipant> Guests => Participants.Where(actor => actor.Combat.Origin == SessionGame.Ff8);

    [JsonIgnore]
    public CrossoverBattleParticipant Guest => Guests.Single();

    public CrossoverBattleParticipant GuestAt(int hostSlot) => Guests.Single(actor => actor.HostSlot == hostSlot);
}

internal sealed record CrossoverFf8BattleKit(int RecordRow, ushort HitPoints, int MaximumHitPoints, int Level, byte Weapon, BattlePlayerStats Stats, NativeBattleStatusAllocation Statuses, BattleNativeAction Attack, BattleNativeLimitDefinition Limit, byte FinisherFlags, ImmutableArray<byte> Commands, ImmutableArray<BattleNativeAction> Spells)
{
    public byte CharacterId { get; init; }
    public uint StatusPassives { get; init; }
    public byte RenzokukenPower { get; init; }

    public string? OpeningCombatUnavailableReason()
    {
        return Attack.AttackType is 1 or 9 or 10 ? null : $"FFVIII physical attack type {Attack.AttackType} has no mixed battle contract.";
    }

    public static CrossoverFf8BattleKit Read(NativeCharacterVitals vitals, BattleAssets resources, int characterId)
    {
        ArgumentNullException.ThrowIfNull(vitals);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentOutOfRangeException.ThrowIfNegative(characterId);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(characterId, 10);
        vitals.Validate();
        int row = vitals.FindPhysicalRow(checked((byte)characterId));
        if ((uint)row >= vitals.Records.Length || vitals.Records.Count(record => record.CharacterId == characterId) != 1)
        {
            throw new NotSupportedException($"Mixed combat requires one original {CrossoverCharacterCatalog.AppearanceNames[characterId]} record in the suspended FFVIII world.");
        }
        NativeCharacterVital saved = vitals.Records[row];
        if (saved.HitPoints.Minimum != saved.HitPoints.Maximum)
        {
            throw new NotSupportedException("Mixed combat requires the selected FFVIII character's exact saved HP.");
        }
        BattleNativePlayerStatRules rules = resources.ReadPlayerNativeStatRules(vitals, row);
        ImmutableArray<BattleNativeAction> spells = resources.NativeActions.Where(action => action.Command == 2 && action.KernelSection == 1 && action.ActionId != 0).ToImmutableArray();
        byte weapon = vitals.DreamEnabled && characterId >= 8 ? vitals.DreamWeapons[characterId - 8] : saved.Weapon;
        return new(row, saved.HitPoints.Minimum, vitals.GetMagicMaximumHitPoints(row), rules.Level, weapon, resources.ReadPlayerStats(vitals, row), resources.ReadPlayerNativeStatusData(vitals, row), resources.ReadPlayerNativeAttackData(vitals, row), resources.ReadNativeLimitDefinition(characterId), rules.WeaponRecord[2], resources.ReadPlayerNativeCommandSlots(vitals, row), spells)
        {
            CharacterId = (byte)characterId,
            StatusPassives = rules.NativeStatusPassiveFlags ?? throw new InvalidDataException("The native character ability passives are missing."),
            RenzokukenPower = rules.CharacterRecord[33]
        };
    }
}

internal sealed record CrossoverBattleAction(long Ordinal, CrossoverBattleActorId Actor, CrossoverBattleActorId Target, CrossoverBattleAbilityId Ability, int OriginRow, string? StockReservation);
internal sealed record CrossoverBattleAdmission(CrossoverBattleAction? Action, BattleNativeAction? NativeAction, NativeCharacterVitals Vitals, string? Reason);

internal sealed record CrossoverBattleStockSnapshot(Guid EncounterId, string InitOutSha256, string KernelSha256, NativeCharacterVital[] Records, MagicSystemState Magic)
{
    public static CrossoverBattleStockSnapshot Capture(CrossoverBattleRoster roster, NativeCharacterVitals working)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(working);
        roster.Validate();
        working.Validate();
        NativeCharacterVitals copy = working.DeepCopy();
        return new(roster.EncounterId, copy.InitOutSha256, copy.KernelSha256, copy.Records, copy.MagicSystem);
    }

    public NativeCharacterVitals Restore(CrossoverBattleRoster roster, NativeCharacterVitals suspendedOrigin)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(suspendedOrigin);
        roster.Validate();
        suspendedOrigin.Validate();
        if (EncounterId != roster.EncounterId || Records is not { Length: 8 } || Magic is null || !Records.Select(record => record?.CharacterId).SequenceEqual(suspendedOrigin.Records.Select(record => (byte?)record.CharacterId)))
        {
            throw new InvalidDataException("Mixed magic continuation does not match its suspended world and physical character owners.");
        }
        if (InitOutSha256 != suspendedOrigin.InitOutSha256 || KernelSha256 != suspendedOrigin.KernelSha256)
        {
            throw new NotSupportedException("Mixed magic continuation requires its original native FFVIII resource profile.");
        }
        NativeCharacterVitals result = suspendedOrigin with { Records = Records, MagicSystem = Magic };
        result.Validate();
        if (Magic.Reservations.Any(reservation => !reservation.ActionId.StartsWith($"mixed:{EncounterId:N}:", StringComparison.Ordinal)))
        {
            throw new InvalidDataException("Mixed magic continuation contains a reservation from a different encounter.");
        }
        return result.DeepCopy();
    }
}

internal static class CrossoverBattleBridge
{
    public static CrossoverBattleAdmission Prepare(CrossoverBattleRoster roster, CrossoverFf8BattleKit kit, NativeCharacterVitals vitals, long ordinal, CrossoverBattleAbilityId ability, CrossoverBattleActorId targetId)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(kit);
        ArgumentNullException.ThrowIfNull(vitals);
        roster.Validate();
        if (ordinal < 1 || ability.Origin != SessionGame.Ff8 || !Enum.IsDefined(ability.Kind))
        {
            throw new InvalidDataException("Mixed action requires a valid action occurrence and native FFVIII ability identity.");
        }
        CrossoverBattleParticipant guest = roster.Guests.SingleOrDefault(actor => actor.Combat.NativeId == kit.CharacterId) ?? throw new InvalidDataException("The native action kit does not belong to an FFVIII guest in this encounter.");
        ValidateGuestOwner(vitals, kit.RecordRow, guest.Combat.NativeId);
        CrossoverBattleParticipant target = roster.Participants.SingleOrDefault(actor => actor.Combat == targetId) ?? throw new InvalidDataException("Mixed action target has no combat occurrence in the active encounter.");
        if (ability.Kind == CrossoverBattleAbilityKind.Draw && !target.ProvidesDrawStock)
        {
            return Denied("This target has no native FFVIII Draw supply.");
        }
        if (ability.Kind == CrossoverBattleAbilityKind.Card && !target.ProvidesCardConversion)
        {
            return Denied("This target has no native FFVIII Card conversion.");
        }
        BattleNativeAction? action = ability.Kind switch
        {
            CrossoverBattleAbilityKind.Attack when kit.Commands is [1, ..] && ability.NativeId == kit.Attack.ActionId => kit.Attack,
            CrossoverBattleAbilityKind.Spell when kit.Commands.Contains((byte)2) => kit.Spells.SingleOrDefault(spell => spell.ActionId == ability.NativeId),
            _ => null
        };
        if (action is null)
        {
            return Denied("The native guest kit cannot prepare this ability.");
        }
        NativeCharacterVitals working = vitals;
        string? reservation = null;
        if (ability.Kind == CrossoverBattleAbilityKind.Spell)
        {
            reservation = ReservationKey(roster, ordinal);
            if (vitals.MagicSystem.CommittedActionIds.Contains(reservation, StringComparer.Ordinal))
            {
                return Denied("This mixed action has already committed its origin stock.");
            }
            if (vitals.MagicSystem.Reservations.Any(item => item.ActionId == reservation))
            {
                return Denied("This mixed action already owns an active stock reservation.");
            }
            MagicCastQuote quote = MagicCastingService.Quote(vitals, kit.RecordRow, ability.NativeId, MagicCastContext.Battle, reservation);
            if (!quote.Allowed)
            {
                return Denied(quote.Reason ?? "The FFVIII guest has no available stock for this spell.");
            }
            working = MagicCastingService.Reserve(vitals, kit.RecordRow, ability.NativeId, MagicCastContext.Battle, reservation);
        }
        return new(new(ordinal, guest.Combat, targetId, ability, kit.RecordRow, reservation), action, working, null);

        CrossoverBattleAdmission Denied(string reason) => new(null, null, vitals, reason);
    }

    public static NativeCharacterVitals CommitStock(CrossoverBattleRoster roster, NativeCharacterVitals working, CrossoverBattleAction action)
    {
        string? reservation = ValidateActionOwner(roster, working, action);
        return reservation is null ? working : MagicCastingService.Commit(working, reservation);
    }

    public static NativeCharacterVitals ReleaseStock(CrossoverBattleRoster roster, NativeCharacterVitals working, CrossoverBattleAction action)
    {
        string? reservation = ValidateActionOwner(roster, working, action);
        return reservation is null ? working : MagicCastingService.Release(working, reservation);
    }

    public static bool HasCommittedStock(CrossoverBattleRoster roster, NativeCharacterVitals working, CrossoverBattleAction action)
    {
        string? reservation = ValidateActionOwner(roster, working, action);
        return reservation is not null && working.MagicSystem.CommittedActionIds.Contains(reservation, StringComparer.Ordinal);
    }

    private static string? ValidateActionOwner(CrossoverBattleRoster roster, NativeCharacterVitals working, CrossoverBattleAction action)
    {
        ArgumentNullException.ThrowIfNull(roster);
        ArgumentNullException.ThrowIfNull(working);
        ArgumentNullException.ThrowIfNull(action);
        roster.Validate();
        string? expected = action.Ability.Kind == CrossoverBattleAbilityKind.Spell ? ReservationKey(roster, action.Ordinal) : null;
        if (action.Ordinal < 1 || !roster.Guests.Any(guest => guest.Combat == action.Actor) || action.Ability.Origin != action.Actor.Origin || action.Ability.Kind is not (CrossoverBattleAbilityKind.Attack or CrossoverBattleAbilityKind.Spell) || !roster.Participants.Any(actor => actor.Combat == action.Target) || action.StockReservation != expected)
        {
            throw new InvalidDataException("Mixed action does not belong to its saved actor and encounter.");
        }
        ValidateGuestOwner(working, action.OriginRow, action.Actor.NativeId);
        if (expected is not null && working.MagicSystem.Reservations.SingleOrDefault(item => item.ActionId == expected) is { } reserved && (reserved.ActorRow != action.OriginRow || reserved.SpellId != action.Ability.NativeId || reserved.Context != MagicCastContext.Battle || reserved.Count != 1))
        {
            throw new InvalidDataException("Mixed magic action differs from its origin stock reservation.");
        }
        return expected;
    }

    private static void ValidateGuestOwner(NativeCharacterVitals vitals, int row, int characterId)
    {
        if ((uint)row >= vitals.Records.Length || vitals.Records[row].CharacterId != characterId || vitals.Records.Count(record => record.CharacterId == characterId) != 1)
        {
            throw new InvalidDataException("The mixed guest requires one original character record in the suspended FFVIII world.");
        }
    }

    private static string ReservationKey(CrossoverBattleRoster roster, long ordinal) => $"mixed:{roster.EncounterId:N}:{ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
}
