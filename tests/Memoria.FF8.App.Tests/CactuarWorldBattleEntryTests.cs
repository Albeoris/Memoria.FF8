using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class CactuarWorldBattleEntryTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void ContactPublishesFormationWithoutFieldLookupOrRandomDraw(int phase, bool contact)
    {
        string source = TestResources.FixturePath("cactuar-world-contact-source");
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        WorldState state = ContactState(scene.State, phase);
        session.SetWorldState(state);
        RandomEncounterSessionState random = session.State.RandomEncounters! with { Enabled = false, Pending = null };
        session.SetRandomEncounters(random);
        NativeWorldSalaryState salary = NativeWorldSalaryState.Begin(session.State);
        object?[] arguments = [state, salary];
        WorldState next = (WorldState)typeof(WorldMapScene).GetMethod("StepRagnarokCactuar", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(scene, arguments)!;
        salary = (NativeWorldSalaryState)arguments[1]!;
        Assert.Equal(random.Native, salary.RandomEncounters!.Native);
        Assert.False(salary.RandomEncounters.Enabled);
        Assert.Equal(contact, salary.RandomEncounters.Pending is not null);
        if (contact)
        {
            Assert.Same(state, next);
            Assert.Equal(new RandomEncounterRequest(SceneKind.WorldMap, session.State.FieldId, state.Frame, 712, scene.AllocationProgramSha256), salary.RandomEncounters.Pending);
            salary.RandomEncounters.Validate();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SavedWorldContactAndRestoredContinuationStartTheOriginalBattle(bool randomEnabled)
    {
        string source = TestResources.FixturePath("cactuar-world-contact-source");
        byte[] hash = SHA256.HashData(File.ReadAllBytes(source));
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        WorldMapScene scene = Assert.IsType<WorldMapScene>(world.WorldScene);
        session.SetWorldState(ContactState(scene.State, 2));
        session.SetRandomEncounters(session.State.RandomEncounters! with { Enabled = randomEnabled, Pending = null });
        string snapshot = TestResources.ArtifactFile($"cactuar-contact-{randomEnabled}.json");
        session.Save(snapshot);
        GameSession restoredSession = new(GameSession.Load(snapshot));
        using GameWorld restored = new(restoredSession, directory, sharedAssets: assets);
        restored.EnsureField();
        Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
        Assert.Equal(session.State.RandomEncounters, restoredSession.State.RandomEncounters);
        Assert.Equal((ushort)712, session.State.RandomEncounters!.Pending!.Formation);
        Assert.Equal(randomEnabled, session.State.RandomEncounters.Enabled);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(restoredSession.State.World));
        world.ApplyBattleInput(new(BattleInputKind.StartEncounter, Mode: BattleAtbMode.TurnBased, NativeSeed: 4));
        BattleSession battle = Assert.IsType<BattleSession>(world.Battle);
        ExpandedBattleInstalledEntry.WaitForEntry(world, battle);
        Assert.Equal(BattleOutcome.Active, battle.Outcome);
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    [Fact]
    public void LatestPlayerSaveRemainsLoadableWithoutChangingItsContents()
    {
        string source = TestResources.FixturePath("cactuar-latest-player-source");
        byte[] hash = SHA256.HashData(File.ReadAllBytes(source));
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(PlayerStateLoader.Load(source, directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 24)).Accepted);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Null(session.State.RandomEncounters?.Pending);
        Assert.Equal(hash, SHA256.HashData(File.ReadAllBytes(source)));
    }

    private static WorldState ContactState(WorldState state, int phase)
    {
        NativeRagnarokWorldRuntimeState runtime = state.NativeRagnarokRuntime!;
        int index = runtime.Objects.Select((item, index) => (item, index)).Single(pair => pair.item.ShapeId == 94).index;
        NativeWorldFootCameraState camera = runtime.ProjectionCamera!;
        NativeRagnarokWorldObject body = runtime.Objects[index] with { Position = new(camera.X, camera.NativeHeight, -camera.RawZ) };
        return state with { NativeRagnarokRuntime = runtime with { Objects = runtime.Objects.SetItem(index, body), Cactuar = new(phase, phase == 0 ? null : 1) } };
    }
}
