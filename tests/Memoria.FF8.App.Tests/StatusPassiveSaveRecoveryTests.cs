using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class StatusPassiveSaveRecoveryTests
{
    [Fact]
    public void LoadedLegacyDefinitionsAreRestoredBeforeMovementAndSurviveColdContinuation()
    {
        string? fixture = Environment.GetEnvironmentVariable("MEMORIA_FF8_MEMORIA_SAVE_FIXTURE");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(fixture) && File.Exists(fixture), "Set MEMORIA_FF8_MEMORIA_SAVE_FIXTURE to a standalone world-map save.");
        byte[] original = File.ReadAllBytes(fixture!);
        GameState source = GameSession.Load(fixture!);
        Assert.Equal(SceneKind.WorldMap, source.Scene);
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals saved = source.CharacterVitals!;
        NativeCharacterVitals legacy = saved with { Definitions = saved.Definitions with { CharacterStatusPassiveFlags = default } };
        NativeCharacterVitals restored = NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(legacy, assets.ReadNewGameCharacterVitals());
        byte row = saved.CombatParty.First(id => id != 255);
        legacy = legacy.DeepCopy();
        legacy.Records[row] = legacy.Records[row] with { HitPoints = new(1, 1), Junction = legacy.Records[row].Junction with { Abilities = 66 } };
        NativeEventMemory threshold = source.EventMemory.Write(12, 4, NativeScalarValue.Exact(7));
        Assert.Throws<NotSupportedException>(() => NativeMovementProgress.Sample(threshold, legacy, source.LimitProgress, null, source.SavedBattleFlags, 1));

        GameSession session = new(source with { CharacterVitals = legacy, EventMemory = threshold });
        using GameWorld world = new(session, TestResources.GameDirectory, sharedAssets: assets);
        Assert.Equal(restored.Definitions.CharacterStatusPassiveFlags, session.CharacterVitals!.Definitions.CharacterStatusPassiveFlags);
        NativeMovementProgressResult recovered = NativeMovementProgress.Sample(threshold, session.CharacterVitals, source.LimitProgress, null, source.SavedBattleFlags, 1);
        Assert.Equal(0, recovered.Memory.Read(12, 4).RequireExact());
        Assert.Equal(new NativeHpWord(1, 1), recovered.Vitals!.Records[row].HitPoints);
        world.EnsureField();
        PlayerAction movement = new(PlayerActionKind.Move, Horizontal: 1, Frames: 8);
        Assert.True(world.Apply(movement).Accepted);
        string coldPath = Path.Combine(Path.GetTempPath(), "Memoria-status-passive-" + Guid.NewGuid().ToString("N") + ".json");
        GameSession cold;
        try
        {
            session.Save(coldPath, compact: true);
            cold = new(GameSession.Load(coldPath));
        }
        finally
        {
            File.Delete(coldPath);
        }
        using GameWorld resumed = new(cold, TestResources.GameDirectory, sharedAssets: assets);
        resumed.EnsureField();
        Assert.Equal(world.Apply(movement), resumed.Apply(movement));
        Assert.Equal(JsonSerializer.Serialize(session.PrepareSnapshot()), JsonSerializer.Serialize(cold.PrepareSnapshot()));

        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RECOVERED_SAVE_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            Assert.False(File.Exists(output), "The recovered snapshot must use a new path.");
            GameState repaired = NativeCharacterInitialization.RestoreMissingStatusPassiveFlags(source, assets.ReadNewGameCharacterVitals());
            new GameSession(repaired).Save(output, compact: true);
            Assert.Equal(JsonSerializer.Serialize(repaired), JsonSerializer.Serialize(GameSession.Load(output)));
        }
        Assert.Equal(original, File.ReadAllBytes(fixture!));
    }
}
