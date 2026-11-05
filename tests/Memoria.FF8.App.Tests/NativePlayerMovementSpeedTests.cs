using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;

namespace Memoria.FF8.App.Tests;

public sealed class NativePlayerMovementSpeedTests
{
    public static IEnumerable<object[]> ConfiguredSpeeds()
    {
        for (int percent = 50; percent <= 300; percent++)
        {
            yield return [percent, 2696];
            yield return [percent, 7929];
        }
    }

    [Theory]
    [MemberData(nameof(ConfiguredSpeeds))]
    public void EveryConfiguredWalkAndRunSpeedUsesTheRealNativeAdapter(int percent, int nativeSpeed)
    {
        short speed = checked((short)(nativeSpeed * percent / 100));
        NativePlayerMovementResult? result = NativePlayerMovementAdapter.Resolve(Field(), Scene(), Query(speed, percent));

        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal(250 * 4096 + 16 * speed, result.FixedX);
        Assert.Equal(250 * 4096, result.FixedY);
        Assert.Equal(40 * 4096, result.FixedZ);
        Assert.Equal(0, result.Triangle);
        Assert.Equal(64, result.Direction);
        Assert.Single(result.Proposals);
        Assert.NotNull(result.ProposalGroundProbes);
        Assert.Equal(3, Assert.Single(result.ProposalGroundProbes).Count);
    }

    [Theory]
    [InlineData(49)]
    [InlineData(301)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void OutOfRangeConfigurationIsRejectedForOrdinaryAndMovieMovement(int percent)
    {
        FieldAssets field = Field();
        Assert.Null(NativePlayerMovementAdapter.Resolve(field, Scene(), Query(2696, percent)));
        Assert.Null(NativePlayerMovementAdapter.Resolve(field, Scene(movie: true), Query(5392, percent)));
    }

    [Theory]
    [InlineData(150, 2696)]
    [InlineData(150, 7929)]
    [InlineData(150, 4043)]
    [InlineData(150, 4045)]
    [InlineData(150, 11892)]
    [InlineData(150, 11894)]
    [InlineData(100, 5392)]
    [InlineData(100, 15859)]
    public void SpeedMustMatchTheDeclaredConfiguration(int percent, short speed)
    {
        Assert.Null(NativePlayerMovementAdapter.Resolve(Field(), Scene(), Query(speed, percent)));
    }

    [Theory]
    [MemberData(nameof(ConfiguredSpeeds))]
    public void MovieMovementKeepsItsNativeCadenceCompensationAtEveryConfiguration(int percent, int nativeSpeed)
    {
        short movieSpeed = nativeSpeed == 2696 ? (short)5392 : (short)15859;
        NativePlayerMovementResult? result = NativePlayerMovementAdapter.Resolve(Field(), Scene(movie: true), Query(movieSpeed, percent));

        Assert.NotNull(result);
        Assert.True(result.Accepted);
        Assert.Equal(250 * 4096 + 16 * movieSpeed, result.FixedX);
        Assert.Equal(250 * 4096, result.FixedY);
        Assert.Equal(40 * 4096, result.FixedZ);
    }

    [Theory]
    [InlineData(150, 8088)]
    [InlineData(150, 23788)]
    [InlineData(100, 2696)]
    [InlineData(100, 7929)]
    public void MovieMovementRejectsScaledOrOrdinarySpeeds(int percent, short speed)
    {
        Assert.Null(NativePlayerMovementAdapter.Resolve(Field(), Scene(movie: true), Query(speed, percent)));
    }

    private static NativePlayerMovementQuery Query(short speed, int percent)
    {
        return new NativePlayerMovementQuery(7, 0, 250 * 4096, 250 * 4096, 40 * 4096, 64, speed, 48, EarlyRetryState: 0, SpeedPercent: percent);
    }

    private static FieldAssets Field()
    {
        WalkmeshTriangle triangle = new(new Vector3(0, 0, 40), new Vector3(0, 1000, 40), new Vector3(1000, 0, 40), -1, -1, -1);
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 256);
        return new FieldAssets(100, "player-speed-component", [], [], [], camera, new FieldWalkmesh([triangle]), []);
    }

    private static SceneEventState Scene(bool movie = false)
    {
        JsonObject state = JsonSerializer.SerializeToNode(new SceneEventState())!.AsObject();
        state["NativeFieldLoadVersion"] = 1;
        state["NativePartyTraceVersion"] = 1;
        state["NativeActorLayoutVersion"] = 1;
        state["NativeActorObjectIds"] = JsonSerializer.SerializeToNode(new[] { 7 });
        state["FieldPartyNativeActorIds"] = JsonSerializer.SerializeToNode(new[] { 0, 255, 255 });
        state["NativePlayerEarlyRetryState"] = 0;
        state["MovieActors"] = movie ? JsonSerializer.SerializeToNode(new NativeMovieActorLifecycle(1, 0, 0, 0, 0, null)) : null;
        return state.Deserialize<SceneEventState>()!;
    }
}
