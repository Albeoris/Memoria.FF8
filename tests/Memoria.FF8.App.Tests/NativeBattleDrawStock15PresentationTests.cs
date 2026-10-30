using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleDrawStock15PresentationTests
{
    [Fact]
    public void Original_Draw_stock_command_adds_magic_once_and_completes_native_visual_and_cold_boundaries()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DRAWSTOCK15_EVIDENCE");
        Assert.SkipUnless(directory is not null && output is not null, "Select installed resources and a distinct Draw Stock evidence directory.");
        Assert.True(Directory.Exists(directory));
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSession? battle = null;
        BattleSnapshot? prior = null;
        int callbacks = 0;
        int smoothCaptured = 0;
        int contactTransitions = 0;
        int completions = 0;
        int expectedQuantity = 0;
        int expectedAdded = 0;
        int selectedSpell = 0;
        bool prepared = false;
        bool initialized = false;
        bool liveRestored = false;
        bool cameraChanged = false;
        int peakChangedRgb = 0;
        string boundary = "original-resources";
        List<string> coldBoundaries = [];
        try
        {
            using GameAssets game = new(directory!);
            using BattleAssets assets = new(directory!);
            BattleEncounter formation = assets.LoadEncounter(94);
            BattleStage stage = assets.LoadStage(formation.StageId);
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
                new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = casterVital.Weapon, Level = casterRules.Level, MaximumHp = casterInitialHp, CurrentHp = casterInitialHp, Attack = casterStatistics.Strength, Defense = casterStatistics.Vitality, Magic = casterStatistics.Magic, Spirit = casterStatistics.Spirit, Speed = Math.Max(1, casterStatistics.Speed), Luck = casterStatistics.Luck, Hit = casterStatistics.Hit, Evasion = casterStatistics.Evasion, NativeMagicInventory = casterVital.MagicInventory.ToArray(), ElementDefenses = casterStatistics.ElementDefenses.ToArray(), ElementAttack = casterStatistics.ElementAttack, ElementAttackPercent = casterStatistics.ElementAttackPercent, NativeStatus0 = casterStatus.Saved, NativeStatus1 = casterStatus.Battle, NativeStatusResistances = casterStatus.Resistances.ToArray(), InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0, NativePlayerStatRules = casterRules, NativeGrowth = casterGrowth, Abilities = [BattleAbility.Draw] },
                new() { Id = "enemy", Name = "Ifrit", ModelId = 61, Level = 7, NativeDrawSlots = assets.ReadEnemyDrawSlots(61, 7), SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1, MaximumHp = 9999, CurrentHp = 9999, NativeStatusResistances = assets.ReadEnemyNativeStatusData(61).Resistances.ToArray() }
            ];
            // Fresh installed-resource component; the ordinary Draw command produces its own stock result and contact.
            BattleDefinition definition = new()
            {
                Combatants = actors,
                EncounterId = formation.Id,
                StageId = formation.StageId,
                ActionDefinitions = assets.ActionDefinitions.ToArray(),
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
                NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([15]),
                NativeKnownMagic = game.ReadNewGameKnownMagic(),
                NativeGlobalMessages = assets.ReadNativeGlobalBattleMessages(),
                NativeCommandNames = assets.ReadNativeCommandNames(),
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
            battle = new(definition, BattleAtbMode.TurnBased);
            BattleScenePresentation scene = new(assets, definition, true);
            prior = battle.Capture();
            ColdNext("before-entry", prior);
            for (int callback = 0; callback < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); callback++)
            {
                prior = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.NativeEntryReady);
            Assert.True(battle.Combatants.Single(actor => actor.Id == "party").IsReady);
            BattleCombatant caster = battle.Combatants.Single(actor => actor.Id == "party");
            BattleCombatant enemy = battle.Combatants.Single(actor => actor.Id == "enemy");
            NativeBattleDrawSlot sourceSlot = enemy.NativeDrawSlots.Where(slot => slot.Id is > 0 and < 64 && caster.NativeMagicSlots.Where(stock => stock.SpellId == slot.Id).Sum(stock => stock.Count) < 100).OrderBy(slot => assets.ActionDefinitions.Single(action => action.Ability == BattleAbility.Magic && action.Id == slot.Id).DrawResist + slot.Resistance).First();
            selectedSpell = sourceSlot.Id;
            BattleActionDefinition spell = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Id == selectedSpell);
            NativeMagicStock[] initialInventory = caster.NativeMagicSlots.ToArray();
            int initialCount = initialInventory.Where(stock => stock.SpellId == selectedSpell).Sum(stock => stock.Count);
            int initialEnemyHp = enemy.CurrentHp;
            int initialSounds = battle.Events.Count(item => item.NativeModuleSound is not null);
            string initialCamera = JsonSerializer.Serialize(battle.Capture().NativeCamera);
            File.WriteAllText(Path.Combine(output!, "fixture-selection.json"), JsonSerializer.Serialize(new { Scenario = "Fresh ordinary Draw Stock; module15 is executable-owned command6, not item32 Friendship", Formation = formation.Id, formation.StageId, EnemyModel = 61, EnemyLevel = enemy.Level, sourceSlot, Spell = spell.Id, spell.DrawResist, CasterLevel = caster.Level, caster.Magic, InitialInventory = initialInventory, InitialKnownMagic = battle.Capture().NativeKnownMagic }));
            ColdNext("before-command", battle.Capture());
            battle.QueueAction("party", "enemy", BattleAbility.Draw, selectedSpell);
            ColdNext("queued", battle.Capture());
            // This bound detects a stalled command; only its actual producer and native task release establish completion.
            for (; callbacks < 4096; callbacks++)
            {
                boundary = "native-callback";
                prior = battle.Capture();
                NativeBattleRandomState candidateRandom = prior.Random ?? throw new InvalidDataException("Ordinary Draw requires its actual battle RNG.");
                candidateRandom = candidateRandom.Draw(out byte roll);
                int candidateQuantity = Math.Clamp(((((caster.Level - enemy.Level + 10) >> 1) - spell.DrawResist + (roll & 31) + 1 + caster.Magic) / 5) - sourceSlot.Resistance, 0, 9);
                battle.Tick();
                BattleSnapshot current = battle.Capture();
                BattleNativeDrawContext? result = current.ActiveAction?.NativeDraw;
                if (!prepared && result is not null)
                {
                    Assert.False(result.Failed, "The actual ordinary Draw roll failed before module15 admission.");
                    Assert.InRange(candidateQuantity, 1, 9);
                    expectedQuantity = candidateQuantity;
                    expectedAdded = Math.Min(100 - initialCount, expectedQuantity);
                    Assert.Equal((byte)selectedSpell, result.Selection);
                    Assert.Equal(expectedQuantity, result.Quantity);
                    Assert.Equal(expectedAdded, result.Added);
                    Assert.Equal(JsonSerializer.Serialize(candidateRandom), JsonSerializer.Serialize(current.Random));
                    prepared = true;
                    ColdNext("stock-produced", current);
                }
                NativeMagicStock[] inventory = current.Combatants.Single(actor => actor.Id == "party").NativeMagicInventory!;
                Assert.Equal(initialCount + (prepared ? expectedAdded : 0), inventory.Where(stock => stock.SpellId == selectedSpell).Sum(stock => stock.Count));
                Assert.Equal(casterInitialHp, current.Combatants.Single(actor => actor.Id == "party").CurrentHp);
                Assert.Equal(initialEnemyHp, current.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
                NativeBattleOriginalEffectState? after = current.NativeGraphics!.OriginalEffect is { Binding.Effect: 15 } effect ? effect : null;
                cameraChanged |= JsonSerializer.Serialize(current.NativeCamera) != initialCamera;
                if (!initialized && after?.ReturnedDescriptor is > 0)
                {
                    Assert.NotNull(after.DrawStock15);
                    Assert.NotNull(current.NativeEffectMemory!.DrawStock15Executable);
                    NativeBattleEffectParameters parameters = after.Parameters!;
                    NativeBattleActionHeaderSlot header = current.NativeActionPackets!.Headers[parameters.HeaderIndex7C];
                    Assert.Equal((byte)0, header.ReadByte(0));
                    Assert.Equal((byte)6, header.ReadByte(1));
                    Assert.Equal((ushort)selectedSpell, NativeBattleActionGraphics.Word(header, 4));
                    Assert.Equal((ushort)15, NativeBattleActionGraphics.Word(header, 6));
                    Assert.Equal((byte)1, current.NativeActionPackets.ContactCount);
                    NativeBattleContactHalf half = current.NativeActionPackets.ReadContact(header.FirstContactIndex!.Value).Primary;
                    Assert.Equal((byte)3, half.ActorIndex);
                    Assert.Equal((ushort)0, half.Amount6);
                    Assert.NotEqual(0, half.ContactFlags & 1);
                    Assert.Equal(JsonSerializer.Serialize(game.ReadNativeDrawStock15Resources().Animation), JsonSerializer.Serialize(current.NativeEffectMemory.Textures!.Animation!.Source));
                    initialized = true;
                    ColdNext("initialized", current);
                }
                if (CrossedContactGate(prior, current))
                {
                    contactTransitions++;
                    NativeBattleModulePoolTask target = current.NativeGraphics.OriginalEffect!.Pools[1].Slots.First(task => task.Callback == 0x8B0C70 && task.ReadByte(0x29) == 3);
                    int contactIndex = current.NativeActionPackets!.Headers[unchecked((sbyte)target.ReadByte(0x2A)) + current.NativeGraphics.OriginalEffect.Parameters!.HeaderIndex7C].FirstContactIndex!.Value + unchecked((sbyte)target.ReadByte(0x2B));
                    NativeBattleContactHalf half = current.NativeActionPackets.ReadContact(contactIndex).Primary;
                    Assert.DoesNotContain(current.NativeGraphics.Effects.Slots, task => (task.Flags0 & 1) != 0 && task.Kind == NativeBattleEffectKind.DamageText5069B0 && task.ModelIndex == half.ActorIndex);
                    ColdNext("contact", current);
                }
                if (after?.ReturnedDescriptor is > 0 || prior.NativeGraphics?.OriginalEffect is { Binding.Effect: 15, ReturnedDescriptor: > 0 })
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> capturedOwned = DrawStockPacketReferences(current);
                    foreach (NativeBattleLogicalCommand command in current.NativeGraphics!.PrimitiveFrame!.LastCommands.Select(item => item.Command).Where(command => capturedOwned.Contains(command.Packet) && command.Opcode is >= 0x20 and < 0x80))
                    {
                        Assert.True(command.PilotGeometry is not null, $"Draw Stock uncaptured owned pass at frame {current.Frame}: {command.Packet}, opcode {command.Opcode:X2}, word {command.WordOffset}.");
                        Assert.Equal((ushort)15, command.PilotGeometry!.Identity.Effect);
                        smoothCaptured++;
                    }
                }
                if (after?.ReturnedDescriptor is > 0 && !liveRestored)
                {
                    ImmutableHashSet<NativeBattlePrimitiveReference> owned = DrawStockPacketReferences(current);
                    if (!owned.IsEmpty)
                    {
                        NativeBattlePrimitiveFrame frame = current.NativeGraphics.PrimitiveFrame!;
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        BattleSnapshot without = current with { NativeGraphics = current.NativeGraphics with { PrimitiveFrame = frame with { LastCommands = frame.LastCommands.Where(command => !owned.Contains(command.Command.Packet)).ToImmutableArray() } } };
                        byte[] plain = new BattleScenePresentation(assets, definition, true).Render(BattleSession.Restore(without), 320, 224).ToArray();
                        peakChangedRgb = CountRgbChanges(pixels, plain);
                        if (peakChangedRgb > 0)
                        {
                            NativeBattleRestorationPresentationTests.Publish(output!, "visible", current, pixels);
                            NativeBattleRestorationPresentationTests.Publish(output!, "without-effect", without, plain);
                            ColdNext("live", current);
                            liveRestored = true;
                        }
                    }
                }
                if (prior.NativeGraphics!.OriginalEffect is { Binding.Effect: 15, ReturnedDescriptor: > 0 } && after?.ReturnedDescriptor == 0)
                {
                    completions++;
                }
                if (initialized && Released(current))
                {
                    break;
                }
            }
            boundary = "acceptance";
            BattleSnapshot completed = battle.Capture();
            Assert.True(prepared);
            Assert.True(initialized);
            Assert.True(liveRestored);
            Assert.True(peakChangedRgb > 0);
            Assert.Equal(1, contactTransitions);
            Assert.Equal(1, completions);
            Assert.True(smoothCaptured > 0);
            Assert.True(cameraChanged);
            Assert.True(Released(completed));
            Assert.Single(battle.Events, item => item.Kind == "Draw" && item.ActorId == "party" && item.TargetId == "enemy" && item.Amount == expectedAdded);
            Assert.Single(battle.Events, item => item.Kind == "ActionComplete" && item.ActorId == "party");
            Assert.True(completed.Events.Count(item => item.NativeModuleSound is not null) > initialSounds);
            foreach (uint sound in new uint[] { 115, 116, 117 })
            {
                Assert.Single(completed.Events, item => item.NativeModuleSound?.Sound == sound);
            }
            Assert.Equal(initialCount + expectedAdded, completed.Combatants.Single(actor => actor.Id == "party").NativeMagicInventory!.Where(stock => stock.SpellId == selectedSpell).Sum(stock => stock.Count));
            Assert.Equal(initialEnemyHp, completed.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
            ColdNext("completed", completed);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", Scenario = "Ordinary original Draw Stock command6, quantity formula, inventory gain and module15 physical contact", selectedSpell, sourceSlot, expectedQuantity, expectedAdded, callbacks, DiagnosticMaximumCallbacks = 4096, contactTransitions, completions, peakChangedRgb, initialized, liveRestored, cameraChanged, RestoredBoundaries = coldBoundaries, completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));

            void ColdNext(string name, BattleSnapshot snapshot)
            {
                boundary = "cold-" + name;
                BattleSnapshot? previous = prior;
                prior = snapshot;
                NativeBattleRestorationPresentationTests.Publish(output!, name, snapshot, new BattleScenePresentation(assets, definition, true).Render(BattleSession.Restore(snapshot), 320, 224).ToArray());
                BattleSession cold = BattleSession.Restore(NativeBattleEffectFamilyRunner.LoadSnapshot(Path.Combine(output!, name + ".battle.json")));
                BattleSession direct = BattleSession.Restore(snapshot);
                cold.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                direct.AttachNativeEffectResourceResolver(effect => game.ReadNativeEffectCatalogForEffects([effect])[effect - 1]);
                Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(cold.Capture()));
                cold.Tick();
                direct.Tick();
                Assert.Equal(JsonSerializer.Serialize(direct.Capture()), JsonSerializer.Serialize(cold.Capture()));
                byte[] pixels = new BattleScenePresentation(assets, definition, true).Render(cold, 320, 224).ToArray();
                Assert.Equal(new BattleScenePresentation(assets, definition, true).Render(direct, 320, 224).ToArray(), pixels);
                NativeBattleRestorationPresentationTests.Publish(output!, name + "-next", cold.Capture(), pixels);
                coldBoundaries.Add(name);
                prior = previous;
            }
        }
        catch (Exception error)
        {
            if (prior is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", prior, null);
            }
            if (battle is not null)
            {
                NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            }
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", boundary, callbacks, selectedSpell, expectedQuantity, expectedAdded, initialized, prepared, liveRestored, contactTransitions, peakChangedRgb, Error = error.ToString() }));
            throw;
        }
    }

    private static bool CrossedContactGate(BattleSnapshot prior, BattleSnapshot current)
    {
        if (prior.NativeGraphics!.OriginalEffect is not { Binding.Effect: 15, ReturnedDescriptor: > 0 } before || current.NativeGraphics!.OriginalEffect is not { Binding.Effect: 15 } after)
        {
            return false;
        }
        return before.Pools[1].Slots.Select((task, index) => (task, index)).Any(item => (item.task.Flags & 1) != 0 && item.task.Callback == 0x8B0C70 && item.task.ReadByte(0x29) == 2 && unchecked((short)item.task.ReadWord(0x24)) >= 15 && after.Pools[1].Slots[item.index].ReadByte(0x29) == 3);
    }

    private static ImmutableHashSet<NativeBattlePrimitiveReference> DrawStockPacketReferences(BattleSnapshot snapshot)
    {
        NativeBattleOriginalEffectState effect = snapshot.NativeGraphics!.OriginalEffect!;
        NativeBattleCuragaState nodes = effect.DrawStock15!.Nodes;
        NativeBattleModulePoolTask root = effect.Pools[0].Slots.First(task => task.Callback == 0x8B0A50 && ((task.Flags & 1) != 0 || effect.ReturnedDescriptor == 0));
        bool odd = (unchecked((ushort)(root.ReadWord(0x5C) - 1)) & 1) != 0;
        int primaryStart = nodes.SourceArenaOffset + (odd ? 0 : 0x9000);
        int secondaryStart = nodes.SourceArenaOffset + (odd ? 0x9000 : 0x12000);
        int primaryEnd = nodes.PrimaryDrawCursor ?? throw new InvalidDataException("Module15 lost its current primary packet cursor.");
        int secondaryEnd = nodes.SecondaryDrawCursor ?? throw new InvalidDataException("Module15 lost its current secondary packet cursor.");
        return snapshot.NativeGraphics.PrimitiveFrame!.LastCommands.Select(command => command.Command.Packet).Where(packet => packet.Bank == "20DFAB8" && (packet.ByteOffset >= primaryStart && packet.ByteOffset < primaryEnd || packet.ByteOffset >= secondaryStart && packet.ByteOffset < secondaryEnd)).ToImmutableHashSet();
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeEffectMemory!.Active == 0 && !graphics.Descriptors.Queue.Any(index => graphics.Descriptors.Packets[index].Command == 104) && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
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
}
