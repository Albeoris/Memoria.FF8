using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayPartyTeleportTests
{
    [Fact]
    public void DiagnosticPartyPlacementPreservesTraceScalarsAndColdRestoreContinuity()
    {
        using InstalledField field = new();
        EventEngine events = field.World.Events;
        AutoPlayTarget target = TrainContact(field.World);
        PartyTraceState trace = events.Scene.PartyTrace!;
        JsonElement before = events.Capture();
        long frame = events.Frame;
        int speed = events.PlayerActor!.MovementSpeed;

        events.TeleportPlayer(target.X, target.Y, target.Z, target.Triangle, target.Direction);

        Assert.Equal(frame, events.Frame);
        Assert.Equal(speed, events.PlayerActor.MovementSpeed);
        AssertPartyAtLeader(events);
        AssertRelocatedTrace(trace, events.Scene.PartyTrace!, events.PlayerActor);
        Assert.Equal(before.GetProperty("Scene").GetProperty("PartyBindingMemory").GetRawText(), events.Capture().GetProperty("Scene").GetProperty("PartyBindingMemory").GetRawText());
        Assert.Equal(before.GetProperty("Scene").GetProperty("NativeFieldEntryStack").GetRawText(), events.Capture().GetProperty("Scene").GetProperty("NativeFieldEntryStack").GetRawText());
        JsonElement placed = events.Capture();
        string checkpoint = Path.Combine(Path.GetTempPath(), "ff8-party-placement-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            new GameSession(field.Session.State with { EventState = placed }).Save(checkpoint);
            using GameWorld restored = new(new GameSession(GameSession.Load(checkpoint)), TestResources.GameDirectory, validateWorldPresentation: false);
            restored.EnsureField();
            Assert.True(JsonElement.DeepEquals(placed, restored.Events.Capture()));
            Assert.Equal(field.World.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
            Assert.True(JsonElement.DeepEquals(events.Capture(), restored.Events.Capture()));
            AssertPartyAtLeader(events);
        }
        finally
        {
            File.Delete(checkpoint);
        }
    }

    [Fact]
    public void OriginalTrainRequestsUseTheirOwnStagingMovesAndCompleteAfterPartyPlacement()
    {
        using InstalledField field = new();
        EventEngine events = field.World.Events;
        AutoPlayTarget target = TrainContact(field.World);
        List<(int Actor, int Operation)> completed = [];
        List<(int Actor, int Operation)> commands = [];
        bool restoredControl = false;
        events.CommandExecuted += command =>
        {
            commands.Add((command.ObjectId, command.Operation));
            if (command.Name == "MOVE" && events.Actors[command.ObjectId].NativeScriptMovement is { Active: false })
            {
                completed.Add((command.ObjectId, command.Operation));
            }
            restoredControl |= command.ObjectId == 14 && command.ScriptId == 140 && command.Operation == 3368 && command.Name == "UCON";
        };

        FieldTeleportContactResult result = events.TeleportPlayerAndDispatchContacts(Position(target), Approach(target));

        Assert.True(result.PlacementApplied);
        Assert.True(result.ContactTriggered);
        Assert.False(result.NativeInterrupted);
        AssertPartyAtLeader(events);
        Assert.True(events.PlayerActor!.Y >= 400);
        for (int action = 0; action < 600 && !restoredControl; action++)
        {
            if (field.World.NextTrigger.Kind == PlayerTriggerKind.Dialogue)
            {
                field.World.Apply(new(PlayerActionKind.Confirm));
            }
            else
            {
                field.World.Apply(new(PlayerActionKind.Wait, Frames: 120));
            }
        }

        Assert.Null(events.LastFault);
        Assert.True(restoredControl, "The original train request did not restore control after its own movement and dialogue sequence.");
        Assert.Contains((4, 1388), commands);
        Assert.Contains((9, 2005), commands);
        Assert.Contains((4, 1403), completed);
        Assert.Contains((9, 2011), completed);
        Assert.Equal(new[] { 0, 1, 5 }, events.Scene.Party);
    }

    [Fact]
    public void OriginalControlLossPreemptsPlacementForEveryPartyMember()
    {
        using InstalledField field = new();
        EventEngine events = field.World.Events;
        AutoPlayTarget target = TrainContact(field.World);
        Assert.True(events.TeleportPlayerAndDispatchContacts(Position(target), Approach(target)).ContactTriggered);
        ActorState[] members = AttachedParty(events);
        var before = members.Select(actor => (actor.X, actor.Y, actor.Z, actor.WalkmeshTriangle)).ToArray();

        FieldTeleportContactResult result = events.TeleportPlayerAndDispatchContacts(new(target.X + 5, target.Y + 5, target.Z, target.Triangle, target.Direction));

        Assert.False(result.PlacementApplied);
        Assert.True(result.NativeInterrupted);
        Assert.False(events.IsUserControlEnabled);
        Assert.Equal(before, members.Select(actor => (actor.X, actor.Y, actor.Z, actor.WalkmeshTriangle)).ToArray());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnsupportedGroundOrIndependentFollowerOwnershipRefusesBeforeMutation(bool independentOwner)
    {
        using InstalledField field = new();
        EventEngine events = field.World.Events;
        AutoPlayTarget target = TrainContact(field.World);
        if (independentOwner)
        {
            // Component-only owner declaration tests refusal; it is never executed as an original scene or saved as a checkpoint.
            typeof(ActorState).GetProperty(nameof(ActorState.MovementActorId))!.SetValue(AttachedParty(events)[1], 0);
        }
        else
        {
            events.ResolveNativeTriangleHeight = null;
        }
        JsonElement before = events.Capture();

        Assert.Throws<NotSupportedException>(() => events.TeleportPlayer(target.X, target.Y, target.Z, target.Triangle, target.Direction));

        Assert.True(JsonElement.DeepEquals(before, events.Capture()));
    }

    [Fact]
    public void OpaqueTraceRecordsStayOpaqueAndDetachedRuntimeFollowersStayInPlace()
    {
        using InstalledField field = new();
        EventEngine events = field.World.Events;
        AutoPlayTarget target = TrainContact(field.World);
        ActorState detached = AttachedParty(events)[1];
        var detachedPosition = (detached.X, detached.Y, detached.Z, detached.WalkmeshTriangle);
        PartyTraceState trace = events.Scene.PartyTrace!;
        PartyTraceSample?[] first = (PartyTraceSample?[])trace.FirstTrail.Clone();
        PartyTraceSample?[] second = (PartyTraceSample?[])trace.SecondTrail.Clone();
        first[5] = null;
        second[5] = null;
        first[10] = first[10]! with { Animation = 2, FirstFlag = 17, SecondFlag = 23 };
        trace = trace with { FirstTrail = first, SecondTrail = second };
        // Explicit component state isolates opaque records and an absent runtime follow slot from the live-scene regression above.
        typeof(SceneEventState).GetProperty(nameof(SceneEventState.PartyTrace))!.SetValue(events.Scene, trace);
        events.Scene.FieldPartyNativeActorIds[1] = byte.MaxValue;

        events.TeleportPlayer(target.X, target.Y, target.Z, target.Triangle, target.Direction);

        Assert.Equal(detachedPosition, (detached.X, detached.Y, detached.Z, detached.WalkmeshTriangle));
        AssertRelocatedTrace(trace, events.Scene.PartyTrace!, events.PlayerActor!);
        Assert.Null(events.Scene.PartyTrace!.FirstTrail[5]);
        Assert.Null(events.Scene.PartyTrace.SecondTrail[5]);
        ActorState attached = AttachedParty(events)[1];
        Assert.Equal(events.PlayerActor!.Y, attached.Y);
        Assert.Equal(events.PlayerActor.WalkmeshTriangle, attached.WalkmeshTriangle);
    }

    private static void AssertPartyAtLeader(EventEngine events)
    {
        ActorState leader = events.PlayerActor!;
        ActorState[] party = AttachedParty(events);
        Assert.Equal(3, party.Length);
        foreach (ActorState follower in party.Skip(1))
        {
            Assert.Equal((int)leader.X, follower.X);
            Assert.Equal((int)leader.Y, follower.Y);
            Assert.Equal((int)leader.Z, follower.Z);
            Assert.Equal(leader.WalkmeshTriangle, follower.WalkmeshTriangle);
        }
    }

    private static void AssertRelocatedTrace(PartyTraceState before, PartyTraceState after, ActorState leader)
    {
        Assert.Equal(before.Cursor, after.Cursor);
        Assert.Equal(before.FirstOffset, after.FirstOffset);
        Assert.Equal(before.SecondOffset, after.SecondOffset);
        foreach ((PartyTraceSample?[] oldTrail, PartyTraceSample?[] newTrail) in new[] { (before.FirstTrail, after.FirstTrail), (before.SecondTrail, after.SecondTrail) })
        {
            for (int index = 0; index < oldTrail.Length; index++)
            {
                PartyTraceSample? sample = oldTrail[index];
                Assert.Equal(sample is null ? null : sample with { X = (int)leader.X, Y = (int)leader.Y, Z = (int)leader.Z, Triangle = leader.WalkmeshTriangle, Direction = (int)leader.Direction }, newTrail[index]);
            }
        }
    }

    private static ActorState[] AttachedParty(EventEngine events)
    {
        return Enumerable.Range(0, 3).Select(slot => NativeFieldPartyBinding.ResolveActor(events.Scene, events.Actors, slot)).Where(actor => actor is not null).Cast<ActorState>().ToArray();
    }

    private static AutoPlayTarget TrainContact(GameWorld world)
    {
        return world.DiscoverAutoPlayTargets().First(target => target.Id == "114:line:14:0:0");
    }

    private static FieldTeleportPosition Position(AutoPlayTarget target)
    {
        return new(target.X, target.Y, target.Z, target.Triangle, target.Direction);
    }

    private static FieldTeleportPosition? Approach(AutoPlayTarget target)
    {
        return target.ApproachX is float x && target.ApproachY is float y ? new(x, y, target.ApproachZ ?? target.Z, target.ApproachTriangle ?? target.Triangle, target.Direction) : null;
    }

    private sealed class InstalledField : IDisposable
    {
        public InstalledField()
        {
            Session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayPartyTeleportTests.1")));
            World = new(Session, TestResources.GameDirectory, validateWorldPresentation: false);
            World.EnsureField();
            Assert.Equal(PlayerTriggerKind.Playable, World.NextTrigger.Kind);
        }

        public GameSession Session { get; }
        public GameWorld World { get; }
        public void Dispose()
        {
            World.Dispose();
        }
    }
}
