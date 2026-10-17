using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayContactOrderingTests
{
    [Fact]
    public void AlreadyRequestedNativeDepartureWinsBeforeEitherApproachOrTargetPlacement()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayContactOrderingTests.1")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        Assert.Equal(120, session.State.FieldId);
        ActorState player = world.Events.PlayerActor!;
        var before = (player.X, player.Y, player.Z, player.WalkmeshTriangle);
        AutoPlayTarget escape = world.DiscoverAutoPlayTargets().First(target => target.CoverageId == "120:gateway:0");
        FieldTeleportContactResult result = world.Events.TeleportPlayerAndDispatchContacts(Position(escape), Approach(escape));
        Assert.False(result.PlacementApplied);
        Assert.False(result.ContactTriggered);
        Assert.True(result.NativeInterrupted);
        Assert.Equal(before, (player.X, player.Y, player.Z, player.WalkmeshTriangle));
        Assert.Equal(123, session.State.FieldId);
        Assert.Null(world.Events.LastFault);
        Assert.Contains(world.Events.Capture().GetProperty("Fibers").EnumerateArray(), fiber => fiber.GetProperty("ObjectId").GetInt32() == 0 && fiber.GetProperty("ScriptId").GetInt32() == 4 && fiber.GetProperty("Position").GetInt32() == 384);
    }

    [Fact]
    public void ControllerDoesNotCreditThePlannedEscapeWhenAnEarlierNativeDepartureWins()
    {
        string manifest = TestResources.FixturePath("App.Tests.AutoPlayContactOrderingTests.2");
        string output = Path.Combine(Path.GetTempPath(), "ff8-contact-ordering-" + Guid.NewGuid().ToString("N"));
        GameSession session = GameSession.CreateColdProcess();
        GameWorld? world = null;
        try
        {
            GameWorld GetWorld()
            {
                return world ??= new GameWorld(session, TestResources.GameDirectory, validateWorldPresentation: false);
            }
            void Restore(GameState state)
            {
                world?.Dispose();
                world = null;
                session.Restore(state);
            }
            AutoPlayController controller = new(session, GetWorld, Restore, output);
            controller.ResumeFrom(manifest);
            AutoPlayVisitCoverage visit = controller.State.Coverage.Visits.Single(item => item.Visit.Key == "120:40");
            string Credit() => JsonSerializer.Serialize(visit.Events.Select(item => new { item.Event.Id, item.Completed, item.BlockedReason, Entries = item.Conditions?.ConsumedEntries.Order().ToArray(), Exits = item.Conditions?.ConsumedExits.Order().ToArray() }));
            string before = Credit();
            controller.Advance(1);
            Assert.Null(controller.Error);
            Assert.Equal(123, session.State.FieldId);
            Assert.Null(controller.State.ActiveEvent);
            Assert.Equal(before, Credit());
        }
        finally
        {
            world?.Dispose();
            if (Directory.Exists(output))
            {
                Directory.Delete(output, true);
            }
        }
    }

    [Fact]
    public void NativeKeyQueryCanTakeControlBeforeDiagnosticCoordinatesAreApplied()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.8")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        world.Apply(new(PlayerActionKind.FieldButtons));
        world.Apply(new(PlayerActionKind.Wait));
        world.Apply(new(PlayerActionKind.FieldButtons, 64));
        bool interrupted = false;
        for (int attempt = 0; attempt < 8 && !interrupted; attempt++)
        {
            Assert.True(world.Events.CanAcceptMovementInput);
            ActorState player = world.Events.PlayerActor!;
            var before = (player.X, player.Y, player.Z, player.WalkmeshTriangle);
            FieldTeleportContactResult result = world.Events.TeleportPlayerAndDispatchContacts(new(player.X + 1, player.Y, player.Z, player.WalkmeshTriangle, (int)player.Direction));
            if (result.NativeInterrupted)
            {
                interrupted = true;
                Assert.False(result.PlacementApplied);
                Assert.False(result.ContactTriggered);
                Assert.False(world.Events.IsUserControlEnabled);
                Assert.Equal(before, (player.X, player.Y, player.Z, player.WalkmeshTriangle));
            }
            else
            {
                world.Apply(new(PlayerActionKind.FieldButtons));
            }
        }
        Assert.True(interrupted, "The original KEYON callback never took control ahead of placement.");
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void AnAmbientAnimationLoopDoesNotPreventPostVmPlacement()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.7")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        ActorState player = world.Events.PlayerActor!;
        NativeActorAnimationHeader? animation = world.Events.Actors[18].NativeAnimationHeader;
        long frame = world.Events.Frame;
        FieldTeleportContactResult result = world.Events.TeleportPlayerAndDispatchContacts(new(player.X + 1, player.Y, player.Z, player.WalkmeshTriangle, (int)player.Direction));
        Assert.True(result.PlacementApplied);
        Assert.False(result.NativeInterrupted);
        Assert.Equal(frame + 1, world.Events.Frame);
        world.Apply(new(PlayerActionKind.Wait, Frames: 7));
        Assert.NotEqual(animation, world.Events.Actors[18].NativeAnimationHeader);
        Assert.False(world.Events.HasPendingPlayerInteraction(18));
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NewLineContactIsPublishedAfterThePlacementCallbackAndRunsOnTheNextCallback()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.2")));
        using GameWorld world = new(session, TestResources.GameDirectory, validateWorldPresentation: false);
        world.EnsureField();
        AutoPlayTarget target = world.DiscoverAutoPlayTargets().First(item => item.CoverageId == "229:line:10");
        int[] contactScripts = JsmProgram.Read(world.Field!.Jsm).Objects[10].Scripts.Skip(2).Select(script => script.Id).ToArray();
        List<ScriptCommand> commands = [];
        world.Events.CommandExecuted += commands.Add;
        long frame = world.Events.Frame;
        FieldTeleportContactResult result = world.Events.TeleportPlayerAndDispatchContacts(Position(target), Approach(target));
        Assert.True(result.PlacementApplied);
        Assert.True(result.ContactTriggered);
        Assert.False(result.NativeInterrupted);
        Assert.Equal(frame + 1, world.Events.Frame);
        Assert.True(world.Events.HasPendingPlayerInteraction(10));
        Assert.DoesNotContain(commands, command => command.ObjectId == 10 && contactScripts.Contains(command.ScriptId));
        world.Apply(new(PlayerActionKind.Wait));
        Assert.Contains(commands, command => command.ObjectId == 10 && contactScripts.Contains(command.ScriptId));
        Assert.Null(world.Events.LastFault);
    }

    private static FieldTeleportPosition Position(AutoPlayTarget target)
    {
        return new(target.X, target.Y, target.Z, target.Triangle, target.Direction);
    }

    private static FieldTeleportPosition? Approach(AutoPlayTarget target)
    {
        return target.ApproachX is float x && target.ApproachY is float y ? new(x, y, target.ApproachZ ?? target.Z, target.ApproachTriangle ?? target.Triangle, target.Direction) : null;
    }
}
