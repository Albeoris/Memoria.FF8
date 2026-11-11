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
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Original_field_victory_settles_only_guest_spell_stock_and_visual_random_with_one_envelope_receipt(bool junctioned, bool recovery)
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
        int initialStock = junctioned ? 50 : 4;
        origin = origin.ChangeMagicStock(row, 1, initialStock, MagicOperationReason.ScriptReward, out _);
        if (junctioned)
        {
            origin.Records[row].Junction.MagicJunctions[0] = 1;
            origin = origin.WithSavedHitPoints(row, origin.GetMagicMaximumHitPoints(row));
        }
        if (recovery)
        {
            origin = origin.ChangeMagicStock(row, 21, 2, MagicOperationReason.ScriptReward, out _);
        }
        ushort originalMaximum = origin.GetMagicMaximumHitPoints(row);
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
        bool restoredCap = false;
        for (int tick = 0; tick < 6000 && !battle.ResultReady; tick++)
        {
            if (battle.ReadyPlayer == 0)
            {
                ushort ability = recovery && casts == 0 ? (ushort)21 : (ushort)1;
                Ff7BattleCommandOption fire = Assert.Single(battle.Commands(0), option => option.Command == Ff7BattleCommand.Magic && option.Argument == ability);
                int target = System.Numerics.BitOperations.TrailingZeroCount(fire.ValidTargets);
                Assert.True(battle.SubmitGuestMagic(0, target, ability));
                casts++;
            }
            session.AdvanceTick();
            if (junctioned && !restoredCap && mixed.Actions.Capture().Fire is not null)
            {
                CombinedSessionSave checkpoint = source with { Ff7 = session.Capture(), MixedBattlePayload = mixed.Capture() };
                using Ff7Session cold = Ff7Session.Restore(installation, RoundTrip(checkpoint.Ff7!));
                using CrossoverMixedBattle coldMixed = new(ff8Root!, checkpoint, cold.Battle!);
                Assert.Equal(battle.GuestProjection, cold.Battle!.GuestProjection);
                cold.AdvanceTick();
                session.AdvanceTick();
                Assert.Equal(JsonSerializer.Serialize(session.Battle!.Capture()), JsonSerializer.Serialize(cold.Battle.Capture()));
                Assert.Equal(mixed.Capture(), coldMixed.Capture());
                restoredCap = true;
            }
        }
        Assert.Equal(Ff7BattleOutcome.Victory, battle.Outcome);
        Assert.True(battle.ResultReady);
        Assert.Equal(recovery ? 3 : 2, casts);
        Assert.Equal(originalPayload, source.Ff8Payload);
        CombinedSessionSave terminal = source with { Ff7 = session.Capture(), MixedBattlePayload = mixed.Capture() };
        CrossoverBattleVitalsSettlementResult settled = mixed.Settle(terminal, installation);
        GameplaySaveComponentDocument result = CrossoverSaveAdapter.ReadFf8(settled.Save);
        NativeCharacterVitals updated = result.Characters.Restore(installed);
        Assert.Equal(initialStock - 2, updated.GetMagicCount(row, 1));
        Assert.Equal(recovery ? 1 : 0, updated.GetMagicCount(row, 21));
        Assert.Equal(origin.Records[row].Junction, updated.Records[row].Junction);
        Assert.Equal(originalMaximum, battle.GuestVitals!.MaximumHp);
        Assert.Equal(updated.GetMagicMaximumHitPoints(row), battle.GuestProjection!.MaximumHp);
        if (junctioned)
        {
            Assert.True(updated.GetMagicMaximumHitPoints(row) < originalMaximum);
            Assert.True(restoredCap);
        }
        Assert.Equal(mixed.Actions.CurrentCrtRandom, result.Progression!.Clocks.CrtRandom);
        Assert.Equal(dormant, JsonSerializer.Serialize(settled.Save.Ff7!.Progress.Characters.Single(character => character.Id == 0)));
        Assert.Single(settled.Save.GuestBattleSettlements!);
        Assert.Same(settled.Save, CrossoverBattleVitalsSettlement.PrepareResult(settled.Save, installation, installed).Save);
        Assert.Equal(originalPayload, source.Ff8Payload);
        output.WriteLine($"Original field request {battle.RequestId}: two original Fire casts settled stock{initialStock}->{initialStock - 2}, maxHP{originalMaximum}->{updated.GetMagicMaximumHitPoints(row)} and native CRT once; dormant Cloud preserved.");
    }

    [Theory]
    [InlineData(0u, false, (ushort)1)]
    [InlineData(0u, false, (ushort)7)]
    [InlineData(0u, false, (ushort)4)]
    [InlineData(8u, false, (ushort)1)]
    [InlineData(0x200u, false, (ushort)1)]
    [InlineData(0x04000000u, false, (ushort)1)]
    [InlineData(4u, false, (ushort)1)]
    [InlineData(0x80u, false, (ushort)1)]
    [InlineData(0u, true, (ushort)1)]
    public void Original_elemental_spell_charges_origin_stock_once_and_publishes_one_prepared_host_receipt_after_cold_casting(uint sourceStatus, bool junctioned, ushort ability)
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
        int initialStock = junctioned ? 100 : 4;
        origin = origin.ChangeMagicStock(row, ability, initialStock, MagicOperationReason.ScriptReward, out _);
        if (junctioned)
        {
            origin.Records[row].Junction.MagicJunctions[3] = checked((byte)ability);
            origin = origin.WithSavedHitPoints(row, origin.GetMagicMaximumHitPoints(row));
        }
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(origin, assets);
        string immutableOrigin = JsonSerializer.Serialize(origin);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleSession host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, 6, 14, 24, 17));
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        host.BindGuestAttack(actions);
        Assert.False(string.IsNullOrWhiteSpace(actions.MagicName(ability)));
        while (host.ReadyPlayer != 0)
        {
            host.Advance();
        }
        if (sourceStatus != 0)
        {
            Ff7BattleSnapshot statusHost = host.Capture();
            statusHost = statusHost with
            {
                Actors = statusHost.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { Statuses = sourceStatus }, Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, sourceStatus) } : actor).ToArray(),
                Inventory = [.. statusHost.Inventory.Where(item => item.ItemId != 8), new(8, 1)]
            };
            CrossoverGunbladeHostSnapshot statusAttack = attack.Capture();
            CrossoverGuestHostActionsSnapshot statusActions = actions.Capture();
            host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: statusHost, animations: animations);
            attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, statusAttack);
            actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, statusActions);
            host.BindGuestAttack(actions);
        }
        if (sourceStatus is 4 or 0x80)
        {
            Assert.DoesNotContain(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic);
            Assert.False(host.SubmitGuestMagic(0, 4, ability));
            Assert.Equal(initialStock, actions.WorkingStock.GetMagicCount(row, ability));
            Assert.Empty(actions.WorkingStock.MagicSystem.Reservations);
            Assert.Empty(actions.Capture().CommittedSpells!.Value);
            return;
        }
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic);
        Assert.Equal(ability, option.Argument);
        Assert.Equal(initialStock, option.Cost);
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, ability, 1 << 4));
        while (host.Capture().Action?.Command.GuestAbility != ability)
        {
            host.Advance();
        }
        CrossoverGuestFireHostState pending = actions.Capture().Fire!;
        Assert.NotNull(pending.Prepared.PreparedContact);
        Assert.Equal(initialStock - 1, actions.WorkingStock.GetMagicCount(row, ability));
        Assert.Equal(Math.Min(pending.Prepared.PreparedContact!.SourceBefore.Hp, host.GuestProjection!.MaximumHp), host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints);
        Assert.Equal(actions.WorkingStock.GetMagicMaximumHitPoints(row), host.GuestProjection.MaximumHp);
        if (junctioned)
        {
            Assert.Equal(host.GuestVitals!.MaximumHp, host.GuestProjection.MaximumHp);
            Assert.True(actions.DescribeMagic(ability).Source.Magic < pending.Prepared.Source.Magic);
        }
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
            if (host.LastHit is { Source: 0, Target: 4 } contact && contact.AttackId == ability)
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
                Assert.Equal(initialStock - 1, actions.WorkingStock.GetMagicCount(row, ability));
                Assert.Equal(immutableOrigin, JsonSerializer.Serialize(origin));
                Assert.Equal(JsonSerializer.Serialize(uninterrupted.Capture()), JsonSerializer.Serialize(host.Capture()));
                Assert.Equal(JsonSerializer.Serialize(uninterruptedActions.Capture()), JsonSerializer.Serialize(actions.Capture()));
                Assert.Equal(3, actions.PeekSoundRequests().Length);
                Assert.Equal(new[] { CrossoverGuestAudioKind.Module, CrossoverGuestAudioKind.Module, ability == 1 ? CrossoverGuestAudioKind.Fire : CrossoverGuestAudioKind.Module }, actions.PeekSoundRequests().Select(receipt => receipt.Kind));
                Assert.All(actions.PeekSoundRequests(), receipt => Assert.NotNull(receipt.Sound.Allocation));
                Assert.Equal(0, host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.MagicPoints);
                if (sourceStatus == Ff7NativeBattleActionResolver.Poison)
                {
                    for (int recoveryTick = 0; recoveryTick < 4000 && host.ReadyPlayer != 0; recoveryTick++)
                    {
                        host.Advance();
                    }
                    Ff7BattleActorState poisoned = host.Actors.Single(actor => actor.Actor.Slot == 0);
                    Assert.True(host.ReadyPlayer == 0, $"Poison recovery has outcome {host.Outcome}, hp {poisoned.Actor.HitPoints}, status {poisoned.Actor.Statuses:X8}, gauge {poisoned.Gauge}, ready {poisoned.AwaitingTurn}, action {host.Capture().Action?.Command.CommandId} at tick {host.Tick}.");
                    Assert.True(host.Submit(0, Ff7BattleCommand.Item, 8, 1));
                    for (int recoveryTick = 0; recoveryTick < 2000 && (host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses & Ff7NativeBattleActionResolver.Poison) != 0; recoveryTick++)
                    {
                        host.Advance();
                    }
                    Assert.Equal(0u, host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses & Ff7NativeBattleActionResolver.Poison);
                    Assert.Equal(initialStock - 1, actions.WorkingStock.GetMagicCount(row, ability));
                }
                output.WriteLine($"Original spell{ability} status{sourceStatus:X8}, junctioned{junctioned}: prepared {hit.Damage.Damage}, contact host tick {hit.Tick}, origin stock {initialStock}->{initialStock - 1} across two cold continuations.");
                return;
            }
        }
        throw new InvalidOperationException("The original guest Fire did not return through its one host action.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Original_Cure_recovers_self_with_one_native_stock_debit_and_cold_hidden_actor(bool junctioned)
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
        int initialStock = 50;
        origin = origin.ChangeMagicStock(row, 21, initialStock, MagicOperationReason.ScriptReward, out _);
        if (junctioned)
        {
            origin.Records[row].Junction.MagicJunctions[0] = 21;
            origin = origin.WithSavedHitPoints(row, origin.GetMagicMaximumHitPoints(row));
        }
        origin = origin.WithSavedHitPoints(row, 100);
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.ReadSquall(origin, assets);
        string immutableOrigin = JsonSerializer.Serialize(origin);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7BattleSession host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, 6, 14, 24, 17));
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets);
        host.BindGuestAttack(actions);
        while (host.ReadyPlayer != 0)
        {
            host.Advance();
        }
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic);
        Assert.Equal((ushort)21, option.Argument);
        Assert.Equal((ushort)1, option.ValidTargets);
        Assert.False(option.DefaultsToEnemies);
        Assert.True(host.Submit(0, Ff7BattleCommand.Magic, 21, 1));
        while (actions.Invocation is null)
        {
            host.Advance();
        }
        CrossoverGuestFireHostState pending = actions.Capture().Fire!;
        Assert.Equal(49, actions.WorkingStock.GetMagicCount(row, 21));
        Assert.Equal(100u, host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints);
        Assert.True(pending.Prepared.PreparedContact!.Receipt.Damage.Damage < 0);
        bool restored = false;
        int publications = 0;
        for (int tick = 0; tick < 1000 && actions.Invocation is not null; tick++)
        {
            if (!restored && actions.Capture().Fire?.Presentation.Cure is { Model.Flags0: var flags } && (flags & 4) != 0)
            {
                Ff7BattleSnapshot savedHost = RoundTrip(host.Capture());
                CrossoverGunbladeHostSnapshot savedAttack = RoundTrip(attack.Capture());
                CrossoverGuestHostActionsSnapshot savedActions = RoundTrip(actions.Capture());
                host = new(formation, original.Progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: savedHost, animations: animations);
                attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128, savedAttack);
                actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, savedActions);
                host.BindGuestAttack(actions);
                restored = true;
            }
            host.Advance();
            if (host.LastHit is { Source: 0, Target: 0, AttackId: 21 } hit && hit.Tick == host.Tick)
            {
                publications++;
                Assert.Null(host.Capture().Reaction);
                Assert.Equal(pending.Prepared.PreparedContact.Receipt.Damage, hit.Damage);
            }
        }
        Assert.True(restored);
        Assert.Null(actions.Invocation);
        Assert.Equal(1, publications);
        Assert.Equal(pending.Prepared.PreparedContact.Receipt.Target.Hp, host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints);
        Assert.Equal(49, actions.WorkingStock.GetMagicCount(row, 21));
        Assert.Single(actions.Capture().CommittedSpells!.Value);
        Assert.Equal((ushort)21, actions.Capture().CommittedSpells!.Value[0].Spell);
        Assert.Equal(immutableOrigin, JsonSerializer.Serialize(origin));
        Assert.Equal(actions.WorkingStock.GetMagicMaximumHitPoints(row), host.GuestProjection!.MaximumHp);
        Assert.Equal(3, actions.PeekSoundRequests().Length);
        output.WriteLine($"Cure self healed {-pending.Prepared.PreparedContact.Receipt.Damage.Damage}, one debit50->49, junctioned{junctioned}, hidden actor cold restore, one contact.");
    }

    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
