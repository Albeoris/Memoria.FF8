using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayCarDiscoveryTests
{
    [Fact]
    public void StationaryCarDiscoveryIsPureAndExecutionPreservesTheNativeEntranceGuard()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayCarWorldTests.1")));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        GameState before = session.State;
        Assert.Equal(SceneKind.WorldMap, before.Scene);
        Assert.Null(before.World!.NativeFootRuntime);
        Assert.Equal(33, before.World.NativeCarRuntime!.EntityType);
        byte[] party = before.CharacterVitals!.CombatParty.ToArray();
        ushort story = before.WorldEntryMemory!.StoryWord;

        IReadOnlyList<WorldAutoPlayTarget> targets = world.DiscoverAutoPlayWorldTargets();
        Assert.Equal(before, session.State);
        Assert.Equal(targets, world.DiscoverAutoPlayWorldTargets());
        Assert.Equal(before, session.State);
        WorldAutoPlayTarget entrance = Assert.Single(targets, target => target.DestinationField == 106);
        WorldState candidate = world.WorldScene!.PrepareCurrentCarPlacement(entrance.X, entrance.RawZ);
        Assert.Equal(before, session.State);
        Assert.Null(candidate.NativeFootRuntime);
        Assert.Equal(33, candidate.NativeCarRuntime!.EntityType);
        Assert.Equal(before.World.Frame, candidate.Frame);
        using WorldAssets assets = new(directory);
        WorldGroundSelection ground = assets.LookupNativeGround(candidate.X, candidate.Z, WorldTerrainSelection.Replacements(candidate.ReplacementMask), [])!;
        Assert.NotNull(ground);
        WorldTriangle triangle = ground.Ground.Triangle;
        uint flags = triangle.GroundType | (uint)triangle.TextureFlags << 8 | (uint)triangle.VertexFlags << 16;
        Assert.True(NativeWorldTerrain.CanTraverse(flags, candidate.NativeCarRuntime.EntityType));

        PlayerActionResult result = world.ExecuteAutoPlayWorldTarget(entrance);
        Assert.True(result.Accepted);
        Assert.Equal(PlayerTriggerKind.Dialogue, result.Trigger.Kind);
        Assert.Equal(49, result.Trigger.MessageId);
        Assert.Equal(SceneKind.WorldMap, session.State.Scene);
        Assert.Equal(before.FieldId, session.State.FieldId);
        Assert.Equal(24, session.State.World!.Frame);
        Assert.Null(session.State.World.NativeFootRuntime);
        Assert.Equal(33, session.State.World.NativeCarRuntime!.EntityType);
        Assert.Equal((byte)14, session.State.World.EventUi!.ScriptMode);
        Assert.Contains(session.State.World.EventUi.Windows, window => window.WindowId == 9 && window.MessageId == 49);
        Assert.Equal(49, world.CurrentDialogue!.MessageId);
        Assert.Equal(story, session.State.WorldEntryMemory!.StoryWord);
        Assert.Equal(party, session.State.CharacterVitals!.CombatParty);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.CharacterVitals), JsonSerializer.SerializeToElement(session.State.CharacterVitals)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.SavedWorld!.CarReturn), JsonSerializer.SerializeToElement(session.State.SavedWorld!.CarReturn)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(before.SavedWorld.FootReturn), JsonSerializer.SerializeToElement(session.State.SavedWorld.FootReturn)));
    }
}
