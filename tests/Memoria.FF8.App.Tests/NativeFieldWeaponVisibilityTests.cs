using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeFieldWeaponVisibilityTests
{
    [Fact]
    public void OriginalJumpShowsWeaponAndRetainsIndependentActorIntentThroughGameplaySave()
    {
        string? checkpoint = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_WEAPON_SNAPSHOT");
        Assert.SkipUnless(File.Exists(checkpoint), "Set MEMORIA_FF8_FIELD_WEAPON_SNAPSHOT to the standalone pre-choice field fixture.");
        string directory = TestResources.GameDirectory;
        using GameWorld world = new(new GameSession(PlayerStateLoader.Load(checkpoint!, directory)), directory, validateWorldPresentation: false);
        Assert.True(world.EnsureField());
        Assert.True(world.Apply(new(PlayerActionKind.Choose, Value: 0)).Accepted);
        PlayerActionResult? result = null;
        for (int callback = 0; callback < 120; callback++)
        {
            result = world.Apply(new(PlayerActionKind.Wait));
            Assert.True(result.Accepted);
            if (callback == 20)
            {
                var actor = world.Events.Actors[0];
                Assert.True(actor.AdditionalControlDisabled);
                Assert.False(actor.ResolvedModelAdditionalControlDisabled);
                ActorVisual visual = world.CreateVisual(actor);
                Assert.False(visual.AdditionalControlDisabled);
                FieldRenderer renderer = new(world.Field!, 640, 448);
                byte[] armed = renderer.Render(new Dictionary<int, int>(), [visual], TimeSpan.Zero).ToArray();
                byte[] hidden = renderer.Render(new Dictionary<int, int>(), [visual with { AdditionalControlDisabled = true }], TimeSpan.Zero).ToArray();
                Assert.False(armed.SequenceEqual(hidden));
            }
            if (result.Trigger.Kind == PlayerTriggerKind.Dialogue)
            {
                break;
            }
        }
        Assert.Equal(PlayerTriggerKind.Dialogue, result!.Trigger.Kind);
        Assert.True(world.Events.Actors[0].AdditionalControlDisabled);
        Assert.False(world.Events.Actors[0].ResolvedModelAdditionalControlDisabled);
        string saved = Path.Combine(Path.GetTempPath(), "Memoria-field-weapon-" + Guid.NewGuid().ToString("N") + ".ff8");
        try
        {
            world.WriteGameplaySave(saved);
            using GameWorld restored = new(new GameSession(PlayerStateLoader.Load(saved, directory)), directory, validateWorldPresentation: false);
            Assert.True(restored.EnsureField());
            Assert.Equal(world.Events.Frame, restored.Events.Frame);
            Assert.True(restored.Events.Actors[0].AdditionalControlDisabled);
            Assert.False(restored.Events.Actors[0].ResolvedModelAdditionalControlDisabled);
            world.SetRenderSize(640, 448);
            restored.SetRenderSize(640, 448);
            Assert.True(world.Render(TimeSpan.Zero).SequenceEqual(restored.Render(TimeSpan.Zero)));
        }
        finally
        {
            File.Delete(saved);
        }
    }
}
