using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class TerminalInputTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public void PersonalDeskTerminalPollsHeldButtonsAndRecreatesOrdinaryNavigation()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(RecreateClassroom(directory));
        using var world = new GameWorld(session, directory);
        OpenPersonalTerminal(world, session);
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Equal(2, session.State.Variables[1024]);

        DriveUntil(world, () => session.State.Variables[1024] == 4, new PlayerAction(PlayerActionKind.FieldButtons, 0x4000));
        Apply(world, new PlayerAction(PlayerActionKind.FieldButtons));
        session.SetEventState(world.Events.Capture());
        GameState source = session.State;
        GameState recreated = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(source))!;
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source, JsonOptions), JsonSerializer.SerializeToElement(recreated, JsonOptions)), "Authentic terminal navigation must recreate complete state.");
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TERMINAL_EXPORT") == "1")
        {
            string evidence = TestResources.ArtifactDirectory;
            session.Save(TestResources.ArtifactFile("App.Tests.TerminalInputTests.2.json"));
        }
        using var rebuilt = new GameWorld(new GameSession(recreated), directory);
        rebuilt.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));

        DriveTogetherUntil(world, rebuilt, () => world.Events.CurrentDialogue?.MessageId == 3, new PlayerAction(PlayerActionKind.FieldButtons, 0x40));
        ApplyTogether(world, rebuilt, new PlayerAction(PlayerActionKind.FieldButtons));
        Assert.Equal(22, session.State.Variables[1024]);
        Assert.Equal(3, world.Events.CurrentDialogue?.FirstChoice);
        Assert.Equal(6, world.Events.CurrentDialogue?.LastChoice);
        ApplyTogether(world, rebuilt, new PlayerAction(PlayerActionKind.Cancel));
        DriveTogetherUntil(world, rebuilt, () => world.Events.CurrentDialogue is null && session.State.Variables[1025] == 0);
        Assert.Equal(4, session.State.Variables[1024]);
        DriveTogetherUntil(world, rebuilt, () => world.Events.FieldId == 234, new PlayerAction(PlayerActionKind.FieldButtons, 0x10));
        ApplyTogether(world, rebuilt, new PlayerAction(PlayerActionKind.FieldButtons));
        DriveTogetherUntil(world, rebuilt, () => world.Events.CurrentDialogue?.MessageId == 8);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_TERMINAL_EXPORT") == "1")
        {
            string proof = TestResources.ArtifactFile("App.Tests.TerminalInputTests.1.json");
            File.WriteAllText(proof, JsonSerializer.Serialize(new { Baseline = "Explicit classroom232 snapshot and ordinary personal desk activation", Implementations = "Current Memoria GameWorld and independent snapshot continuation under the same code", CheckpointField = source.FieldId, CheckpointFrame = source.EventState!.Value.GetProperty("Frame").GetInt64(), CompleteStateEqual = true, IndependentCpuPixelsEqual = true, TerminalDownHeldMask = 0x4000, ConfirmHeldMask = 0x40, CancelHeldMask = 0x10, SubmenuMessage = 3, SubmenuCancelResumedMain = true, FinalField = world.Events.FieldId, FinalMessage = world.Events.CurrentDialogue?.MessageId, Limitation = "Original field scripts exercised; original FF8 runtime and GPU parity are not established." }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void PersonalDeskTutorialBrowserNaturallyNamesGuardianForcesAndRecreatesItsYield()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = new GameSession(RecreateClassroom(directory));
        using var world = new GameWorld(session, directory);
        OpenPersonalTerminal(world, session);
        Assert.Equal(0, session.State.Variables.GetValueOrDefault(384) & 2);
        DriveUntil(world, () => world.Events.CurrentDialogue?.MessageId == 1, new PlayerAction(PlayerActionKind.FieldButtons, 0x40));
        Apply(world, new PlayerAction(PlayerActionKind.FieldButtons));
        Apply(world, new PlayerAction(PlayerActionKind.Confirm));
        DriveUntil(world, () => world.Events.CurrentDialogue?.MessageId == 2);
        Assert.Contains(0, world.Events.AcquiredGuardianForces!);
        Assert.Contains(1, world.Events.AcquiredGuardianForces!);
        Assert.Equal(2, session.State.Variables[384] & 2);
        Apply(world, new PlayerAction(PlayerActionKind.Confirm));
        DriveUntil(world, () => world.Events.Scene.MinimalMenuCompletions.Any(completion => completion.MenuId == 26));
        MinimalMenuCompletion browser = Assert.Single(world.Events.Scene.MinimalMenuCompletions, completion => completion.MenuId == 26);
        Assert.Equal((byte)1, browser.Selection);
        Assert.Null(browser.GuardianForceId);
        Assert.NotEmpty(world.Events.Scene.MinimalMenuWaits);
        session.SetEventState(world.Events.Capture());
        GameState source = session.State;
        GameState recreated = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(source))!;
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source, JsonOptions), JsonSerializer.SerializeToElement(recreated, JsonOptions)), "The ordinary tutorial browser request must recreate at its real yielding operation.");
        using var rebuilt = new GameWorld(new GameSession(recreated), directory);
        rebuilt.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));
        DriveTogetherUntil(world, rebuilt, () => world.Events.CurrentDialogue is null && session.State.Variables[1025] == 0);
        Assert.Equal(2, session.State.Variables[1024]);
        Assert.Empty(world.Events.Scene.MinimalMenuWaits);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));
    }

    private static void OpenPersonalTerminal(GameWorld world, GameSession session)
    {
        world.EnsureField();
        Apply(world, new PlayerAction(PlayerActionKind.Move, Horizontal: -1));
        Apply(world, new PlayerAction(PlayerActionKind.Confirm));
        DriveUntil(world, () => world.Events.FieldId == 234 && world.Events.CurrentDialogue?.MessageId == 2);
        Apply(world, new PlayerAction(PlayerActionKind.Choose, 1));
        DriveUntil(world, () => world.Events.FieldId == 235 && session.State.Variables.GetValueOrDefault(1024) == 2 && session.State.Variables.GetValueOrDefault(1025) == 0);
    }

    private static void Apply(GameWorld world, PlayerAction action)
    {
        PlayerActionResult result = world.Apply(action);
        Assert.True(result.Accepted, result.Detail);
        if (action.Kind == PlayerActionKind.FieldButtons)
        {
            Assert.Equal(0, result.FramesAdvanced);
        }
        world.EnsureField();
        Assert.Null(world.Events.LastFault);
    }

    private static void ApplyTogether(GameWorld world, GameWorld rebuilt, PlayerAction action)
    {
        Assert.Equal(world.Apply(action), rebuilt.Apply(action));
        world.EnsureField();
        rebuilt.EnsureField();
        Assert.Null(world.Events.LastFault);
        Assert.Null(rebuilt.Events.LastFault);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), rebuilt.Events.Capture()));
    }

    private static void DriveUntil(GameWorld world, Func<bool> reached, PlayerAction? action = null)
    {
        for (int tick = 0; tick < 300 && !reached(); tick++)
        {
            Apply(world, action ?? new PlayerAction(PlayerActionKind.Wait));
            if (action?.Kind == PlayerActionKind.FieldButtons)
            {
                Apply(world, new PlayerAction(PlayerActionKind.Wait));
            }
        }
        Assert.True(reached(), $"Expected terminal boundary was not reached at field{world.Events.FieldId}/frame{world.Events.Frame}.");
    }

    private static void DriveTogetherUntil(GameWorld world, GameWorld rebuilt, Func<bool> reached, PlayerAction? action = null)
    {
        for (int tick = 0; tick < 300 && !reached(); tick++)
        {
            ApplyTogether(world, rebuilt, action ?? new PlayerAction(PlayerActionKind.Wait));
            if (action?.Kind == PlayerActionKind.FieldButtons)
            {
                ApplyTogether(world, rebuilt, new PlayerAction(PlayerActionKind.Wait));
            }
        }
        Assert.True(reached(), $"Expected terminal boundary was not reached at field{world.Events.FieldId}/frame{world.Events.Frame}.");
    }

    private static GameState RecreateClassroom(string directory)
    {
        var session = new GameSession(SnapshotFixtures.Require("MEMORIA_FF8_CLASSROOM_SNAPSHOT", 232));
        using var world = new GameWorld(session, directory);
        world.EnsureField();
        for (int tick = 0; tick < 3000 && world.Events.NextTrigger.Kind != PlayerTriggerKind.Playable; tick++)
        {
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction action = dialogue is { CanConfirm: true } ? dialogue.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Apply(world, action);
        }
        Assert.Equal(PlayerTriggerKind.Playable, world.Events.NextTrigger.Kind);
        Assert.Equal(232, world.Events.FieldId);
        session.SetEventState(world.Events.Capture());
        return session.State;
    }

    private static string Repository()
    {
        DirectoryInfo? repository = new(TestResources.FixtureDirectory);
        return repository!.FullName;
    }
}
