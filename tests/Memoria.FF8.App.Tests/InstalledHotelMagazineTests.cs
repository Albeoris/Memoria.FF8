using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledHotelMagazineTests
{
    [Fact]
    public void Hotel_magazine_continuation_renders_valid_fade_samples_and_reloads_without_repeating_the_reward()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.InstalledHotelMagazineTests.1");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        GameSession session = new(PlayerStateLoader.Load(source, directory));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(615, world.Events.FieldId);
        Assert.Equal(50, world.Events.Frame);
        Assert.Equal(0, session.EventMemory.Read(305, 1).RequireExact() & 1);
        uint gil = session.State.Economy!.Gil;

        void Apply(PlayerAction action, int count = 1)
        {
            for (int index = 0; index < count; index++)
            {
                world.Apply(action);
                world.EnsureField();
            }
        }

        // Approach the reception and accept the original room dialogue.
        Apply(new(PlayerActionKind.Move, Horizontal: -1, Vertical: -1), 10);
        Apply(new(PlayerActionKind.Move, Vertical: -1), 20);
        Apply(new(PlayerActionKind.Confirm));
        Apply(new(PlayerActionKind.Wait), 30);
        Assert.Equal(0, world.Events.CurrentDialogue?.MessageId);
        Apply(new(PlayerActionKind.Choose, 2));
        for (int index = 0; index < 300 && world.Events.FieldId != 616; index++)
        {
            Apply(world.Events.CurrentDialogue is { CanConfirm: true } dialogue ? new(dialogue.FirstChoice is null ? PlayerActionKind.Confirm : PlayerActionKind.Choose, dialogue.FirstChoice ?? 0) : new(PlayerActionKind.Wait));
        }
        Assert.Equal(616, world.Events.FieldId);
        Assert.Equal(gil - 100, session.State.Economy!.Gil);
        Apply(new(PlayerActionKind.Wait), 60);

        // Walk to the magazine and use the native interaction query.
        Apply(new(PlayerActionKind.Move, Horizontal: 1, Vertical: -1), 30);
        Apply(new(PlayerActionKind.Move, Vertical: -1), 40);
        Apply(new(PlayerActionKind.Move, Horizontal: 1), 5);
        Apply(new(PlayerActionKind.Move, Vertical: -1));
        Assert.Equal(11, world.Events.SelectInteractionTarget());
        world.SetRenderSize(320, 224);
        world.Render(TimeSpan.Zero);
        Apply(new(PlayerActionKind.Confirm));
        Assert.False(world.Events.IsUserControlEnabled);
        for (int index = 0; index < 40 && world.Events.CurrentDialogue?.MessageId != 39; index++)
        {
            world.CommitFieldPresentation();
            world.Render(TimeSpan.Zero, fieldInterpolation: 0.0185f);
            world.Events.Step();
            world.EnsureField();
        }
        Assert.Equal(39, world.Events.CurrentDialogue?.MessageId);
        Assert.Equal(new NativeFadeDraw(NativeFadeDrawMode.Subtract, 255, 255, 255), world.Events.FieldFade!.Draw);

        // Keep the saturated native draw across callbacks, including the fraction that
        // used to produce 255.00002 and fail the renderer's strict validation.
        for (int index = 0; index < 4; index++)
        {
            world.Events.Step();
            world.CommitFieldPresentation();
            foreach (float fraction in new[] { 0, 0.0185f, 0.5f, 1 })
            {
                world.Render(TimeSpan.Zero, fieldInterpolation: fraction);
            }
        }
        for (int index = 0; index < 600 && !world.Events.IsUserControlEnabled; index++)
        {
            if (world.Events.CurrentDialogue is { CanConfirm: true })
            {
                world.Apply(new(PlayerActionKind.Confirm));
            }
            world.Events.Step();
            world.EnsureField();
            world.CommitFieldPresentation();
            world.Render(TimeSpan.Zero, fieldInterpolation: 0.0185f);
        }
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.CurrentDialogue);
        Assert.Null(world.Events.LastFault);
        Assert.Equal(1, session.EventMemory.Read(305, 1).RequireExact() & 1);
        Assert.False(world.Events.Actors[11].Visible);
        Assert.Equal(NativeFadeDrawMode.Subtract, world.Events.FieldFade!.Draw.Mode);
        Assert.Equal((byte)0, world.Events.FieldFade.Draw.Red);

        string saved = TestResources.ArtifactFile("hotel-magazine.ff8save");
        session.SetEventState(world.Events.Capture());
        session.Save(saved, compact: true);
        GameSession restoredSession = new(GameSession.Load(saved));
        using GameWorld restored = new(restoredSession, directory);
        restored.EnsureField();
        restored.SetRenderSize(320, 224);
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(world.Apply(new(PlayerActionKind.Wait)), restored.Apply(new(PlayerActionKind.Wait)));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(1, restoredSession.EventMemory.Read(305, 1).RequireExact() & 1);
        Assert.False(restored.Events.Actors[11].Visible);
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_HOTEL_SAVE_OUTPUT");
        if (output is not null)
        {
            File.Copy(saved, output, overwrite: false);
        }
    }
}
