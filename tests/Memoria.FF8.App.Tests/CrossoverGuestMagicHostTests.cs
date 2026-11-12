using System.Text.Json;
using Memoria.FF7.Formats;
using Memoria.FF7.Events;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CrossoverGuestMagicHostTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    public void Original_field_victory_settles_only_guest_spell_stock_and_visual_random_with_one_envelope_receipt(bool junctioned, int recovery)
    {
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using GameAssets game = new(ff8Root!);
        using BattleAssets diagnostics = new(ff8Root!);
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
        if (recovery == 1)
        {
            origin = origin.ChangeMagicStock(row, 21, 2, MagicOperationReason.ScriptReward, out _);
        }
        if (recovery == 2)
        {
            int itemAbility = origin.MenuDefinitions.CommandAbilityCommands.IndexOf((byte)4) + 20;
            origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)(magicAbility | itemAbility << 8) } };
            origin = origin.WithSavedHitPoints(row, 100);
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
        HashSet<int> fadingEnemies = [];
        int reportedContacts = 0;
        bool observedReprojection = false;
        bool healedByPotion = false;
        int originalPotion = document.Economy.Items.Single(stock => stock.ItemId == 1).Count;
        int hostPotion = battle.Capture().Inventory.Single(item => item.ItemId == 0).Quantity;
        for (int tick = 0; tick < 6000 && !battle.ResultReady; tick++)
        {
            if (battle.ReadyPlayer == 0 && (recovery != 2 || casts != 0 || battle.Action is null))
            {
                if (recovery == 2 && casts == 0)
                {
                    Ff7BattleCommandOption potion = Assert.Single(battle.Commands(0), option => option.Command == Ff7BattleCommand.Item && option.Argument == 1);
                    Assert.Equal((ushort)1, potion.Argument);
                    Assert.Equal(originalPotion, potion.Cost);
                    Assert.False(string.IsNullOrWhiteSpace(mixed.Actions.ItemName(potion.Argument)));
                    Assert.True(battle.SubmitGuestItem(0, 0, potion.Argument));
                    Assert.Equal(originalPotion - 1, mixed.Actions.ItemStock(1));
                    Assert.Equal(hostPotion, battle.Capture().Inventory.Single(item => item.ItemId == 0).Quantity);
                    CombinedSessionSave pending = source with { Ff7 = session.Capture(), MixedBattlePayload = mixed.Capture() };
                    Ff7BattleSnapshot savedBattle = pending.Ff7!.Battle!;
                    Ff7BattleActorState[] sleeping = savedBattle.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { Statuses = 4 }, Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, 4) } : actor).ToArray();
                    Ff7BattleCommandQueueState isolatedQueue = Ff7BattleCommandQueue.StageReady(Ff7BattleCommandQueueState.Create(), 0, (byte)Ff7BattleCommand.Item, 1, 1, guestAttack: true, guestAbility: 1);
                    using Ff7Session cancelled = Ff7Session.Restore(installation, pending.Ff7 with { Battle = savedBattle with { Actors = sleeping, Queue = isolatedQueue } });
                    using CrossoverMixedBattle cancelledMixed = new(ff8Root!, pending, cancelled.Battle!);
                    for (int attempt = 0; attempt < 240 && cancelledMixed.Actions.Capture().Items!.Pending is not null; attempt++)
                    {
                        cancelled.AdvanceTick();
                    }
                    Ff7BattleSnapshot afterCancel = cancelled.Battle!.Capture();
                    CrossoverGuestItemInventory cancelStock = cancelledMixed.Actions.Capture().Items!;
                    Assert.True(originalPotion == cancelledMixed.Actions.ItemStock(1), $"Native cancellation left item stock {cancelledMixed.Actions.ItemStock(1)}, pending {cancelStock.Pending is not null}, ledger {cancelStock.Ledger.Length}, start action {savedBattle.Action?.Command.CommandId}, end action {afterCancel.Action?.Command.CommandId}, status {afterCancel.Actors.Single(actor => actor.Actor.Slot == 0).Actor.Statuses:X}, outcome {afterCancel.Outcome}, tick {afterCancel.Tick}.");
                    Assert.True(Assert.Single(cancelledMixed.Actions.Capture().Items!.Ledger).Refunded);
                }
                else
                {
                    ushort ability = recovery == 1 && casts == 0 ? (ushort)21 : (ushort)1;
                    Ff7BattleCommandOption fire = Assert.Single(battle.Commands(0), option => option.Command == Ff7BattleCommand.Magic && option.Argument == ability);
                    ushort targets = (ushort)(fire.ValidTargets & (ability == 21 ? 7 : 0x3F0));
                    Assert.True(targets != 0, $"Ability {ability} has no expected side: {fire}.");
                    int target = System.Numerics.BitOperations.TrailingZeroCount(targets);
                    if (casts < 8)
                    {
                        BattleNativeAction native = mixed.Actions.ActiveAbility?.Presentation.Action ?? CrossoverFf8BattleKit.Read(mixed.Actions.WorkingStock, diagnostics, 0).Spells.Single(spell => spell.ActionId == ability);
                        output.WriteLine($"Submit {casts}: ability {ability}, native targets {native.TargetFlags:X2}, menu {fire}, selected {target}, HP {string.Join(",", battle.Actors.Select(actor => $"{actor.Actor.Slot}:{actor.Actor.HitPoints}"))}.");
                    }
                    Assert.True(battle.SubmitGuestMagic(0, target, ability));
                }
                casts++;
            }
            session.AdvanceTick();
            if (reportedContacts < 16 && battle.LastHit is { Source: 0 } reported && reported.Tick == battle.Tick)
            {
                output.WriteLine($"Contact at {reported.Tick}: ability {reported.AttackId}, target {reported.Target}, damage {reported.Damage}, HP {string.Join(",", battle.Actors.Select(actor => $"{actor.Actor.Slot}:{actor.Actor.HitPoints}"))}.");
                reportedContacts++;
            }
            foreach (Ff7BattleActorState enemy in battle.Actors.Where(actor => actor.Actor.Slot >= 4 && actor.Actor.HitPoints == 0))
            {
                if (battle.ActorModelOutput(enemy.Actor.Slot) is { Visible: true, Red: > 0, Alpha: < 255 })
                {
                    Assert.Equal(Ff7BattleOutcome.Fighting, battle.Outcome);
                    fadingEnemies.Add(enemy.Actor.Slot);
                }
            }
            if (recovery == 2 && battle.LastHit is { Source: 0, Target: 0, AttackId: 1 } potionHit && potionHit.Tick == battle.Tick)
            {
                Assert.False(healedByPotion);
                Assert.Equal(-200, potionHit.Damage.Damage);
                healedByPotion = true;
            }
            if (junctioned && mixed.Actions.ActiveAbility is not null)
            {
                Assert.Equal(mixed.Actions.WorkingStock.GetMagicMaximumHitPoints(row), battle.GuestProjection!.MaximumHp);
                Assert.True(battle.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints <= battle.GuestProjection.MaximumHp);
                observedReprojection = true;
            }
        }
        Assert.Equal(Ff7BattleOutcome.Victory, battle.Outcome);
        Assert.True(battle.ResultReady);
        Assert.Equal(new[] { 4, 5 }, fadingEnemies.Order());
        Assert.All(fadingEnemies, slot => Assert.False(battle.ActorModelOutput(slot).Visible));
        Assert.Equal(recovery == 0 ? 2 : 3, casts);
        Assert.Equal(recovery == 2, healedByPotion);
        Assert.Equal(originalPayload, source.Ff8Payload);
        CombinedSessionSave terminal = source with { Ff7 = session.Capture(), MixedBattlePayload = mixed.Capture() };
        CrossoverBattleVitalsSettlementResult settled = mixed.Settle(terminal, installation);
        GameplaySaveComponentDocument result = CrossoverSaveAdapter.ReadFf8(settled.Save);
        NativeCharacterVitals updated = result.Characters.Restore(installed);
        Assert.Equal(initialStock - 2, updated.GetMagicCount(row, 1));
        Assert.Equal(recovery == 1 ? 1 : 0, updated.GetMagicCount(row, 21));
        Assert.Equal(originalPotion - (recovery == 2 ? 1 : 0), result.Economy.Items.Single(stock => stock.ItemId == 1).Count);
        Assert.Equal(origin.Records[row].Junction, updated.Records[row].Junction);
        Assert.Equal(originalMaximum, battle.GuestVitals!.MaximumHp);
        Assert.Equal(updated.GetMagicMaximumHitPoints(row), battle.GuestProjection!.MaximumHp);
        if (junctioned)
        {
            Assert.True(updated.GetMagicMaximumHitPoints(row) < originalMaximum);
            Assert.True(observedReprojection);
        }
        Assert.Equal(mixed.Actions.CurrentCrtRandom, result.Progression!.Clocks.CrtRandom);
        Assert.Equal(dormant, JsonSerializer.Serialize(settled.Save.Ff7!.Progress.Characters.Single(character => character.Id == 0)));
        Assert.Single(settled.Save.GuestBattleSettlements!);
        Assert.Same(settled.Save, CrossoverBattleVitalsSettlement.PrepareResult(settled.Save, installation, installed).Save);
        Assert.Equal(originalPayload, source.Ff8Payload);
        output.WriteLine($"Original field request {battle.RequestId}: two original Fire casts settled stock{initialStock}->{initialStock - 2}, maxHP{originalMaximum}->{updated.GetMagicMaximumHitPoints(row)} and native CRT once; recovery mode {recovery}, dormant Cloud preserved.");
    }

    [Theory]
    [InlineData(0u, false, (ushort)1, false)]
    [InlineData(0u, false, (ushort)7, false)]
    [InlineData(0u, false, (ushort)4, false)]
    [InlineData(8u, false, (ushort)1, false)]
    [InlineData(0x200u, false, (ushort)1, false)]
    [InlineData(0x04000000u, false, (ushort)1, false)]
    [InlineData(4u, false, (ushort)1, false)]
    [InlineData(0x80u, false, (ushort)1, false)]
    [InlineData(0u, true, (ushort)1, false)]
    [InlineData(0u, false, (ushort)1, true)]
    [InlineData(0u, true, (ushort)1, true)]
    public void Original_elemental_spell_charges_origin_stock_once_and_publishes_one_prepared_host_receipt_through_uninterrupted_casting(uint sourceStatus, bool junctioned, ushort ability, bool smart)
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
        if (smart)
        {
            origin = origin with { MagicSystem = origin.MagicSystem with { AttackModes = origin.MagicSystem.AttackModes.Select((mode, index) => index == row ? MagicAttackMode.SmartMagic : mode).ToArray() } };
        }
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
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
        if (sourceStatus != 0 || smart)
        {
            Ff7BattleSnapshot statusHost = host.Capture();
            statusHost = statusHost with
            {
                Actors = statusHost.Actors.Select(actor => actor.Actor.Slot == 0 ? actor with { Actor = actor.Actor with { Statuses = sourceStatus }, Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, sourceStatus) } : smart && actor.Actor.Slot == 4 ? actor with { Actor = actor.Actor with { HitPoints = 30000, MaxHitPoints = 30000, Defense = 510, MagicDefense = 0 } } : actor).ToArray(),
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
            if (sourceStatus == 0x80)
            {
                Ff7BattleCommandOption blocked = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic && option.Argument == ability);
                Assert.Contains("status", blocked.UnsupportedReason, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Assert.DoesNotContain(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic);
            }
            Assert.False(host.SubmitGuestMagic(0, 4, ability));
            Assert.Equal(initialStock, actions.WorkingStock.GetMagicCount(row, ability));
            Assert.Empty(actions.WorkingStock.MagicSystem.Reservations);
            Assert.Empty(actions.Capture().CommittedSpells!.Value);
            return;
        }
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == Ff7BattleCommand.Magic);
        Assert.Equal(ability, option.Argument);
        Assert.Equal(initialStock, option.Cost);
        if (smart)
        {
            string hostBefore = JsonSerializer.Serialize(host.Capture());
            string actionsBefore = JsonSerializer.Serialize(actions.Capture());
            string attackBefore = JsonSerializer.Serialize(attack.Capture());
            BattleSmartAttackPreview physical = actions.PreviewSmartAttack(4, true);
            BattleSmartAttackPreview preview = actions.PreviewSmartAttack(4);
            Assert.Equal(BattleAbility.Attack, physical.Ability);
            Assert.True(physical.ForcePhysical);
            Assert.Equal(BattleAbility.Magic, preview.Ability);
            Assert.Equal(ability, preview.ActionId);
            Assert.Equal(1, preview.CastCount);
            Assert.True(preview.ExpectedDamage > physical.ExpectedDamage);
            Assert.Equal(preview, actions.PreviewSmartAttack(4));
            Assert.Equal(hostBefore, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(actionsBefore, JsonSerializer.Serialize(actions.Capture()));
            Assert.Equal(attackBefore, JsonSerializer.Serialize(attack.Capture()));
            Assert.False(actions.SubmitSmartAttack(preview with { Revision = "stale" }));
            Assert.Equal(hostBefore, JsonSerializer.Serialize(host.Capture()));
            Assert.Equal(actionsBefore, JsonSerializer.Serialize(actions.Capture()));
            Assert.True(actions.SubmitSmartAttack(preview));
        }
        else
        {
            Assert.True(host.Submit(0, Ff7BattleCommand.Magic, ability, 1 << 4), $"The original spell was rejected: {option}; target reason {actions.AbilityTargetUnsupportedReason(ability, 4)}.");
        }
        for (int tick = 0; tick < 1000 && host.Capture().Action?.Command.GuestAbility != ability && host.GuestAbilityFailure is null; tick++)
        {
            host.Advance();
        }
        Assert.Null(host.GuestAbilityFailure);
        Assert.NotNull(actions.ActiveAbility);
        CrossoverGuestFireHostState pending = actions.ActiveAbility!;
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
        bool observedCasting = false;
        bool observedEffect = false;
        bool rejectedLiveCapture = false;
        Ff7BattleHit? hit = null;
        for (int tick = 0; tick < 1000; tick++)
        {
            if (actions.ActiveAbility is { } active)
            {
                observedCasting |= active.Presentation.Modules.PublishedCount > 0;
                observedEffect |= active.Presentation.Effect is not null;
                if (!rejectedLiveCapture && active.Presentation.Effect is not null)
                {
                    Assert.Throws<NotSupportedException>(() => actions.Capture());
                    rejectedLiveCapture = true;
                }
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
                Assert.True(observedCasting && observedEffect && rejectedLiveCapture);
                Assert.Equal(initialStock - 1, actions.WorkingStock.GetMagicCount(row, ability));
                Assert.Equal(immutableOrigin, JsonSerializer.Serialize(origin));
                Assert.Equal(3, actions.PeekSoundRequests().Length);
                Assert.All(actions.PeekSoundRequests(), receipt => Assert.Equal(CrossoverGuestAudioKind.Module, receipt.Kind));
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
                output.WriteLine($"Original spell{ability} status{sourceStatus:X8}, junctioned{junctioned}: prepared {hit.Damage.Damage}, contact host tick {hit.Tick}, origin stock {initialStock}->{initialStock - 1} through uninterrupted native casting.");
                return;
            }
        }
        throw new InvalidOperationException("The original guest Fire did not return through its one host action.");
    }

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    public void Original_Cure_or_item_recovers_bound_party_actor_with_one_native_debit_and_native_redraw(bool junctioned, bool ally, bool phoenix, bool remedy)
    {
        bool nativeItem = phoenix || remedy;
        ushort ability = phoenix ? (ushort)7 : remedy ? (ushort)16 : (ushort)21;
        string? ff7Root = Environment.GetEnvironmentVariable("MEMORIA_FF7_GAME_DIR");
        string? ff8Root = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(ff7Root) && !string.IsNullOrWhiteSpace(ff8Root), "Select both original game installations.");
        Ff7Installation installation = Ff7Installation.Discover(ff7Root!);
        using Ff7Session original = Ff7Session.NewGame(installation);
        using GameAssets game = new(ff8Root!);
        using BattleAssets assets = new(ff8Root!);
        NativeCharacterVitals origin = game.ReadNewGameCharacterVitals().DeepCopy();
        int row = origin.FindPhysicalRow(0);
        int magicAbility = origin.MenuDefinitions.CommandAbilityCommands.IndexOf(nativeItem ? (byte)4 : (byte)2) + 20;
        origin.Records[row] = origin.Records[row] with { Junction = origin.Records[row].Junction with { Commands = (uint)magicAbility } };
        int initialStock = 50;
        origin = origin.ChangeMagicStock(row, 21, initialStock, MagicOperationReason.ScriptReward, out _);
        if (junctioned)
        {
            origin.Records[row].Junction.MagicJunctions[0] = 21;
            origin = origin.WithSavedHitPoints(row, origin.GetMagicMaximumHitPoints(row));
        }
        origin = origin.WithSavedHitPoints(row, 100);
        if (remedy)
        {
            origin.Records[row] = origin.Records[row] with { Status = 2 };
        }
        CrossoverFf8BattleKit kit = CrossoverFf8BattleKit.Read(origin, assets, 0);
        GameplaySaveEconomy? economy = nativeItem ? GameplaySaveEconomy.Capture(game.ReadNewGameEconomy()) : null;
        int originalItemStock = economy?.Items.Single(item => item.ItemId == ability).Count ?? 0;
        string immutableOrigin = JsonSerializer.Serialize(origin);
        using LgpArchive archive = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.BattleArchive).Path);
        using LgpArchive magic = LgpArchive.Open(installation.GetRequired(Ff7ResourceRole.MagicArchive).Path);
        IReadOnlyList<KernelSection> kernel = KernelArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.Kernel).Path));
        SceneFormation formation = SceneArchive.Read(File.ReadAllBytes(installation.GetRequired(Ff7ResourceRole.BattleScenes).Path)).Single(item => item.FormationId == 300);
        Ff7PcExecutableData executable = Ff7PcExecutableData.Load(installation);
        Ff7BattleAnimationResources animations = new(archive, Ff7PcExecutableData.LoadBattlePlacement(installation), magic, Ff7PcExecutableData.LoadBattleJumpDeltas(installation), Ff7PcExecutableData.LoadBattleHitmarks(installation), Ff7PcExecutableData.LoadBattleActorModelResources(installation));
        Ff7Progress progress = original.Progress;
        int targetSlot = ally ? 1 : 0;
        if (ally)
        {
            Ff7CharacterState barret = progress.Characters.Single(character => character.Id == 1);
            byte[] record = (byte[])barret.NativeRecord.Clone();
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(44), phoenix ? (ushort)0 : (ushort)100);
            progress = progress with { Party = [0, 1, 255], Characters = progress.Characters.Select(character => character.Id == 1 ? character with { HitPoints = phoenix ? 0 : 100, NativeRecord = record } : character).ToArray() };
        }
        Ff7BattleRenderer renderer = new(archive, formation.LocationId);
        Ff7BattleSession host = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, requestFlags: 4, animations: animations);
        uint remedyStatuses = Ff7NativeBattleActionResolver.Poison | Ff7NativeBattleActionResolver.Darkness | Ff7NativeBattleActionResolver.Slow;
        if (remedy && ally)
        {
            Ff7BattleSnapshot initial = host.Capture();
            Ff7BattleActorState[] actors = initial.Actors.Select(actor => actor.Actor.Slot == targetSlot ? actor with { Actor = actor.Actor with { Statuses = remedyStatuses }, Timers = Ff7NativeBattleStatusClock.Apply(actor.Timers, actor.Actor.Statuses, remedyStatuses) } : actor).ToArray();
            host = new(formation, progress, kernel, executable.NativeMateriaStatModifiers, 1, snapshot: initial with { Actors = actors }, animations: animations);
        }
        CrossoverBattleRoster roster = CrossoverBattleRoster.Create(Guid.NewGuid(), host.Capture(), [new(0, 0, 0)], []);
        host.BindGuestVitals(new(roster.EncounterId, new('A', 64), 0, 0, 0, row, kit.HitPoints, (ushort)kit.MaximumHitPoints, 6, 14, 24, 17) { OriginStatuses = kit.Statuses.Saved });
        CrossoverGunbladeHostAttack attack = new(roster, kit, assets, game, game.ReadNewGameGlobalRandom(), () => host.Data.Cameras[0], _ => 128);
        attack.PrepareStanding(host.VisualPlacement(0));
        CrossoverGuestHostActions actions = new(roster, kit, origin, NativeCrtRandomState.FromColdProcess(), attack, host, game, assets, originEconomy: economy);
        int capturedAllyFaces = 0;
        Bind();
        for (int tick = 0; tick < 1000 && host.ReadyPlayer != 0; tick++)
        {
            host.Advance();
        }
        Assert.Equal(0, host.ReadyPlayer);
        Ff7BattleCommand command = nativeItem ? Ff7BattleCommand.Item : Ff7BattleCommand.Magic;
        Ff7BattleCommandOption option = Assert.Single(host.Commands(0), option => option.Command == command && option.Argument == ability);
        if (ally && (option.ValidTargets & 1 << targetSlot) == 0)
        {
            int previousStock = nativeItem ? actions.ItemStock(ability) : actions.MagicStock(ability);
            string beforeQueue = JsonSerializer.Serialize(host.Capture().Queue);
            Assert.False(host.Submit(0, command, ability, (ushort)(1 << targetSlot)));
            Assert.Equal(previousStock, nativeItem ? actions.ItemStock(ability) : actions.MagicStock(ability));
            Assert.Equal(beforeQueue, JsonSerializer.Serialize(host.Capture().Queue));
            string? reason = nativeItem ? actions.ItemTargetUnsupportedReason(ability, targetSlot) : actions.AbilityTargetUnsupportedReason(ability, targetSlot);
            Assert.False(string.IsNullOrWhiteSpace(reason));
            Assert.Contains("native", reason, StringComparison.OrdinalIgnoreCase);
            output.WriteLine($"Foreign recovery {ability} was refused before debit: {reason}");
            if (phoenix)
            {
                Ff7BattleCommandOption attackOption = Assert.Single(host.Commands(0), choice => choice.Command == Ff7BattleCommand.Attack);
                int enemy = System.Numerics.BitOperations.TrailingZeroCount(attackOption.ValidTargets);
                Assert.True(host.SubmitGuestAttack(0, enemy));
                for (int tick = 0; tick < 1000 && host.LastHit?.Source != 0; tick++)
                {
                    host.Advance();
                }
                Assert.Equal(0, host.LastHit?.Source);
                Assert.Equal(previousStock, actions.ItemStock(ability));
                return;
            }
            targetSlot = 0;
        }
        Assert.True((option.ValidTargets & 1 << targetSlot) != 0, $"Recovery option has no usable self target: {option}.");
        Assert.False(option.DefaultsToEnemies);
        bool foreignTarget = targetSlot != 0;
        if (phoenix)
        {
            Assert.Equal(6, host.RenderActorPose(1).Clip);
        }
        Assert.True(host.Submit(0, command, ability, (ushort)(1 << targetSlot)));
        for (int tick = 0; tick < 1000 && actions.Invocation is null && host.GuestAbilityFailure is null; tick++)
        {
            host.Advance();
        }
        Assert.Null(host.GuestAbilityFailure);
        Assert.NotNull(actions.ActiveAbility);
        CrossoverGuestFireHostState pending = actions.ActiveAbility!;
        Assert.Equal(nativeItem ? 50 : 49, actions.WorkingStock.GetMagicCount(row, 21));
        Assert.Equal(100u, host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints);
        Assert.True(remedy ? pending.Prepared.PreparedContact!.Receipt.Damage.Damage == 0 : pending.Prepared.PreparedContact!.Receipt.Damage.Damage < 0);
        bool observedEffect = false;
        bool observedHiddenTarget = false;
        bool observedContact = false;
        int publications = 0;
        int overlayPackets = 0;
        int effectPackets = 0;
        int peakStageDepth = 0;
        NativeBattleCureTextureProjection overlayTexture = NativeBattleCureActorPackets.TextureProjection8D7050(512, 384, 320, 241, 128, 128);
        for (int tick = 0; tick < 1000 && actions.Invocation is not null; tick++)
        {
            if (actions.ActiveAbility is { } active)
            {
                observedEffect |= active.Presentation.Effect is not null;
                observedHiddenTarget |= foreignTarget ? actions.SuppressForeignActor(targetSlot) : (active.Presentation.Cast.Model.Flags0 & 4) != 0;
                observedContact |= active.ContactPublished;
            }
            host.Advance();
            if ((foreignTarget || remedy) && actions.EffectVisual?.Ordering is { } ordering)
            {
                effectPackets = Math.Max(effectPackets, ordering.Packets.Count);
                overlayPackets = Math.Max(overlayPackets, ordering.Packets.Count(packet => packet.Words.Length == 10 && packet.Words[1] >> 24 == 0x36 && packet.Words[3] >> 16 == overlayTexture.Clut && packet.Words[6] >> 16 == overlayTexture.TexturePage));
            }
            peakStageDepth = Math.Max(peakStageDepth, actions.EffectVisual?.StageColor?.Depth ?? 0);
            if (host.LastHit is { Source: 0 } hit && hit.AttackId == ability && hit.Target == targetSlot && hit.Tick == host.Tick)
            {
                publications++;
                Assert.Null(host.Capture().Reaction);
                Assert.Equal(pending.Prepared.PreparedContact.Receipt.Damage, hit.Damage);
            }
        }
        Assert.True(observedEffect);
        if (!nativeItem)
        {
            Assert.True(observedHiddenTarget);
        }
        Assert.Null(actions.Invocation);
        Assert.Equal(1, publications);
        Assert.Equal(pending.Prepared.PreparedContact.Receipt.Target.Hp, host.Actors.Single(actor => actor.Actor.Slot == targetSlot).Actor.HitPoints);
        Assert.Equal(nativeItem ? 50 : 49, actions.WorkingStock.GetMagicCount(row, 21));
        if (nativeItem)
        {
            Assert.True(observedContact);
            Assert.Empty(actions.Capture().CommittedSpells!.Value);
            Assert.Equal(originalItemStock - 1, actions.ItemStock(ability));
            Assert.False(Assert.Single(actions.Capture().Items!.Ledger).Refunded);
            if (phoenix)
            {
                Assert.Equal(0u, host.Actors.Single(actor => actor.Actor.Slot == targetSlot).Actor.Statuses & 1);
                Assert.NotEqual(6, host.RenderActorPose(targetSlot).Clip);
                Assert.Equal(-Math.Max(1, (int)pending.Prepared.PreparedContact.TargetBefore.MaxHp / 8), pending.Prepared.PreparedContact.Receipt.Damage.Damage);
            }
            else
            {
                Assert.Equal(0u, host.Actors.Single(actor => actor.Actor.Slot == targetSlot).Actor.Statuses & Ff7NativeBattleActionResolver.Poison);
                if (foreignTarget)
                {
                    Assert.Equal(remedyStatuses, pending.Prepared.PreparedContact.TargetBefore.Statuses & remedyStatuses);
                    Assert.Equal(Ff7NativeBattleActionResolver.Slow, host.Actors.Single(actor => actor.Actor.Slot == targetSlot).Actor.Statuses & remedyStatuses);
                }
                Assert.Equal(1024, peakStageDepth);
                Assert.True(effectPackets > 0);
            }
        }
        else
        {
            Assert.Single(actions.Capture().CommittedSpells!.Value);
            Assert.Equal((ushort)21, actions.Capture().CommittedSpells!.Value[0].Spell);
        }
        Assert.Equal(immutableOrigin, JsonSerializer.Serialize(origin));
        Assert.Equal(actions.WorkingStock.GetMagicMaximumHitPoints(row), host.GuestProjection!.MaximumHp);
        Assert.True(actions.PeekSoundRequests().Length >= (nativeItem ? 1 : 3));
        if (foreignTarget)
        {
            Assert.True(effectPackets > 0);
            Assert.True(capturedAllyFaces > 0);
            Assert.Equal(0, overlayPackets);
            Assert.Equal(100u, host.Actors.Single(actor => actor.Actor.Slot == 0).Actor.HitPoints);
            Assert.False(actions.SuppressForeignActor(targetSlot));
        }
        output.WriteLine($"Original recovery{ability} target{targetSlot}, posed faces{capturedAllyFaces}, effect packets{effectPackets}, overlay{overlayPackets}, healed {-pending.Prepared.PreparedContact.Receipt.Damage.Damage}, one native debit, stage{peakStageDepth}, junctioned{junctioned}, uninterrupted native redraw, one contact.");

        void Bind()
        {
            if (ally)
            {
                actions.BindForeignGeometry(CaptureGeometry);
            }
            host.BindGuestAttack(actions);
        }

        Ff7BattleActorGeometry CaptureGeometry(int slot)
        {
            Ff7BattleActorState actor = host.Actors.Single(actor => actor.Actor.Slot == slot);
            Ff7BattlePlacement placement = host.RenderActorPlacement(slot);
            (int clip, int frame) = host.RenderActorPose(slot);
            Ff7BattleActorGeometry geometry = renderer.CaptureActorGeometry(new(Ff7BattleAnimationResources.ModelPrefix(actor.Identity, false), placement.Position, placement.Yaw * MathF.Tau / 4096, clip, frame, host.WeaponModel(slot), host.RenderActorLimit(slot)) { ModelModifiers = host.ActorModelOutput(slot) });
            capturedAllyFaces = Math.Max(capturedAllyFaces, geometry.Faces.Length);
            return geometry;
        }
    }
}
