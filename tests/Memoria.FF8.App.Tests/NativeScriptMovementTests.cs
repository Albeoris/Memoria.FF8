using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeScriptMovementTests
{
    [Fact]
    public void FreshNativeCorridorNpcUsesGenericAuthorityWhileLegacyHistoryRemainsUnknown()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativeScriptMovementTests.1");
        GameState state = GameSession.Load(source);
        using var legacy = new GameWorld(new GameSession(state), directory);
        legacy.EnsureField();
        Assert.All(legacy.Events.Actors, actor => Assert.Null(actor.NativeMotionAuthority));
        string departure = TestResources.FixturePath("App.Tests.NativeScriptMovementTests.2");
        using var fresh = new GameWorld(new GameSession(state with { EventState = null, DepartingFieldState = GameSession.Load(departure).EventState }), directory);
        fresh.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            Assert.True(fresh.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        }
        ActorState npc = fresh.Events.Actors.Single(actor => actor.ObjectId == 13);
        Assert.Equal(new NativeMotionAuthority(1, 0, 0), npc.NativeMotionAuthority);
        Assert.True(NativeScriptMovementAdapter.Supports(fresh.Field!, fresh.Events.Scene, npc));
        // Isolated adapter input: limit7 is outside the legacy selected limit5.
        JsonNode input = JsonNode.Parse(JsonSerializer.Serialize(npc))!;
        input["MovementDirectionLimit"] = 7;
        ActorState generic = input.Deserialize<ActorState>()!;
        Assert.True(NativeScriptMovementAdapter.Supports(fresh.Field!, fresh.Events.Scene, generic));
        var query = new NativeScriptDirectionQuery(generic.ObjectId, -10125 * 4096, 1124 * 4096, -5945 * 4096, 315 * 4096, (short)generic.MovementSpeed, 0, 48, 0, (byte)generic.Direction, 0, generic.MovementDirectionLimit);
        NativeScriptDirectionResult result = NativeScriptMovementAdapter.ResolveDirection(fresh.Field!, fresh.Events.Scene, query, generic)!;
        Assert.Equal(NativeScriptDirectionStatus.Moving, result.Status);
        input["NativeMotionAuthority"] = null;
        Assert.False(NativeScriptMovementAdapter.Supports(fresh.Field!, fresh.Events.Scene, input.Deserialize<ActorState>()!));
        input["NativeMotionAuthority"] = JsonSerializer.SerializeToNode(new NativeMotionAuthority(1, 0, 1));
        Assert.False(NativeScriptMovementAdapter.Supports(fresh.Field!, fresh.Events.Scene, input.Deserialize<ActorState>()!));
        using var resumed = new GameWorld(new GameSession(state with { EventState = fresh.Events.Capture(), DepartingFieldState = null }), directory);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(fresh.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(npc.NativeMotionAuthority, resumed.Events.Actors.Single(actor => actor.ObjectId == 13).NativeMotionAuthority);
    }

    [Fact]
    public void NaturalInfirmaryAisleStartsNativeNpcThirteenAndRestoresTicks()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativeScriptMovementTests.3");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(149, world.Events.Frame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0.88991f, Vertical: 0.45614f)).Accepted);
        NativeScriptMovementState motion = world.Events.Actors.Single(actor => actor.ObjectId == 13).NativeScriptMovement!;
        Assert.Equal(552, motion.Operation);
        Assert.Equal(-5945 * 4096, motion.TargetX);
        Assert.Equal(315 * 4096, motion.TargetY);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            var action = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        }
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NaturalWestCorridorCurveStartsTheRemainingParallelNpcAndRestoresTicks()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativeScriptMovementTests.4");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(55, world.Events.Frame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 0.92642f, Vertical: -0.37649f)).Accepted);
        ActorState actor = world.Events.Actors.Single(value => value.ObjectId == 16);
        NativeScriptMovementState state = actor.NativeScriptMovement!;
        Assert.True(state.Active);
        Assert.Equal(1705, state.Operation);
        Assert.Equal(5, actor.MovementDirectionLimit);
        Assert.Equal(2500, state.Speed);
        Assert.Equal(-4462 * 4096, state.TargetX);
        Assert.Equal(-4570 * 4096, state.TargetY);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            var action = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        }
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NaturalWestRingNpcStartsOriginalMoveAndRestoresEverySubsequentTick()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativeScriptMovementTests.5");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(168, world.Events.FieldId);
        Assert.Equal(4, world.Events.Frame);
        ActorState npc = world.Events.Actors.Single(actor => actor.ObjectId == 17);
        Assert.False(npc.Visible);
        Assert.Null(npc.NativeScriptMovement);
        Assert.True(NativeScriptMovementAdapter.Supports(world.Field!, world.Events.Scene, npc));
        Assert.False(NativeScriptMovementAdapter.Supports(world.Field!, world.Events.Scene, world.Events.Actors.Single(actor => actor.ObjectId == 19)));
        Assert.False(NativeScriptMovementAdapter.Supports(world.Field! with { Id = 165 }, world.Events.Scene, npc));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        Assert.Equal(5, world.Events.Frame);
        Assert.True(npc.Visible);
        NativeScriptMovementState state = npc.NativeScriptMovement!;
        Assert.True(state.Active);
        Assert.Equal(69, state.ScriptId);
        Assert.Equal(1926, state.Operation);
        Assert.Equal(-5925 * 4096, state.TargetX);
        Assert.Equal(730 * 4096, state.TargetY);
        Assert.Equal(582 * 4096, state.TargetZ);
        Assert.Equal(2500, state.Speed);
        Assert.Equal(5, npc.MovementDirectionLimit);
        Assert.Equal(-5, npc.NativeTurnAccumulator474!.Value);
        Assert.Equal((ushort)1, npc.NativeMovementCompletion542);
        Assert.Equal(npc.WalkAnimationId, npc.AnimationId);
        Assert.False(npc.CollisionEnabled);
        Assert.False(npc.PushEnabled);
        ActorState parallel = world.Events.Actors.Single(actor => actor.ObjectId == 18);
        Assert.Equal(14, parallel.ModelId);
        Assert.Equal(73, parallel.NativeScriptMovement!.ScriptId);
        Assert.Equal(2364, parallel.NativeScriptMovement.Operation);
        Assert.Equal(-6116 * 4096, parallel.NativeScriptMovement.TargetX);
        Assert.Equal(494 * 4096, parallel.NativeScriptMovement.TargetY);
        Assert.Equal(582 * 4096, parallel.NativeScriptMovement.TargetZ);
        Assert.Equal(2500, parallel.NativeScriptMovement.Speed);
        Assert.Equal(5, parallel.MovementDirectionLimit);
        Assert.False(parallel.CollisionEnabled);
        Assert.False(parallel.PushEnabled);

        // Independent original first direction/proposal: DIR0, MLIMIT5, sin5=501/cos5=4065.
        FieldNativeMovementRequest proposal = new(187, -5120 * 4096, 3641 * 4096, 582 * 4096, 251, 2500, 48, false, true, 0, true);
        FieldNativeMovementResult ground = FieldNativeMovementProbe.Resolve(world.Field!.Walkmesh, proposal, world.Events.Scene.LockedTriangles);
        Assert.NotEqual(FieldNativeMovementStatus.Unsupported, ground.Status);
        Assert.Equal(-20976412, ground.Proposals[0].FixedX);
        Assert.Equal(14873839, ground.Proposals[0].FixedY);
        Assert.Equal(ground.FixedX, state.FixedX);
        Assert.Equal(ground.FixedY, state.FixedY);
        Assert.Equal(ground.FixedZ, state.FixedZ);
        Assert.Equal(ground.Triangle, state.Triangle);
        Assert.Equal(ground.Direction, state.MovementDirection);
        Assert.Equal(ground.Direction, state.FacingDirection);
        JsonElement fiber = world.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("Sequence").GetInt64() == state.FiberSequence);
        Assert.Equal(1926, fiber.GetProperty("Position").GetInt32());
        Assert.Equal(0, fiber.GetProperty("Stack").GetArrayLength());
        Assert.Equal("NativeScriptMovement", fiber.GetProperty("WaitReason").GetString());

        Assert.Null(NativeScriptMovementAdapter.ResolveDirection(world.Field, world.Events.Scene, new NativeScriptDirectionQuery(17, state.FixedX, state.FixedY, state.TargetX, state.TargetY, 2500, 1, 48, state.MovementDirection, state.FacingDirection, npc.NativeTurnAccumulator474!.Value, 5)));
        Assert.Null(NativeScriptMovementAdapter.ResolveDirection(world.Field, world.Events.Scene, new NativeScriptDirectionQuery(17, state.FixedX, state.FixedY, 32768 * 4096, state.TargetY, 2500, 0, 48, state.MovementDirection, state.FacingDirection, npc.NativeTurnAccumulator474!.Value, 5)));
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 6; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            var action = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
        }
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NaturalLobbyNpcKeepsNativeBlockedTurnAndRestoresEverySubsequentTick()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativeScriptMovementTests.6");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(5, world.Events.Frame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        ActorState actor = world.Events.Actors.Single(actor => actor.ObjectId == 20);
        Assert.Equal(6, world.Events.Frame);
        Assert.Equal(-2864, actor.X);
        Assert.Equal(-5474, actor.Y);
        Assert.Equal(567, actor.Z);
        Assert.Equal(212, actor.WalkmeshTriangle);
        NativeScriptMovementState state = actor.NativeScriptMovement!;
        Assert.True(state.Active);
        Assert.Equal(4, state.MovementDirection);
        Assert.Equal(4, state.FacingDirection);
        Assert.Equal(4, actor.NativeTurnAccumulator474!.Value);
        Assert.Equal((ushort)1, actor.NativeMovementCompletion542);
        Assert.Equal(1861, state.Operation);
        JsonElement fiber = world.Events.Capture().GetProperty("Fibers").EnumerateArray().Single(value => value.GetProperty("Sequence").GetInt64() == state.FiberSequence);
        Assert.Equal(1861, fiber.GetProperty("Position").GetInt32());
        Assert.Equal(0, fiber.GetProperty("Stack").GetArrayLength());
        Assert.Equal("NativeScriptMovement", fiber.GetProperty("WaitReason").GetString());
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 12; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            var action = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
        }
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
        Assert.False(NativeScriptMovementAdapter.Supports(world.Field!, world.Events.Scene, world.Events.Actors.Single(value => value.ObjectId == 17)));
    }

    [Fact]
    public void NaturalLobbyRunnerStartsOriginalMoveAndRestoresConcurrentNativeActors()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativeScriptMovementTests.7");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        Assert.Equal(65, world.Events.Frame);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        ActorState runner = world.Events.Actors.Single(actor => actor.ObjectId == 19);
        Assert.Equal(66, world.Events.Frame);
        NativeScriptMovementState state = runner.NativeScriptMovement!;
        Assert.True(state.Active);
        Assert.Equal(1397, state.Operation);
        Assert.Equal(-1781 * 4096, state.TargetX);
        Assert.Equal(-5752 * 4096, state.TargetY);
        Assert.Equal(6500, state.Speed);
        Assert.Equal(6, runner.MovementDirectionLimit);
        Assert.Equal(30, state.FacingDirection);
        Assert.Equal(6, runner.NativeTurnAccumulator474!.Value);
        Assert.Equal(runner.RunAnimationId, runner.AnimationId);
        Assert.Equal((ushort)1, runner.NativeMovementCompletion542);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            var action = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
        }
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
    }

    [Fact]
    public void NaturalLobbyRunnerRetainsRawCompletionAcrossFollowingOriginalMove()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        string source = TestResources.FixturePath("App.Tests.NativeScriptMovementTests.8");
        var session = new GameSession(GameSession.Load(source));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        ActorState runner = world.Events.Actors.Single(actor => actor.ObjectId == 19);
        Assert.Equal(110, world.Events.Frame);
        Assert.Equal(1397, runner.NativeScriptMovement!.Operation);
        Assert.Equal((ushort)2, runner.NativeMovementCompletion542);
        Assert.Equal(-1781 * 4096, runner.NativeScriptMovement.FixedX);
        Assert.Equal(-5752 * 4096, runner.NativeScriptMovement.FixedY);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        NativeScriptMovementState state = runner.NativeScriptMovement!;
        Assert.Equal(111, world.Events.Frame);
        Assert.True(state.Active);
        Assert.Equal(1404, state.Operation);
        Assert.Equal(-1086 * 4096, state.TargetX);
        Assert.Equal(-6240 * 4096, state.TargetY);
        Assert.Equal(567 * 4096, state.TargetZ);
        Assert.Equal((ushort)1, runner.NativeMovementCompletion542);
        Assert.Equal(6500, state.Speed);
        Assert.Equal(6, runner.MovementDirectionLimit);
        Assert.Equal(runner.RunAnimationId, runner.AnimationId);
        using var resumed = new GameWorld(new GameSession(session.State with { EventState = world.Events.Capture() }), directory);
        resumed.EnsureField();
        for (int tick = 0; tick < 4; tick++)
        {
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
            Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
            var action = new PlayerAction(PlayerActionKind.Wait);
            Assert.Equal(world.Apply(action), resumed.Apply(action));
        }
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), resumed.Render(TimeSpan.Zero));
        Assert.Null(world.Events.LastFault);
    }
}
