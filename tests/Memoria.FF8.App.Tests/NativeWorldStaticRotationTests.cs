using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldStaticRotationTests
{
    [Theory]
    [InlineData(1024, 0, 0, 1, 0, 0, 0, 1, 0)]
    [InlineData(0, 1024, 0, 1, 0, 0, 0, 0, -1)]
    [InlineData(0, 0, 1024, 0, 1, 0, 0, 0, 1)]
    public void ManagedAngleNamesPreserveTheNativeZThenYThenXAxisMapping(short pitch, short yaw, short roll, int x, int y, int z, int expectedX, int expectedY, int expectedZ)
    {
        Matrix4x4 rotation = WorldStaticModelTransform.CreateRotation(68, new NativeWorldPathAngles(pitch, yaw, roll));
        Vector3 result = Vector3.Transform(new Vector3(x, y, z), rotation);
        Assert.True(Vector3.Distance(new Vector3(expectedX, expectedY, expectedZ), result) < 0.00001f);
    }

    [Theory]
    [InlineData(68, 13, -11, 7)]
    [InlineData(80, 7, -13, 11)]
    public void MixedAnglesFollowTheOriginalStandardOrModelSixteenMultiplicationOrder(byte shape, int expectedX, int expectedY, int expectedZ)
    {
        Matrix4x4 rotation = WorldStaticModelTransform.CreateRotation(shape, new NativeWorldPathAngles(1024, 1024, 1024));
        Vector3 result = Vector3.Transform(new Vector3(7, 11, 13), rotation);
        Assert.True(Vector3.Distance(new Vector3(expectedX, expectedY, expectedZ), result) < 0.00001f);
    }

    [Fact]
    public void ThePreviouslyProvedZeroNativeXRailTransformIsPreserved()
    {
        foreach (byte shape in new byte[] { 66, 67, 78 })
        {
            foreach (short pitch in new short[] { -1024, -45, 0, 29, 1024 })
            {
                foreach (short yaw in new short[] { 0, 1, 1024, 2048, 4096 })
                {
                    Matrix4x4 previous = Matrix4x4.CreateRotationZ(pitch * 0.0015339808305725455f) * Matrix4x4.CreateRotationY(yaw * 0.0015339808305725455f);
                    Assert.Equal(previous, WorldStaticModelTransform.CreateRotation(shape, new NativeWorldPathAngles(pitch, yaw, 0)));
                }
            }
        }
    }

    [Fact]
    public void OriginalStory345AllocationMapsTheCompletePackedCoordinateAngles()
    {
        using WorldAssets assets = new(TestResources.GameDirectory);
        NativeTrainWorldAllocation allocation = assets.ReadTrainWorldAllocation(345, 0);
        Assert.Equal(4, allocation.ClauseIndex);
        Assert.Equal(13, allocation.Objects.Length);
        NativeTrainWorldObject negativeSlope = allocation.Objects.Single(item => item.ShapeId == 69 && item.CoordinateIndex == 13);
        NativeTrainWorldObject positiveSlope = allocation.Objects.Single(item => item.ShapeId == 68 && item.CoordinateIndex == 14);
        NativeTrainWorldObject trainClass = allocation.Objects.Single(item => item.ShapeId == 75 && item.CoordinateIndex == 5);
        Assert.Equal(new NativeWorldPathPosition(-39870, -697, 25092), negativeSlope.Position);
        Assert.Equal(-325632, negativeSlope.PackedOrientation);
        Assert.Equal(new NativeWorldPathAngles(-5, 2048, 0), WorldStaticModelTransform.AllocationAngles(negativeSlope));
        Assert.Equal(new NativeWorldPathPosition(-38398, -687, 25092), positiveSlope.Position);
        Assert.Equal(655360, positiveSlope.PackedOrientation);
        Assert.Equal(new NativeWorldPathAngles(10, 0, 0), WorldStaticModelTransform.AllocationAngles(positiveSlope));
        Assert.Equal(new NativeWorldPathAngles(20, -192, 0), WorldStaticModelTransform.AllocationAngles(trainClass));
    }

    [Fact]
    public void DeclaredStory345SpawnSixRendersAndContinuesAfterIndependentRestore()
    {
        // This is an explicit component allocation, not a fabricated player journey.
        GameSession initialized = GameSession.CreateColdProcess();
        initialized.StartNewGame();
        using (GameAssets assets = new(TestResources.GameDirectory))
        {
            initialized.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            initialized.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        }
        WorldTransition packet = new(6, 0, -1);
        GameSession session = new(initialized.State with { Scene = SceneKind.WorldMap, FieldId = 713, WorldArrival = packet, WorldConfiguration = NativeWorldConfiguration.FromFieldPacket(packet, 713), EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [256] = 345 }) });
        using WorldMapScene world = new(session, TestResources.GameDirectory);
        Assert.Equal(13, world.State.NativeFootRuntime!.NormalObjects!.Allocation.Objects.Length);
        byte[] pixels = world.Render(320, 224).ToArray();
        Assert.Equal(320 * 224 * 4, pixels.Length);
        GameState independentState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        GameSession independentSession = new(independentState);
        using WorldMapScene independent = new(independentSession, TestResources.GameDirectory);
        Assert.Equal(pixels, independent.Render(320, 224));
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), independent.Apply(wait));
        Assert.Equal(JsonSerializer.Serialize(session.State.World), JsonSerializer.Serialize(independentSession.State.World));
        Assert.Equal(world.Render(320, 224), independent.Render(320, 224));
    }
}
