using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class InstalledFieldTransitionInputTests
{
    [Fact]
    public void Held_movement_during_a_black_field_entry_advances_the_script_without_requiring_an_unused_motor()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.InstalledFieldTransitionInputTests.1");
        byte[] sourceHash = SHA256.HashData(File.ReadAllBytes(source));
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory);
        world.EnsureField();
        Assert.Equal(770, world.Events.FieldId);
        Assert.Equal(121, world.Events.Frame);

        void Apply(PlayerAction action, int count = 1)
        {
            for (int index = 0; index < count; index++)
            {
                world.Apply(action);
                world.EnsureField();
            }
        }

        void AdvanceDialogue(int limit)
        {
            for (int index = 0; index < limit && world.Events.FieldId == 770; index++)
            {
                PlayerAction action = world.Events.CurrentDialogue is { CanConfirm: true } dialog ? new(dialog.FirstChoice is null ? PlayerActionKind.Confirm : PlayerActionKind.Choose, Value: dialog.FirstChoice ?? 0) : new(PlayerActionKind.Wait);
                Apply(action);
            }
        }

        // Reach the timer console, select ten minutes and leave through the side door.
        Apply(new(PlayerActionKind.Move, Horizontal: 1), 40);
        Apply(new(PlayerActionKind.Confirm));
        AdvanceDialogue(600);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Null(world.Events.CurrentDialogue);
        Assert.True(session.ExamTimer!.TimerMemoryActivated);
        Apply(new(PlayerActionKind.Move, Horizontal: -1), 40);
        Apply(new(PlayerActionKind.Move, Vertical: 1), 6);
        Apply(new(PlayerActionKind.Move, Horizontal: -1), 25);
        Apply(new(PlayerActionKind.Confirm));
        AdvanceDialogue(200);
        Apply(new(PlayerActionKind.Move, Horizontal: -1), 5);
        AdvanceDialogue(200);
        Assert.Equal(769, world.Events.FieldId);
        Assert.Equal(0, world.Events.Frame);
        Apply(new(PlayerActionKind.Wait));
        Assert.Equal(1, world.Events.Frame);
        Assert.True(world.Events.IsUserControlEnabled);
        Assert.Equal(-1, world.Events.PlayerActor!.ModelId);
        Assert.Equal(4, world.Events.FieldFade!.Physical.Mode);
        Assert.False(NativePlayerMovementAdapter.Supports(world.Field!, world.Events.Scene, world.Events.PlayerActor));
        FieldFixedPosition? before = world.Events.Scene.NativePlayerPosition;
        int motorCalls = 0;
        var resolve = world.Events.ResolveNativePlayerMovement!;
        world.Events.ResolveNativePlayerMovement = query =>
        {
            motorCalls++;
            return resolve(query);
        };
        Assert.True(world.Apply(new(PlayerActionKind.Move, Horizontal: -1)).Accepted);
        Assert.Equal(2, world.Events.Frame);
        Assert.False(world.Events.IsUserControlEnabled);
        Assert.Equal(0, motorCalls);
        Assert.Equal(before, world.Events.Scene.NativePlayerPosition);
        Assert.Null(world.Events.LastFault);

        string saved = Path.Combine(TestResources.ArtifactDirectory, "field-entry.json");
        session.SetEventState(world.Events.Capture());
        session.Save(saved, compact: true);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_ENTRY_SAVE_OUTPUT");
        if (output is not null)
        {
            File.Copy(saved, output, overwrite: false);
        }
        using GameWorld restored = new(new GameSession(GameSession.Load(saved)), directory);
        restored.EnsureField();
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        PlayerAction next = new(PlayerActionKind.Wait);
        Assert.Equal(world.Apply(next), restored.Apply(next));
        Assert.True(JsonElement.DeepEquals(world.Events.Capture(), restored.Events.Capture()));
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.Equal(sourceHash, SHA256.HashData(File.ReadAllBytes(source)));
    }
}
