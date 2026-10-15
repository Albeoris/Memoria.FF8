using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;
using System.Text.Json.Nodes;
using Xunit;

namespace Memoria.FF8.App.Tests;

public sealed class SceneCollectionCheckpointTests
{
    [Fact]
    public void BitmapMutationAndRestorationKeepCanonicalStateWithoutReorderingParty()
    {
        SceneEventState original = new();
        foreach (SortedSet<int> bitmap in new[] { original.LockedTriangles, original.UnlockedTutorials })
        {
            bitmap.UnionWith([7, 2, 5, 1]);
            bitmap.Remove(2);
            bitmap.Remove(7);
            bitmap.Add(7);
            bitmap.Add(2);
        }
        original.Party.Clear();
        original.Party.AddRange([5, 255, 1]);
        string captured = JsonSerializer.Serialize(original);
        SceneEventState restored = JsonSerializer.Deserialize<SceneEventState>(captured)!;
        Assert.Equal(captured, JsonSerializer.Serialize(restored));
        foreach (SortedSet<int> bitmap in new[] { restored.LockedTriangles, restored.UnlockedTutorials })
        {
            Assert.Equal(new[] { 1, 2, 5, 7 }, bitmap);
        }
        Assert.Equal(new[] { 5, 255, 1 }, restored.Party);
        Assert.Equal(new[] { 5, 1 }, restored.ActiveParty);
        restored.LockedTriangles.Remove(5);
        restored.LockedTriangles.Add(5);
        Assert.Equal(captured, JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void MemberAndGuardianForceViewsComeFromRequiredSavedRowsWithoutSceneDuplicates()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed native initialization resources are required.");
        using GameAssets assets = new(directory);
        NativeCharacterVitals vitals = assets.ReadNewGameCharacterVitals().WithMemberFlags(1, true).WithMemberFlags(5, true).AcquireGuardianForce(1).AcquireGuardianForce(7);
        var session = new GameSession(new GameState { CharacterVitals = vitals });
        var engine = new EventEngine(session);
        Assert.Contains(1, engine.NormalMembers!);
        Assert.Contains(5, engine.NormalMembers!);
        Assert.Contains(1, engine.AcquiredGuardianForces!);
        Assert.Contains(7, engine.AcquiredGuardianForces!);
        JsonElement scene = JsonSerializer.SerializeToElement(engine.Scene);
        Assert.False(scene.TryGetProperty("Members", out _));
        Assert.False(scene.TryGetProperty("AcquiredGuardianForces", out _));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(vitals))!;
        restored.Validate();
        var resumedSession = new GameSession(new GameState { CharacterVitals = restored });
        var resumed = new EventEngine(resumedSession);
        Assert.Equal(engine.NormalMembers!.Order(), resumed.NormalMembers!.Order());
        Assert.Equal(engine.AcquiredGuardianForces!.Order(), resumed.AcquiredGuardianForces!.Order());
        JsonNode missing = JsonSerializer.SerializeToNode(vitals)!;
        missing["Records"]![0]!.AsObject().Remove("MemberFlags");
        Assert.Throws<JsonException>(() => missing.Deserialize<NativeCharacterVitals>());
        JsonNode missingGuardianForces = JsonSerializer.SerializeToNode(vitals)!;
        missingGuardianForces.AsObject().Remove("GuardianForces");
        Assert.Throws<JsonException>(() => missingGuardianForces.Deserialize<NativeCharacterVitals>());
        var unknownSession = new GameSession(new GameState());
        var unknown = new EventEngine(unknownSession);
        Assert.Null(unknown.NormalMembers);
        Assert.Null(unknown.AcquiredGuardianForces);
    }
}
