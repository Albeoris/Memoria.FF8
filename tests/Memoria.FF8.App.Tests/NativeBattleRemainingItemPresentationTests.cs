using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleRemainingItemPresentationTests
{
    [Fact]
    public void Execute_selected_original_item_and_restore_its_command_boundaries()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? selection = Environment.GetEnvironmentVariable("MEMORIA_FF8_ITEM_ACTION");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_ITEM_EVIDENCE");
        Assert.SkipUnless(directory is not null && selection is not null && output is not null, "Select installed resources, an item ID and a distinct evidence directory.");
        Assert.True(Directory.Exists(directory));
        Assert.False(Directory.Exists(output));
        int itemId = int.Parse(selection!, System.Globalization.CultureInfo.InvariantCulture);
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Item && action.Id == itemId);
        BattleNativeAction native = action.NativeAction!;
        Assert.Equal((byte)4, native.Command);
        Assert.Contains(native.MagicId, new ushort[] { 7, 50, 51, 52, 292, 293, 294, 295, 296 });
        string? expectedEffect = Environment.GetEnvironmentVariable("MEMORIA_FF8_ITEM_EFFECT");
        if (expectedEffect is not null)
        {
            Assert.Equal(ushort.Parse(expectedEffect, System.Globalization.CultureInfo.InvariantCulture), native.MagicId);
        }
        bool revival = native.MagicId == 7;
        bool statusCure = native.MagicId is >= 292 and <= 296;
        ushort initialSavedStatus = statusCure ? checked((ushort)(native.Statuses0 & 0x7E)) : (ushort)0;
        uint initialBattleStatus = statusCure ? native.Statuses1 & ~(0x3FFFu | 0x4008000u) : 0;
        if (statusCure)
        {
            Assert.Equal((byte)0, native.Power);
            Assert.True(initialSavedStatus != 0 || initialBattleStatus != 0, "The original removal mask must include a supported non-KO saved status or untimed battle status for this fresh fixture.");
        }
        Assert.Equal(revival ? (byte)5 : native.MagicId == 52 ? (byte)32 : (byte)4, native.AttackType);
        if (revival)
        {
            Assert.True(action.AllTargets, "Mega Phoenix must retain its original group target selection.");
        }
        NativeCharacterVitals vitals = game.ReadNewGameCharacterVitals();
        BattleNativePlayerStatRules casterRules = assets.ReadPlayerNativeStatRules(vitals, 0);
        NativeCharacterVital casterVital = vitals.Records[0];
        BattleNativeCharacterGrowth casterGrowth = new(casterVital.HitPointBonus, casterVital.StatBonuses.ToImmutableArray());
        BattleNativePlayerStatistics casterStatistics = casterRules.Project(casterVital.MagicInventory, casterGrowth);
        NativeBattleStatusAllocation casterStatus = assets.ReadPlayerNativeStatusData(vitals, 0);
        Assert.Equal(casterRules.ProjectStatusResistances(casterVital.MagicInventory).ToArray(), casterStatus.Resistances.ToArray());
        int casterInitialHp = casterStatistics.MaximumHp;
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = casterVital.Weapon, MaximumHp = casterInitialHp, CurrentHp = casterInitialHp, Attack = casterStatistics.Strength, Defense = casterStatistics.Vitality, Magic = casterStatistics.Magic, Spirit = casterStatistics.Spirit, Speed = Math.Max(1, casterStatistics.Speed), Luck = casterStatistics.Luck, Hit = casterStatistics.Hit, Evasion = casterStatistics.Evasion, NativeMagicInventory = casterVital.MagicInventory.ToArray(), ElementDefenses = casterStatistics.ElementDefenses.ToArray(), ElementAttack = casterStatistics.ElementAttack, ElementAttackPercent = casterStatistics.ElementAttackPercent, NativeStatus0 = casterStatus.Saved, NativeStatus1 = casterStatus.Battle, NativeStatusResistances = casterStatus.Resistances.ToArray(), InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0, NativePlayerStatRules = casterRules, NativeGrowth = casterGrowth, Abilities = [BattleAbility.Item] },
            new() { Id = "target", Name = "Zell", IsPlayer = true, ModelId = 1, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id, MaximumHp = 30000, CurrentHp = revival ? 0 : 1000, NativeStatus0 = revival ? (ushort)1 : initialSavedStatus, NativeStatus1 = initialBattleStatus, NativeDeathCount = revival ? (ushort)1 : (ushort)0, NativeStatusResistances = assets.ReadPlayerNativeStatusData(vitals, 1).Resistances.ToArray() },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1, MaximumHp = 9999, CurrentHp = 9999, NativeStatusResistances = assets.ReadEnemyNativeStatusData(61).Resistances.ToArray() }
        ];
        if (revival)
        {
            BattleCombatantDefinition companion = actors[1] with { Id = "companion", Name = "Quistis", ModelId = 2, NativeStatusResistances = assets.ReadPlayerNativeStatusData(vitals, 2).Resistances.ToArray(), WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 2).Id };
            actors = [actors[0], actors[1], companion, actors[2]];
        }
        //Explicit fresh installed-resource component; all contacts and descriptors must be produced by the ordinary command.
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            ActionDefinitions = [action],
            Items = [new(itemId, 2)],
            NativeActions = assets.NativeActions.ToArray(),
            NativeStatusRules = assets.ReadNativeStatusRules(0),
            NativeFieldBattleRules = 0,
            NativeClockRules = assets.ReadNativeClockRules(),
            NativeSupportRules = assets.ReadNativeSupportRules(),
            NativeBattleSpeed = 0,
            NativeMessageSpeed = 3,
            NativeElapsedCounter = 0,
            NativeSavedBattleFlags = 8,
            NativeSavedBattleFlagsKnownMask = 255,
            NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(),
            NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5),
            NativeVibrationProcess = NativeVibrationProcessState.FromColdProcess(),
            NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(),
            NativeBattleUiRandom = new(0),
            NativeFlags = 0x80,
            NativeEntry = new(0, 0),
            NativeGlobalRandom = new(0),
            NativeCrtRandom = new(1),
            NativeGraphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), formation),
            NativeCommandSequences = game.ReadNativeBattleCommandSequences(),
            NativeModuleResources = game.ReadNativeBattleModuleResources(),
            NativeDefensiveResources = game.ReadNativeBattleDefensiveResources(),
            NativeEffectMeshes = game.ReadNativeEffectMeshes(),
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([native.MagicId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeScannedModels = game.ReadNewGameScannedModels(),
            NativeScanAlwaysShow = (game.ReadNewGameInputConfiguration().Options & 0x100) != 0,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        Directory.CreateDirectory(output!);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        BattleSnapshot prior = battle.Capture();
        bool selected = false;
        bool restoredLive = false;
        bool contact = false;
        int transitions = 0;
        int statusTransitions = 0;
        bool statusPresentationRemoved = false;
        int callbacks = 0;
        int peakChangedRgb = 0;
        int visibleSamples = 0;
        BattleSnapshot? peakVisible = null;
        BattleSnapshot? peakWithoutEffect = null;
        byte[]? peakPixels = null;
        byte[]? peakWithoutPixels = null;
        int formulaAmount = statusCure ? 0 : revival ? 30000 / (((casterRules.NativeStatusPassiveFlags ?? throw new InvalidDataException("Installed caster rules lack item-revival passive flags.")) & 2) != 0 ? 4 : 8) : Math.Min((native.AttackFlags & 8) != 0 ? 60000 : 9999, native.AttackType == 32 ? native.Power * 30000 / 16 : 50 * native.Power);
        int initialHp = revival ? 0 : 1000;
        int expectedHp = Math.Min(30000, initialHp + formulaAmount);
        ushort displayedHp = checked((ushort)initialHp);
        try
        {
            battle = VerifyContinuation("before-entry", prior);
            for (int callback = 0; callback < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); callback++)
            {
                prior = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.NativeEntryReady);
            Assert.True(battle.Combatants.Single(actor => actor.Id == "party").IsReady);
            Assert.Equal(2, Assert.Single(battle.Items, stock => stock.Id == itemId).Count);
            battle = VerifyContinuation("before-command", battle.Capture());
            if (statusCure)
            {
                NativeBattleStatusSnapshot beforeStatus = battle.Capture().Combatants.Single(actor => actor.Id == "target").NativeStatus!;
                Assert.Equal(initialSavedStatus, beforeStatus.Saved & initialSavedStatus);
                Assert.Equal(initialBattleStatus, beforeStatus.Battle & initialBattleStatus);
            }
            battle.QueueAction("party", "target", BattleAbility.Item, itemId);
            Assert.Equal(1, Assert.Single(battle.Items, stock => stock.Id == itemId).Count);
            battle = VerifyContinuation("queued", battle.Capture());
            // This bound detects a stalled command; only native task release establishes completion.
            for (; callbacks < 4096; callbacks++)
            {
                prior = battle.Capture();
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                NativeBattleGraphicsState graphics = current.NativeGraphics!;
                selected |= graphics.OriginalEffect?.RequestedEffect == native.MagicId;
                NativeBattleActionPacketsState packets = current.NativeActionPackets!;
                if (packets.ContactCount > 0)
                {
                    Assert.Equal(revival ? (byte)2 : (byte)1, packets.ContactCount);
                    NativeBattleContactHalf[] contacts = Enumerable.Range(0, packets.ContactCount).Select(index => packets.ReadContact(index).Primary).ToArray();
                    NativeBattleContactHalf half = Assert.Single(contacts, item => item.ActorIndex == 1);
                    Assert.Equal(checked((ushort)formulaAmount), half.Amount6);
                    if (statusCure)
                    {
                        Assert.Equal(0, half.SavedStatuses4 & initialSavedStatus);
                        Assert.Equal(0u, half.BattleStatuses8 & initialBattleStatus);
                        Assert.Equal(0, half.HitFlags3 & 4);
                    }
                    if (revival)
                    {
                        Assert.Equal(0, half.SavedStatuses4 & 1);
                        NativeBattleContactHalf companion = Assert.Single(contacts, item => item.ActorIndex == 2);
                        Assert.Equal(checked((ushort)formulaAmount), companion.Amount6);
                        Assert.Equal(0, companion.SavedStatuses4 & 1);
                    }
                    contact = true;
                }
                // Recovery geometry and status-cure ACT packets use the current effect arena's canonical20DFAB8 bank.
                // Require the selected typed owner as well; unrelated packet banks never establish visibility.
                if ((native.MagicId is 7 or 50 or 51 or 52 || statusCure) && graphics.OriginalEffect is { ReturnedDescriptor: > 0 } effect && (native.MagicId == 7 && effect.Recovery is not null || native.MagicId == 50 && effect.Cure is not null || native.MagicId == 51 && effect.Cura is not null || native.MagicId == 52 && effect.Curaga is not null || statusCure && effect.StatusCureItem is not null) && effect.RequestedEffect == native.MagicId && graphics.PrimitiveFrame is { } frame && frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    byte[] rendered = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame withoutFrame = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSnapshot withoutEffect = current with { NativeGraphics = graphics with { PrimitiveFrame = withoutFrame } };
                    byte[] plain = new BattleScenePresentation(assets, definition, true).Render(BattleSession.Restore(withoutEffect), 320, 224).ToArray();
                    int changedRgb = CountRgbChanges(rendered, plain);
                    visibleSamples++;
                    if (changedRgb > peakChangedRgb)
                    {
                        peakChangedRgb = changedRgb;
                        peakVisible = current;
                        peakWithoutEffect = withoutEffect;
                        peakPixels = rendered;
                        peakWithoutPixels = plain;
                    }
                    if (changedRgb > 0 && !restoredLive)
                    {
                        battle = VerifyContinuation("live", current);
                        restoredLive = true;
                    }
                }
                else if (native.MagicId is not (7 or 50 or 51 or 52) && !statusCure && !restoredLive && graphics.OriginalEffect is { ReturnedDescriptor: > 0 } otherEffect && otherEffect.RequestedEffect == native.MagicId && graphics.PrimitiveFrame is { LastCommands.IsEmpty: false })
                {
                    battle = VerifyContinuation("live", current);
                    restoredLive = true;
                }
                if (statusCure)
                {
                    NativeBattleStatusSnapshot currentStatus = current.Combatants.Single(actor => actor.Id == "target").NativeStatus!;
                    NativeBattleStatusSnapshot priorStatus = prior.Combatants.Single(actor => actor.Id == "target").NativeStatus!;
                    bool priorRawPresent = (priorStatus.Saved & initialSavedStatus) != 0 || (priorStatus.Battle & initialBattleStatus) != 0;
                    bool rawRemoved = (currentStatus.Saved & initialSavedStatus) == 0 && (currentStatus.Battle & initialBattleStatus) == 0;
                    if (priorRawPresent && rawRemoved)
                    {
                        statusTransitions++;
                        battle = VerifyContinuation("status-formula", current);
                    }
                    // B92/B1C are separate retained mirrors; entry does not promise to publish freshly declared raw status bits.
                    statusPresentationRemoved = (currentStatus.Presentation.Saved & initialSavedStatus) == 0 && (currentStatus.Presentation.Battle & initialBattleStatus) == 0;
                }
                ushort hp = current.NativePartyPresentation!.Rows[1]!.HitPoints;
                if (hp != displayedHp)
                {
                    Assert.Equal(checked((ushort)initialHp), displayedHp);
                    Assert.Equal(checked((ushort)expectedHp), hp);
                    displayedHp = hp;
                    transitions++;
                    battle = VerifyContinuation("contact", current);
                }
                if (selected && Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(selected, "The actual item command never selected its original effect.");
            Assert.True(restoredLive, "No live effect task with primitive commands reached disk restoration.");
            if (native.MagicId is 7 or 50 or 51 or 52 || statusCure)
            {
                Assert.True(peakChangedRgb > 0, "The original item effect arena packets changed no RGB pixels.");
                PublishPeak();
                VerifyContinuation("visible", peakVisible!, published: true);
            }
            Assert.True(contact, "The actual item formula produced no expected contact.");
            Assert.Equal(statusCure ? 0 : 1, transitions);
            if (statusCure)
            {
                NativeBattleStatusSnapshot finalStatus = completed.Combatants.Single(actor => actor.Id == "target").NativeStatus!;
                Assert.Equal(0, finalStatus.Saved & initialSavedStatus);
                Assert.Equal(0u, finalStatus.Battle & initialBattleStatus);
                Assert.True(statusPresentationRemoved, "The completed item must leave its presentation mirrors clear of the removed status.");
                Assert.Equal(1, statusTransitions);
            }
            Assert.Equal(expectedHp, completed.Combatants.Single(actor => actor.Id == "target").CurrentHp);
            Assert.Equal(checked((ushort)expectedHp), displayedHp);
            if (revival)
            {
                Assert.Equal(expectedHp, completed.Combatants.Single(actor => actor.Id == "companion").CurrentHp);
                Assert.Equal(checked((ushort)expectedHp), completed.NativePartyPresentation!.Rows[2]!.HitPoints);
                Assert.All(completed.Combatants.Where(actor => actor.Id is "target" or "companion"), actor => Assert.Equal(0, actor.NativeStatus!.Saved & 1));
            }
            Assert.Equal(casterInitialHp, completed.Combatants.Single(actor => actor.Id == "party").CurrentHp);
            Assert.Equal(1, Assert.Single(battle.Items, stock => stock.Id == itemId).Count);
            Assert.Single(battle.Events, item => item.Kind == "Windup" && item.ActorId == "party");
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "party");
            Assert.True(Released(completed), "Item action, pause, descriptors and effect tasks must all release.");
            Assert.Equal((byte)0, completed.NativeEffectMemory!.Active);
            VerifyContinuation("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", itemId, native.MagicId, native.AttackType, native.Power, expectedHp, callbacks, transitions, statusTransitions, initialSavedStatus, initialBattleStatus, selected, restoredLive, contact, peakChangedRgb, visibleSamples, VisibleFrame = peakVisible?.Frame, VisibleCallback = peakVisible?.NativeGraphics?.Callback, completed.Frame, Callback = completed.NativeGraphics!.Callback }));
        }
        catch (Exception error)
        {
            PublishPeak();
            Publish("before-boundary", prior, false);
            Publish("failed-live", battle.Capture(), false);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", itemId, native.MagicId, callbacks, selected, restoredLive, contact, transitions, statusTransitions, initialSavedStatus, initialBattleStatus, peakChangedRgb, visibleSamples, Error = error.ToString() }));
            throw;
        }

        void PublishPeak()
        {
            if (peakVisible is not null && peakWithoutEffect is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "visible", peakVisible, peakPixels);
                NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", peakWithoutEffect, peakWithoutPixels);
            }
        }

        void Publish(string name, BattleSnapshot snapshot, bool render)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, render ? new BattleScenePresentation(assets, definition, true).Render(BattleSession.Restore(snapshot), 320, 224).ToArray() : null);
        }

        BattleSession Load(string name)
        {
            BattleSnapshot loaded = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(Path.Combine(output!, name + ".battle.json")))!;
            BattleSession restored = BattleSession.Restore(loaded);
            Assert.Equal(JsonSerializer.Serialize(loaded), JsonSerializer.Serialize(restored.Capture()));
            restored.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
            return restored;
        }

        BattleSession VerifyContinuation(string name, BattleSnapshot snapshot, bool published = false)
        {
            if (!published)
            {
                Publish(name, snapshot, true);
            }
            BattleSession first = Load(name);
            BattleSession second = Load(name);
            first.Tick();
            second.Tick();
            Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
            Assert.Equal(scene.Render(first, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(second, 320, 224).ToArray());
            Publish(name + "-next", first.Capture(), true);
            return Load(name);
        }
    }

    private static int CountRgbChanges(byte[] first, byte[] second)
    {
        Assert.Equal(first.Length, second.Length);
        int changed = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                changed++;
            }
        }
        return changed;
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }
}
