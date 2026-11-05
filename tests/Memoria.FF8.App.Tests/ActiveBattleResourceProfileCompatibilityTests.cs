using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class ActiveBattleResourceProfileCompatibilityTests
{
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
