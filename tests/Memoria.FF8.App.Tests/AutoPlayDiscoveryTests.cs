using System.Buffers.Binary;
using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class AutoPlayDiscoveryTests
{
    [Fact]
    public void CavernDiscoveryKeepsTheGuardInteractionBeforeTheUnpreparedExamEntrance()
    {
        string directory = TestResources.GameDirectory;
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.9")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(129, session.State.FieldId);
        Assert.False(session.State.ExamTimer!.TimerMemoryActivated);
        IReadOnlyList<AutoPlayTarget> targets = world.DiscoverAutoPlayTargets();
        Assert.DoesNotContain(targets, target => target.DestinationField == 131);
        Assert.Contains(targets, target => target.Kind == AutoPlayTargetKind.Talk && target.ObjectId is 9 or 10);
        Assert.Null(world.Events.LastFault);
    }

    [Theory]
    [InlineData(Jsm.Opcode.POPM_B, 255, false)]
    [InlineData(Jsm.Opcode.POPM_B, 256, true)]
    [InlineData(Jsm.Opcode.POPM_B, 257, true)]
    [InlineData(Jsm.Opcode.POPM_W, 255, true)]
    [InlineData(Jsm.Opcode.POPM_L, 254, true)]
    [InlineData(Jsm.Opcode.POPM_W, 258, false)]
    [InlineData(Jsm.Opcode.PSHM_W, 256, false)]
    [InlineData(Jsm.Opcode.POPI_L, 0, false)]
    public void StoryClassifierUsesTypedWriteOverlap(Jsm.Opcode opcode, int address, bool expected)
    {
        JsmProgram program = JsmProgram.Read(Program([(Jsm.Opcode.PSHN_L, 1), (opcode, address)]));
        Assert.Equal(expected, GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
    }

    [Fact]
    public void StoryClassifierFollowsRequestedLabelWithoutClassifyingUnrequestedScripts()
    {
        byte[] source = Program([(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.REQ, 0)], [(Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.POPM_W, 256)], [(Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 700)]);
        JsmProgram program = JsmProgram.Read(source);
        Assert.True(GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
        source = Program([(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 2), (Jsm.Opcode.REQ, 0)], [(Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.POPM_W, 256)], [(Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 700)]);
        program = JsmProgram.Read(source);
        Assert.False(GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
    }

    [Fact]
    public void ClearClassifiesItsCanonicalStoryBankMutation()
    {
        JsmProgram program = JsmProgram.Read(Program([(Jsm.Opcode.CLEAR, 0)]));
        Assert.True(GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
    }

    [Theory]
    [InlineData(Jsm.Opcode.PREQ)]
    [InlineData(Jsm.Opcode.PREQSW)]
    [InlineData(Jsm.Opcode.PREQEW)]
    public void PartyRequestsUsePhysicalSelectorsAndKeepUnknownPointersConservative(Jsm.Opcode opcode)
    {
        JsmProgram program = JsmProgram.Read(ProgramWithLabel(0, [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 1), (opcode, 0)], [(Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.POPM_W, 256)], [(Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 700)]));
        Assert.True(GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
        program = JsmProgram.Read(ProgramWithLabel(0, [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 2), (opcode, 0)], [(Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.POPM_W, 256)], [(Jsm.Opcode.PSHN_L, 9), (Jsm.Opcode.POPM_W, 700)]));
        Assert.False(GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
        program = JsmProgram.Read(ProgramWithLabel(0, [(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 41), (opcode, 0)], [(Jsm.Opcode.PSHN_L, 5), (Jsm.Opcode.POPM_W, 256)]));
        Assert.True(GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
    }

    [Fact]
    public void DynamicPartyScriptSelectionIsConservativeButInvalidPartySlotsDoNotCreateEdges()
    {
        JsmProgram program = JsmProgram.Read(Program([(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.PREQ, 0)], [(Jsm.Opcode.CLEAR, 0)]));
        Assert.True(GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
        program = JsmProgram.Read(Program([(Jsm.Opcode.PSHN_L, 7), (Jsm.Opcode.PSHN_L, 1), (Jsm.Opcode.PREQ, 3)], [(Jsm.Opcode.CLEAR, 0)]));
        Assert.False(GameWorld.AutoPlayScriptChangesStory(program, program.Objects[0].Scripts[0]));
    }

    [Fact]
    public void ScriptDestinationsRequireAnUnambiguousOriginalLiteralPacket()
    {
        JsmProgram program = JsmProgram.Read(Program([(Jsm.Opcode.PSHN_L, 157), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.MAPJUMP, 5)]));
        Assert.Equal(157, GameWorld.AutoPlayScriptDestination(program, program.Objects[0].Scripts[0]));
        program = JsmProgram.Read(Program([(Jsm.Opcode.PSHM_W, 256), (Jsm.Opcode.PSHN_L, 10), (Jsm.Opcode.PSHN_L, 20), (Jsm.Opcode.PSHN_L, 64), (Jsm.Opcode.MAPJUMP, 5)]));
        Assert.Null(GameWorld.AutoPlayScriptDestination(program, program.Objects[0].Scripts[0]));
    }

    [Fact]
    public void ReachabilityExcludesDisconnectedAndLockedTriangles()
    {
        WalkmeshTriangle triangle = new(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, 1, -1, -1);
        FieldWalkmesh walkmesh = new([triangle, triangle with { NeighborA = 0, NeighborB = 2 }, triangle with { NeighborA = 1 }, triangle with { NeighborA = -1 }]);
        Assert.Equal(new[] { 0, 1, 2 }, GameWorld.AutoPlayReachableTriangles(walkmesh, 0, new HashSet<int>()).Order());
        Assert.Equal(new[] { 0 }, GameWorld.AutoPlayReachableTriangles(walkmesh, 0, new HashSet<int> { 1 }).Order());
    }

    [Fact]
    public void DiagnosticPlacementRequiresAControlledFieldLeader()
    {
        var engine = new EventEngine(new GameSession(new GameState()));
        Assert.Throws<InvalidOperationException>(() => engine.TeleportPlayer(0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.TeleportPlayer(0, 0, 0, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => engine.TeleportPlayer(0, 0, 0, 0, 256));
    }

    [Fact]
    public void ScriptedRawInputDiscoveryExpandsNativeAnyBitMasksWithoutChangingTheVm()
    {
        var engine = new EventEngine(new GameSession(new GameState()));
        engine.LoadField(600, Program([(Jsm.Opcode.PSHN_L, 0x140), (Jsm.Opcode.KEYSCAN2, 0)]), []);
        JsonElement before = engine.Capture();
        ScriptedInputMetadata input = Assert.IsType<ScriptedInputMetadata>(engine.DiscoverScriptedInput());
        Assert.Equal(new[] { 0x40, 0x100 }, input.Masks);
        Assert.Equal(0x140, Assert.Single(input.Queries).Mask);
        Assert.True(JsonElement.DeepEquals(before, engine.Capture()));
        Assert.Equal(input.Fingerprint, engine.DiscoverScriptedInput()!.Fingerprint);
    }

    [Fact]
    public void DynamicNativeRawMasksExposeFiniteSingleBitAlternatives()
    {
        var engine = new EventEngine(new GameSession(new GameState()));
        engine.LoadField(600, Program([(Jsm.Opcode.PSHI_L, 0), (Jsm.Opcode.KEYSCAN2, 0)]), []);
        ScriptedInputMetadata input = Assert.IsType<ScriptedInputMetadata>(engine.DiscoverScriptedInput());
        Assert.Null(Assert.Single(input.Queries).Mask);
        Assert.Equal(Enumerable.Range(0, 16).Select(bit => 1 << bit), input.Masks);
    }

    [Fact]
    public void FieldsWithoutActiveKeyQueriesHaveNoScriptedInputMetadata()
    {
        var engine = new EventEngine(new GameSession(new GameState()));
        engine.LoadField(600, Program([(Jsm.Opcode.NOP, 0)]), []);
        Assert.Null(engine.DiscoverScriptedInput());
    }

    [Fact]
    public void DiagnosticPlacementPreservesNativeSaveAndDoesNotRunScripts()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.1")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        ActorState player = world.Events.PlayerActor!;
        long frame = world.Events.Frame;
        int triangle = player.WalkmeshTriangle;
        float x = player.X;
        float y = player.Y;
        float z = player.Z;
        int direction = (int)player.Direction;
        JsonElement before = world.Events.Capture();
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Events.TeleportPlayer(float.NaN, y, z, triangle));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        world.Events.TeleportPlayer(x + 1, y, z, triangle, direction);
        Assert.Equal(frame, world.Events.Frame);
        Assert.Equal(x + 1, player.X);
        Assert.Equal(new FieldFixedPosition((int)((x + 1) * 4096), (int)(y * 4096), (int)(z * 4096)), world.Events.Scene.NativePlayerPosition);
        JsonElement saved = world.Events.Capture();
        using var restored = new GameWorld(new GameSession(session.State with { EventState = saved }), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(saved, restored.Events.Capture()));
        Assert.All(world.DiscoverAutoPlayTargets(), target => Assert.InRange(target.Triangle, 0, world.Field!.Walkmesh.Triangles.Count - 1));
        Assert.True(JsonElement.DeepEquals(saved, world.Events.Capture()));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    [Fact]
    public void PlayerPlacementUpdatesTheRetainedCompletedNativeMoveBeforeSnapshot()
    {
        string directory = TestResources.GameDirectory;
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.10")));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        ActorState player = world.Events.PlayerActor!;
        NativeScriptMovementState previous = Assert.IsType<NativeScriptMovementState>(player.NativeScriptMovement);
        Assert.False(previous.Active);
        Assert.False(previous.Cancelled);
        Assert.Equal((ushort)2, player.NativeMovementCompletion542);
        long frame = world.Events.Frame;
        int direction = ((int)player.Direction + 64) & 255;
        world.Events.TeleportPlayer(player.X + 1, player.Y + 1, player.Z, player.WalkmeshTriangle, direction);
        NativeScriptMovementState current = Assert.IsType<NativeScriptMovementState>(player.NativeScriptMovement);
        Assert.Equal(frame, world.Events.Frame);
        Assert.Equal(world.Events.Scene.NativePlayerPosition, new FieldFixedPosition(current.FixedX, current.FixedY, current.FixedZ));
        Assert.Equal(player.WalkmeshTriangle, current.Triangle);
        Assert.Equal(player.MovementSpeed, current.Speed);
        Assert.Equal((byte)player.Direction, current.FacingDirection);
        Assert.Equal(player.NativeMotionAuthority!.MovementDirection, current.MovementDirection);
        Assert.Equal(previous.TargetX, current.TargetX);
        Assert.Equal(previous.TargetY, current.TargetY);
        Assert.Equal(previous.TargetZ, current.TargetZ);
        Assert.Equal(previous.FiberSequence, current.FiberSequence);
        Assert.Equal(previous.ScriptId, current.ScriptId);
        Assert.Equal(previous.Operation, current.Operation);
        Assert.Equal(previous.Active, current.Active);
        Assert.Equal(previous.Cancelled, current.Cancelled);
        Assert.Null(player.MovementTarget);
        JsonElement saved = world.Events.Capture();
        using GameWorld restored = new(new GameSession(session.State with { EventState = saved }), directory, validateWorldPresentation: false);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(saved, restored.Events.Capture()));
        Assert.Equal(world.Apply(new PlayerAction(PlayerActionKind.Wait)), restored.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
    }

    [Fact]
    public void NativeLineReceiptRemainsPendingDuringFreeControlUntilItsSourceScriptReturns()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.2")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.False(world.Events.HasPendingPlayerInteraction(10));
        AutoPlayTarget target = world.DiscoverAutoPlayTargets().First(target => target.CoverageId == "229:line:10");
        Assert.False(world.Events.TeleportPlayer(target.ApproachX!.Value, target.ApproachY!.Value, target.ApproachZ!.Value, target.ApproachTriangle!.Value, target.Direction));
        Assert.True(world.Events.TeleportPlayer(target.X, target.Y, target.Z, target.Triangle, target.Direction, true));
        Assert.True(world.Events.HasPendingPlayerInteraction(10));
        for (int frame = 0; frame < 4; frame++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.True(world.Events.HasPendingPlayerInteraction(10));
        for (int frame = 0; frame < 4000 && (world.Events.HasPendingPlayerInteraction(10) || !world.Events.IsUserControlEnabled); frame++)
        {
            world.Apply(new PlayerAction(world.CurrentDialogue is { CanConfirm: true } ? PlayerActionKind.Confirm : PlayerActionKind.Wait));
        }
        Assert.False(world.Events.HasPendingPlayerInteraction(10));
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NativeActionOnlyDoorUsesItsOrdinaryConfirmEdge()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.3")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        AutoPlayTarget[] targets = world.DiscoverAutoPlayTargets().Where(target => target.ObjectId == 23).ToArray();
        Assert.NotEmpty(targets);
        Assert.All(targets, target => Assert.True(target.RequiresConfirm));
        AutoPlayTarget target = targets[0];
        Assert.Equal("232:line-action:23", target.CoverageId);
        Assert.Equal(234, target.DestinationField);
        world.Events.TeleportPlayer(target.ApproachX!.Value, target.ApproachY!.Value, target.ApproachZ!.Value, target.ApproachTriangle!.Value, target.Direction);
        Assert.False(world.Events.TeleportPlayer(target.X, target.Y, target.Z, target.Triangle, target.Direction, true));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Confirm)).Accepted);
        Assert.True(world.Events.HasPendingPlayerInteraction(23));
        for (int frame = 0; frame < 300 && session.State.FieldId == 232; frame++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.Equal(234, session.State.FieldId);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NativeScriptedScreenDiscoversHeldMasksAndExitsWithAReleaseThenCancel()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.4")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.False(world.Events.IsUserControlEnabled);
        JsonElement before = world.Events.Capture();
        ScriptedInputMetadata metadata = Assert.IsType<ScriptedInputMetadata>(world.Events.DiscoverScriptedInput());
        Assert.Contains(0x10, metadata.Masks);
        Assert.Contains(0x40, metadata.Masks);
        Assert.Contains(metadata.Queries, query => query.Opcode == "KEYSCAN" && query.Mask == 0x10);
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        using var restored = new GameWorld(new GameSession(session.State with { EventState = before }), directory);
        restored.EnsureField();
        Assert.Equal(metadata.Fingerprint, restored.Events.DiscoverScriptedInput()!.Fingerprint);
        world.Apply(new PlayerAction(PlayerActionKind.FieldButtons));
        for (int frame = 0; frame < 32; frame++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
            Assert.Equal(metadata.Fingerprint, world.Events.DiscoverScriptedInput()!.Fingerprint);
        }
        int selection = session.State.Variables.GetValueOrDefault(1024);
        world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0x4000));
        for (int frame = 0; frame < 100 && session.State.Variables.GetValueOrDefault(1024) == selection; frame++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.NotEqual(selection, session.State.Variables.GetValueOrDefault(1024));
        world.Apply(new PlayerAction(PlayerActionKind.FieldButtons));
        world.Apply(new PlayerAction(PlayerActionKind.Wait));
        Assert.NotEqual(metadata.Fingerprint, world.Events.DiscoverScriptedInput()!.Fingerprint);
        world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 0x10));
        for (int frame = 0; frame < 300 && session.State.FieldId == 235; frame++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.Equal(234, session.State.FieldId);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NativeNestedScreenCancelReturnsItsParentDespiteBackgroundKeyPolling()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.5")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(30, world.CurrentDialogue?.MessageId);
        Assert.Equal(3, world.CurrentDialogue?.CancelChoice);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        for (int callback = 0; callback < 32 && world.CurrentDialogue?.MessageId != 7; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.Equal(7, world.CurrentDialogue?.MessageId);
        Assert.Equal(5, world.CurrentDialogue?.CancelChoice);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        for (int callback = 0; callback < 32; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.Null(world.CurrentDialogue);
        Assert.NotNull(world.Events.DiscoverScriptedInput());
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void CoordinateDiscoveryExcludesNativeExitCandidatesOutsideTheCurrentWalkmeshComponent()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.6")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        JsonElement before = world.Events.Capture();
        GameState state = session.State;
        int executedCommands = 0;
        world.Events.CommandExecuted += _ => executedCommands++;
        IReadOnlyList<AutoPlayTarget> targets = world.DiscoverAutoPlayTargets();
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        Assert.Same(state, session.State);
        Assert.Equal(0, executedCommands);
        Assert.NotEmpty(targets);
        HashSet<int> reachable = GameWorld.AutoPlayReachableTriangles(world.Field!.Walkmesh, world.Events.PlayerActor!.WalkmeshTriangle, world.Events.Scene.LockedTriangles);
        Assert.All(targets, target =>
        {
            Assert.Contains(target.Triangle, reachable);
            Assert.DoesNotContain(target.Triangle, world.Events.Scene.LockedTriangles);
            if (target.ApproachTriangle is int approach)
            {
                Assert.Contains(approach, reachable);
                Assert.DoesNotContain(approach, world.Events.Scene.LockedTriangles);
            }
        });
        Assert.All(targets, target => Assert.InRange(target.Order, 0, 99999));
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void CoordinateDiscoveryRequiresConnectedUnlockedGatewayTargetsAndApproaches()
    {
        byte[] source = Program([(Jsm.Opcode.NOP, 0)]);
        GameSession session = new(new GameState());
        EventEngine events = new(session);
        events.LoadField(600, source, []);
        events.Scene.LockedTriangles.Add(1);
        events.InfGateways =
        [
            new(new Vector3(30, 50, 0), new Vector3(80, 50, 0), new FieldTransition(601, 0, 0, 0, 0)),
            new(new Vector3(130, 150, 0), new Vector3(180, 150, 0), new FieldTransition(602, 0, 0, 0, 0)),
            new(new Vector3(530, 50, 0), new Vector3(580, 50, 0), new FieldTransition(603, 0, 0, 0, 0))
        ];
        WalkmeshTriangle first = new(new Vector3(0, 0, 0), new Vector3(200, 0, 0), new Vector3(0, 200, 0), 1, -1, -1);
        WalkmeshTriangle connected = new(new Vector3(200, 0, 0), new Vector3(200, 200, 0), new Vector3(0, 200, 0), -1, 0, -1);
        WalkmeshTriangle disconnected = new(new Vector3(500, 0, 0), new Vector3(700, 0, 0), new Vector3(500, 200, 0), -1, -1, -1);
        FieldWalkmesh walkmesh = new([first, connected, disconnected]);
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 1);
        FieldAssets field = new(600, "component", source, [], [], camera, walkmesh, []);
        ActorState player = new(0, 0);
        JsonElement before = events.Capture();
        GameState state = session.State;
        int executedCommands = 0;
        events.CommandExecuted += _ => executedCommands++;
        IReadOnlyList<AutoPlayTarget> targets = GameWorld.DiscoverAutoPlayTargets(field, events, player);
        Assert.Contains(targets, target => target.DestinationField == 601);
        Assert.DoesNotContain(targets, target => target.DestinationField == 602);
        Assert.DoesNotContain(targets, target => target.DestinationField == 603);
        Assert.All(targets, target => Assert.Equal(0, target.Triangle));
        Assert.All(targets, target => Assert.Equal(0, target.ApproachTriangle));
        Assert.All(targets, target => Assert.DoesNotContain(target.Triangle, events.Scene.LockedTriangles));
        Assert.All(targets.Where(target => target.ApproachTriangle is not null), target => Assert.DoesNotContain(target.ApproachTriangle!.Value, events.Scene.LockedTriangles));
        Assert.True(JsonElement.DeepEquals(before, events.Capture()));
        Assert.Same(state, session.State);
        Assert.Equal(0, executedCommands);
        events.Scene.LockedTriangles.Clear();
        before = events.Capture();
        targets = GameWorld.DiscoverAutoPlayTargets(field, events, player);
        Assert.Contains(targets, target => target.DestinationField == 602);
        Assert.DoesNotContain(targets, target => target.DestinationField == 603);
        Assert.All(targets, target => Assert.InRange(target.Triangle, 0, 1));
        Assert.All(targets, target => Assert.InRange(target.ApproachTriangle!.Value, 0, 1));
        Assert.True(JsonElement.DeepEquals(before, events.Capture()));
        Assert.Same(state, session.State);
        Assert.Equal(0, executedCommands);
    }

    [Fact]
    public void AReachableContactSideCannotUseAnApproachAcrossADisconnectedNeighbor()
    {
        byte[] source = Program([(Jsm.Opcode.NOP, 0)]);
        GameSession session = new(new GameState());
        EventEngine events = new(session);
        events.LoadField(600, source, []);
        events.InfGateways = [new(new Vector3(60, 140, 0), new Vector3(140, 60, 0), new FieldTransition(601, 0, 0, 0, 0))];
        WalkmeshTriangle first = new(Vector3.Zero, new Vector3(200, 0, 0), new Vector3(0, 200, 0), -1, -1, -1);
        WalkmeshTriangle neighbor = new(new Vector3(200, 0, 0), new Vector3(200, 200, 0), new Vector3(0, 200, 0), -1, -1, -1);
        FieldCamera camera = new(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 1);
        FieldAssets field = new(600, "component", source, [], [], camera, new([first, neighbor]), []);
        ActorState player = new(0, 0);
        JsonElement before = events.Capture();
        GameState state = session.State;
        int executedCommands = 0;
        events.CommandExecuted += _ => executedCommands++;

        Assert.Empty(GameWorld.DiscoverAutoPlayTargets(field, events, player));

        FieldAssets connected = field with { Walkmesh = new([first with { NeighborA = 1 }, neighbor with { NeighborB = 0 }]) };
        IReadOnlyList<AutoPlayTarget> targets = GameWorld.DiscoverAutoPlayTargets(connected, events, player);
        Assert.Equal(6, targets.Count);
        Assert.Contains(targets, target => target.Triangle == 0 && target.ApproachTriangle == 1);
        Assert.Contains(targets, target => target.Triangle == 1 && target.ApproachTriangle == 0);
        Assert.True(JsonElement.DeepEquals(before, events.Capture()));
        Assert.Same(state, session.State);
        Assert.Equal(0, executedCommands);
    }

    [Fact]
    public void AnAmbientRequestedAnimationLoopDoesNotKeepACompletedTalkPending()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.7")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Null(world.CurrentDialogue);
        Assert.Contains(world.Events.Capture().GetProperty("Fibers").EnumerateArray(), fiber => fiber.GetProperty("ObjectId").GetInt32() == 18 && fiber.GetProperty("ScriptId").GetInt32() == 95 && !fiber.GetProperty("Complete").GetBoolean());
        Assert.False(world.Events.HasPendingPlayerInteraction(18));
        NativeActorAnimationHeader? animation = world.Events.Actors[18].NativeAnimationHeader;
        world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 8));
        Assert.False(world.Events.HasPendingPlayerInteraction(18));
        Assert.NotEqual(animation, world.Events.Actors[18].NativeAnimationHeader);
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void ControlledContactPollingRequiresAnActualRisingEdgeAtItsOriginalKeyQuery()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(GameSession.Load(TestResources.FixturePath("App.Tests.AutoPlayDiscoveryTests.8")));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.True(world.Events.HasPendingPlayerInteraction(19));
        ScriptedInputMetadata metadata = Assert.IsType<ScriptedInputMetadata>(world.Events.DiscoverScriptedInput(19));
        Assert.All(metadata.Queries, query => Assert.Equal(19, query.ObjectId));
        Assert.Contains(metadata.Queries, query => query.Opcode == "KEYON" && query.Mask == 192);
        Assert.Contains(64, metadata.Masks);
        bool edgeConsumed = false;
        world.Events.CommandExecuted += command =>
        {
            if (command.Name == "KEYON" && command.ObjectId == 19)
            {
                edgeConsumed |= (world.Events.ButtonInput!.Pressed!.Value & command.Arguments[0] & 64) != 0;
            }
        };
        for (int attempt = 0; attempt < 8 && !edgeConsumed; attempt++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.FieldButtons));
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
            world.Apply(new PlayerAction(PlayerActionKind.FieldButtons, 64));
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        world.Apply(new PlayerAction(PlayerActionKind.FieldButtons));
        Assert.True(edgeConsumed);
        for (int callback = 0; callback < 8 && world.Events.IsUserControlEnabled; callback++)
        {
            world.Apply(new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.LastFault);
    }

    private static byte[] Program(params (Jsm.Opcode Opcode, int Parameter)[][] scripts)
    {
        return ProgramWithLabel(0, scripts);
    }

    private static byte[] ProgramWithLabel(int firstLabel, params (Jsm.Opcode Opcode, int Parameter)[][] scripts)
    {
        List<(Jsm.Opcode Opcode, int Parameter)> operations = [];
        List<ushort> positions = [];
        for (int index = 0; index < scripts.Length; index++)
        {
            positions.Add(checked((ushort)operations.Count));
            operations.Add((Jsm.Opcode.LBL, firstLabel + index));
            operations.AddRange(scripts[index]);
            operations.Add((Jsm.Opcode.RET, 8));
        }
        positions.Add(checked((ushort)operations.Count));
        int operationsOffset = 10 + positions.Count * 2;
        byte[] bytes = new byte[operationsOffset + operations.Count * 4];
        bytes[3] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), checked((ushort)operationsOffset));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), checked((ushort)((firstLabel << 7) | (scripts.Length - 1))));
        for (int index = 0; index < positions.Count; index++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10 + index * 2), positions[index]);
        }
        for (int index = 0; index < operations.Count; index++)
        {
            (Jsm.Opcode opcode, int parameter) = operations[index];
            int encoded = parameter != 0 || opcode is Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHI_L or Jsm.Opcode.REQ or Jsm.Opcode.PREQ or Jsm.Opcode.PREQSW or Jsm.Opcode.PREQEW or Jsm.Opcode.LBL ? (int)opcode << 24 | parameter & 0xFFFFFF : (int)opcode;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(operationsOffset + index * 4), encoded);
        }
        return bytes;
    }
}
