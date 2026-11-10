using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestMagicHostTests(ITestOutputHelper output)
{
    [Fact]
    public void Original_field_victory_settles_only_guest_spell_stock_and_visual_random_with_one_envelope_receipt()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using GameAssets game = new(ff8Root!);
        NativeCharacterVitals installed = game.ReadNewGameCharacterVitals();
        NativeCharacterVitals origin = installed.DeepCopy();
        int row = origin.FindPhysicalRow(0);
        int magicAbility = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)magicAbility } };
        origin = origin.ChangeMagicStock(row, 1, 4, MagicOperationReason.ScriptReward, out _);
        GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(game, world: false) with { Characters = GameplaySaveCharacters.Capture(origin) };
        using MemoryStream payload = new();
        GameplaySaveCodec.Write(payload, document);
        byte[] originalPayload = payload.ToArray();
        using Ff7Session session = Ff7Session.NewGame(installation);
        session.SetRandomEncountersEnabled(false);
        session.SetFullCombatEnabled(true);
        for (int tick = 0; tick < 4500 && session.Battle is null; tick++)
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
        Ff7BattleSession battle = Assert.IsType<Ff7BattleSession>(session.Battle);
        string dormant = JsonSerializer.Serialize(session.Progress.Characters.Single(character => character.Id == 0));
        CombinedSessionSave source = new() { ActiveGame = SessionGame.Ff7, Ff7 = session.Capture(), Ff8Payload = originalPayload, Ff8PayloadFormat = CrossoverSaveAdapter.Ff8Codec, MixedCombat = new(0) };
        using CrossoverMixedBattle mixed = new(ff8Root!, source, battle);
        int casts = 0;
        for (int tick = 0; tick < 6000 && !battle.ResultReady; tick++)
        {
            if (battle.ReadyPlayer == 0)
            {
                Ff7BattleCommandOption fire = Assert.Single(battle.Commands(0), option => option.Command == Ff7BattleCommand.Magic);
                int target = System.Numerics.BitOperations.TrailingZeroCount(fire.ValidTargets);
                Assert.True(battle.SubmitGuestMagic(0, target));
                casts++;
            }
            session.AdvanceTick();
        }
        Assert.Equal(Ff7BattleOutcome.Victory, battle.Outcome);
        Assert.True(battle.ResultReady);
        Assert.Equal(2, casts);
        Assert.Equal(originalPayload, source.Ff8Payload);
        CombinedSessionSave terminal = source with { Ff7 = session.Capture(), MixedBattlePayload = mixed.Capture() };
        CrossoverBattleVitalsSettlementResult settled = mixed.Settle(terminal, installation);
        GameplaySaveComponentDocument result = CrossoverSaveAdapter.ReadFf8(settled.Save);
        NativeCharacterVitals updated = result.Characters.Restore(installed);
        Assert.Equal(2, updated.GetMagicCount(row, 1));
        Assert.Equal(mixed.Actions.CurrentCrtRandom, result.Progression!.Clocks.CrtRandom);
        Assert.Equal(dormant, JsonSerializer.Serialize(settled.Save.Ff7!.Progress.Characters.Single(character => character.Id == 0)));
        Assert.Single(settled.Save.GuestBattleSettlements!);
        Assert.Same(settled.Save, CrossoverBattleVitalsSettlement.PrepareResult(settled.Save, installation, installed).Save);
        Assert.Equal(originalPayload, source.Ff8Payload);
        output.WriteLine($"Original field request {battle.RequestId}: two original Fire casts settled stock4->2 and native CRT once; dormant Cloud preserved.");
    }

    [Fact]
    public void Original_Fire_charges_origin_stock_once_and_publishes_one_prepared_host_receipt_after_cold_casting()
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = origin.FindPhysicalRow(0);
        int magicAbility = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)2) + 20;
        origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)magicAbility } };
        origin = origin.ChangeMagicStock(row, 1, 4, MagicOperationReason.ScriptReward, out _);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(origin, assets);
        string immutableOrigin = JsonSerializer.Serialize(origin);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation));
        Ff7BattleSession host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, 6, 14, 24, 17));
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        host.BindGuestAttack(actions);
        Assert.False(string.IsNullOrWhiteSpace(actions.MagicName(1)));
        while (host.ReadyPlayer != 0)
        {
            host.Advance();
        }
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic);
        Assert.Equal((ushort)1, option.Argument);
        Assert.Equal(4, option.Cost);
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, 1, 1 << 4));
        while (host.Capture().Action is not { Command.GuestAbility: 1 })
        {
            host.Advance();
        }
        CrossoverGuestFireHostState pending = actions.Capture().Fire!;
        Assert.NotNull(pending.Prepared.PreparedContact);
        Assert.Equal(3, actions.WorkingStock.GetMagicCount(row, 1));
        Assert.Single(actions.WorkingStock.MagicSystem.CommittedActionIds, id => id.StartsWith($"mixed:{roster.EncounterId:N}:", StringComparison.Ordinal));
        Assert.Equal(pending.Prepared.PreparedContact!.TargetBefore.Hp, host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints);
        Ff7BattleSession uninterrupted = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: RoundTrip(host.Capture()), animations: animations);
        CrossoverGunbladeHostAttack uninterruptedAttack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => uninterrupted.Data.Cameras[0], _ => 128, RoundTrip(attack.Capture()));
        CrossoverGuestHostActions uninterruptedActions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), uninterruptedAttack, uninterrupted, game, assets, RoundTrip(actions.Capture()));
        uninterrupted.BindGuestAttack(uninterruptedActions);
        for (int tick = 0; tick < 1000 && uninterruptedActions.Invocation is not null; tick++)
        {
            uninterrupted.Advance();
        }
        Assert.Null(uninterruptedActions.Invocation);
        bool restoredCasting = false;
        bool restoredContact = false;
        Ff7BattleHit? hit = null;
        for (int tick = 0; tick < 1000; tick++)
        {
            CrossoverGuestHostActionsSnapshot captured = actions.Capture();
            if (!restoredCasting && captured.Fire?.Presentation.Modules.PublishedCount > 0 || !restoredContact && hit is not null)
            {
                Ff7BattleSnapshot savedHost = RoundTrip(host.Capture());
                CrossoverGunbladeHostSnapshot savedAttack = RoundTrip(attack.Capture());
                CrossoverGuestHostActionsSnapshot savedActions = RoundTrip(captured);
                host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: savedHost, animations: animations);
                attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, savedAttack);
                actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, savedActions);
                host.BindGuestAttack(actions);
                restoredCasting = true;
                restoredContact |= hit is not null;
            }
            host.Advance();
            if (host.LastHit is { Source: 0 } contact)
            {
                Assert.Equal(pending.Prepared.PreparedContact.Receipt.Damage, contact.Damage);
                Assert.Equal(pending.Prepared.PreparedContact.Receipt.Target.Hp, host.Actors.Single(actor => actor.Actor.Slot == 4).Actor.HitPoints);
                if (hit is null)
                {
                    hit = contact;
                }
                else
                {
                    Assert.Equal(hit, contact);
                }
            }
            if (hit is not null && actions.Invocation is null)
            {
                Assert.True(restoredCasting && restoredContact);
                Assert.Equal(3, actions.WorkingStock.GetMagicCount(row, 1));
                Assert.Equal(immutableOrigin, JsonSerializer.Serialize(origin));
                Assert.Equal(JsonSerializer.Serialize(uninterrupted.Capture()), JsonSerializer.Serialize(host.Capture()));
                Assert.Equal(JsonSerializer.Serialize(uninterruptedActions.Capture()), JsonSerializer.Serialize(actions.Capture()));
                Assert.Equal(0, host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.MagicPoints);
                output.WriteLine($"Original Fire prepared {hit.Damage.Damage} damage, contact host tick {hit.Tick}, return {host.Tick}, origin stock 4->3 across two cold continuations.");
                return;
            }
        }
        throw new InvalidOperationException("The original guest Fire did not return through its one host action.");
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
