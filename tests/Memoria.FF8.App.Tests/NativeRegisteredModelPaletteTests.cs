using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeRegisteredModelPaletteTests
{
    [Fact]
    public void Fresh_registered_model_without_eye_animation_uses_uploaded_palette_and_matches_restored_next_callback()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original field resources are required.");
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        using GameWorld world = new(session, directory);
        ReachPlayableCorridor(session, world);
        Assert.Equal(229, world.Events.FieldId);
        Assert.Equal(PlayerTriggerKind.Playable, world.Events.NextTrigger.Kind);
        Assert.False(session.IsMoviePlaying);
        Assert.True(world.CanPresentCurrentField);
        NativeFieldRegistrationState registration = Assert.IsType<NativeFieldRegistrationState>(world.Events.Scene.NativeFieldRegistration);
        var actor = world.Events.Actors.First(actor => actor.Active && actor.Visible && actor.NativeEyes is { Mode: 0 } && registration.Instances.Any(instance => instance.ObjectId == actor.ObjectId && instance.ModelId >= 0));
        var visual = world.CaptureFieldActor(actor).Visual;
        Assert.True(visual.Visible);
        IReadOnlyList<PixelImage> selected = Assert.IsAssignableFrom<IReadOnlyList<PixelImage>>(visual.RegisteredTextures);
        NativeFieldRegisteredResource resource = registration.Resources.Single(resource => resource.ModelId == visual.ModelId);
        NativeBattleEffectTextureState textures = session.CurrentNativeBattleMemory!.EffectMemory!.Textures!;
        AssertPalette(selected, resource, textures);
        Assert.Same(selected[0], world.CaptureFieldActor(actor).Visual.RegisteredTextures![0]);
        Assert.Contains(Enumerable.Range(0, selected.Count), ordinal => !selected[ordinal].Pixels.AsSpan().SequenceEqual(world.Field!.Models[visual.ModelId].Textures[ordinal].Pixels));
        NativeCrtRandomState? random = session.State.CrtRandom;
        long frame = world.Events.Frame;
        byte[] pixels = world.Render(TimeSpan.Zero).ToArray();
        Assert.Equal(random, session.State.CrtRandom);
        Assert.Equal(frame, world.Events.Frame);
        GameState checkpoint = JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.PrepareSnapshot()))!;
        GameSession resumed = new(checkpoint);
        using GameWorld restored = new(resumed, directory);
        restored.EnsureField();
        Assert.Equal(world.Events.Capture().GetRawText(), restored.Events.Capture().GetRawText());
        Assert.Equal(pixels, restored.Render(TimeSpan.Zero));
        var restoredActor = restored.Events.Actors.Single(item => item.ObjectId == actor.ObjectId);
        AssertPalette(restored.CaptureFieldActor(restoredActor).Visual.RegisteredTextures!, restored.Events.Scene.NativeFieldRegistration!.Resources.Single(item => item.ModelId == visual.ModelId), resumed.CurrentNativeBattleMemory!.EffectMemory!.Textures!);
        world.Events.Step();
        restored.Events.Step();
        Assert.Equal(world.Events.Capture().GetRawText(), restored.Events.Capture().GetRawText());
        Assert.Equal(session.State.CrtRandom, resumed.State.CrtRandom);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
    }

    private static void ReachPlayableCorridor(GameSession session, GameWorld world)
    {
        // The same genuine opening and corridor entry used by the stationary
        // Quistis regression, stopping before movement can start the corridor movie.
        for (int input = 0; input < 6000; input++)
        {
            world.EnsureField();
            if (world.Events.FieldId == 229 && world.Events.NextTrigger.Kind == PlayerTriggerKind.Playable)
            {
                return;
            }
            DialogueState? dialogue = world.Events.CurrentDialogue;
            PlayerAction next = session.IsMoviePlaying ? new(PlayerActionKind.SkipMovie) : dialogue?.IsNamePrompt == true ? new(PlayerActionKind.EnterName, Text: "Squall") : dialogue is { CanConfirm: true, FirstChoice: int choice } ? new(PlayerActionKind.Choose, choice) : dialogue is { CanConfirm: true } ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait);
            PlayerActionResult result = world.Apply(next);
            Assert.True(result.Accepted);
            if (next.Kind == PlayerActionKind.Wait && result.FramesAdvanced == 0 && world.Events.CurrentDialogue is { CanConfirm: false, IsNamePrompt: false })
            {
                long frame = world.Events.Frame;
                world.AdvanceAutoPlayInputFrame();
                Assert.Equal(frame + 1, world.Events.Frame);
            }
            Assert.Null(world.Events.LastFault);
        }
        Assert.Fail("The genuine opening route did not reach the playable corridor.");
    }

    private static void AssertPalette(IReadOnlyList<PixelImage> selected, NativeFieldRegisteredResource resource, NativeBattleEffectTextureState textures)
    {
        Assert.Equal(resource.Textures.Length, selected.Count);
        for (int ordinal = 0; ordinal < selected.Count; ordinal++)
        {
            NativeFieldRegisteredTexture texture = resource.Textures[ordinal];
            PixelImage image = selected[ordinal];
            int shift = 2 - texture.Mode;
            Assert.Equal(texture.WidthWords << shift, image.Width);
            Assert.Equal(texture.Height, image.Height);
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    ushort packed = textures.ReadVramWord(texture.X + (x >> shift), texture.Y + y);
                    ushort color = texture.Mode switch
                    {
                        0 => textures.ReadVramWord(texture.PaletteX + ((packed >> ((x & 3) * 4)) & 15), texture.PaletteY),
                        1 => textures.ReadVramWord(texture.PaletteX + ((packed >> ((x & 1) * 8)) & 255), texture.PaletteY),
                        _ => packed
                    };
                    Assert.Equal(Rgba32.FromPsxBgr555(color), image[x, y]);
                }
            }
        }
    }
}
