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
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original world corpus is required.");
        // Isolated component fixture; no live boarding, session history or field VM is fabricated.
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameAssets initializationAssets = new(directory);
        session.InitializeNewGameCharacterVitals(initializationAssets.ReadNewGameCharacterVitals());
        NativeSavedWorldState initial = session.State.SavedWorld!;
        var previous = new WorldState { Frame = 500, X = 12390, Z = -26709, NativeHeight = -301, BodyYaw = 2256, CameraYaw = 2256, NativeCarRuntime = NativeCarWorldRuntimeState.Create(initial, NativeWorldGroundCacheState.CreateFresh(new(0, 0, 0, 0), processSelection: session.WorldProcess!.GroundSelection), session.WorldProcess) };
        NativeSavedWorldCarReturn car = NativeSavedWorldCarReturn.Create(previous, 262, 40, 1);
        GameState departure = session.State with { FieldId = 123, FieldLoadContext = null, DepartingWorldState = previous, SavedWorld = initial.WithOrdinaryCarEntry().WithCarReturn(car), WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 40), ExamTimer = NativeExamTimerState.FromReachedSet(600), WorldControls = session.WorldControls!.WithSalaryEnabled(true), SeeD = session.SeeD!.WithSalaryUiEnabled(true) with { SalaryMovementCounter = 123, RankPoints = 65535 } };
        var packet = new WorldTransition(11, 66, 61);
        session.Restore(departure);
        using PreparedWorldEntry ticket = WorldEntryPreflight.Prepare(departure, directory, packet);
        Assert.Same(departure, session.State);
        Assert.Equal(0, ticket.InitialState.Frame);
        Assert.Null(ticket.InitialState.NativeScriptedRuntime!.Path.Presentation[1].Angles);
        Assert.Null(ticket.InitialState.NativeScriptedRuntime.Path.Presentation[1].Projection);
        session.CommitPreparedWorldEntry(packet, ticket.InitialState, session.WorldControls!, ticket.ValidateAdoption, ticket.InitialSavedWorld);
        using WorldMapScene scene = ticket.Adopt(session);
        Assert.True(scene.IsPresentationDeferred);
        Assert.Throws<NativeWorldPresentationDeferredException>(() => scene.Render(320, 224));
        GameState beforeCallback = session.State;
        PlayerActionResult first = scene.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1, Vertical: -1));
        Assert.Equal(1, first.FramesAdvanced);
        Assert.False(scene.IsPresentationDeferred);
        NativeWorldScriptedPathState path = scene.State.NativeScriptedRuntime!.Path;
        Assert.Equal(1, path.Callback);
        AssertGroundSelection(scene.State.NativeScriptedRuntime.GroundCache!, session.WorldProcess!, 1);
        Assert.Equal(123u, session.SeeD!.SalaryMovementCounter);
        Assert.Equal((ushort)65535, session.SeeD.RankPoints);
        Assert.NotNull(path.Presentation[1].Angles);
        Assert.NotNull(path.Presentation[1].Projection);
        Assert.Equal(beforeCallback.World!.X, scene.State.X);
        Assert.Equal(beforeCallback.World.Z, scene.State.Z);
        byte[] pixels = scene.Render(320, 224);
        var replaySession = new GameSession(beforeCallback);
        using WorldMapScene replay = new(replaySession, directory);
        Assert.Equal(1, replay.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: -1, Vertical: -1)).FramesAdvanced);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.State), JsonSerializer.SerializeToElement(replaySession.State)));
        Assert.Equal(pixels, replay.Render(320, 224));
        // Only the just-executed action is replayed locally; this fixture is never
        // presented as a reconstructable New Game checkpoint.
        GameState restoredState = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!;
        using WorldMapScene restored = new(new GameSession(restoredState), directory);
        Assert.Equal(pixels, restored.Render(320, 224));
        NativeWorldPathOrientationReceipt orientation = path.Presentation[1].Orientation!;
        NativeWorldPathNodeProjection shiftedMiddle = orientation.Middle with { Words = orientation.Middle.Words with { X = unchecked((short)(orientation.Middle.Words.X + 1)) } };
        NativeWorldPathNodeProjection shiftedLinked = orientation.Linked with { Words = orientation.Linked.Words with { X = unchecked((short)(orientation.Linked.Words.X + 1)) } };
        NativeWorldScriptedPathState alteredPath = path with { Presentation = path.Presentation.SetItem(1, path.Presentation[1] with { Projection = shiftedMiddle, Orientation = orientation with { Middle = shiftedMiddle, Linked = shiftedLinked } }) };
        using (var assets = new WorldAssets(directory))
        {
            alteredPath.Validate(assets.ReadScriptedPath(11));
        }
        NativeScriptedWorldRuntimeState alteredBoat = scene.State.NativeScriptedRuntime with { Path = alteredPath };
        var alteredSession = new GameSession(session.State with { World = scene.State with { NativeScriptedRuntime = alteredBoat } });
        GameState unchanged = alteredSession.State;
        Assert.Throws<InvalidDataException>(() => new WorldMapScene(alteredSession, directory));
        Assert.Same(unchanged, alteredSession.State);
        scene.PrepareFieldEntry = (selection, final, location, salary) =>
        {
            NativeSavedWorldScriptedReturn receipt = final.NativeScriptedRuntime!.Return!;
            Assert.Equal(61, selection.Entry.Index);
            Assert.Equal(260, selection.Entry.FieldId);
            Assert.Equal(receipt.MapLocationId, location);
            Assert.Equal(0u, salary.MovementUnits);
            GameState current = session.State;
            return () => session.CommitPreparedScriptedFieldEntry(current, final, receipt.FieldEntry, salary, _ => { });
        };
        Assert.Equal(229, scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 229)).FramesAdvanced);
        Assert.True(scene.State.NativeScriptedRuntime!.Path.EndpointRequested);
        AssertGroundSelection(scene.State.NativeScriptedRuntime.GroundCache!, session.WorldProcess!, 1);
        // Declared retained UI process: native formatting starts a live phase just
        // before the already-requested departure fade; no payout history is claimed.
        NativeSalaryPresentation beforeFade = session.SalaryPresentation!.StartPayment(6, 5, 4000, 3000);
        session.SetSalaryPresentation(beforeFade);
        PlayerActionResult completed = scene.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 16));
        Assert.Equal(16, completed.FramesAdvanced);
        Assert.Equal(beforeFade, session.SalaryPresentation);
        Assert.Equal(PlayerTriggerKind.FieldTransition, completed.Trigger.Kind);
        Assert.Equal(260, session.State.FieldId);
        Assert.Equal(230, session.State.SavedWorld!.ScriptedReturn!.SourceWorldFrame);
        Assert.Equal(246, session.State.DepartingWorldState!.Frame);
        Assert.Equal(246, scene.LastDeparture!.Frame);
        AssertGroundSelection(session.State.DepartingWorldState.NativeScriptedRuntime!.GroundCache!, session.WorldProcess!, 1);
        Assert.Equal(pixels.Length, scene.Render(320, 224).Length);
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(departure, directory, new WorldTransition(12, 66, -1)));

        // This second isolated allocation uses the genuinely executed preceding boat callbacks,
        // not a car seed or a patched retained world. Story135 selects the same original static-foot clause.
        GameState returnDeparture = session.State with { WorldEntryMemory = session.State.WorldEntryMemory!.WriteBytes(256, 2, 135) };
        NativeSavedWorldScriptedReturn outboundReturn = returnDeparture.SavedWorld!.ScriptedReturn!;
        var returnPacket = new WorldTransition(12, 66, 60);
        Assert.Throws<InvalidDataException>(() => WorldEntryPreflight.Prepare(returnDeparture with { DepartingWorldState = returnDeparture.DepartingWorldState! with { Frame = 245 } }, directory, returnPacket));
        Assert.Throws<NotSupportedException>(() => WorldEntryPreflight.Prepare(returnDeparture with { DepartingWorldState = previous }, directory, returnPacket));
        using PreparedWorldEntry returnTicket = WorldEntryPreflight.Prepare(returnDeparture, directory, returnPacket);
        NativeScriptedWorldLoadReceipt expectedReturnLoad = outboundReturn.Load with { SourceWorldFrame = returnDeparture.DepartingWorldState!.Frame };
        Assert.Equal(expectedReturnLoad, returnTicket.InitialState.NativeScriptedRuntime!.Load);
        Assert.Equal(outboundReturn, returnTicket.InitialSavedWorld!.ScriptedReturn);
        AssertGroundSelection(returnTicket.InitialState.NativeScriptedRuntime.GroundCache!, session.WorldProcess!, 1);
        session.Restore(returnDeparture);
        session.CommitPreparedWorldEntry(returnPacket, returnTicket.InitialState, session.WorldControls!, returnTicket.ValidateAdoption, returnTicket.InitialSavedWorld);
        using WorldMapScene returning = returnTicket.Adopt(session);
        Assert.True(returning.IsPresentationDeferred);
        returning.PrepareFieldEntry = (selection, final, location, salary) =>
        {
            Assert.Equal(60, selection.Entry.Index);
            Assert.Equal(new FieldTransition(123, 881, 937, 25, 180), final.NativeScriptedRuntime!.Return!.FieldEntry);
            Assert.Equal(0u, salary.MovementUnits);
            GameState current = session.State;
            return () => session.CommitPreparedScriptedFieldEntry(current, final, final.NativeScriptedRuntime.Return.FieldEntry, salary, _ => { });
        };
        Assert.Equal(1, returning.Apply(new PlayerAction(PlayerActionKind.Wait)).FramesAdvanced);
        Assert.Equal(outboundReturn, session.State.SavedWorld!.ScriptedReturn);
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
        Assert.Equal((ushort)135, session.State.SavedWorld!.ScriptedReturn!.StoryWord);
        Assert.Equal(returnPacket, session.State.SavedWorld.ScriptedReturn.Packet);
        Assert.Equal(expectedReturnLoad, session.State.SavedWorld.ScriptedReturn.Load);
        Assert.Equal(session.State.SavedWorld.ScriptedReturn.SourceWorldFrame + 16, session.State.DepartingWorldState!.Frame);
        AssertGroundSelection(session.State.DepartingWorldState.NativeScriptedRuntime!.GroundCache!, session.WorldProcess!, 1);

        // A structural ordinary loader continuation consumes the process metadata
        // established by these boat callbacks; no intervening field travel is claimed.
        using PreparedWorldEntry footTicket = WorldEntryPreflight.Prepare(session.State, directory, new WorldTransition(1, 1, -1));
        AssertGroundSelection(footTicket.InitialState.NativeFootRuntime!.GroundCache!, session.WorldProcess!, 1);
    }

    private static void AssertGroundSelection(NativeWorldGroundCacheState cache, NativeWorldProcess process, int group)
    {
        Assert.All(cache.Alternatives, alternative =>
        {
            Assert.Equal(group, alternative.PreferredGroup);
            Assert.Equal(0, alternative.FailedSelections);
        });
        Assert.Equal(new NativeWorldGroundSelectionAlternative(group, 0), Assert.Single(process.GroundSelection!.Alternatives));
    }
}
