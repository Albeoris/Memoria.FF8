using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FootstepGameplayTests
{
    [Fact]
    public void AuthenticOpeningMovementEmitsBothFeetAndRestoresTheirPreviousModelOrigin()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field corpus is required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        List<NativeFootstepSound> cues = CaptureCues(world);
        ReachCorridor(world, session);
        ActorState leader = Assert.IsType<ActorState>(world.Events.PlayerActor);
        Assert.True(leader.FootstepsEnabled);
        Assert.NotNull(leader.NativeFootstepPair);
        Assert.Equal(byte.MaxValue, leader.NativeFootstepPair.First392);
        Assert.Equal(byte.MaxValue, leader.NativeFootstepPair.Second393);
        Assert.False(leader.NativeFootstepSpecial);
        int openingCues = cues.Count;
        for (int frame = 0; frame < 32; frame++)
        {
            Apply(world, new PlayerAction(PlayerActionKind.Wait));
        }
        cues.Clear();
        for (int frame = 0; frame < 32; frame++)
        {
            Apply(world, new PlayerAction(PlayerActionKind.Wait));
        }
        Assert.Empty(cues);
        int movedFrames = 0;
        List<object> movementFrames = [];
        for (int step = 0; step < 32; step++)
        {
            float oldX = leader.X;
            float oldY = leader.Y;
            Apply(world, Movement(step));
            movementFrames.Add(new { Step = step, world.Events.Frame, leader.X, leader.Y, leader.Z, leader.AnimationId, leader.AnimationFrame, leader.AnimationSpeed, leader.NativeAnimationHeader, leader.NativeMovementCompletion542, Cues = cues.Count });
            if (leader.X != oldX || leader.Y != oldY)
            {
                movedFrames++;
            }
        }
        Assert.Equal(229, world.Events.FieldId);
        Assert.True(movedFrames > 0, "Ordinary corridor MOVE actions must change the selected leader position.");
        NativeFootstepSound[] movementCues = cues.Where(cue => cue.ObjectId == leader.ObjectId).ToArray();
        Assert.True(movementCues.Length > 0, JsonSerializer.Serialize(new { Leader = leader.ObjectId, MovedFrames = movedFrames, Callback = world.Events.ResolveFootstepSound is not null, Frames = movementFrames }));
        Assert.Contains(movementCues, cue => cue.Mode == 0);
        Assert.Contains(movementCues, cue => cue.Mode == 1);
        Assert.All(movementCues, cue =>
        {
            Assert.InRange(cue.Pan, 0u, 255u);
            Assert.InRange(cue.Volume, 17u, 119u);
            Assert.Equal(0x400000u, cue.Flags);
            Assert.Equal(cue.Mode == 0 ? 3u : 2u, cue.EffectId);
        });
        Assert.NotNull(leader.NativeFootstepModelOrigin);
        session.SetEventState(world.Events.Capture());
        string? evidence = Environment.GetEnvironmentVariable("MEMORIA_FF8_FOOTSTEP_EVIDENCE");
        string output = Path.Combine(evidence ?? Path.GetTempPath(), "footsteps-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        string source = Path.Combine(output, "fresh-corridor-source.json");
        string recreatedPath = Path.Combine(output, "fresh-corridor-recreated.json");
        try
        {
            session.Save(source);
            GameState sourceState = GameSession.Load(source);
            GameState recreatedState = GameSession.Load(source);
            new GameSession(recreatedState).Save(recreatedPath);
            string[] snapshotDifferences = StateDifferences(sourceState, recreatedState);
            AssertSameFootstepState(sourceState, recreatedState, leader.ObjectId);
            GameState repeatedState = GameSession.Load(recreatedPath);
            AssertSameFootstepState(recreatedState, repeatedState, leader.ObjectId);
            GameSession restoredSession = new(GameSession.Load(source));
            AssertSameState(sourceState, restoredSession.State, "direct source load");
            using GameWorld restored = new(restoredSession, directory);
            List<NativeFootstepSound> resumedCues = CaptureCues(restored);
            restored.EnsureField();
            Assert.Empty(resumedCues);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            using GameWorld recreated = new(new GameSession(recreatedState), directory);
            using GameWorld repeated = new(new GameSession(repeatedState), directory);
            List<NativeFootstepSound> recreatedCues = CaptureCues(recreated);
            List<NativeFootstepSound> repeatedCues = CaptureCues(repeated);
            recreated.EnsureField();
            repeated.EnsureField();
            Assert.Empty(recreatedCues);
            Assert.Empty(repeatedCues);
            cues.Clear();
            for (int step = 32; step < 64; step++)
            {
                Apply(world, Movement(step));
                Apply(restored, Movement(step));
                Apply(recreated, Movement(step));
                Apply(repeated, Movement(step));
            }
            Assert.Equal(cues, resumedCues);
            Assert.NotEmpty(resumedCues);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
            Assert.Equal(recreatedCues, repeatedCues);
            Assert.NotEmpty(recreatedCues);
            var summary = new { Field = world.Events.FieldId, Leader = leader.ObjectId, OpeningCues = openingCues, MovedFrames = movedFrames, MovementCues = movementCues.Length, Mode0 = movementCues.Count(cue => cue.Mode == 0), Mode1 = movementCues.Count(cue => cue.Mode == 1), ResumedCues = resumedCues.Count, RecreatedCues = recreatedCues.Count, Movement = movementCues, Resumed = resumedCues, Source = source, Recreated = recreatedPath, DirectRestoreStateEqual = true, SnapshotFootstepStateEqual = true, SnapshotFullStateDifferences = snapshotDifferences, RepeatedSnapshotFullStateDifferences = StateDifferences(recreatedState, repeatedState) };
            File.WriteAllText(Path.Combine(output, "summary.json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(summary));
        }
        finally
        {
            if (evidence is null)
            {
                File.Delete(source);
                File.Delete(recreatedPath);
                File.Delete(Path.Combine(output, "summary.json"));
                Directory.Delete(output);
            }
        }
    }

    private static List<NativeFootstepSound> CaptureCues(GameWorld world)
    {
        List<NativeFootstepSound> cues = [];
        world.Events.AudioCommandExecuted += command =>
        {
            if (command is NativeFootstepSound cue)
            {
                cues.Add(cue);
            }
        };
        return cues;
    }

    private static void ReachCorridor(GameWorld world, GameSession session)
    {
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (world.Events.FieldId == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                return;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction action = session.IsMoviePlaying ? new PlayerAction(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new PlayerAction(PlayerActionKind.EnterName, Text: "Squall") : dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Apply(world, action);
        }
        Assert.Fail("Authentic opening exceeded its bounded action budget before the playable corridor.");
    }

    private static PlayerAction Movement(int step)
    {
        return step < 2 ? new(PlayerActionKind.Move, Horizontal: -1) : new(PlayerActionKind.Move, Vertical: 1);
    }

    private static void Apply(GameWorld world, PlayerAction action)
    {
        PlayerActionResult result = world.Apply(action);
        Assert.True(result.Accepted, $"{action} at field{world.Events.FieldId}/frame{world.Events.Frame}: {result.Detail}");
        Assert.Null(world.Events.LastFault);
    }

    private static void AssertSameState(GameState first, GameState second, string context)
    {
        string[] differences = StateDifferences(first, second);
        Assert.True(differences.Length == 0, $"{context} changed: {string.Join(", ", differences)}");
    }

    private static string[] StateDifferences(GameState first, GameState second)
    {
        JsonElement expected = JsonSerializer.SerializeToElement(first);
        JsonElement actual = JsonSerializer.SerializeToElement(second);
        return expected.EnumerateObject().Where(property => !actual.TryGetProperty(property.Name, out JsonElement other) || !JsonElement.DeepEquals(property.Value, other)).Select(property => property.Name).ToArray();
    }

    private static void AssertSameFootstepState(GameState first, GameState second, int leaderId)
    {
        Assert.Equal(first.Scene, second.Scene);
        Assert.Equal(first.FieldId, second.FieldId);
        Assert.Equal(first.X, second.X);
        Assert.Equal(first.Y, second.Y);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first.Variables), JsonSerializer.SerializeToElement(second.Variables)));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(first.GlobalSoundProcess), JsonSerializer.SerializeToElement(second.GlobalSoundProcess)));
        JsonElement firstEvents = first.EventState!.Value;
        JsonElement secondEvents = second.EventState!.Value;
        JsonElement firstLeader = firstEvents.GetProperty("Actors").EnumerateArray().Single(actor => actor.GetProperty("ObjectId").GetInt32() == leaderId);
        JsonElement secondLeader = secondEvents.GetProperty("Actors").EnumerateArray().Single(actor => actor.GetProperty("ObjectId").GetInt32() == leaderId);
        Assert.True(JsonElement.DeepEquals(firstLeader, secondLeader), "Snapshot continuation changed the selected leader, animation header, position or previous model origin.");
        Assert.True(JsonElement.DeepEquals(firstEvents.GetProperty("Scene").GetProperty("Party"), secondEvents.GetProperty("Scene").GetProperty("Party")));
    }
}
