using System.Numerics;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class WorldTests
{
    [Fact]
    public void Host_world_preserves_custom_name_across_actual_fields_and_composites_real_side_view_ramp()
    {
        Assert.Equal("Squall\nQuistis\nZell", DialogueTextFormatter.Format("{Char Squall}{Line}{Char Quistis}{Next}{Char Zell}{End}"));
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? LaunchOptions.DefaultGameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed field corpus is required.");
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using var world = new GameWorld(session, directory);
        List<int> fields = [];
        EventEngine persistent = world.Events;
        bool named = false;
        bool capturedRamp = false;
        bool returned = false;
        bool capturedDialogue = false;
        bool capturedSide = false;
        bool capturedMovieTail = false;
        for (int iteration = 0; iteration < 5000; iteration++)
        {
            if (world.EnsureField())
            {
                fields.Add(world.Field!.Id);
                Assert.Same(persistent, world.Events);
                if (named)
                {
                    Assert.Contains("Cecilia", world.Events.Scene.Names.Values);
                }
                if (world.Field.Id == 181)
                {
                    string nativeText = world.Field.Messages.First(message => message.Contains("{Char Squall}", StringComparison.Ordinal));
                    string formatted = DialogueTextFormatter.Format(nativeText, world.Events.Scene.Names);
                    Assert.Contains("Cecilia", formatted);
                    Assert.DoesNotContain("{Char Squall}", formatted);
                    Assert.Equal("Cecilia", world.Events.Scene.Names[0]);
                }
                if (fields.Count > 3 && world.Field.Id == 180)
                {
                    returned = true;
                }
            }
            if (session.IsMoviePlaying)
            {
                if (!capturedMovieTail && session.State.MovieIndex == 1)
                {
                    session.SetEventState(world.Events.Capture());
                    session.SetMoviePosition(12);
                    session.Save(Path.Combine(FindRepository(), "../fixtures/external-fixture.json"));
                    capturedMovieTail = true;
                }
                Assert.True(world.Apply(new PlayerAction(PlayerActionKind.SkipMovie)).Accepted);
            }
            else if (world.Events.CurrentDialogue is DialogueState dialogue)
            {
                if (!capturedDialogue && world.Field!.Id == 180)
                {
                    session.SetEventState(world.Events.Capture());
                    session.Save(Path.Combine(FindRepository(), "../fixtures/external-fixture.json"));
                    capturedDialogue = true;
                }
                PlayerAction action = dialogue.IsNamePrompt ? new PlayerAction(PlayerActionKind.EnterName, Text: "Cecilia") : dialogue.FirstChoice is int choice ? new PlayerAction(PlayerActionKind.Choose, choice) : new PlayerAction(PlayerActionKind.Confirm);
                Assert.True(world.Apply(action).Accepted);
                named |= dialogue.IsNamePrompt;
                session.DismissDialog();
                (int selection, string name) = DialoguePresentation.ResolveDraft(world.Events.CurrentDialogue, null, "Cecilia");
                if (dialogue.FirstChoice.HasValue)
                {
                    Assert.Null(world.Events.CurrentDialogue);
                    Assert.Equal(0, selection);
                    Assert.Equal("Cecilia", name);
                }
            }
            else
            {
                world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: world.Field!.Id == 181 ? 1 : 900));
            }
            NativeFadeDraw draw = world.Events.FieldFade!.Draw;
            Vector3 color = draw.Mode == NativeFadeDrawMode.Subtract ? new(draw.Red, draw.Green, draw.Blue) : Vector3.Zero;
            if (!capturedSide && world.Field!.Id == 181 && color.X == 0 && world.Events.Actors.Any(actor => actor.Active && actor.Visible))
            {
                session.SetEventState(world.Events.Capture());
                session.Save(Path.Combine(FindRepository(), "../fixtures/external-fixture.json"));
                capturedSide = true;
            }
            if (!capturedRamp && world.Field!.Id == 181 && color.X > 120 && color.X < 135)
            {
                byte[] pixels = world.Render(TimeSpan.FromSeconds(world.Events.Frame / 30d));
                Assert.Contains(pixels.Where((value, index) => index % 4 != 3), value => value > 0 && value < 136);
                session.SetEventState(world.Events.Capture());
                session.Save(Path.Combine(FindRepository(), "../fixtures/external-fixture.json"));
                capturedRamp = true;
            }
            if (world.Field!.Id == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                session.SetEventState(world.Events.Capture());
                session.Save(Path.Combine(FindRepository(), "../fixtures/external-fixture.json"));
                break;
            }
        }
        Assert.True(named && capturedRamp && returned);
        Assert.Equal(new[] { 74, 180, 181, 180, 229 }, fields);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Equal(192, world.Field!.Information!.ControlDirection);
        Assert.Equal(192, world.Events.InputAxisDirection);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Horizontal: 1)).Accepted);
        ActorState player = world.Events.Actors.First(actor => actor.Active && actor.CharacterId == 0);
        Assert.Equal(0, player.Direction);
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Move, Vertical: 1)).Accepted);
        Assert.Equal(64, player.Direction);
    }

    [Fact]
    public void Rgb_addition_subtraction_and_movie_tail_completion_preserve_their_contracts()
    {
        byte[] rgba = [180, 20, 120, 255, 40, 80, 160, 255];
        FieldColorComposite.Apply(rgba, new NativeFadeDraw(NativeFadeDrawMode.Subtract, 100, 10, 30));
        Assert.Equal(new byte[] { 80, 10, 90, 255, 0, 70, 130, 255 }, rgba);
        Assert.False(MoviePresentation.IsComplete(13.6, 13.667, true, true, 0));
        Assert.False(MoviePresentation.IsComplete(13.667, 13.667, true, true, 1));
        Assert.False(MoviePresentation.IsComplete(13.667, 13.667, true, false, 0));
        Assert.True(MoviePresentation.IsComplete(13.7, 13.667, true, true, 0));
    }

    [Theory]
    [InlineData(NativeFadeDrawMode.None, 0, 0, 0)]
    [InlineData(NativeFadeDrawMode.Add, 12, 255, 0)]
    [InlineData(NativeFadeDrawMode.Subtract, 44, 17, 127)]
    [InlineData(NativeFadeDrawMode.Black, 0, 0, 0)]
    public void Color_lookup_preserves_every_channel_value_and_alpha(NativeFadeDrawMode mode, byte red, byte green, byte blue)
    {
        int sign = mode == NativeFadeDrawMode.Add ? 1 : mode == NativeFadeDrawMode.Subtract ? -1 : 0;
        byte[] pixels = Enumerable.Range(0, 256).SelectMany(value => new byte[] { (byte)value, (byte)(255 - value), (byte)(value ^ 127), (byte)value }).ToArray();
        byte[] expected = pixels.ToArray();
        for (int offset = 0; offset < expected.Length; offset += 4)
        {
            expected[offset] = mode == NativeFadeDrawMode.Black ? (byte)0 : (byte)Math.Clamp(expected[offset] + sign * red, 0, 255);
            expected[offset + 1] = mode == NativeFadeDrawMode.Black ? (byte)0 : (byte)Math.Clamp(expected[offset + 1] + sign * green, 0, 255);
            expected[offset + 2] = mode == NativeFadeDrawMode.Black ? (byte)0 : (byte)Math.Clamp(expected[offset + 2] + sign * blue, 0, 255);
        }
        FieldColorComposite.Apply(pixels, new NativeFadeDraw(mode, red, green, blue));
        Assert.Equal(expected, pixels);
    }

    private static string FindRepository()
    {
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        return directory?.FullName ?? throw new DirectoryNotFoundException("FF8 research root was not found.");
    }
}
