using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeThreePartyMovementBindingTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(255, 2)]
    [InlineData(1, 255)]
    [InlineData(255, 255)]
    public void NativeThreeSlotsAdmitIndependentAbsentFollowersAndPreserveSavedBindings(int second, int third)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(260);
        SceneEventState scene = SyntheticScene([0, second, third]);
        ActorState player = SyntheticPlayer(0);
        string before = JsonSerializer.Serialize(scene);
        Assert.True(NativePlayerMovementAdapter.Supports(field, scene, player));
        NativePlayerMovementQuery query = new(0, 18, 503808, 417792, 0, 192, 2696, 48, 0, _ => false);
        NativePlayerMovementResult? result = NativePlayerMovementAdapter.Resolve(field, scene, query);
        Assert.NotNull(result);
        Assert.NotEmpty(result.Proposals);
        Assert.All(result.ProposalGroundProbes!, probes => Assert.Equal(3, probes.Count));
        if (result.Accepted)
        {
            Assert.NotEqual((query.FixedX, query.FixedY), (result.FixedX, result.FixedY));
        }
        else
        {
            Assert.Equal((query.FixedX, query.FixedY, query.FixedZ), (result.FixedX, result.FixedY, result.FixedZ));
        }
        Assert.Equal(before, JsonSerializer.Serialize(scene));
        Assert.Equal(new NativePartyBindingMemory(1, 0, 1, 2), scene.PartyBindingMemory);
    }

    [Theory]
    [InlineData(255, 255, 255)]
    [InlineData(-1, 255, 255)]
    [InlineData(3, 255, 255)]
    [InlineData(0, 0, 255)]
    [InlineData(0, 1, 1)]
    [InlineData(0, -1, 255)]
    [InlineData(0, 255, 3)]
    public void MissingLeaderAndMalformedPresentFollowersStillRefuse(int first, int second, int third)
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(260);
        SceneEventState scene = SyntheticScene([first, second, third]);
        string before = JsonSerializer.Serialize(scene);
        Assert.False(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(0)));
        Assert.Null(NativePlayerMovementAdapter.Resolve(field, scene, new(0, 18, 503808, 417792, 0, 192, 2696, 48, 0, _ => false)));
        Assert.Equal(before, JsonSerializer.Serialize(scene));
    }

    [Fact]
    public void ThreeSlotAdmissionRetainsDynamicLeaderAndCanonicalPartyBounds()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(260);
        SceneEventState scene = SyntheticScene([1, 255, 255]);
        scene.NativeActorObjectIds[1] = 4;
        Assert.True(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(4)));
        Assert.False(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(0)));
        scene.Party.Add(3);
        Assert.False(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(4)));
        scene.Party.RemoveAt(3);
        scene.Party[2] = 1;
        Assert.False(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(4)));
        scene.Party.RemoveAt(2);
        scene.Party.RemoveAt(1);
        scene.FieldPartyNativeActorIds[2] = 2;
        Assert.False(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(4)));
    }

    [Fact]
    public void SparseCanonicalPartyRetainsThirdSlotAndSkipsOnlyTheAbsentFollower()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        using GameAssets assets = new(directory);
        FieldAssets field = assets.LoadField(260);
        SceneEventState scene = SyntheticScene([0, 255, 2]);
        scene.Party[1] = 255;
        string before = JsonSerializer.Serialize(scene);
        Assert.Equal(new[] { 0, 255, 6 }, scene.Party);
        Assert.Equal(new[] { 0, 6 }, scene.ActiveParty);
        Assert.True(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(0)));
        Assert.NotNull(NativePlayerMovementAdapter.Resolve(field, scene, new(0, 18, 503808, 417792, 0, 192, 2696, 48, 0, _ => false)));
        Assert.Equal(before, JsonSerializer.Serialize(scene));
        scene.FieldPartyNativeActorIds[1] = 1;
        Assert.True(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(0)));
        scene.Party[0] = 5;
        Assert.True(NativePlayerMovementAdapter.Supports(field, scene, SyntheticPlayer(0)));
    }

    private static SceneEventState SyntheticScene(int[] runtimeBindings)
    {
        // This isolated admission/geometry fixture is not a research checkpoint.
        // Saved bindings deliberately differ from independently detached runtime slots.
        JsonNode input = JsonSerializer.SerializeToNode(new SceneEventState())!;
        input[nameof(SceneEventState.NativeFieldLoadVersion)] = 1;
        input[nameof(SceneEventState.NativeActorLayoutVersion)] = 1;
        input[nameof(SceneEventState.NativePartyTraceVersion)] = 1;
        input[nameof(SceneEventState.NativePlayerEarlyRetryState)] = 0;
        input[nameof(SceneEventState.NativeActorObjectIds)] = JsonSerializer.SerializeToNode(new[] { 0, 1, 2 });
        input[nameof(SceneEventState.FieldPartyNativeActorIds)] = JsonSerializer.SerializeToNode(runtimeBindings);
        input[nameof(SceneEventState.Party)] = JsonSerializer.SerializeToNode(new[] { 0, 1, 6 });
        input[nameof(SceneEventState.PartyBindingMemory)] = JsonSerializer.SerializeToNode(new NativePartyBindingMemory(1, 0, 1, 2));
        return input.Deserialize<SceneEventState>()!;
    }

    private static ActorState SyntheticPlayer(int objectId)
    {
        JsonNode input = JsonSerializer.SerializeToNode(new ActorState(objectId, 0))!;
        input[nameof(ActorState.ModelId)] = 0;
        input[nameof(ActorState.CharacterId)] = 0;
        return input.Deserialize<ActorState>()!;
    }
}
