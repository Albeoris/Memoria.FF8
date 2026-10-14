using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBoatWorldTests
{
    [Fact]
    public void PreparedBoatDefersAllocationDrawThenReplaysActualCallbacksAndRejectsAlteredProjectionWords()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world corpus is required.");
        // Isolated component fixture; no live boarding, session history or field VM is fabricated.
        NativeSavedWorldState initial = NativeSavedWorldState.CreateNewGame();
        var previous = new WorldState { Frame = 500, X = 12390, Z = -26709, NativeHeight = -301, BodyYaw = 2256, CameraYaw = 2256, NativeCarRuntime = NativeCarWorldRuntimeState.Create(initial, NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0))) };
        NativeSavedWorldCarReturn car = NativeSavedWorldCarReturn.Create(previous, 262, 40, 1);
        var departure = new GameState { Scene = SceneKind.Field, FieldId = 123, DepartingWorldState = previous, SavedWorld = initial.WithOrdinaryCarEntry().WithCarReturn(car), WorldControls = NativeWorldControlState.CreateNewGame(), Variables = new() { [256] = 40 }, ExamTimer = NativeExamTimerState.FromReachedSet(600) };
        var packet = new WorldTransition(11, 66, 61);
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets initializationAssets = new(directory);
        departure = departure with { ReplayJournal = session.State.ReplayJournal, WorldControls = departure.WorldControls!.WithSalaryEnabled(true), SeeD = session.SeeD!.WithSalaryUiEnabled(true) with { SalaryMovementCounter = 123, RankPoints = 65535 }, CharacterVitals = initializationAssets.ReadNewGameCharacterVitals() };
        session.Restore(departure);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(departure, directory, packet);
        Assert.Same(departure, session.State);
        Assert.Equal(0, ticket.InitialState.Frame);
        Assert.Null(ticket.InitialState.NativeBoatRuntime!.Path.ModelAngles);
        Assert.Null(ticket.InitialState.NativeBoatRuntime.Path.ModelProjection);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.True(scene.IsPresentationDeferred);
        Assert.Throws<NativeWorldPresentationDeferredException>(() => scene.Render(320, 224));
        GameState beforeCallback = session.State;
        PlayerActionResult first = scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: -1));
        Assert.Equal(1, first.FramesAdvanced);
        Assert.False(scene.IsPresentationDeferred);
        NativeWorldScriptedPathState path = scene.State.NativeBoatRuntime!.Path;
        Assert.Equal(1, path.Callback);
        Assert.Equal(123u, session.SeeD!.SalaryMovementCounter);
        Assert.Equal((ushort)65535, session.SeeD.RankPoints);
        Assert.NotNull(path.ModelAngles);
        Assert.NotNull(path.ModelProjection);
        Assert.Equal(beforeCallback.World!.X, scene.State.X);
        Assert.Equal(beforeCallback.World.Z, scene.State.Z);
        byte[] pixels = scene.Render(320, 224);
        var replaySession = new GameSession(beforeCallback);
        using WorldMapScene replay = new(replaySession, directory);
        Assert.Equal(1, replay.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1, Vertical: -1)).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(replaySession.State)));
        Assert.Equal(pixels, replay.Render(320, 224));
        // Only the just-executed action is replayed locally; this fixture is never
        // presented as a reconstructable New Game checkpoint.
        session.MarkReplayUnavailable("Isolated boat lifecycle fixture has no authentic preceding route history.");
        replaySession.MarkReplayUnavailable("Isolated boat lifecycle fixture has no authentic preceding route history.");
        GameState restoredState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        using WorldMapScene restored = new(new GameSession(restoredState), directory);
        Assert.Equal(pixels, restored.Render(320, 224));
        NativeWorldPathOrientationReceipt orientation = path.OrientationReceipt!;
        NativeWorldPathNodeProjection shiftedMiddle = orientation.Middle with { Words = orientation.Middle.Words with { X = unchecked((short)(orientation.Middle.Words.X + 1)) } };
        NativeWorldPathNodeProjection shiftedLinked = orientation.Linked with { Words = orientation.Linked.Words with { X = unchecked((short)(orientation.Linked.Words.X + 1)) } };
        NativeWorldScriptedPathState alteredPath = path with { ModelProjection = shiftedMiddle, OrientationReceipt = orientation with { Middle = shiftedMiddle, Linked = shiftedLinked } };
        using (var assets = new WorldAssets(directory))
        {
            alteredPath.Validate(assets.ReadScriptedBoatPath(11));
        }
        NativeBoatWorldRuntimeState alteredBoat = scene.State.NativeBoatRuntime with { Path = alteredPath };
        var alteredSession = new GameSession(session.State with { World = scene.State with { NativeBoatRuntime = alteredBoat } });
        GameState unchanged = alteredSession.State;
        Assert.Throws<InvalidDataException>(() => new WorldMapScene(alteredSession, directory));
        Assert.Same(unchanged, alteredSession.State);
        scene.PrepareFieldEntry = (selection, final, location, salaryUnits) =>
        {
            NativeSavedWorldBoatReturn receipt = final.NativeBoatRuntime!.Return!;
            Assert.Equal(61, selection.Entry.Index);
            Assert.Equal(260, selection.Entry.FieldId);
            Assert.Equal(receipt.MapLocationId, location);
            GameState current = session.State;
            return () => session.CommitPreparedBoatFieldEntry(current, final, receipt.FieldEntry, _ => { });
        };
        PlayerActionResult completed = scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 300));
        Assert.Equal(245, completed.FramesAdvanced);
        Assert.Equal(PlayerTriggerKind.FieldTransition, completed.Trigger.Kind);
        Assert.Equal(260, session.State.FieldId);
        Assert.Equal(230, session.State.SavedWorld!.BoatReturn!.SourceWorldFrame);
        Assert.Equal(246, session.State.DepartingWorldState!.Frame);
        Assert.Equal(246, scene.LastDeparture!.Frame);
        Assert.Equal(pixels.Length, scene.Render(320, 224).Length);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure, directory, new WorldTransition(12, 66, -1)));

        // This second isolated allocation uses the genuinely executed preceding boat callbacks,
        // not a car seed or a patched retained world. Story135 selects the same original static-foot clause.
        GameState returnDeparture = session.State with { Variables = new() { [256] = 135 } };
        NativeSavedWorldBoatReturn outboundReturn = returnDeparture.SavedWorld!.BoatReturn!;
        var returnPacket = new WorldTransition(12, 66, 60);
        Assert.Throws<InvalidDataException>(() => WorldEntryPreflight.Prepare(returnDeparture with { DepartingWorldState = returnDeparture.DepartingWorldState! with { Frame = 245 } }, directory, returnPacket));
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(returnDeparture with { DepartingWorldState = previous }, directory, returnPacket));
        using PreparedWorldEntry returnTicket = WorldEntryPreflight.Prepare(returnDeparture, directory, returnPacket);
        Assert.Equal(outboundReturn.Load, returnTicket.InitialState.NativeBoatRuntime!.Load);
        Assert.Equal(outboundReturn, returnTicket.InitialSavedWorld!.BoatReturn);
        session.Restore(returnDeparture);
        session.CommitPreparedWorldEntry(returnPacket, returnTicket.InitialState, session.WorldControls!, returnTicket.ValidateAdoption, returnTicket.InitialSavedWorld);
        using WorldMapScene returning = returnTicket.Adopt(session);
        Assert.True(returning.IsPresentationDeferred);
        returning.PrepareFieldEntry = (selection, final, location, salaryUnits) =>
        {
            Assert.Equal(60, selection.Entry.Index);
            Assert.Equal(new FieldTransition(123, 881, 937, 25, 180), final.NativeBoatRuntime!.Return!.FieldEntry);
            GameState current = session.State;
            return () => session.CommitPreparedBoatFieldEntry(current, final, final.NativeBoatRuntime.Return.FieldEntry, _ => { });
        };
        Assert.Equal(1, returning.Apply(new PlayerAction(PlayerActionKind.Wait)).FramesAdvanced);
        Assert.Equal(outboundReturn, session.State.SavedWorld!.BoatReturn);
        byte[] returnPixels = returning.Render(320, 224);
        GameState returningRestored = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        using (WorldMapScene restoredReturn = new(new GameSession(returningRestored), directory))
        {
            Assert.Equal(returnPixels, restoredReturn.Render(320, 224));
        }
        PlayerActionResult returned = returning.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1000));
        Assert.Equal(PlayerTriggerKind.FieldTransition, returned.Trigger.Kind);
        Assert.Equal(123, session.State.FieldId);
        Assert.Equal(260, session.State.PreviousFieldId);
        Assert.Equal((ushort)135, session.State.SavedWorld!.BoatReturn!.StoryWord);
        Assert.Equal(returnPacket, session.State.SavedWorld.BoatReturn.Packet);
        Assert.Equal(outboundReturn.Load, session.State.SavedWorld.BoatReturn.Load);
        Assert.Equal(session.State.SavedWorld.BoatReturn.SourceWorldFrame + 16, session.State.DepartingWorldState!.Frame);
    }
}
