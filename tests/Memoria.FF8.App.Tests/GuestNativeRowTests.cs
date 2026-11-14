using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GuestNativeRowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Expected_hash_projects_only_native_status_and_row_and_retains_every_other_character_byte(bool back)
    {
        byte[] record = Enumerable.Range(0, 132).Select(value => (byte)value).ToArray();
        Ff7CharacterState original = new(0, "Cloud", 6, 123, 314, 54, record);
        Ff7BattleAiActor actor = new(0, Ff7NativeBattleStatuses.Fury, back ? 0x40u : 0u, 0, 0, 486, 486);
        byte[] expected = record.ToArray();
        expected[31] = (byte)((expected[31] & ~Ff7GuestBattleStatuses.SupportedNativeOnly) | Ff7NativeBattleStatuses.Fury);
        expected[32] = (byte)((expected[32] & ~1) | (back ? 0 : 1));
        Ff7CharacterState projected = original with { NativeRecord = expected };
        string hash = CrossoverBattleVitalsSettlement.ExpectedCharacterHash(original, actor);
        Assert.Equal(Hash(projected), hash);
        Assert.Equal(Enumerable.Range(0, 132).Select(value => (byte)value), original.NativeRecord);
        for (int index = 0; index < expected.Length; index++)
        {
            byte[] tampered = expected.ToArray();
            tampered[index] ^= 0x80;
            Assert.NotEqual(hash, Hash(projected with { NativeRecord = tampered }));
        }
        Assert.NotEqual(hash, Hash(projected with { HitPoints = projected.HitPoints - 1 }));
        Assert.NotEqual(hash, Hash(projected with { Name = "changed" }));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Order_projection_shows_and_enables_the_underlying_Ff7_row_for_a_mapped_guest(bool back)
    {
        byte[] record = new byte[132];
        record[32] = (byte)(0xA4 | (back ? 0 : 1));
        Ff7Progress progress = new() { InitialKernelData = [1], Characters = [new(0, "Cloud", 6, 0, 314, 54, record)], Party = [0, 255, 255], Inventory = [], Materia = [] };
        MenuCharacter guest = new() { Id = "ff7-character/0", Name = "Squall", InParty = true };
        MenuEntry row = Assert.Single(Ff7Scene.ReadNativeOrderEntries([guest], progress, 0));
        Assert.True(row.Enabled);
        Assert.Equal("Squall   " + (back ? "Back" : "Front"), row.Label);
        Assert.Equal("Switch this character's front/back row.", row.Help);
        Assert.Equal(new MenuAction("ff7-order-apply", guest.Id), row.Action);
        Assert.Equal((byte)(0xA4 | (back ? 0 : 1)), record[32]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Escape_settlement_publishes_native_guest_row_and_fury_once_with_exact_origin_and_full_hash_checks(bool back)
    {
        string? ff7 = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8 = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7) && !string.IsNullOrWhiteSpace(ff8), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7!);
        using GameAssets game = new(ff8!);
        using BattleAssets assets = new(ff8!);
        NativeCharacterVitals installed = game.ReadNewGameCharacterVitals();
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, world: false) with { Characters = GameplaySaveCharacters.Capture(installed) };
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        byte[] origin = payload.ToArray();
        using Ff7Session initial = Ff7Session.NewGame(installation);
        initial.SetFullCombatEnabled(true);
        Ff7SessionSave initialSave = initial.Capture();
        Ff7CharacterState original = initialSave.Progress.Characters.Single(character => character.Id == 0);
        byte[] record = original.NativeRecord.ToArray();
        record[31] = (byte)(0x84 | Ff7NativeBattleStatuses.Sadness);
        record[32] = (byte)(0xA4 | (back ? 1 : 0));
        original = original with { NativeRecord = record };
        using Ff7Session session = Ff7Session.Restore(installation, initialSave with { Progress = initialSave.Progress with { Characters = initialSave.Progress.Characters.Select(character => character.Id == 0 ? original : character).ToArray() } });
        session.Host.StartEncounter(new(session.Progress.NextEncounterRequestId, 0, 7, 0, new FieldBattleCommand(301)), 0);
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(session.Battle);
        CombinedSessionSave envelope = new() { ActiveGame = SessionGame.Ff7, Ff7 = session.Capture(), Ff8Payload = origin, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec };
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), battle.Capture(), [new(0, 0, 0)], []);
        Ff7BattleGuestVitalsBinding binding = CrossoverBattleVitalsSettlement.ReadBinding(envelope, roster, installed, assets);
        battle.BindGuestVitals(binding);
        Ff7BattleSnapshot snapshot = battle.Capture();
        Ff7BattleAiActor changed = snapshot.Actors.Single(actor => actor.Actor.Slot == 0).Actor with { Statuses = Ff7NativeBattleStatuses.Fury, Conditions = (snapshot.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Conditions & ~0x40u) | (back ? 0x40u : 0u) };
        Ff7BattleSnapshot terminal = snapshot with { Outcome = Ff7BattleOutcome.Escaped, TerminalFrames = 8, Actors = snapshot.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = changed } : actor).ToArray() };
        envelope = envelope with { Ff7 = session.Capture() with { Battle = terminal } };
        string before = JsonSerializer.Serialize(envelope);
        CrossoverBattleVitalsSettlementResult settled = CrossoverBattleVitalsSettlement.PrepareResult(envelope, installation, installed);
        Ff7SessionSave settledHost = settled.Save.Ff7!;
        Ff7CharacterState expected = Ff7NativeBattlePartyRecord.ProjectStatusesAndRow(original, changed);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(settledHost.Progress.Characters.Single(character => character.Id == 0)));
        Assert.Equal(Hash(expected), settled.Receipt.DormantCharacterHash);
        Assert.Equal(JsonSerializer.Serialize(document.Characters), JsonSerializer.Serialize(CrossoverSaveAdapter.ReadFf8(settled.Save).Characters));
        Assert.Equal(before, JsonSerializer.Serialize(envelope));
        Assert.Equal(origin, envelope.Ff8Payload);
        Assert.Same(settled.Save, CrossoverBattleVitalsSettlement.PrepareResult(settled.Save, installation, installed).Save);
        CombinedSessionSave cold = JsonSerializer.Deserialize<CombinedSessionSave>(JsonSerializer.Serialize(settled.Save))!;
        Assert.Same(cold, CrossoverBattleVitalsSettlement.PrepareResult(cold, installation, installed).Save);
        byte[] corrupt = expected.NativeRecord.ToArray();
        corrupt[33] ^= 1;
        Ff7SessionSave corruptHost = settledHost with { Progress = settledHost.Progress with { Characters = settledHost.Progress.Characters.Select(character => character.Id == 0 ? character with { NativeRecord = corrupt } : character).ToArray() } };
        Assert.Throws<InvalidDataException>(() => CrossoverBattleVitalsSettlement.PrepareResult(settled.Save with { Ff7 = corruptHost }, installation, installed));
    }

    private static string Hash(Ff7CharacterState character) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(character)));
}
