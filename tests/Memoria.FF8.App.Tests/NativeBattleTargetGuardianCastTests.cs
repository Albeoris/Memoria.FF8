using System.Text.Json;
using System.Collections.Immutable;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed partial class NativeBattleTargetGuardianCastTests
{
    [Fact]
    public void Installed_Quezacotl_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(0, 116);

    [Fact]
    public void Installed_Shiva_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(1, 185);

    [Fact]
    public void Installed_Ifrit_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(2, 201);

    [Fact]
    public void Installed_Ifrit_entry_reaches_the_shared_scripted_opening_and_independent_disk_state() => Run(2, 201, true);

    [Theory]
    [InlineData(7, 6)]
    [InlineData(12, 202)]
    [InlineData(9, 203)]
    [InlineData(10, 204)]
    [InlineData(4, 205)]
    [InlineData(15, 206)]
    public void Installed_cinematic_guardian_entry_preserves_its_selected_profile_and_independent_disk_state(int guardian, ushort effect) => Run(guardian, effect, true);

    [Theory]
    [InlineData(7, 6)]
    [InlineData(12, 202)]
    [InlineData(9, 203)]
    [InlineData(10, 204)]
    [InlineData(4, 205)]
    [InlineData(15, 206)]
    public void Installed_cinematic_guardian_ordinary_command_completes_graphics_contact_and_disk_continuations(int guardian, ushort effect) => Run(guardian, effect);

    [Fact]
    public void Installed_Cactuar_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(13, 199);

    [Fact]
    public void Installed_Tonberry_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(14, 90);

    [Fact]
    public void Installed_Siren_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(3, 95);

    [Fact]
    public void Installed_Carbuncle_ordinary_command_completes_graphics_contact_and_disk_continuations() => Run(6, 278);

    [Fact]
    public void Continue_the_named_cinematic_guardian_checkpoint_to_completion()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_CINEMATIC_SNAPSHOT");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_CINEMATIC_EVIDENCE");
        Assert.SkipUnless(source is not null && output is not null, "Select a standalone cinematic guardian snapshot and a distinct evidence directory.");
        Assert.False(Directory.Exists(output));
        Directory.CreateDirectory(output!);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(source!))!;
        using GameAssets game = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = BattleSession.Restore(snapshot);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(battle.Capture()));
        battle.AttachNativeEffectResourceResolver(id => game.ReadNativeEffectCatalogForEffects([id])[id - 1]);
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        BattleSnapshot before = snapshot;
        try
        {
            for (int index = 0; index < 4096; index++)
            {
                before = battle.Capture();
                battle.Tick();
                if (battle.NativeGraphics?.OriginalEffect is { ReturnedDescriptor: > 0 } && index == 60)
                {
                    NativeBattleRestorationPresentationTests.Publish(output!, "midpoint", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
                }
                if (Released(battle.Capture()))
                {
                    BattleSnapshot completed = battle.Capture();
                    NativeBattleRestorationPresentationTests.Publish(output!, "completed", completed, scene.Render(battle, 320, 224).ToArray());
                    BattleSession clone = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(completed))!);
                    Assert.Equal(JsonSerializer.Serialize(completed), JsonSerializer.Serialize(clone.Capture()));
                    battle.Tick();
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, snapshot.Definition, true).Render(clone, 320, 224).ToArray());
                    NativeBattleRestorationPresentationTests.Publish(output!, "completed-next", battle.Capture(), scene.Render(battle, 320, 224).ToArray());
                    File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "completed", completed.Frame, NativeCallback = completed.NativeGraphics!.Callback }));
                    return;
                }
            }
            Assert.Fail("The guardian did not complete within its diagnostic callback bound.");
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output!, "before-boundary", before, null);
            NativeBattleRestorationPresentationTests.Publish(output!, "failed-live", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output!, "result.json"), JsonSerializer.Serialize(new { Status = "blocked", error = error.ToString(), before.Frame, NativeCallback = before.NativeGraphics?.Callback }));
            BattleSession checkpoint = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(before))!);
            Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(checkpoint.Capture()));
            throw;
        }
    }

    [Fact]
    public void Resume_the_named_target_guardian_checkpoint_for_one_callback()
    {
        string? path = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_SNAPSHOT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(path), "Select a concrete standalone target guardian checkpoint.");
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(File.ReadAllBytes(path!))!;
        Assert.True(snapshot.NativeGraphics!.OriginalEffect!.Binding.Effect is 6 or 185 or 201 or 202 or 203 or 204 or 205 or 206);
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        Assert.Equal(snapshot.NativeGraphics.Callback + 1, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        byte[] pixels = new BattleScenePresentation(assets, snapshot.Definition, true).Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_RESUME_EVIDENCE");
        if (output is not null)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "guardian-next-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff"), first.Capture(), pixels);
        }
        Console.WriteLine($"Guardian checkpoint {snapshot.Frame}/{snapshot.NativeGraphics.Callback} continued at {first.Frame}/{first.NativeGraphics.Callback}; whole JSON and RGBA agree.");
    }

    private static void Run(int guardian, ushort effectId, bool openingOnly = false, NativeBattleRetainedMemory? retainedMemory = null)
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Select installed original resources with MEMORIA_FF8_GAME_DIR.");
        using GameAssets game = new(directory!);
        using BattleAssets assets = new(game.GameDirectory);
        BattleDefinition definition = CreateDefinition(game, assets, guardian, effectId);
        if (retainedMemory is not null)
        {
            definition = definition with { NativeRetainedMemory = retainedMemory };
        }
        string output = Path.Combine(Environment.GetEnvironmentVariable("MEMORIA_FF8_TARGET_GUARDIAN_EVIDENCE") ?? Path.GetTempPath(), "guardian-" + guardian + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Console.WriteLine($"Ordinary guardian-cast evidence: {output}");
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        BattleScenePresentation scene = new(assets, definition, true);
        List<NativeBattleModuleSound> sounds = [];
        int stopSoundCalls = 0;
        BattleAudioPresentation audio = new(_ => { }, playModule: sounds.Add, stopSounds: () => stopSoundCalls++);
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
            Publish("before", battle.Capture());
            BattleActionDefinition guardianAction = definition.ActionDefinitions.Single(action => action.Ability == BattleAbility.GuardianForce && action.Id == guardian);
            string target = guardianAction.Allies ? "party" : "enemy";
            battle.QueueAction("party", target, BattleAbility.GuardianForce, guardian);
            BattleSession? clone = SaveAndLoad("queued");
            bool produced = false;
            bool selected = false;
            bool visible = false;
            bool contact = false;
            bool cameraChanged = false;
            bool castingHidden = false;
            bool pendingTimPreserved = false;
            int visibleRgbChanges = 0;
            string beforeCamera = JsonSerializer.Serialize(battle.Capture().NativeCamera);
            HashSet<string> boundaries = [];
            for (int callback = 0; callback < 4096; callback++)
            {
                prior = battle.Capture();
                battle.Tick();
                audio.Update(battle);
                BattleSnapshot current = battle.Capture();
                if (clone is not null)
                {
                    clone.Tick();
                    Assert.Equal(JsonSerializer.Serialize(current), JsonSerializer.Serialize(clone.Capture()));
                    Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(clone, 320, 224).ToArray());
                    clone = null;
                }
                produced |= current.ActiveAction?.NativeQueuedCommand == 254;
                NativeBattleGraphicsState graphics = current.NativeGraphics!;
                bool active = graphics.OriginalEffect is { ReturnedDescriptor: > 0 } && graphics.OriginalEffect.Binding.Effect == effectId;
                if (active)
                {
                    Assert.Equal((byte)254, current.NativeActionScratch!.Command);
                    Assert.Equal((ushort)(64 + guardian), current.NativeActionScratch.Action);
                    selected = true;
                    if (openingOnly)
                    {
                        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(effectId);
                        Assert.Equal(effectId, current.NativeEffectMemory!.Cinematic!.Effect);
                        Assert.NotNull(graphics.OriginalEffect!.Binding.ScriptedInitializationDirectories);
                        Assert.Equal(profile.Callback, Assert.Single(Assert.Single(graphics.OriginalEffect.Pools).Slots).Callback);
                        _ = SaveAndLoad("scripted-opening");
                        File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "opening-validated", guardian, effectId, current.Frame, NativeCallback = graphics.Callback, Boundary = "Initialized selected original resources and retained cinematic task; full cast remains pending" }));
                        return;
                    }
                    cameraChanged |= beforeCamera != JsonSerializer.Serialize(current.NativeCamera);
                    castingHidden |= (graphics.Models[0].Flags0 & (effectId == 278 ? 4 : 8)) != 0;
                    NativeBattlePrimitiveFrame primitive = graphics.PrimitiveFrame!;
                    ImmutableHashSet<NativeBattlePrimitiveReference> effectPackets = visible ? [] : GuardianPacketReferences(prior, current, effectId, game);
                    if (!visible && effectPackets.Count > 0)
                    {
                        byte[] pixels = scene.Render(battle, 320, 224).ToArray();
                        NativeBattlePrimitiveFrame withoutPrivate = primitive with { LastCommands = primitive.LastCommands.Where(command => !effectPackets.Contains(command.Command.Packet)).ToImmutableArray() };
                        BattleSession background = BattleSession.Restore(current with { NativeGraphics = graphics with { PrimitiveFrame = withoutPrivate } });
                        byte[] backgroundPixels = new BattleScenePresentation(assets, definition, true).Render(background, 320, 224).ToArray();
                        visibleRgbChanges = CountRgbChanges(pixels, backgroundPixels);
                        if (visibleRgbChanges > 0)
                        {
                            visible = true;
                            clone = SaveAndLoad("visible");
                        }
                    }
                    string? boundary = null;
                    if (effectId == 185)
                    {
                        NativeBattleModulePoolTask primary = graphics.OriginalEffect!.Pools[1].Slots[0];
                        if (!pendingTimPreserved && primary.Callback == 0x5C0F30 && primary.ReadWord(0x0C) == 15)
                        {
                            NativeBattleEffectFileAsset texture = game.ReadNativeShivaResources().Files[0];
                            Assert.Equal(texture.Data.ToArray(), current.NativeEffectMemory!.Arena!.Read(0x10000, texture.Data.Length));
                            pendingTimPreserved = true;
                            clone = SaveAndLoad("pending-texture-upload");
                        }
                        NativeBattleModulePoolTask? model = graphics.OriginalEffect!.Pools[1].Slots.FirstOrDefault(task => task.Callback == 0x5C4AD0 && (task.Flags & 1) != 0);
                        NativeBattleModulePoolTask? shatter = graphics.OriginalEffect.Pools[1].Slots.FirstOrDefault(task => task.Callback == 0x5C77C0 && (task.Flags & 1) != 0);
                        if (model is not null && model.ReadWord(0x0C) == 1)
                        {
                            boundary = "model-plane";
                        }
                        else if (shatter is not null && shatter.ReadWord(0x0C) == 17)
                        {
                            boundary = "private-scroll";
                        }
                        else if (shatter is not null && shatter.ReadWord(0x0C) == 33)
                        {
                            boundary = "common-shatter";
                        }
                    }
                    else if (graphics.OriginalEffect!.Pools.SelectMany(pool => pool.Slots).Any(task => (task.Flags & 1) != 0 && task.ReadWord(0x0C) == 32))
                    {
                        boundary = "active-geometry";
                    }
                    if (boundary is not null && boundaries.Add(boundary))
                    {
                        clone = SaveAndLoad(boundary);
                    }
                }
                if (!contact && current.Events.Any(entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == target))
                {
                    contact = true;
                    clone = SaveAndLoad("contact");
                }
                if (selected && Released(current))
                {
                    break;
                }
            }
            BattleSnapshot completed = battle.Capture();
            Assert.Single(battle.Events, entry => entry.Kind == "GuardianForceCharging" && entry.ActorId == "party");
            Assert.Single(battle.Events, entry => entry.Kind == "GuardianForceReady" && entry.ActorId == "party");
            Assert.True(produced);
            Assert.True(selected);
            Assert.True(visible);
            Assert.True(cameraChanged);
            Assert.True(castingHidden);
            Assert.True(contact);
            Assert.NotEmpty(sounds);
            if (effectId != 278)
            {
                Assert.True(stopSoundCalls > 0);
            }
            BattleEvent damage = Assert.Single(battle.Events, entry => entry.Kind == "Damage" && entry.ActorId == "party" && entry.TargetId == target);
            Assert.Equal((target == "party" ? 1000 : 9999) - damage.Amount, completed.Combatants.Single(actor => actor.Id == target).CurrentHp);
            Assert.True(visibleRgbChanges > 0);
            if (guardianAction.Allies)
            {
                Assert.Equal(0, damage.Amount);
                Assert.Equal(9999, completed.Combatants.Single(actor => actor.Id == "enemy").CurrentHp);
                uint statuses = guardianAction.NativeAction!.Statuses1;
                Assert.NotEqual(0u, statuses);
                Assert.Equal(statuses, completed.Combatants.Single(actor => actor.Id == "party").NativeStatus!.Battle & statuses);
            }
            else
            {
                Assert.InRange(completed.Combatants.Single(actor => actor.Id == "enemy").CurrentHp, 1, 9998);
            }
            Assert.True(Released(completed));
            Assert.Equal(0, completed.NativeGraphics!.Models[0].Flags0 & 0xC);
            if (effectId == 185)
            {
                Assert.True(pendingTimPreserved);
                Assert.Equal(3, completed.NativeEffectMemory!.Shiva!.Entry!.Counter22BD010);
                Assert.False(completed.NativeVibrationProcess!.IsPlaying(completed.NativeEffectMemory.Shiva.VibrationHandle));
                foreach (uint sound in game.ReadNativeShivaResources().Sounds)
                {
                    Assert.Contains(sounds, item => item.Sound == sound);
                }
                Assert.Subset(boundaries, new HashSet<string> { "model-plane", "private-scroll", "common-shatter" });
                Assert.Equal(3, boundaries.Count);
            }
            BattleSession after = SaveAndLoad("completed");
            prior = completed;
            battle.Tick();
            after.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(after.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, definition, true).Render(after, 320, 224).ToArray());
            Publish("completed-next", battle.Capture());
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "passed", guardian, effectId, completed.Frame, NativeCallback = completed.NativeGraphics.Callback, boundaries, visibleRgbChanges, SoundCount = sounds.Count }));
        }
        catch (Exception error)
        {
            NativeBattleRestorationPresentationTests.Publish(output, "before-failure", prior, null);
            NativeBattleRestorationPresentationTests.Publish(output, "failed", battle.Capture(), null);
            File.WriteAllText(Path.Combine(output, "result.json"), JsonSerializer.Serialize(new { Status = "failed", guardian, effectId, error = error.ToString(), prior.Frame, NativeCallback = prior.NativeGraphics?.Callback }));
            throw;
        }

        void Publish(string name, BattleSnapshot snapshot) => NativeBattleRestorationPresentationTests.Publish(output, name, snapshot, scene.Render(BattleSession.Restore(snapshot), 320, 224).ToArray());

        BattleSession SaveAndLoad(string name)
        {
            BattleSnapshot snapshot = battle.Capture();
            Publish(name, snapshot);
            using FileStream input = File.OpenRead(Path.Combine(output, name + ".battle.json"));
            BattleSnapshot loaded = JsonSerializer.Deserialize<BattleSnapshot>(input)!;
            BattleSession restored = BattleSession.Restore(loaded);
            Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(restored.Capture()));
            Assert.Equal(scene.Render(battle, 320, 224).ToArray(), new BattleScenePresentation(assets, loaded.Definition, true).Render(restored, 320, 224).ToArray());
            return restored;
        }
    }

    private static bool Released(BattleSnapshot snapshot)
    {
        NativeBattleGraphicsState graphics = snapshot.NativeGraphics!;
        return snapshot.ActiveAction is null && snapshot.QueuedActions.Length == 0 && snapshot.GuardianForceCasts.Length == 0 && snapshot.NativeClock!.ActionPause == 0 && graphics.Descriptors.Queue.IsEmpty && graphics.OriginalEffect is { ReturnedDescriptor: 0 } effect && (effect.Binding.Effect == 116 ? snapshot.NativeEffectMemory is { Active: 0 } && !graphics.Descriptors.Tasks.Any(task => (task.Flags & 1) != 0 && task.Kind == NativeBattleDescriptorTaskKind.Action104) : effect.Pools.All(pool => pool.Slots.All(task => (task.Flags & 1) == 0)));
    }

    private static int CountRgbChanges(byte[] first, byte[] second)
    {
        int changes = 0;
        for (int offset = 0; offset < first.Length; offset += 4)
        {
            if (!first.AsSpan(offset, 3).SequenceEqual(second.AsSpan(offset, 3)))
            {
                changes++;
            }
        }
        return changes;
    }

    internal static BattleDefinition CreateDefinition(GameAssets game, BattleAssets assets, int guardian, ushort effectId)
    {
        BattleEncounter formation = assets.LoadEncounter(94);
        BattleStage stage = assets.LoadStage(formation.StageId);
        BattleActionDefinition action = assets.ActionDefinitions.Single(item => item.Ability == BattleAbility.GuardianForce && item.Id == guardian) with { Level = 10, Available = true, NativeGuardianBoostAvailable = false };
        Assert.Equal(effectId, action.NativeAction!.MagicId);
        Assert.Equal((byte)254, action.NativeAction.Command);
        Assert.Equal((ushort)(64 + guardian), action.NativeAction.ActionId);
        NativeBattleStatusAllocation partyStatus = assets.ReadPlayerNativeStatusData(game.ReadNewGameCharacterVitals(), 0);
        BattleCombatantDefinition[] actors =
        [
            new() { Id = "party", Name = "Squall", IsPlayer = true, ModelId = 0, WeaponId = 0, MaximumHp = 1000, CurrentHp = 1000, InitialAtb = BattleSession.AtbMaximum, NativeStatusResistances = partyStatus.Resistances.ToArray(), GuardianForceCompatibility = new ushort[16], EquipmentIds = [guardian], Abilities = [BattleAbility.Attack, BattleAbility.GuardianForce] },
            new() { Id = "enemy", Name = "Ifrit", ModelId = 61, SceneSlot = formation.Enemies.Single(enemy => enemy.ModelId == 61).Slot, Speed = 1, MaximumHp = 9999, CurrentHp = 9999, NativeStatusResistances = assets.ReadEnemyNativeStatusData(61).Resistances.ToArray() }
        ];
        // Explicit independent installed-resource component: the ordinary command owns charge, headers, formula and contact production.
        BattleDefinition definition = new()
        {
            Combatants = actors,
            EncounterId = formation.Id,
            StageId = formation.StageId,
            GuardianForces = [new(guardian, 1000, 1000)],
            ActionDefinitions = [action],
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
            NativeEffectCatalog = game.ReadNativeEffectCatalogForEffects([effectId]),
            NativeCommonEffectTexture = game.ReadNativeCommonEffectTexture(),
            NativeSavedOptions = game.ReadNewGameInputConfiguration().Options,
            NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap,
            NativeActionCameraRules = game.ReadNativeActionCameraRules(),
            NativeStageSource = stage.NativeSource,
            NativeStageCameraBank = stage.NativeCameraBank,
            NativeStageCameraSelectors = [formation.PrimaryCamera, formation.AlternativeCamera],
            NativeStageCameraControl = stage.NativeCameraControl
        };
        return NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
    }
}
