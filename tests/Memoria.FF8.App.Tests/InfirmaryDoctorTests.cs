using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InfirmaryDoctorTests
{

    [Fact]
    public void EarlyDoctorConversationRendersAndRecreatesFromOrdinaryInputs()
    {
        string game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original game corpus is required.");
        DirectoryInfo? repository = new(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "docs", "recreate-checkpoint.md")))
        {
            repository = repository.Parent;
        }
        Assert.NotNull(repository);
        var session = new GameSession(SnapshotFixtures.Require("MEMORIA_FF8_DOCTOR_START_SNAPSHOT", 165));
        using var world = new GameWorld(session, game);
        world.EnsureField();
        string output = Path.Combine(repository.FullName, "../fixtures");
        Directory.CreateDirectory(output);
        try
        {
            foreach (int destination in new[] { 168, 227, 179 })
            {
                IfritCausalRecreationTests.WalkGateway(world, session, destination, lineObjectId: destination == 227 ? 24 : null);
                Console.WriteLine($"Entered {world.Events.FieldId}/{world.Events.Frame}.");
            }
            IfritCausalRecreationTests.WalkRoutedToInteraction(world, session, 10);
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Interact, 10)).Accepted);
            FinishConversation(world, session);
            VerifyRecreation(world, session, game, Path.Combine(repository.FullName, "../fixtures/external-fixture.json"));
        }
        catch (Exception exception)
        {
            session.SetEventState(world.Events.Capture());
            session.Save(Path.Combine(output, "boundary.json"));
            Console.WriteLine(JsonSerializer.Serialize(new { exception.Message, world.Events.FieldId, world.Events.Frame, world.Events.LastFault, Draws = world.Events.Scene.ModelParts?.Draws, Actors = world.Events.Actors.Where(actor => actor.Visible).Select(actor => new { actor.ObjectId, actor.ModelId }) }));
            throw;
        }
    }

    private static void FinishConversation(GameWorld world, GameSession session)
    {
        bool spoke = false;
        for (int callback = 0; callback < 1000; callback++)
        {
            Assert.Null(world.Events.LastFault);
            Assert.NotEmpty(world.Render(TimeSpan.Zero));
            DialogueState? dialogue = world.Events.CurrentDialogue;
            if (dialogue is not null)
            {
                spoke = true;
            }
            else if (spoke && world.Events.IsUserControlEnabled && world.Events.Actors[10] is { FaceReturning: false } doctor && doctor.FaceProgress == doctor.FaceDuration)
            {
                return;
            }
            PlayerAction next = dialogue?.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new PlayerAction(PlayerActionKind.Confirm) : new PlayerAction(PlayerActionKind.Wait);
            Assert.True(world.Apply(next).Accepted);
        }
        Assert.Fail("Doctor conversation did not complete within its bounded ordinary input budget.");
    }

    private static void VerifyRecreation(GameWorld world, GameSession session, string game, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        session.SetEventState(world.Events.Capture());
        session.Save(path);
        GameState source = session.State;
        GameState recreated = GameSession.Load(path);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(source), JsonSerializer.SerializeToElement(recreated)));
        using var rebuilt = new GameWorld(new GameSession(recreated), game);
        rebuilt.EnsureField();
        Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));
        PlayerAction wait = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(wait), rebuilt.Apply(wait));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), rebuilt.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), rebuilt.Render(TimeSpan.Zero));
    }
}
