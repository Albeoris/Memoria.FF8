using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeHiddenFootstepTests
{
    [Fact]
    public void AuthenticHiddenNeverDrawnActorRecordsSoundIdentityAndRestoresWithoutInventingProjection()
    {
        string game = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(game), "Installed original resources are required.");
        DirectoryInfo? root = new(TestResources.FixtureDirectory);
        string source = Path.Combine(root!.FullName, "../fixtures/external-fixture.json");
        byte[] original = File.ReadAllBytes(source);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, game);
        world.EnsureField();
        Assert.Equal(350, world.Events.FieldId);
        Assert.Equal(137, world.Events.Frame);
        Assert.False(world.Events.Actors[0].Visible);
        Assert.Null(world.Events.Actors[0].NativeFootstepModelOrigin);
        List<NativeFootstepSound> sounds = [];
        world.Events.AudioCommandExecuted += command =>
        {
            if (command is NativeFootstepSound sound)
            {
                sounds.Add(sound);
            }
        };
        using GameWorld restored = new(new GameSession(session.State with { EventState = world.Events.Capture() }), game);
        restored.EnsureField();
        List<NativeFootstepSound> restoredSounds = [];
        restored.Events.AudioCommandExecuted += command =>
        {
            if (command is NativeFootstepSound sound)
            {
                restoredSounds.Add(sound);
            }
        };
        for (int frame = 0; frame < 60; frame++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(restored.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.Null(world.Events.LastFault);
            Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        }
        Assert.NotEmpty(sounds);
        Assert.Equal(sounds, restoredSounds);
        Assert.All(sounds, sound =>
        {
            Assert.Equal(0, sound.ObjectId);
            Assert.Equal(sound.Mode == 0 ? 3u : 2u, sound.EffectId);
            Assert.Equal(0x400000u, sound.Flags);
            Assert.Null(sound.Spatialization);
            Assert.NotNull(sound.UnsupportedProjectionReason);
            Assert.Throws<NotSupportedException>(() => FieldAudioPresentation.ResolveFootstepPlayback(sound));
        });
        Assert.Null(world.Events.Actors[0].NativeFootstepModelOrigin);
        Assert.Equal(sounds[^1].EffectId, session.GlobalSoundProcess!.OriginalEffectIds[23]);
        GameSession resumedSession = new(session.State with { EventState = world.Events.Capture() });
        using GameWorld resumed = new(resumedSession, game);
        resumed.EnsureField();
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.GlobalSoundProcess), JsonSerializer.SerializeToElement(resumedSession.GlobalSoundProcess)));
        Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), resumed.Events.Capture()));
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(session.GlobalSoundProcess), JsonSerializer.SerializeToElement(resumedSession.GlobalSoundProcess)));
        Assert.Equal(original, File.ReadAllBytes(source));
    }
}
