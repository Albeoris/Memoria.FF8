using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ActiveBattleResourceProfileCompatibilityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Completed_legacy_fight_returns_to_selected_allocation_once(int fixture)
    {
        string path = TestResources.FixturePath("App.Tests.ActiveBattleResourceProfileCompatibilityTests." + fixture);
        string sourceDirectory = TestResources.GameDirectory;
        string? targetDirectory = Environment.GetEnvironmentVariable("MEMORIA_FF8_CROSS_EDITION_GAME_DIR");
        Assert.SkipUnless(targetDirectory is not null && Directory.Exists(targetDirectory), "Configure MEMORIA_FF8_CROSS_EDITION_GAME_DIR for the other installed edition.");
        byte[] originalHash = Hash(path);
        GameState physical = Complete(sourceDirectory);
        GameState projected = Complete(targetDirectory!);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(physical.ExamTimer), JsonSerializer.SerializeToElement(projected.ExamTimer)));
        Assert.Equal(physical.CrtRandom, projected.CrtRandom);
        Assert.Equal(physical.AppliedBattleResultIds, projected.AppliedBattleResultIds);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(physical.Economy), JsonSerializer.SerializeToElement(projected.Economy)));
        GameplaySaveCharacters originalCharacters = GameplaySaveCharacters.Capture(physical.CharacterVitals!);
        GameplaySaveCharacters selectedCharacters = GameplaySaveCharacters.Capture(projected.CharacterVitals!);
        JsonElement expectedCharacters = JsonSerializer.SerializeToElement(originalCharacters);
        JsonElement actualCharacters = JsonSerializer.SerializeToElement(selectedCharacters);
        Assert.True(JsonElement.DeepEquals(expectedCharacters, actualCharacters), string.Join(", ", expectedCharacters.EnumerateObject().Where(property => !actualCharacters.TryGetProperty(property.Name, out JsonElement value) || !JsonElement.DeepEquals(property.Value, value)).Select(property => property.Name)));
        Assert.Equal(originalHash, Hash(path));

        GameState Complete(string directory)
        {
            using GameResourceHost resources = new(directory, watch: false);
            resources.ConnectEncounterMods();
            using GameAssets assets = new(directory, resources: resources.Resources, modelOverrides: resources);
            GameSession session = new(PlayerStateLoader.Load(path, directory, sharedAssets: assets, resources: resources.Resources, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy));
            using GameWorld world = World(session, directory, resources, assets);
            world.EnsureField();
            BattleEncounterOccurrence occurrence = session.State.PendingBattleEncounter!;
            int results = session.State.AppliedBattleResultIds.Length;
            world.ApplyBattleInput(new(BattleInputKind.InstantVictory));
            long frame = world.Events.Frame;
            world.ApplyBattleInput(new(BattleInputKind.Return));
            Assert.Null(world.Events.LastFault);
            Assert.Null(world.Battle);
            Assert.Null(session.State.PendingBattleEncounter);
            Assert.Equal(frame + 1, world.Events.Frame);
            Assert.Equal(results + 1, session.State.AppliedBattleResultIds.Length);
            Assert.Contains(occurrence.OccurrenceId, session.State.AppliedBattleResultIds);
            GameState returned = session.PrepareSnapshot();
            Assert.Equal(assets.NativeExecutableHash, world.Events.NativeRuntimeProfile?.ExecutableHash);
            Assert.Equal(assets.NativeExecutableHash, returned.NativeBattleMemory?.ColdExecutableSha256);
            Assert.Equal(assets.NativeExecutableHash, returned.NativeBattleMemory?.ModelCpuResources?.Cpu.EffectiveLayout.ExecutableHash);
            Assert.Throws<InvalidOperationException>(() => world.ApplyBattleInput(new(BattleInputKind.Return)));
            Assert.Equal(returned.AppliedBattleResultIds, session.State.AppliedBattleResultIds);
            GameSession coldSession = new(returned);
            using GameWorld cold = World(coldSession, directory, resources, assets);
            cold.EnsureField();
            Assert.Equal(world.Events.Frame, cold.Events.Frame);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(returned.ExamTimer), JsonSerializer.SerializeToElement(coldSession.State.ExamTimer)));
            Assert.Equal(returned.CrtRandom, coldSession.State.CrtRandom);
            Assert.Equal(assets.NativeExecutableHash, world.CreateEncounterBattle(occurrence.Formation).NativeGraphics?.Rules.ExecutableSha256);
            if (world.Events.IsUserControlEnabled && !world.HasPendingBattle)
            {
                _ = world.CaptureGameplaySave();
            }
            return returned;
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(4, true)]
    [InlineData(5, false)]
    [InlineData(5, true)]
    [InlineData(6, false)]
    [InlineData(6, true)]
    public void Active_battle_retains_guest_allocation_and_execution_under_selected_resources(int fixture, bool otherEdition)
    {
        string path = TestResources.FixturePath("App.Tests.ActiveBattleResourceProfileCompatibilityTests." + fixture);
        string sourceDirectory = TestResources.GameDirectory;
        string? targetDirectory = otherEdition ? Environment.GetEnvironmentVariable("MEMORIA_FF8_CROSS_EDITION_GAME_DIR") : sourceDirectory;
        Assert.SkipUnless(targetDirectory is not null && Directory.Exists(targetDirectory), "Configure MEMORIA_FF8_CROSS_EDITION_GAME_DIR for the other installed edition.");
        byte[] originalHash = Hash(path);
        using GameResourceHost sourceResources = new(sourceDirectory, watch: false);
        sourceResources.ConnectEncounterMods();
        using GameAssets sourceAssets = new(sourceDirectory, resources: sourceResources.Resources, modelOverrides: sourceResources);
        GameSession sourceSession = new(PlayerStateLoader.Load(path, sourceDirectory, sharedAssets: sourceAssets, resources: sourceResources.Resources, modelOverrides: sourceResources, readGameplayPolicy: () => sourceResources.GameplayPolicy));
        using GameWorld source = World(sourceSession, sourceDirectory, sourceResources, sourceAssets);
        source.EnsureField();
        BattleSnapshot original = source.Battle!.Capture();
        using GameResourceHost targetResources = new(targetDirectory!, watch: false);
        targetResources.ConnectEncounterMods();
        using GameAssets targetAssets = new(targetDirectory!, resources: targetResources.Resources, modelOverrides: targetResources);
        GameSession targetSession = new(PlayerStateLoader.Load(path, targetDirectory!, sharedAssets: targetAssets, resources: targetResources.Resources, modelOverrides: targetResources, readGameplayPolicy: () => targetResources.GameplayPolicy));
        using GameWorld target = World(targetSession, targetDirectory!, targetResources, targetAssets);
        target.EnsureField();
        AssertBattle(original, target.Battle!.Capture());
        Assert.Equal(source.Events.NativeRuntimeProfile, target.Events.NativeRuntimeProfile);
        Assert.Equal(sourceSession.State.NativeBattleMemory!.ModelCpuResources!.Cpu.EffectiveLayout, targetSession.State.NativeBattleMemory!.ModelCpuResources!.Cpu.EffectiveLayout);
        GameSession coldSession = new(targetSession.PrepareSnapshot());
        using GameWorld cold = World(coldSession, targetDirectory!, targetResources, targetAssets);
        cold.EnsureField();
        AssertBattle(original, cold.Battle!.Capture());
        if (otherEdition && targetAssets.NativeExecutableHash != source.Events.NativeRuntimeProfile!.ExecutableHash)
        {
            Assert.Throws<NotSupportedException>(() => target.CreateEncounterBattle(original.Definition.EncounterId!.Value, original.Definition.NativeSeed));
        }
        GameState originalState = sourceSession.PrepareSnapshot();
        Assert.False(original.NativeGraphics?.OriginalEffect?.ReturnedDescriptor is > 0);
        source.ApplyBattleInput(new(BattleInputKind.Tick));
        target.ApplyBattleInput(new(BattleInputKind.Tick));
        cold.ApplyBattleInput(new(BattleInputKind.Tick));
        AssertBattle(source.Battle.Capture(), target.Battle.Capture());
        AssertBattle(target.Battle.Capture(), cold.Battle.Capture());
        if (fixture == 1 && otherEdition)
        {
            BattleCombatantDefinition[] combatants = original.Definition.Combatants.ToArray();
            int enemy = Array.FindIndex(combatants, actor => !actor.IsPlayer);
            combatants[enemy] = combatants[enemy] with { MaximumHp = checked(combatants[enemy].MaximumHp + 1) };
            BattleSnapshot malformed = original with { Definition = original.Definition with { Combatants = combatants } };
            Assert.Throws<InvalidDataException>(() =>
            {
                GameSession rejectedSession = new(originalState with { Battle = malformed });
                using GameWorld rejected = World(rejectedSession, targetDirectory!, targetResources, targetAssets);
                rejected.EnsureField();
            });
        }
        Assert.Equal(originalHash, Hash(path));
    }

    private static GameWorld World(GameSession session, string directory, GameResourceHost resources, GameAssets assets) => new(session, directory, resources: resources.Resources, resolveGameFile: resources.ResolveGameFile, sharedAssets: assets, readSceneMods: () => resources.SceneMods, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy, applyEncounterMods: resources.ApplyEncounterMods);

    private static void AssertBattle(BattleSnapshot expected, BattleSnapshot actual) => Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected), JsonSerializer.SerializeToElement(actual)));

    private static byte[] Hash(string path)
    {
        using FileStream source = File.OpenRead(path);
        return SHA256.HashData(source);
    }
}
