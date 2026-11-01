using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeWorldAcceptedMovementPredicateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(256)]
    public void OperandWordMatchesTheExecutedAcceptedMagnitudeRatherThanMotorSpeed(ushort operand)
    {
        string directory = TestResources.GameDirectory;
        GameSession session = NativeRagnarokFootTests.Fixture();
        using WorldMapScene scene = new(session, directory);
        using WorldAssets assets = new(directory);
        using GameAssets game = new(directory);
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame();
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0));
        NativeWorldProcess process = NativeWorldProcess.FromColdProcess();
        NativeFootWorldRuntimeState foot = NativeFootWorldRuntimeState.CreateFirstNormalLoad(saved);
        NativeCarWorldRuntimeState car = NativeCarWorldRuntimeState.Create(saved, cache, process);
        NativeWorldEntryMemory entry = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 750).WriteBytes(264, 1, 8).WriteBytes(265, 1, 1);
        NativeGardenWorldAllocation allocation = assets.ReadGardenAllocation(new(3, 48, -1), entry, NativeEventMemory.Empty, saved, game.ReadNewGameCharacterVitals().AcquireGuardianForce(13));
        NativeGardenWorldRuntimeState garden = NativeGardenWorldRuntimeState.CreateFresh(saved, allocation, cache, process, assets.ReadGardenCameraDefaults(), currentGroundHeight: 0, priorTerrainOcclusion: false);
        foreach (int magnitude in new[] { 0, 1, 64 })
        {
            bool expected = operand == (magnitude == 0 ? 0 : 1);
            WorldState footState = new() { NativeFootRuntime = foot with { MovementMagnitude20409F4 = magnitude } };
            WorldState carState = new() { NativeCarRuntime = car with { Speed = magnitude == 0 ? 32 : 0, MovementMagnitude20409F4 = magnitude } };
            WorldState gardenState = new() { NativeGardenRuntime = garden with { Speed = magnitude == 0 ? 32 : 0, MovementMagnitude20409F4 = magnitude } };
            Assert.Equal(expected, scene.EvaluateWorldEventPredicate(footState, 0, saved, 0xFF38, operand));
            Assert.Equal(expected, scene.EvaluateWorldEventPredicate(carState, 0, saved, 0xFF38, operand));
            Assert.Equal(expected, scene.EvaluateWorldEventPredicate(gardenState, 0, saved, 0xFF38, operand));
            WorldState parkedGarden = gardenState with { NativeFootRuntime = foot with { MovementMagnitude20409F4 = null } };
            Assert.Null(scene.EvaluateWorldEventPredicate(parkedGarden, 0, saved, 0xFF38, operand));
        }
    }

    [Fact]
    public void UnknownFootAndFlightMagnitudeRemainUnknown()
    {
        GameSession session = NativeRagnarokFootTests.Fixture();
        using WorldMapScene scene = new(session, TestResources.GameDirectory);
        NativeSavedWorldState saved = session.State.SavedWorld!;
        NativeFootWorldRuntimeState unknownFoot = NativeFootWorldRuntimeState.CreateFirstNormalLoad(NativeSavedWorldState.CreateNewGame());
        WorldState unknown = scene.State with { NativeFootRuntime = unknownFoot };
        foreach (ushort operand in new ushort[] { 0, 1, 2 })
        {
            Assert.Null(scene.EvaluateWorldEventPredicate(unknown, 0, saved, 0xFF38, operand));
            Assert.Null(scene.EvaluateWorldEventPredicate(scene.State, 0, saved, 0xFF38, operand));
        }
    }
}
