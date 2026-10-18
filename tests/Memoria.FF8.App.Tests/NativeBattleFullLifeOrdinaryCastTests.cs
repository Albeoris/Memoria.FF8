using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleFullLifeOrdinaryCastTests
{
    [Fact]
    public void Installed_Full_life_ordinary_cast_revives_a_KO_target_and_completes_through_disk_restore()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleActionDefinition fullLife = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.Id == 25);
        Assert.Equal("Full-life", fullLife.Name);
        Assert.Equal((ushort)27, fullLife.NativeAction!.MagicId);
        Assert.Equal((byte)6, fullLife.NativeAction.AttackType);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 1000, CurrentHp = 1000, Magic = 20, InitialAtb = BattleSession.AtbMaximum, NativeDeathCount = 0, MagicStocks = [new(25, 2)], Abilities = [BattleAbility.Attack, BattleAbility.Magic] },
            new() { Id = "target", Name = "Zell", IsPlayer = true, ModelId = 1, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == 1).Id, MaximumHp = 1000, CurrentHp = 0, NativeStatus0 = 1, NativeDeathCount = 1 },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1, MaximumHp = 9999, CurrentHp = 9999 }
        ];
        //Explicit fresh installed-resource component; all contacts and descriptors must be produced by the ordinary command.
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            ActionDefinitions = [fullLife],
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
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([27]),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_FULL_LIFE_CAST_EVIDENCE") ?? Path.GetTempPath(), "full-life-ordinary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Full-life ordinary-cast standalone evidence: {output}");
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        List<NativeBattleModuleSound> heard = [];
        BattleAudioPresentation audio = new(_ => { }, playModule: heard.Add);
        BattleSnapshot prior = battle.Capture();
        try
        {
            for (int callback = 0; callback < 400 && (!battle.NativeEntryReady || !battle.Combatants.Single(actor => actor.Id == "party").IsReady); callback++)
            {
                prior = battle.Capture();
                battle.Tick();
            }
            Assert.True(battle.NativeEntryReady);
            Assert.True(battle.Combatants.Single(actor => actor.Id == "party").IsReady);
            Assert.Equal(2, Assert.Single(battle.Combatants.Single(actor => actor.Id == "party").MagicStocks).Count);
            Assert.Equal((ushort)0, battle.Capture().NativePartyPresentation!.Rows[1]!.HitPoints);
            Assert.Equal(0, battle.Combatants.Single(actor => actor.Id == "target").CurrentHp);
            Assert.False(battle.Combatants.Single(actor => actor.Id == "target").IsAlive);
            Assert.Equal(1, battle.Capture().Combatants.Single(actor => actor.Id == "target").NativeStatus!.Saved & 1);
            Publish("before", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
            string cameraBefore = JsonSerializer.Serialize(battle.Capture().NativeCamera);
            battle.QueueAction("party", "target", BattleAbility.Magic, 25);
            BattleSession? clone = SaveAndLoad("queued");
            bool selectedCasting = false;
            bool selectedFullLife = false;
            bool cameraChanged = false;
            bool visibleChecked = false;
            bool contactChecked = false;
            bool producedRevivalContact = false;
            int changedRgbPixels = 0;
            int healingTransitions = 0;
            ushort displayedHp = 0;
            for (int callback = 0; callback < 2048; callback++)
            {
                prior = battle.Capture();
                battle.Tick();
                audio.Update(battle);
                audio.Update(battle);
                BattleSnapshot current = battle.Capture();
                if (clone is not null)
                {
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
                    clone = null;
                }
                NativeBattleActionPacketsState actionPackets = current.NativeActionPackets ?? throw new InvalidDataException("The ordinary Full-life cast lost its action packet owner.");
                if (actionPackets.ContactCount > 0)
                {
                    Assert.Equal((byte)1, actionPackets.ContactCount);
                    NativeBattleContactHalf contact = actionPackets.ReadContact(0).Primary;
                    Assert.Equal((byte)1, contact.ActorIndex);
                    Assert.Equal((ushort)1000, contact.Amount6);
                    Assert.Equal(0, contact.SavedStatuses4 & 1);
                    producedRevivalContact = true;
                }
                NativeBattleGraphicsState graphics = current.NativeGraphics!;
                selectedCasting |= graphics.ActionGraphics?.Callback == 0x50A9A0;
                selectedFullLife |= graphics.OriginalEffect is { Binding.Effect: 27, Restoration: not null };
                cameraChanged |= JsonSerializer.Serialize(current.NativeCamera) != cameraBefore;
                Assert.NotNull(current.NativeCamera!.Projection!.View);
                ushort hp = current.NativePartyPresentation!.Rows[1]!.HitPoints;
                if (hp != displayedHp)
                {
                    Assert.Equal((ushort)0, displayedHp);
                    Assert.Equal((ushort)1000, hp);
                    displayedHp = hp;
                    healingTransitions++;
                    clone = SaveAndLoad("contact");
                    contactChecked = true;
                }
                NativeBattlePrimitiveFrame frame = graphics.PrimitiveFrame!;
                if (!visibleChecked && frame.LastCommands.Any(command => command.Command.Packet.Bank == "20DFAB8"))
                {
                    byte[] visible = scene.Render(battle, 320, 224).ToArray();
                    NativeBattlePrimitiveFrame withoutEffect = frame with { LastCommands = frame.LastCommands.Where(command => command.Command.Packet.Bank != "20DFAB8").ToImmutableArray() };
                    BattleSession background = BattleSession.Restore(current with { NativeGraphics = graphics with { PrimitiveFrame = withoutEffect } });
                    byte[] plain = new BattleScenePresentation(assets, definition, true).Render(background, 320, 224).ToArray();
                    changedRgbPixels = CountRgbChanges(visible, plain);
                    if (changedRgbPixels > 0)
                    {
                        clone = SaveAndLoad("visible");
                        visibleChecked = true;
                    }
                }
                if (selectedFullLife && Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.True(selectedCasting);
            Assert.True(selectedFullLife);
            Assert.True(cameraChanged);
            Assert.True(visibleChecked);
            Assert.True(contactChecked);
            Assert.True(producedRevivalContact);
            Assert.True(changedRgbPixels > 0);
            Assert.Equal(1, healingTransitions);
            BattleCombatantSnapshot revived = completed.Combatants.Single(actor => actor.Id == "target");
            Assert.Equal(1000, revived.CurrentHp);
            Assert.Equal((ushort)1000, displayedHp);
            Assert.Equal(0, revived.NativeStatus!.Saved & 1);
            Assert.NotNull(revived.NativeStatus.Presentation.Saved);
            Assert.Equal(0, revived.NativeStatus.Presentation.Saved.Value & 1);
            Assert.True(battle.Combatants.Single(actor => actor.Id == "target").IsAlive);
            Assert.Equal(1000, completed.Combatants.Single(actor => actor.Id == "party").CurrentHp);
            NativeMagicStock remainingStock = Assert.Single(completed.Combatants.Single(actor => actor.Id == "party").NativeMagicInventory!, stock => stock.SpellId == 25);
            Assert.Equal((byte)1, remainingStock.Count);
            Assert.Equal(9999, completed.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
            Assert.True(Released(completed), "Full-life must release the ordinary action, action pause, descriptor, root and active tasks.");
            Assert.Equal((byte)0, completed.NativeEffectMemory!.Active);
            Assert.Equal(0, completed.NativeGraphics!.Models[0].Flags0 & 4);
            uint effectSound = game.ReadNativeRestorationResources(27).Sound;
            NativeBattleModuleSound sound = Assert.Single(heard, item => item.Sound == effectSound);
            Assert.Equal((byte)128, sound.Pan);
            Assert.Single(battle.Events, entry => entry.NativeModuleSound?.Sound == effectSound);
            BattleSession after = SaveAndLoad("completed");
            int soundCount = heard.Count;
            audio.Update(after);
            Assert.Equal(soundCount, heard.Count);
            prior = completed;
            battle.Tick();
            after.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(after.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(after, 320, 224).ToArray());
            Publish("completed-next", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", RevivedHitPoints = revived.CurrentHp, changedRgbPixels, healingTransitions, completed.Frame, Callback = completed.NativeGraphics.Callback }));
        }
        catch (Exception exception)
        {
            Publish("before-failure", prior, null);
            Publish("failed", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", Exception = exception.ToString(), prior.Frame, Callback = prior.NativeGraphics?.Callback }));
            throw;
        }

        BattleSession SaveAndLoad(string name)
        {
            BattleSnapshot snapshot = battle.Capture();
            byte[] pixels = scene.Render(battle, 320, 224).ToArray();
            Publish(name, snapshot, pixels);
            using FileStream input = File.OpenRead(Path.Combine(output, name + ".battle.json"));
            BattleSnapshot loaded = JsonSerializer.Deserialize<BattleSnapshot>(input)!;
            BattleSession restored = BattleSession.Restore(loaded);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(pixels, new BattleScenePresentation(assets, loaded.Definition, true).Render(restored, 320, 224).ToArray());
            return restored;
        }

        void Publish(string name, BattleSnapshot snapshot, byte[]? pixels)
        {
            NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, pixels);
        }
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0));
    }

    private static int CountRgbChanges(byte[] first, byte[] second)
    {
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
