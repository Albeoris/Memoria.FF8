using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SessionTests
{
    [Fact]
    public void BattleTestHostsUseIsolatedAchievementComposition()
    {
        Assert.True(new LaunchOptions { BattleTestPath = "test.json" }.IsAchievementToolSession);
    }
    [Fact]
    public void Achievement_local_only_is_explicit_and_tools_are_isolated()
    {
        Assert.False(LaunchOptions.Parse([]).AchievementsLocalOnly);
        Assert.True(LaunchOptions.Parse(["--achievements-local-only"]).AchievementsLocalOnly);
        Assert.False(LaunchOptions.Parse(["--update-control-dir", "update"]).IsAchievementToolSession);
        foreach (string[] arguments in new[] { new[] { "--validate" }, new[] { "--frames", "1" }, new[] { "--exit-after", "1" }, new[] { "--control-dir", "control" }, new[] { "--capture-frame", "preview.png" }, new[] { "--debug-room" } })
        {
            Assert.True(LaunchOptions.Parse(arguments).IsAchievementToolSession);
        }
    }

    [Fact]
    public void Update_control_directory_preserves_normal_gameplay_and_audio_options()
    {
        string directory = Path.Combine("Launcher session", "Control queue");
        LaunchOptions normal = LaunchOptions.Parse([]);
        LaunchOptions update = LaunchOptions.Parse(["--update-control-dir", directory]);
        Assert.Equal(Path.GetFullPath(directory), update.UpdateControlDirectory);
        Assert.Null(update.ControlDirectory);
        Assert.False(update.ControlStep);
        Assert.Equal(normal.AudioEnabled, update.AudioEnabled);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--update-control-dir"]));
    }

    [Fact]
    public void Mods_directory_argument_accepts_paths_with_spaces_and_requires_a_value()
    {
        string directory = Path.Combine("Custom package", "Texture packs");
        Assert.Null(LaunchOptions.Parse([]).ModsDirectory);
        Assert.Equal(Path.GetFullPath(directory), LaunchOptions.Parse(["--mods-dir", directory]).ModsDirectory);
        Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--mods-dir"]));
    }

    [Fact]
    public void New_game_movie_completion_and_field_transition_preserve_story_variables()
    {
        var session = GameSession.CreateColdProcess();
        session.StartNewGame();
        Assert.Equal(74, session.State.FieldId);
        session.SetWorldEntryMemory(session.WorldEntryMemory!.WriteBytes(256, 2, 7));
        session.RequestMovie(30);
        Assert.True(session.IsMoviePlaying);
        session.CompleteMovie();
        Assert.Equal(SceneKind.Field, session.State.Scene);
        Assert.Equal(74, session.State.FieldId);
        session.RequestField(180, 0);
        Assert.Equal("bghoke_2", session.State.FieldName);
        Assert.Equal(7, session.State.Variables[256]);
        session.RequestMovie(1);
        session.CompleteMovie();
        Assert.Equal(180, session.State.FieldId);
    }

    [Fact]
    public void Checkpoint_roundtrip_launch_arguments_and_invalid_state_are_verified()
    {
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            LaunchOptions options = LaunchOptions.Parse(["--field", "bghoke_2", "--entry", "0"]);
            var session = new GameSession(options.InitialState);
            session.SetPosition(123.5f, -24);
            session.SetEventMemory(session.State.EventMemory.Write(256, 4, NativeScalarValue.Exact(9)));
            session.SetDialogueDraft(new DialogueDraft(1, 3, 2, "Cec"));
            session.Save(path);
            GameState state = LaunchOptions.Parse(["--state", path]).InitialState;
            Assert.Equal(180, state.FieldId);
            Assert.Equal(0, state.EntryPoint);
            Assert.Throws<NotSupportedException>(() => LaunchOptions.Parse(["--field", "bghoke_2", "--entry", "3"]));
            Assert.Equal(123.5f, state.X);
            Assert.Equal(9, state.Variables[256]);
            Assert.Equal(new DialogueDraft(1, 3, 2, "Cec"), state.DialogueDraft);
            Assert.Throws<InvalidDataException>(() => new GameSession(state with { X = float.NaN }));
            Assert.Throws<ArgumentException>(() => LaunchOptions.Parse(["--field"]));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
