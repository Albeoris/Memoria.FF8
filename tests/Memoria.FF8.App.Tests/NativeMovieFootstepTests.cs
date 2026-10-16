using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeMovieFootstepTests
{
    [Fact]
    public void ActualOutsideMovieRetainsSuppressedDrawsThenProjectsContactsFromCamAndRestoresBothPhases()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original field and CAM corpus is required.");
        DirectoryInfo? directory = new(TestResources.FixtureDirectory);
        string source = Path.Combine(directory!.FullName, "../fixtures/external-fixture.json");
        Assert.SkipUnless(File.Exists(source), "The preserved authentic606 movie14 preboundary save is required.");
        byte[] original = File.ReadAllBytes(source);
        byte[] cam = File.ReadAllBytes(MovieClock.GetCameraPath(game, 1, 14));
        NativeMovieCameraFlags flags = NativeMovieCameraReader.Read(cam);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        Assert.Equal(606, world.Events.FieldId);
        Assert.Equal(4, world.Events.Frame);
        Assert.Equal(1, world.Events.Scene.NativeMovieMask!.Playback!.AppliedRecord);
        JsonElement initial = world.Events.Capture();
        List<NativeFootstepSound> sounds = [];
        world.Events.AudioCommandExecuted += command =>
        {
            if (command is NativeFootstepSound sound)
            {
                sounds.Add(sound);
            }
        };
        GameWorld? restored = null;
        List<NativeFootstepSound> restoredSounds = [];
        try
        {
            for (int record = 2; record <= 95; record++)
            {
                MoviePlaybackState playback = new(record, flags.Flags.Length - 1, true, false, false);
                Assert.True(world.AdvanceMovieFrame(playback, record / 15.0));
                JsonElement current = world.Events.Capture();
                if (record <= 50)
                {
                    Assert.True(JsonElement.DeepEquals(initial.GetProperty("Scene").GetProperty("ModelParts"), current.GetProperty("Scene").GetProperty("ModelParts")));
                    for (int actor = 0; actor < world.Events.Actors.Count; actor++)
                    {
                        Assert.True(JsonElement.DeepEquals(initial.GetProperty("Actors")[actor].GetProperty("NativeFootstepModelOrigin"), current.GetProperty("Actors")[actor].GetProperty("NativeFootstepModelOrigin")));
                    }
                }
                if (restored is not null)
                {
                    Assert.True(restored.AdvanceMovieFrame(playback, record / 15.0));
                    Assert.True(JsonElement.DeepEquals(current, restored.Events.Capture()));
                }
                if (record is 25 or 70)
                {
                    restored?.Dispose();
                    restored = new GameWorld(new GameSession(session.State with { EventState = current }), game);
                    restored.EnsureField();
                    Assert.True(JsonElement.DeepEquals(current, restored.Events.Capture()));
                    sounds.Clear();
                    restoredSounds.Clear();
                    restored.Events.AudioCommandExecuted += command =>
                    {
                        if (command is NativeFootstepSound sound)
                        {
                            restoredSounds.Add(sound);
                        }
                    };
                }
            }
            Assert.NotEmpty(sounds);
            Assert.Equal(sounds, restoredSounds);
            Assert.All(world.Events.Actors.Where(actor => actor.NativeFootstepModelOrigin is { Frame: > 4 }), actor => Assert.Null(actor.NativeFootstepModelOrigin!.UnsupportedReason));
            Assert.Equal(original, File.ReadAllBytes(source));
        }
        finally
        {
            restored?.Dispose();
        }
    }
}
