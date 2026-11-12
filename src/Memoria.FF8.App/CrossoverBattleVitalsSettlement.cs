using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Collections.Immutable;

namespace Memoria.FF8;

internal sealed record CrossoverBattleVitalsSettlementResult(CombinedSessionSave Save, Ff7GuestBattleSettlementReceipt Receipt);
internal sealed record CrossoverGuestItemSettlement(Guid EncounterId, CrossoverGuestItemInventory Inventory, NativeCrtRandomState? OriginCrtRandom, NativeCrtRandomState CrtRandom);
internal sealed record CrossoverGuestMagicSettlement(CrossoverBattleRoster Roster, CrossoverBattleStockSnapshot Stock, NativeCrtRandomState? OriginCrtRandom, NativeCrtRandomState CrtRandom, ImmutableArray<CrossoverGuestCommittedSpell> Costs, bool LegacyBattleMaskContinuation = false);
internal sealed record CrossoverGuestCrtSettlement(NativeCrtRandomState? Origin, NativeCrtRandomState Current);

/// <summary>Stages an FFVII field continuation and FFVIII character update together from immutable source snapshots.</summary>
internal static class CrossoverBattleVitalsSettlement
{
    public static Ff7BattleGuestVitalsBinding ReadBinding(CombinedSessionSave source, CrossoverBattleRoster roster, NativeCharacterVitals installed, BattleAssets assets)
        => ReadBindings(source, roster, installed, assets).Single();

    public static Ff7BattleGuestVitalsBinding[] ReadBindings(CombinedSessionSave source, CrossoverBattleRoster roster, NativeCharacterVitals installed, BattleAssets assets)
    {
        source.Validate();
        roster.Validate();
        if (source is not { ActiveGame: SessionGame.Ff7, Ff7.Battle: { } host } || host.RequestId != roster.HostRequestId || host.FormationId != roster.HostFormationId)
        {
            throw new InvalidDataException("Guest vitals require the exact suspended FFVIII payload and matching host battle occurrence.");
        }
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        NativeCharacterVitals vitals = document.Characters.Restore(installed);
        string sourceHash = Hash(source.Ff8Payload!);
        return roster.Guests.OrderBy(guest => guest.HostSlot).Select(guest => ReadBinding(roster.EncounterId, guest, sourceHash, document, vitals, assets)).ToArray();
    }

    private static Ff7BattleGuestVitalsBinding ReadBinding(Guid encounter, CrossoverBattleParticipant guest, string sourceHash, GameplaySaveComponentDocument document, NativeCharacterVitals vitals, BattleAssets assets)
    {
        int character = guest.Combat.NativeId;
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(vitals, assets, character);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        ushort initialHp = Math.Min(kit.HitPoints, projection.MaximumHp);
        return new(encounter, sourceHash, guest.HostSlot, guest.Story.NativeId, character, kit.RecordRow, initialHp, projection.MaximumHp, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense)
        {
            OriginStatuses = kit.Statuses.Saved,
            OriginBattleStatuses = kit.Statuses.Battle,
            OriginName = CrossoverGuestFieldVitals.ReadGuestName(document, character)
        };
    }

    public static CrossoverBattleVitalsSettlementResult PrepareVictory(CombinedSessionSave source, Ff7Installation installation, NativeCharacterVitals installed, Ff7GuestBattleSettlementReceipt? completed = null, CrossoverGunbladeHostSnapshot? presentation = null)
    {
        if (source.Ff7?.Battle is { Outcome: not Ff7BattleOutcome.Victory })
        {
            throw new InvalidOperationException("Victory settlement requires a victory result.");
        }
        return PrepareResult(source, installation, installed, completed, presentation);
    }

    public static CrossoverBattleVitalsSettlementResult PrepareResult(CombinedSessionSave source, Ff7Installation installation, NativeCharacterVitals installed, Ff7GuestBattleSettlementReceipt? completed = null, CrossoverGunbladeHostSnapshot? presentation = null, CrossoverGuestMagicSettlement? magic = null, CrossoverGuestItemSettlement? items = null, CrossoverGuestCrtSettlement? crt = null)
        => PrepareResult(source, installation, installed, presentation is null ? [] : [presentation], completed, magic, items, crt);

    public static CrossoverBattleVitalsSettlementResult PrepareResult(CombinedSessionSave source, Ff7Installation installation, NativeCharacterVitals installed, IReadOnlyList<CrossoverGunbladeHostSnapshot> presentations, Ff7GuestBattleSettlementReceipt? completed = null, CrossoverGuestMagicSettlement? magic = null, CrossoverGuestItemSettlement? items = null, CrossoverGuestCrtSettlement? crt = null)
    {
        source.Validate();
        if (source is not { ActiveGame: SessionGame.Ff7, Ff7: { } host, Ff8Payload: { Length: > 0 } payload })
        {
            throw new InvalidDataException("Guest settlement requires the complete active FFVII and suspended FFVIII save payloads.");
        }
        IReadOnlyList<Ff7BattleGuestSlotState> slots = host.Battle?.GetGuestSlotStates() ?? [];
        Ff7BattleGuestVitalsBinding[] bindings = slots.Select(slot => slot.Binding).ToArray();
        Ff7BattleGuestVitalsBinding? first = bindings.FirstOrDefault();
        string sourceHash = Hash(payload);
        completed ??= host.Battle is null ? source.GuestBattleSettlements?.LastOrDefault() : source.GuestBattleSettlements?.SingleOrDefault(receipt => receipt.EncounterId == first?.EncounterId);
        if (completed is not null)
        {
            if (host.Battle is not null && (completed.Outcome != Ff7BattleOutcome.Defeat || completed.EncounterId != first?.EncounterId)
                || source.GuestBattleSettlements?.Any(receipt => SameReceipt(receipt, completed)) != true || sourceHash != completed.SettledFf8Hash
                || completed.GetGuests().Any(guest => CharacterHash(host, guest.StoryCharacter) != guest.DormantCharacterHash))
            {
                throw new InvalidDataException("The completed guest settlement receipt does not belong to this combined continuation.");
            }
            return new(source, completed);
        }
        if (host.Battle is not { Outcome: not Ff7BattleOutcome.Fighting } battle || first is null || bindings.Any(binding => binding.EncounterId != first.EncounterId || binding.SourcePayloadSha256 != sourceHash)
            || source.GuestBattleSettlements?.Any(receipt => receipt.EncounterId == first.EncounterId) == true || host.Progress.CompletedBattles.Any(receipt => receipt.FieldGeneration == host.FieldGeneration && receipt.RequestId == battle.RequestId))
        {
            throw new InvalidOperationException("Guest settlement requires the unsettled terminal host and its exact suspended source payload.");
        }
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        if ((battle.GuestAttackUsed || battle.GuestPresentationAttached) != (presentations.Count > 0) || presentations.Count > 0 && presentations.Count != bindings.Length)
        {
            throw new InvalidDataException("Guest settlement requires the native presentation result exactly when the host executed a guest attack.");
        }
        NativeCharacterVitals original = document.Characters.Restore(installed);
        foreach (Ff7BattleGuestVitalsBinding binding in bindings)
        {
            ValidateOrigin(original, binding);
        }
        NativeCharacterVitals finalStock = original;
        if (magic is not null)
        {
            if (magic.Roster.EncounterId != first.EncounterId || magic.Roster.HostRequestId != battle.RequestId || magic.Roster.HostFormationId != battle.FormationId || magic.Roster.Guests.Count() != bindings.Length
                || bindings.Any(binding => !magic.Roster.Guests.Any(guest => guest.HostSlot == binding.HostSlot && guest.Story.NativeId == binding.StoryCharacter && guest.Combat.NativeId == binding.OriginCharacter))
                || document.Progression?.Clocks.CrtRandom != magic.OriginCrtRandom)
            {
                throw new InvalidDataException("Guest magic settlement has a different encounter or original visual RNG owner.");
            }
            finalStock = RestoreCommittedStock(magic, original, bindings.Select(binding => binding.OriginRow).ToArray());
        }
        GameplaySaveCharacters characters = document.Characters;
        if (magic is not null)
        {
            characters = document.Characters with { Records = finalStock.Records, Magic = GameplaySaveCharacters.Capture(finalStock).Magic };
        }
        NativeCharacterVital[] rows = (NativeCharacterVital[])characters.Records.Clone();
        List<Ff7GuestBattleSettlementCharacter> guests = [];
        foreach (Ff7BattleGuestSlotState slot in slots)
        {
            Ff7BattleGuestVitalsBinding binding = slot.Binding;
            int row = binding.OriginRow;
            Ff7BattleAiActor projected = battle.Actors.Single(actor => actor.Actor.Slot == binding.HostSlot && actor.Identity == binding.StoryCharacter).Actor;
            ushort maximumHp = CrossoverGuestRecord.MaximumHitPoints(finalStock, row, binding.OriginCharacter);
            ushort hp = checked((ushort)projected.HitPoints);
            if (maximumHp != (slot.Projection?.MaximumHp ?? binding.MaximumHp) || projected.MaxHitPoints != maximumHp || hp > maximumHp || (projected.Statuses & ~Ff7GuestBattleStatuses.SupportedHost) != 0 || (hp == 0) != ((projected.Statuses & 1) != 0))
            {
                throw new InvalidDataException("Guest settlement requires consistent HP and mapped statuses for every admitted slot.");
            }
            rows[row] = rows[row] with { HitPoints = new(hp, hp), Status = Ff7GuestBattleStatuses.ToOrigin(projected.Statuses, original.Records[row].Status) };
            guests.Add(new(binding.StoryCharacter, row, hp, CharacterHash(host, binding.StoryCharacter)));
        }
        GameplaySaveComponentDocument updated = document with { Characters = characters with { Records = rows } };
        if (magic is not null)
        {
            updated = updated with { Progression = updated.Progression! with { Clocks = updated.Progression!.Clocks with { CrtRandom = magic.CrtRandom } } };
        }
        if (items is not null)
        {
            if (items.EncounterId != first.EncounterId || document.Progression?.Clocks.CrtRandom != items.OriginCrtRandom || magic is not null && magic.CrtRandom != items.CrtRandom)
            {
                throw new InvalidDataException("Guest item settlement has a different encounter or original visual RNG owner.");
            }
            CrossoverGuestItemInventory inventory = items.Inventory.Restore(document.Economy, battle.RequestId, bindings.Select(binding => binding.HostSlot).ToArray(), battle.Tick);
            if (inventory.AllPending.Any())
            {
                throw new InvalidDataException("Guest item settlement requires every reservation to be spent or refunded.");
            }
            updated = updated with { Economy = document.Economy with { Items = inventory.Slots }, Progression = updated.Progression! with { Clocks = updated.Progression!.Clocks with { CrtRandom = items.CrtRandom } } };
        }
        if (presentations.Count > 0)
        {
            NativeGlobalRandomState globalRandom = ReadPresentationRandom(presentations, bindings, battle.Tick, document.Progression?.Clocks.GlobalRandom);
            updated = updated with { Progression = updated.Progression! with { Clocks = updated.Progression!.Clocks with { GlobalRandom = globalRandom } } };
        }
        if (crt is not null)
        {
            if (presentations.Count == 0 || magic is not null || items is not null || document.Progression is not { } progression || progression.Clocks.CrtRandom != crt.Origin)
            {
                throw new InvalidDataException("Guest attack CRT settlement requires its exact source and completed presentation.");
            }
            updated = updated with { Progression = updated.Progression! with { Clocks = updated.Progression!.Clocks with { CrtRandom = crt.Current } } };
        }
        using Ff7Session staged = Ff7Session.Restore(installation, host);
        bool returnsAfterDefeat = staged.Battle!.ReturnsAfterDefeat;
        bool confirmed = battle.Outcome switch
        {
            Ff7BattleOutcome.Victory => staged.ConfirmGuestBattleVictory(bindings),
            Ff7BattleOutcome.Escaped => staged.ConfirmGuestBattleEscape(bindings),
            Ff7BattleOutcome.Defeat when returnsAfterDefeat => staged.ConfirmGuestBattleDefeat(bindings),
            Ff7BattleOutcome.Defeat => staged.Battle!.ResultReady,
            _ => false
        };
        if (!confirmed)
        {
            throw new InvalidOperationException("The guest result has not reached the native field-result boundary.");
        }
        Ff7SessionSave settledHost = staged.Capture() with { AudioPresentation = host.AudioPresentation };
        if (battle.Outcome == Ff7BattleOutcome.Victory)
        {
            Ff7BattleReward reward = settledHost.Progress.CompletedBattles.Single(receipt => receipt.FieldGeneration == host.FieldGeneration && receipt.RequestId == battle.RequestId).Reward;
            NativeCharacterVitals rewarded = updated.Characters.Restore(installed);
            byte[] guestParty = bindings.Select(binding => checked((byte)binding.OriginRow)).Concat(Enumerable.Repeat(byte.MaxValue, 3 - bindings.Length)).ToArray();
            int[] eligible = BattleResultProjection.EligibleGuardianForces(rewarded with { CombatParty = guestParty });
            // Native AP saturates the current learning byte without carrying into the next ability.
            ushort points = checked((ushort)Math.Min(ushort.MaxValue, reward.Ff8AbilityPoints));
            foreach (int id in eligible)
            {
                rewarded = rewarded.AwardGuardianForceAbilityPoints(id, points);
            }
            if (reward.Ff8AbilityPoints != 0 && eligible.Length > 0)
            {
                updated = updated with { Characters = GameplaySaveCharacters.Capture(rewarded) };
            }
        }
        using MemoryStream ff8 = new();
        GameplaySaveCodec.Write(ff8, updated);
        byte[] settledPayload = ff8.ToArray();
        if (guests.Any(guest => CharacterHash(settledHost, guest.StoryCharacter) != guest.DormantCharacterHash))
        {
            throw new InvalidDataException("Guest settlement changed the dormant FFVII story character record.");
        }
        Ff7GuestBattleSettlementCharacter primary = guests[0];
        Ff7GuestBattleSettlementReceipt receipt = new(first.EncounterId, host.FieldGeneration, battle.RequestId, battle.FormationId, primary.StoryCharacter, primary.OriginRow, sourceHash, Hash(settledPayload), primary.SettledHp, primary.DormantCharacterHash)
        {
            Outcome = battle.Outcome,
            AdditionalGuests = guests.Count > 1 ? guests.Skip(1).ToArray() : null
        };
        CombinedSessionSave settled = source with { Ff7 = settledHost, Ff8Payload = settledPayload, MixedBattlePayload = battle.Outcome == Ff7BattleOutcome.Defeat && !returnsAfterDefeat ? source.MixedBattlePayload : null, GuestBattleSettlements = [.. source.GuestBattleSettlements ?? [], receipt] };
        settled.Validate();
        return new(settled, receipt);
    }

    private static string CharacterHash(Ff7SessionSave host, int storyCharacter)
        => Hash(JsonSerializer.SerializeToUtf8Bytes(host.Progress.Characters.Single(character => character.Id == storyCharacter)));

    private static string Hash(byte[] payload) => Convert.ToHexString(SHA256.HashData(payload));

    private static bool SameReceipt(Ff7GuestBattleSettlementReceipt left, Ff7GuestBattleSettlementReceipt right)
        => left with { AdditionalGuests = null } == right with { AdditionalGuests = null } && left.GetGuests().SequenceEqual(right.GetGuests());

    private static void ValidateOrigin(NativeCharacterVitals original, Ff7BattleGuestVitalsBinding binding)
    {
        int row = binding.OriginRow;
        if (!CrossoverGuestRecord.OwnsBattleRow(original, row, binding.OriginCharacter))
        {
            throw new InvalidDataException("The suspended FFVIII character record no longer belongs to the admitted guest.");
        }
        NativeCharacterVital record = original.Records[row];
        if (record.HitPoints.Minimum != record.HitPoints.Maximum || Math.Min(record.HitPoints.Minimum, binding.MaximumHp) != binding.InitialHp
            || record.Status != binding.OriginStatuses || CrossoverGuestRecord.MaximumHitPoints(original, row, binding.OriginCharacter) != binding.MaximumHp)
        {
            throw new InvalidDataException("The suspended FFVIII character record no longer matches the admitted guest vitals.");
        }
    }

    private static NativeGlobalRandomState ReadPresentationRandom(IReadOnlyList<CrossoverGunbladeHostSnapshot> presentations, IReadOnlyList<Ff7BattleGuestVitalsBinding> bindings, long hostTick, NativeGlobalRandomState? originRandom)
    {
        HashSet<int> presentedSlots = [];
        NativeGlobalRandomState finalRandom = presentations[0].GlobalRandom;
        foreach (CrossoverGunbladeHostSnapshot presentation in presentations)
        {
            if (originRandom is null || presentation.OriginGlobalRandom != originRandom || presentation.GlobalRandom != finalRandom || presentation.EncounterId != bindings[0].EncounterId
                || presentation.Invocation is not null || presentation.Presentation is not { Complete: true } && !presentation.Cancelled || presentation.HostTick > hostTick
                || presentation.Presentation is not { Model: { } model } || !presentedSlots.Add(model.Index) || !bindings.Any(binding => binding.HostSlot == model.Index && binding.OriginCharacter == model.ModelId4))
            {
                throw new InvalidDataException("Guest settlement requires each completed native presentation and one exact global RNG owner.");
            }
        }
        return finalRandom;
    }

    internal static NativeCharacterVitals RestoreCommittedStock(CrossoverGuestMagicSettlement magic, NativeCharacterVitals original, IReadOnlyCollection<int> rows)
    {
        NativeCharacterVitals actual = magic.Stock.Restore(magic.Roster, original);
        if (actual.MagicSystem.Reservations.Length != 0 || magic.Costs.IsDefault || magic.Costs.IsEmpty)
        {
            throw new InvalidDataException("Guest spell settlement requires completed native stock costs.");
        }
        string encounter = $"mixed:{magic.Roster.EncounterId:N}";
        NativeCharacterVitals expected = magic.LegacyBattleMaskContinuation ? original with { MagicSystem = original.MagicSystem with { EncounterId = encounter } } : MagicCastingService.BeginEncounter(original, encounter);
        for (int index = 0; index < magic.Costs.Length; index++)
        {
            CrossoverGuestCommittedSpell cost = magic.Costs[index];
            if (cost.Ordinal != index + 1 || cost.Spell == 0 || cost.Spell >= original.MagicSystem.SharedBattleMasks.Length || cost.DrawQuantity is < 0 or > 9 || cost.DrawQuantity > 0 && cost.Spell > 56 || !rows.Contains(cost.OriginRow))
            {
                throw new InvalidDataException("Guest spell settlement contains a foreign cost occurrence.");
            }
            string action = $"{encounter}:{cost.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            int character = magic.Roster.Guests.Single(guest => CrossoverGuestRecord.OwnsBattleRow(original, cost.OriginRow, guest.Combat.NativeId)).Combat.NativeId;
            expected = cost.DrawQuantity > 0
                ? NativeBattleDrawRules.Resolve(expected, cost.OriginRow, cost.Spell, cost.DrawQuantity, false, out _)
                : MagicCastingService.Commit(MagicCastingService.Reserve(expected, cost.OriginRow, cost.Spell, MagicCastContext.Battle, action, characterId: character), action);
        }
        if (!actual.Records.SequenceEqual(expected.Records) || JsonSerializer.Serialize(actual.MagicSystem) != JsonSerializer.Serialize(expected.MagicSystem))
        {
            throw new InvalidDataException("Guest spell stock or junction state differs from its native committed cost projection.");
        }
        return actual;
    }

}
