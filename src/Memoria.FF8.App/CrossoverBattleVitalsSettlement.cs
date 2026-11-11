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
    {
        source.Validate();
        roster.Validate();
        if (source is not { ActiveGame: SessionGame.Ff7, Ff7.Battle: { } host } || host.RequestId != roster.HostRequestId || host.FormationId != roster.HostFormationId)
        {
            throw new InvalidDataException("Guest vitals require the exact suspended FFVIII payload and matching host battle occurrence.");
        }
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        NativeCharacterVitals vitals = document.Characters.Restore(installed);
        int character = roster.Guest.Combat.NativeId;
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(vitals, assets, character);
        Ff7BattleGuestProjection projection = CrossoverGuestHostActions.Projection(kit);
        return new(roster.EncounterId, Hash(source.Ff8Payload!), roster.Guest.HostSlot, roster.Guest.Story.NativeId, character, kit.RecordRow, kit.HitPoints, projection.MaximumHp, projection.Level, projection.Luck, projection.Defense, projection.MagicDefense)
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
    {
        source.Validate();
        if (source is not { ActiveGame: SessionGame.Ff7, Ff7: { } host, Ff8Payload: { Length: > 0 } payload })
        {
            throw new InvalidDataException("Guest settlement requires the complete active FFVII and suspended FFVIII save payloads.");
        }
        if (host.Battle is null || source.GuestBattleSettlements?.Any(receipt => receipt.EncounterId == host.Battle.GuestVitals?.EncounterId) == true)
        {
            completed ??= source.GuestBattleSettlements?.LastOrDefault();
        }
        if (completed is not null)
        {
            if (host.Battle is not null && completed.Outcome != Ff7BattleOutcome.Defeat || source.GuestBattleSettlements?.Contains(completed) != true || Hash(payload) != completed.SettledFf8Hash || CharacterHash(host, completed.StoryCharacter) != completed.DormantCharacterHash)
            {
                throw new InvalidDataException("The completed guest settlement receipt does not belong to this combined continuation.");
            }
            return new(source, completed);
        }
        if (host.Battle is not { Outcome: not Ff7BattleOutcome.Fighting, GuestVitals: { } binding } battle || Hash(payload) != binding.SourcePayloadSha256 || source.GuestBattleSettlements?.Any(receipt => receipt.EncounterId == binding.EncounterId) == true || host.Progress.CompletedBattles.Any(receipt => receipt.FieldGeneration == host.FieldGeneration && receipt.RequestId == battle.RequestId))
        {
            throw new InvalidOperationException("Guest settlement requires the unsettled terminal host and its exact suspended source payload.");
        }
        GameplaySaveComponentDocument document = CrossoverSaveAdapter.ReadFf8(source);
        if ((battle.GuestAttackUsed || battle.GuestPresentationAttached) != (presentation is not null))
        {
            throw new InvalidDataException("Guest settlement requires the native presentation result exactly when the host executed a guest attack.");
        }
        NativeCharacterVitals original = document.Characters.Restore(installed);
        int row = original.FindPhysicalRow(checked((byte)binding.OriginCharacter));
        NativeCharacterVital saved = original.Records[row];
        if (row != binding.OriginRow || saved.HitPoints != new NativeHpWord(binding.InitialHp, binding.InitialHp) || saved.Status != binding.OriginStatuses || original.GetMagicMaximumHitPoints(row) != binding.MaximumHp)
        {
            throw new InvalidDataException("The suspended FFVIII character record no longer matches the admitted guest vitals.");
        }
        Ff7BattleAiActor projected = battle.Actors.Single(actor => actor.Actor.Slot == binding.HostSlot && actor.Identity == binding.StoryCharacter).Actor;
        NativeCharacterVitals finalStock = original;
        if (magic is not null)
        {
            if (magic.Roster.EncounterId != binding.EncounterId || magic.Roster.Guest.HostSlot != binding.HostSlot || document.Progression?.Clocks.CrtRandom != magic.OriginCrtRandom)
            {
                throw new InvalidDataException("Guest magic settlement has a different encounter or original visual RNG owner.");
            }
            finalStock = RestoreCommittedStock(magic, original, row);
        }
        ushort maximumHp = finalStock.GetMagicMaximumHitPoints(row);
        Ff7BattleGuestProjection currentProjection = battle.GuestProjection ?? new(binding.MaximumHp, binding.Level, binding.Luck, binding.Defense, binding.MagicDefense);
        ushort hp = checked((ushort)projected.HitPoints);
        if (maximumHp != currentProjection.MaximumHp || projected.MaxHitPoints != maximumHp || hp > maximumHp || (projected.Statuses & ~Ff7GuestBattleStatuses.SupportedHost) != 0 || (hp == 0) != ((projected.Statuses & 1) != 0))
        {
            throw new InvalidDataException("Guest settlement requires consistent HP and mapped statuses.");
        }
        NativeHpWord exactHp = finalStock.WithSavedHitPoints(row, hp).Records[row].HitPoints;
        GameplaySaveCharacters characters = document.Characters;
        if (magic is not null)
        {
            characters = document.Characters with { Records = finalStock.Records, Magic = GameplaySaveCharacters.Capture(finalStock).Magic };
        }
        NativeCharacterVital[] rows = (NativeCharacterVital[])characters.Records.Clone();
        rows[row] = rows[row] with { HitPoints = exactHp, Status = Ff7GuestBattleStatuses.ToOrigin(projected.Statuses, saved.Status) };
        GameplaySaveComponentDocument updated = document with { Characters = characters with { Records = rows } };
        if (magic is not null)
        {
            updated = updated with { Progression = updated.Progression! with { Clocks = updated.Progression!.Clocks with { CrtRandom = magic.CrtRandom } } };
        }
        if (items is not null)
        {
            if (items.EncounterId != binding.EncounterId || document.Progression?.Clocks.CrtRandom != items.OriginCrtRandom || magic is not null && magic.CrtRandom != items.CrtRandom)
            {
                throw new InvalidDataException("Guest item settlement has a different encounter or original visual RNG owner.");
            }
            CrossoverGuestItemInventory inventory = items.Inventory.Restore(document.Economy, battle.RequestId, binding.HostSlot, battle.Tick);
            if (inventory.Pending is not null)
            {
                throw new InvalidDataException("Guest item settlement requires every reservation to be spent or refunded.");
            }
            updated = updated with { Economy = document.Economy with { Items = inventory.Slots }, Progression = updated.Progression! with { Clocks = updated.Progression!.Clocks with { CrtRandom = items.CrtRandom } } };
        }
        if (presentation is not null)
        {
            if (presentation.EncounterId != binding.EncounterId || presentation.Invocation is not null || presentation.Presentation is not { Complete: true } && !presentation.Cancelled || presentation.HostTick > battle.Tick || document.Progression?.Clocks.GlobalRandom is not { } originRandom || presentation.OriginGlobalRandom != originRandom)
            {
                throw new InvalidDataException("Guest settlement requires the completed native presentation and its exact original global RNG owner.");
            }
            updated = updated with { Progression = updated.Progression! with { Clocks = updated.Progression!.Clocks with { GlobalRandom = presentation.GlobalRandom } } };
        }
        if (crt is not null)
        {
            if (presentation is null || magic is not null || items is not null || document.Progression is not { } progression || progression.Clocks.CrtRandom != crt.Origin)
            {
                throw new InvalidDataException("Guest attack CRT settlement requires its exact source and completed presentation.");
            }
            updated = updated with { Progression = updated.Progression! with { Clocks = updated.Progression!.Clocks with { CrtRandom = crt.Current } } };
        }
        using MemoryStream ff8 = new();
        GameplaySaveCodec.Write(ff8, updated);
        byte[] settledPayload = ff8.ToArray();
        string dormantBefore = CharacterHash(host, binding.StoryCharacter);
        using Ff7Session staged = Ff7Session.Restore(installation, host);
        bool returnsAfterDefeat = staged.Battle!.ReturnsAfterDefeat;
        bool confirmed = battle.Outcome switch
        {
            Ff7BattleOutcome.Victory => staged.ConfirmGuestBattleVictory(binding),
            Ff7BattleOutcome.Escaped => staged.ConfirmGuestBattleEscape(binding),
            Ff7BattleOutcome.Defeat when returnsAfterDefeat => staged.ConfirmGuestBattleDefeat(binding),
            Ff7BattleOutcome.Defeat => staged.Battle!.ResultReady,
            _ => false
        };
        if (!confirmed)
        {
            throw new InvalidOperationException("The guest victory has not reached the native field-result boundary.");
        }
        Ff7SessionSave settledHost = staged.Capture() with { AudioPresentation = host.AudioPresentation };
        if (CharacterHash(settledHost, binding.StoryCharacter) != dormantBefore)
        {
            throw new InvalidDataException("Guest settlement changed the dormant FFVII story character record.");
        }
        Ff7GuestBattleSettlementReceipt receipt = new(binding.EncounterId, host.FieldGeneration, battle.RequestId, battle.FormationId, binding.StoryCharacter, row, binding.SourcePayloadSha256, Hash(settledPayload), hp, dormantBefore) { Outcome = battle.Outcome };
        CombinedSessionSave settled = source with { Ff7 = settledHost, Ff8Payload = settledPayload, MixedBattlePayload = battle.Outcome == Ff7BattleOutcome.Defeat && !returnsAfterDefeat ? source.MixedBattlePayload : null, GuestBattleSettlements = [.. source.GuestBattleSettlements ?? [], receipt] };
        settled.Validate();
        return new(settled, receipt);
    }

    private static string CharacterHash(Ff7SessionSave host, int storyCharacter)
        => Hash(JsonSerializer.SerializeToUtf8Bytes(host.Progress.Characters.Single(character => character.Id == storyCharacter)));

    private static string Hash(byte[] payload) => Convert.ToHexString(SHA256.HashData(payload));

    private static NativeCharacterVitals RestoreCommittedStock(CrossoverGuestMagicSettlement magic, NativeCharacterVitals original, int row)
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
            if (cost.Ordinal != index + 1 || cost.Spell is not (1 or 4 or 7 or 21) || cost.OriginRow != row)
            {
                throw new InvalidDataException("Guest spell settlement contains a foreign cost occurrence.");
            }
            string action = $"{encounter}:{cost.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            expected = MagicCastingService.Commit(MagicCastingService.Reserve(expected, row, cost.Spell, MagicCastContext.Battle, action), action);
        }
        if (!actual.Records.SequenceEqual(expected.Records) || JsonSerializer.Serialize(actual.MagicSystem) != JsonSerializer.Serialize(expected.MagicSystem))
        {
            throw new InvalidDataException("Guest spell stock or junction state differs from its native committed cost projection.");
        }
        return actual;
    }

}
