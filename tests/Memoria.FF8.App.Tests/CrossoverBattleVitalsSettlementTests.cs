using System.Text.Json;
using Memoria.FF7.Resources;
using Memoria.FF7.Events;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverBattleVitalsSettlementTests(ITestOutputHelper output)
{
    [Fact]
    public void Opening_MP_damage_recovery_and_victory_preserve_Cloud_and_update_the_exact_Squall_payload()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        NativeCharacterVitals installed = game.ReadNewGameCharacterVitals();
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, world: false) with
        {
            Characters = GameplaySaveCharacters.Capture(installed),
            Economy = GameplaySaveEconomy.Capture(game.ReadNewGameEconomy()),
            Limits = GameplaySaveLimitProgress.Capture(game.ReadNewGameLimitProgress())
        };
        using MemoryStream originalPayload = new();
        GameplaySaveCodec.Write(originalPayload, document);
        byte[] sourcePayload = originalPayload.ToArray();
        using Ff7Session host = Ff7Session.NewGame(installation);
        host.SetRandomEncountersEnabled(false);
        host.SetFullCombatEnabled(true);
        for (int tick = 0; tick < 4500 && host.Battle is null; tick++)
        {
            StepOpening(host, tick);
        }
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(host.Battle);
        Assert.Equal((300, 0L), (battle.Formation.FormationId, battle.Tick));
        Assert.True(host.Host.HasUnfinishedBattle);
        string originalCloud = JsonSerializer.Serialize(host.Progress.Characters.Single(character => character.Id == 0));
        CombinedSessionSave source = new() { ActiveGame = SessionGame.Ff7, Ff7 = host.Capture(), Ff8Payload = sourcePayload, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec };
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), battle.Capture(), [new(0, 0, 0)], []);
        Ff7BattleGuestVitalsBinding binding = CrossoverBattleVitalsSettlement.ReadBinding(source, roster, installed, assets);
        Assert.Equal((486, 486, 24, 17), ((int)binding.InitialHp, (int)binding.MaximumHp, (int)binding.Defense, (int)binding.MagicDefense));
        battle.BindGuestVitals(binding);
        Assert.Equal((uint)binding.InitialHp, GuestHp(battle));
        Assert.Throws<InvalidDataException>(() => battle.BindGuestVitals(binding with { EncounterId = Guid.NewGuid() }));
        for (int tick = 0; tick < 2000 && battle.LastHit is null; tick++)
        {
            host.AdvanceTick();
        }
        Ff7BattleHit incoming = Assert.IsType<Ff7BattleHit>(battle.LastHit);
        Assert.Equal((4, 0, 273), (incoming.Source, incoming.Target, (int)incoming.AttackId));
        Assert.Equal((uint)(binding.InitialHp - incoming.Damage.Damage), GuestHp(battle));
        Assert.Equal(7, incoming.Damage.Damage);
        Assert.Equal(originalCloud, JsonSerializer.Serialize(host.Progress.Characters.Single(character => character.Id == 0)));
        Assert.Equal((byte)0, battle.Actors.Single(actor => actor.Actor.Slot == 0).LimitGauge);
        using Ff7Session cold = Ff7Session.Restore(installation, RoundTrip(host.Capture()));
        battle = Assert.IsType<Ff7BattleSession>(cold.Battle);
        battle.BindGuestVitals(binding);
        Assert.Equal(479U, GuestHp(battle));
        using (Ff7Session defeated = Ff7Session.Restore(installation, RoundTrip(cold.Capture())))
        {
            Ff7BattleSession lethal = Assert.IsType<Ff7BattleSession>(defeated.Battle);
            for (int tick = 0; tick < 60000 && !lethal.ResultReady; tick++)
            {
                defeated.AdvanceTick();
            }
            Assert.Equal(Ff7BattleOutcome.Defeat, lethal.Outcome);
            Assert.Equal((0U, 1U), (GuestHp(lethal), lethal.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses));
            Assert.False(defeated.ConfirmBattleDefeat());
            Assert.Equal(originalCloud, JsonSerializer.Serialize(defeated.Progress.Characters.Single(character => character.Id == 0)));
            Assert.Throws<InvalidOperationException>(() => CrossoverBattleVitalsSettlement.PrepareVictory(source with { Ff7 = defeated.Capture() }, installation, installed));
            CrossoverBattleVitalsSettlementResult loss = CrossoverBattleVitalsSettlement.PrepareResult(source with { Ff7 = defeated.Capture() }, installation, installed);
            Assert.Equal(Ff7BattleOutcome.Defeat, loss.Receipt.Outcome);
            Assert.Equal(new NativeHpWord(0, 0), CrossoverSaveAdapter.ReadFf8(loss.Save).Characters.Records[binding.OriginRow].HitPoints);
            Assert.Equal((ushort)1, CrossoverSaveAdapter.ReadFf8(loss.Save).Characters.Records[binding.OriginRow].Status);
            Assert.Same(loss.Save, CrossoverBattleVitalsSettlement.PrepareResult(loss.Save, installation, installed).Save);
        }
        for (int tick = 0; tick < 2000 && battle.ReadyPlayer != 0; tick++)
        {
            cold.AdvanceTick();
        }
        Assert.Equal(0, battle.ReadyPlayer);
        Assert.False(battle.Submit(0, Ff7BattleCommand.Attack, 0, 16));
        int potionBefore = battle.Capture().Inventory.Single(item => item.ItemId == 0).Quantity;
        Assert.True(battle.Submit(0, Ff7BattleCommand.Item, 0, 1));
        for (int tick = 0; tick < 2000 && battle.LastHit is not { Source: 0, Target: 0, AttackId: 0 }; tick++)
        {
            cold.AdvanceTick();
        }
        Assert.Equal((uint)binding.MaximumHp, GuestHp(battle));
        Assert.Equal(potionBefore - 1, battle.Capture().Inventory.SingleOrDefault(item => item.ItemId == 0)?.Quantity ?? 0);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(document.Characters.Restore(installed), assets);
        CrossoverGunbladeHostAttack gunblade = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => battle.Data.Cameras[0], _ => 128);
        battle.BindGuestAttack(gunblade);
        for (int tick = 0; tick < 6500 && !battle.ResultReady; tick++)
        {
            if (battle.ReadyPlayer == 0)
            {
                Ff7BattleActorState? enemy = battle.Actors.FirstOrDefault(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints != 0);
                if (enemy is not null)
                {
                    Assert.True(battle.SubmitGuestAttack(0, enemy.Actor.Slot));
                }
            }
            if (gunblade.TriggerWindowOpen)
            {
                gunblade.PressTrigger();
            }
            cold.AdvanceTick();
        }
        Assert.Equal(Ff7BattleOutcome.Victory, battle.Outcome);
        Assert.True(battle.ResultReady);
        Ff7BattleReward expectedReward = battle.PreviewVictory();
        int potionDrops = expectedReward.Items.Where(item => item.ItemId == 0).Sum(item => item.Quantity);
        Assert.False(cold.ConfirmBattleVictory());
        ushort expectedHp = checked((ushort)GuestHp(battle));
        Assert.InRange(expectedHp, 1, binding.InitialHp - 1);
        CombinedSessionSave terminal = source with { Ff7 = cold.Capture() };
        string before = JsonSerializer.Serialize(terminal);
        byte[] wrongOrigin = (byte[])sourcePayload.Clone();
        wrongOrigin[^1] ^= 1;
        Assert.Throws<InvalidOperationException>(() => CrossoverBattleVitalsSettlement.PrepareVictory(terminal with { Ff8Payload = wrongOrigin }, installation, installed));
        CrossoverBattleVitalsSettlementResult staged = CrossoverBattleVitalsSettlement.PrepareVictory(terminal, installation, installed, presentation: gunblade.Capture());
        Assert.Equal(before, JsonSerializer.Serialize(terminal));
        Assert.True(cold.Host.HasUnfinishedBattle);
        Assert.Equal((uint)expectedHp, GuestHp(battle));
        Assert.Equal((expectedHp, roster.EncounterId), (staged.Receipt.SettledHp, staged.Receipt.EncounterId));
        Assert.Null(staged.Save.Ff7!.Battle);
        Assert.Equal(expectedReward.Items, Assert.Single(staged.Save.Ff7.Progress.CompletedBattles).Reward.Items);
        Assert.Equal(originalCloud, JsonSerializer.Serialize(staged.Save.Ff7.Progress.Characters.Single(character => character.Id == 0)));
        Assert.Equal(Math.Min(99, potionBefore - 1 + potionDrops), staged.Save.Ff7.Progress.Inventory.SingleOrDefault(item => item.ItemId == 0)?.Quantity ?? 0);
        GameplaySaveComponentDocument updated = CrossoverSaveAdapter.ReadFf8(staged.Save);
        NativeCharacterVital original = document.Characters.Records[binding.OriginRow];
        NativeCharacterVital changed = updated.Characters.Records[binding.OriginRow];
        Assert.Equal(original with { HitPoints = new(expectedHp, expectedHp) }, changed);
        NativeCharacterVital[] reverted = (NativeCharacterVital[])updated.Characters.Records.Clone();
        reverted[binding.OriginRow] = original;
        Assert.Equal(gunblade.Capture().GlobalRandom, updated.Progression!.Clocks.GlobalRandom);
        Assert.NotEqual(document.Progression!.Clocks.GlobalRandom, updated.Progression.Clocks.GlobalRandom);
        Assert.Equal(JsonSerializer.Serialize(document), JsonSerializer.Serialize(updated with { Characters = updated.Characters with { Records = reverted }, Progression = updated.Progression with { Clocks = updated.Progression.Clocks with { GlobalRandom = document.Progression.Clocks.GlobalRandom } } }));
        GameState restoredOrigin = GameplaySaveSessionLoader.Restore(updated, ff8Root!, sharedAssets: game);
        Assert.Equal(new NativeHpWord(expectedHp, expectedHp), restoredOrigin.CharacterVitals!.Records[binding.OriginRow].HitPoints);
        Assert.Equal(gunblade.Capture().GlobalRandom, restoredOrigin.GlobalRandom);
        using MemoryStream combined = new();
        CombinedSessionCodec.Write(combined, staged.Save);
        combined.Position = 0;
        CombinedSessionSave settled = CombinedSessionCodec.Read(combined);
        CrossoverBattleVitalsSettlementResult retry = CrossoverBattleVitalsSettlement.PrepareVictory(settled, installation, installed, RoundTrip(staged.Receipt));
        Assert.Same(settled, retry.Save);
        Assert.Same(settled, CrossoverBattleVitalsSettlement.PrepareVictory(settled, installation, installed).Save);
        Assert.Single(settled.GuestBattleSettlements!);
        Assert.Throws<InvalidDataException>(() => (settled with { GuestBattleSettlements = [staged.Receipt, staged.Receipt] }).Validate());
        Assert.Throws<InvalidDataException>(() => (settled with { GuestBattleSettlements = [staged.Receipt with { RequestId = staged.Receipt.RequestId + 1 }] }).Validate());
        CrossoverBattleVitalsSettlementResult independent = CrossoverBattleVitalsSettlement.PrepareVictory(terminal, installation, installed, presentation: gunblade.Capture());
        Assert.Equal(JsonSerializer.Serialize(staged), JsonSerializer.Serialize(independent));
        using (Ff7Session escapeSession = Ff7Session.Restore(installation, settled.Ff7!))
        {
            escapeSession.AdvanceTick();
            long request = escapeSession.Progress.NextEncounterRequestId;
            int entity = escapeSession.Host.Process.Actors.Single(actor => actor.PlayerControlled).Entity;
            escapeSession.Host.StartEncounter(new(request, entity, 7, 0, new FieldBattleCommand(301)), 0);
            Ff7BattleSession escaping = Assert.IsType<Ff7BattleSession>(escapeSession.Battle);
            CombinedSessionSave escapeSource = settled with { Ff7 = escapeSession.Capture() };
            CrossoverBattleRoster nextRoster = CrossoverBattleRoster.Create(Guid.NewGuid(), escaping.Capture(), [new(0, 0, 0)], []);
            Ff7BattleGuestVitalsBinding nextBinding = CrossoverBattleVitalsSettlement.ReadBinding(escapeSource, nextRoster, installed, assets);
            escaping.BindGuestVitals(nextBinding);
            for (int tick = 0; tick < 6000 && !escaping.ResultReady; tick++)
            {
                escapeSession.AdvanceTick(advanceClock: false, escapeBattleHeld: true);
            }
            Assert.Equal(Ff7BattleOutcome.Escaped, escaping.Outcome);
            Assert.False(escapeSession.ConfirmBattleEscape());
            CrossoverBattleVitalsSettlementResult escaped = CrossoverBattleVitalsSettlement.PrepareResult(escapeSource with { Ff7 = escapeSession.Capture() }, installation, installed);
            Assert.Equal(Ff7BattleOutcome.Escaped, escaped.Receipt.Outcome);
            Assert.Null(escaped.Save.Ff7!.Battle);
            Assert.Equal((ushort)1, escaped.Save.Ff7.Progress.BattleEscapes);
            Assert.Single(escaped.Save.Ff7.Progress.CompletedBattles);
            Assert.Equal(originalCloud, JsonSerializer.Serialize(escaped.Save.Ff7.Progress.Characters.Single(character => character.Id == 0)));
            Assert.Same(escaped.Save, CrossoverBattleVitalsSettlement.PrepareResult(escaped.Save, installation, installed).Save);
        }
        output.WriteLine($"Squall HP {binding.InitialHp} -> {binding.InitialHp - incoming.Damage.Damage} -> {binding.MaximumHp} -> {expectedHp}; native opening result {staged.Receipt.RequestId}");
    }

    private static uint GuestHp(Ff7BattleSession battle) => battle.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints;

    private static T RoundTrip<T>(T source) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(source))!;

    private static void StepOpening(Ff7Session session, int tick)
    {
        if (session.Host.Process.Movie is { Playing: true } movie)
        {
            session.Host.PublishMovieFrame(Math.Min(movie.Frame + tick % 2, 1799), movie.Frame >= 1799);
        }
        if (tick % 30 == 0)
        {
            foreach (Ff7Dialogue dialogue in session.Host.Process.Dialogues.Where(dialogue => !dialogue.Completed))
            {
                session.Host.CompleteDialogue(dialogue.RequestId, dialogue.FirstChoice ?? 0);
            }
        }
        if (tick >= 4110 && !session.Host.Process.PlayerLocked)
        {
            session.MovePlayer(0, -1, true);
        }
        session.AdvanceTick();
    }
}
