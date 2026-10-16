using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRagnarokTransferSceneTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Fact]
    public void ActualLandingCallbacksRestoreMidTransferAndKeepPreTerrainCameraSeparateFromLateGlobals()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using (GameWorld initialization = new(session, GameDirectory))
        {
        }
        session.Restore(session.State with { Scene = SceneKind.WorldMap, WorldArrival = new(32, 50, -1), WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 3500), SavedWorld = session.State.SavedWorld! with { WorldEventFlags116 = NativeWorldEventFlags.CreateNewGame().WithOperand(260) } });
        using WorldMapScene scene = new(session, GameDirectory);
        scene.Apply(new(PlayerActionKind.Wait, Frames: 24));
        scene.Apply(new(PlayerActionKind.Confirm));
        scene.Apply(new(PlayerActionKind.Wait, Frames: 17));
        NativeRagnarokWorldRuntimeState runtime = scene.State.NativeRagnarokRuntime!;
        Assert.Equal(18, runtime.Transfer!.Counter);
        NativeRagnarokTransferStep expected = (runtime.Transfer with { Counter = 17 }).Advance();
        Assert.Equal(expected.Camera.CurveThreshold, runtime.TransferCamera!.CurveThreshold);
        Assert.NotEqual(runtime.TransferCamera.CurveThreshold, runtime.ProjectionCurveThreshold);
        string path = Path.Combine(Path.GetTempPath(), "Memoria-FF8-ragnarok-transfer-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            session.Save(path);
            GameSession restoredSession = new(GameSession.Load(path));
            using WorldMapScene restored = new(restoredSession, GameDirectory);
            scene.Apply(new(PlayerActionKind.Wait, Frames: 43));
            restored.Apply(new(PlayerActionKind.Wait, Frames: 43));
            Assert.NotNull(scene.State.NativeFootRuntime);
            Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(restoredSession.State)));
            NativeRagnarokWorldRuntimeState completed = scene.State.NativeRagnarokRuntime!;
            Assert.Null(completed.Transfer);
            Assert.Equal(5229, completed.TransferCamera!.CurveThreshold);
            Assert.Equal(new short[] { 2196, 2496, 2746, 2946, 4095 }, completed.TransferCamera.ProjectionWords);
            Assert.Equal(new short[] { 1896, 2196, 2496, 2746 }, completed.TransferCamera.PerspectiveWords);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
