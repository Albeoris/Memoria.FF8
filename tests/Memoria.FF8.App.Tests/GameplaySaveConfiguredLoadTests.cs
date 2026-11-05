using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveConfiguredLoadTests
{
    [Fact]
    public void Pending_GF_history_survives_live_publication_player_loading_and_a_second_cold_save()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameState initial = GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, true), directory, sharedAssets: assets);
        NativeCharacterVitals characters = initial.CharacterVitals!;
        int ability = characters.MenuDefinitions.ResolveLearning(0).Select(rule => (int)rule.Ability).First(ability => ability > 0 && !characters.GuardianForces[0].HasLearnedAbility(ability));
        GuardianForceProgression?[] history = new GuardianForceProgression?[16];
        history[0] = new(true, [new(ability, true, 17, ItemTaught: true, Active: false)]);
        characters = characters with { GuardianForceRecoveryProgression = history.ToImmutableArray() };
        string expected = JsonSerializer.Serialize(characters.GuardianForceRecoveryProgression);
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-pending-gf-host-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string first = Path.Combine(folder, "first.sav");
            string second = Path.Combine(folder, "second.sav");
            GameSession session = new(initial with { CharacterVitals = characters });
            using (GameWorld source = new(session, directory, sharedAssets: assets))
            {
                source.EnsureField();
                _ = source.WriteGameplaySave(first);
            }
            byte[] original = File.ReadAllBytes(first);
            GameState loaded = PlayerStateLoader.Load(first, directory, sharedAssets: assets);
            Assert.Equal(expected, JsonSerializer.Serialize(loaded.CharacterVitals!.GuardianForceRecoveryProgression));
            Assert.False(loaded.CharacterVitals.GuardianForces[0].HasLearnedAbility(ability));
            GameSession resumed = new(loaded);
            using (GameWorld source = new(resumed, directory, sharedAssets: assets))
            {
                source.EnsureField();
                _ = source.WriteGameplaySave(second);
            }
            GameState again = PlayerStateLoader.Load(second, directory, sharedAssets: assets);
            Assert.Equal(expected, JsonSerializer.Serialize(again.CharacterVitals!.GuardianForceRecoveryProgression));
            Assert.False(again.CharacterVitals.GuardianForces[0].HasLearnedAbility(ability));
            Assert.Equal(original, File.ReadAllBytes(first));
            Assert.Equal(initial.Economy!.Gil, again.Economy!.Gil);
            Assert.Equal(initial.Playtime, again.Playtime);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Installed_GF_layers_are_composed_before_saved_learning_and_AP_are_restored()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets originals = new(directory);
        NativeCharacterVitals initial = originals.ReadNewGameCharacterVitals();
        initial.GuardianForces[0] = initial.GuardianForces[0] with { Flags = (byte)(initial.GuardianForces[0].Flags | 1) };
        NativeCharacterVitals available = initial.ApplyGuardianForceTemplates(initial.ResolveGuardianForceTemplates());
        int original = available.GuardianForceAbilities(0).First(entry => !entry.Learned && entry.RequiredPoints > 7).Ability;
        int replacement = Enumerable.Range(1, initial.MenuDefinitions.AbilityPoints.Length - 1).First(ability => initial.ResolveGuardianForceTemplates()[0].All(entry => entry.Ability != ability) && initial.MenuDefinitions.AbilityPoints[ability] > 7);
        GuardianForceAbilityPatchDocument patch = new()
        {
            DocumentId = "selected.gf-replacement",
            Operations = [new() { Id = "replace", GuardianForce = 0, Kind = GuardianForceAbilityOperationKind.ReplaceAbility, Ability = original, Replacement = replacement }]
        };
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-selected-gf-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(folder, "mods");
        string package = Path.Combine(mods, "Memoria", "Selected");
        string payload = Path.Combine(package, GuardianForceAbilityMods.PayloadPath);
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        string path = Path.Combine(folder, "slot.sav");
        try
        {
            File.WriteAllText(Path.Combine(package, "mod.ini"), "enabled=1\n");
            File.WriteAllBytes(payload, GuardianForceAbilityMods.Serialize(patch));
            using (GameResourceHost selected = new(directory, watch: false, modsDirectory: mods))
            using (GameAssets assets = new(directory, resources: selected.Resources, modelOverrides: selected))
            {
                Assert.Single(selected.GuardianForceMods.Layers);
                GameplaySaveComponentDocument document = GameplaySaveSessionLoaderTests.Document(assets, true);
                GameplaySaveGuardianForce[] guardians = document.Characters.GuardianForces.ToArray();
                guardians[0] = guardians[0] with { Flags = (byte)(guardians[0].Flags | 1) };
                document = document with { Characters = document.Characters with { GuardianForces = guardians } };
                GameState restored = GameplaySaveSessionLoader.Restore(document, directory, assets, selected.Resources, selected, () => selected.GameplayPolicy, initialized => GameplaySaveGuardianForceDefinitions.Select(initialized, selected));
                NativeCharacterVitals earned = restored.CharacterVitals!.SelectGuardianForceLearning(0, replacement).AwardGuardianForceAbilityPoints(0, 3);
                GameSession session = new(restored with { CharacterVitals = earned });
                using GameWorld world = new(session, directory, resources: selected.Resources, sharedAssets: assets, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy);
                world.EnsureField();
                _ = world.WriteGameplaySave(path);
            }
            NativeCharacterVitals loaded = PlayerStateLoader.Parse(["--state", path, "--game-dir", directory, "--mods-dir", mods]).InitialState.CharacterVitals!;
            Assert.Equal((byte)replacement, loaded.GuardianForces[0].Learning);
            Assert.Equal(3, loaded.GuardianForceAbilities(0).Single(entry => entry.Ability == replacement).Progress);
            Assert.Equal(patch.DocumentId, Assert.Single(loaded.GuardianForceProfile!.AppliedLayers).Document.DocumentId);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void Complete_legacy_startup_does_not_open_unneeded_game_or_mod_sources()
    {
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-legacy-startup-" + Guid.NewGuid().ToString("N"));
        GameState original = GameSession.CreateColdProcess().State with { Scene = SceneKind.Field };
        try
        {
            new GameSession(original).Save(path, compact: true);
            LaunchOptions options = PlayerStateLoader.Parse(["--state", path, "--game-dir", "missing-selected-game", "--mods-dir", "missing-selected-mods"]);
            Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(options.InitialState));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Semantic_startup_uses_the_selected_script_mod_before_checking_continuation_identity()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets originals = new(directory);
        byte[] script = originals.LoadField(165).Jsm;
        byte[] replacement = [.. script, .. script.AsSpan(script.Length - 4).ToArray()];
        string folder = Path.Combine(Path.GetTempPath(), "Memoria.FF8-configured-gameplay-" + Guid.NewGuid().ToString("N"));
        string mods = Path.Combine(folder, "mods");
        string package = Path.Combine(mods, "Junction", "Script");
        string payload = Path.Combine(package, "data", "field", "mapdata", "bg", "bghall_1", "bghall_1.jsm");
        Directory.CreateDirectory(Path.GetDirectoryName(payload)!);
        string path = Path.Combine(folder, "slot.sav");
        try
        {
            File.WriteAllText(Path.Combine(package, "mod.xml"), "<ModInfo><Name>Script</Name><ModFolder Folder=\"data\" ActiveWhen=\"1\" /></ModInfo>");
            File.WriteAllBytes(payload, replacement);
            using (GameResourceHost selected = new(directory, watch: false, modsDirectory: mods))
            using (GameAssets assets = new(directory, resources: selected.Resources, modelOverrides: selected))
            {
                Assert.Equal(replacement, assets.LoadField(165).Jsm);
                GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveFieldPartyResumeTests.ThreePartyDocument(assets), directory, assets, selected.Resources, selected, () => selected.GameplayPolicy));
                using GameWorld world = new(session, directory, resources: selected.Resources, sharedAssets: assets, modelOverrides: selected, readGameplayPolicy: () => selected.GameplayPolicy);
                world.EnsureField();
                Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
                _ = world.WriteGameplaySave(path);
            }
            LaunchOptions loaded = PlayerStateLoader.Parse(["--state", path, "--mods-dir", mods, "--game-dir", directory]);
            Assert.Equal(SceneKind.Field, loaded.InitialState.Scene);
            Assert.Equal(165, loaded.InitialState.FieldId);
            Assert.Equal(replacement, File.ReadAllBytes(payload));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
