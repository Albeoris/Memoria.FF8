using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeFootWorldRuntimeStateTests
{
    [Fact]
    public void ExecutedFirstLoaderRequiresCurrentNativeInitialization()
    {
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame();
        Assert.Equal<byte?>(0, saved.InitialMapDisplayMode110);
        NativeFootWorldRuntimeState runtime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(saved);
        Assert.Equal((byte)0, runtime.AnimationVariant);
        Assert.Equal((byte)0, runtime.MapDisplayMode);
        Assert.Equal(NativeFootWorldRuntimeState.FirstLoadRule, runtime.Rule);
        Assert.Null(runtime.ReloadSourceWorldFrame);
        runtime.RequireSupportedNormalFoot();

        NativeSavedWorldState incomplete = saved with { InitialMapDisplayMode110 = null };
        string before = JsonSerializer.Serialize(incomplete);
        Assert.Throws<InvalidDataException>(() => NativeFootWorldRuntimeState.CreateFirstNormalLoad(incomplete));
        Assert.Equal(before, JsonSerializer.Serialize(incomplete));
        WorldState old = JsonSerializer.Deserialize<WorldState>("{\"Frame\":1649,\"X\":30240,\"Z\":-29393,\"NativeHeight\":-691,\"BodyYaw\":165,\"CameraYaw\":199}")!;
        Assert.Null(old.NativeFootRuntime);
        Assert.DoesNotContain("NativeFootRuntime", JsonSerializer.Serialize(old));
        Assert.Throws<NotSupportedException>(() => NativeSavedWorldFootReturn.RecordFootDeparture(old, 159, 17, 1));
    }

    [Fact]
    public void NativeDepartureCarriesLiveAuxiliariesAndSavedReloadRetainsExactPoseMetadata()
    {
        var world = new WorldState { Frame = 1650, X = 30240, Z = -29393, NativeHeight = -691, BodyYaw = 165, CameraYaw = 199, NativeFootRuntime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(NativeSavedWorldState.CreateNewGame()) };
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(world, 159, 17, 1);
        receipt.RequireCompleteFootReload();
        Assert.Equal(new NativeSavedWorldPose(30240, -29393, -691, 165), receipt.Pose0);
        Assert.Equal(199, receipt.CameraYaw96);
        Assert.Equal<byte?>(0, receipt.AnimationVariant108);
        Assert.Equal<byte?>(0, receipt.MapDisplayMode110);
        string current = JsonSerializer.Serialize(receipt);
        Assert.Contains("\"MapDisplayMode110\":0", current);
        Assert.DoesNotContain("MovementMode110", current);
        Assert.Equal(receipt, JsonSerializer.Deserialize<NativeSavedWorldFootReturn>(current));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeSavedWorldFootReturn>(current.Replace("MapDisplayMode110", "MovementMode110", StringComparison.Ordinal)));
        NativeFootWorldRuntimeState reload = NativeFootWorldRuntimeState.CreateSavedReload(receipt);
        Assert.Equal(NativeFootWorldRuntimeState.SavedReloadRule, reload.Rule);
        Assert.Equal(1650, reload.ReloadSourceWorldFrame);
        Assert.Equal(reload, JsonSerializer.Deserialize<NativeFootWorldRuntimeState>(JsonSerializer.Serialize(reload)));
        NativeSavedWorldFootReturn fullscreen = NativeSavedWorldFootReturn.RecordFootDeparture(world with { NativeFootRuntime = world.NativeFootRuntime with { MapDisplayMode = 3 } }, 159, 17, 1);
        Assert.Equal<byte?>(0, fullscreen.MapDisplayMode110);
    }

    [Fact]
    public void MalformedAuthorityAndUnsupportedLiveVariantsCannotPassTheFootPath()
    {
        NativeFootWorldRuntimeState first = NativeFootWorldRuntimeState.CreateFirstNormalLoad(NativeSavedWorldState.CreateNewGame());
        Assert.Throws<InvalidDataException>(() => (first with { ProofVersion = 2 }).Validate());
        Assert.Throws<InvalidDataException>(() => (first with { Rule = "unproved" }).Validate());
        Assert.Throws<InvalidDataException>(() => (first with { AnimationVariant = 1 }).Validate());
        Assert.Throws<InvalidDataException>(() => (first with { ReloadSourceWorldFrame = 0 }).Validate());
        Assert.Throws<InvalidDataException>(() => (first with { MapDisplayMode = 4 }).Validate());
        Assert.Throws<InvalidDataException>(() => (first with { EntityType = 3 }).Validate());
        Assert.Throws<NotSupportedException>(() => (first with { MapDisplayMode = 1 }).RequireSupportedNormalFoot());
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeFootWorldRuntimeState>("{}"));
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(new WorldState(), 159, 17, 1, 1, 0);
        NativeFootWorldRuntimeState reload = NativeFootWorldRuntimeState.CreateSavedReload(receipt);
        reload.RequireSupportedNormalFoot();
        Assert.Equal(640, reload.CameraTransition!.ProjectionDistance);
        Assert.Throws<InvalidDataException>(() => (reload with { ReloadSourceWorldFrame = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (reload with { ReloadSourceWorldFrame = -1 }).Validate());
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    [InlineData((byte)2)]
    public void FootEntryTypeSurvivesDepartureSerializationAndSavedReload(byte entityType)
    {
        NativeFootWorldRuntimeState runtime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(NativeSavedWorldState.CreateNewGame(), entityType);
        var world = new WorldState { Frame = 1, NativeFootRuntime = runtime };
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.RecordFootDeparture(world, 159, 135, 1);
        Assert.Equal(entityType, receipt.EntityType105);
        NativeSavedWorldFootReturn restored = JsonSerializer.Deserialize<NativeSavedWorldFootReturn>(JsonSerializer.Serialize(receipt))!;
        NativeFootWorldRuntimeState reload = NativeFootWorldRuntimeState.CreateSavedReload(restored);
        Assert.Equal(entityType, reload.EntityType);
        reload.RequireSupportedNormalFoot();
        Assert.Equal(runtime.EntityType, JsonSerializer.Deserialize<NativeFootWorldRuntimeState>(JsonSerializer.Serialize(runtime))!.EntityType);
    }
}
