using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeLadderPresentationTests
{
    [Fact]
    public void Party_ladder_entry_descent_reversal_and_exit_keep_short_animation_arcs()
    {
        string directory = TestResources.GameDirectory;
        string source = TestResources.FixturePath("App.Tests.NativeLadderPresentationTests.1");
        using var world = new GameWorld(new GameSession(GameSession.Load(source)), directory);
        world.EnsureField();
        var presentation = new FieldPresentation();
        object owner = new();
        FieldPresentationFrame Capture() => new(owner, world.Events.Frame, 0, world.Events.Actors.Select(world.CaptureFieldActor).ToArray(), new BackgroundScrollFrame(new Dictionary<int, Vector2>(), Vector2.Zero));
        FieldPresentationFrame before = Capture();
        presentation.Sample(before, 1, true);
        HashSet<int> reverseActors = [];
        HashSet<int> forwardActors = [];
        void Apply(PlayerAction action)
        {
            Assert.True(world.Apply(action).Accepted);
            if (!world.CanPresentCurrentField)
            {
                presentation.Reset();
                return;
            }
            FieldPresentationFrame after = Capture();
            presentation.Commit(owner, after.Frame, () => after);
            FieldPresentationSample sample = presentation.Sample(after, 0.5f, true);
            foreach (FieldActorSample current in after.Actors.Where(actor => actor.Visual.Visible && actor.Visual.ModelId >= 0))
            {
                FieldActorSample previous = before.Actors.Single(actor => actor.Visual.ObjectId == current.Visual.ObjectId);
                if (current.AnimationRevision != previous.AnimationRevision || current.Visual.AnimationId != previous.Visual.AnimationId || current.AnimationStep == 0 || world.Events.Actors[current.Visual.ObjectId].PresentationAnimationStep is null)
                {
                    continue;
                }
                FieldAnimationBlend blend = sample.Actors.Single(actor => actor.ObjectId == current.Visual.ObjectId).AnimationBlend!.Value;
                float delta = blend.CurrentFrame - blend.PreviousFrame;
                Assert.InRange(delta, -2, 2);
                if (delta < 0)
                {
                    reverseActors.Add(current.Visual.ObjectId);
                    Assert.True(current.AnimationStep < 0);
                }
                if (delta > 0)
                {
                    forwardActors.Add(current.Visual.ObjectId);
                    Assert.True(current.AnimationStep > 0);
                }
            }
            before = after;
        }
        void Wait(int count)
        {
            for (int tick = 0; tick < count; tick++)
            {
                Apply(new(PlayerActionKind.Wait));
            }
        }
        for (int tick = 0; tick < 45; tick++)
        {
            Apply(new(PlayerActionKind.Move, Vertical: -1));
        }
        Apply(new(PlayerActionKind.Confirm));
        Wait(85);
        ActorState player = world.Events.PlayerActor!;
        Assert.True(player.NativeLadder is { Active: true, Up: false });
        Apply(new(PlayerActionKind.FieldButtons, 0x4000));
        Wait(70);
        Apply(new(PlayerActionKind.FieldButtons, 0x1000));
        Wait(35);
        Apply(new(PlayerActionKind.FieldButtons, 0));
        Wait(5);
        Apply(new(PlayerActionKind.FieldButtons, 0x4000));
        for (int tick = 0; tick < 150 && player.NativeLadder is { Active: true }; tick++)
        {
            Apply(new(PlayerActionKind.Wait));
        }
        Apply(new(PlayerActionKind.FieldButtons, 0));
        Assert.Null(player.NativeLadder);
        Assert.Equal(3, reverseActors.Count);
        Assert.Equal(3, forwardActors.Count);
        Wait(2);
        Assert.All(world.Events.Actors, actor => Assert.Null(actor.PresentationAnimationStep));
        Assert.Null(world.Events.LastFault);
    }
}
