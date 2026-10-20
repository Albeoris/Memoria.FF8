using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

[Collection("GPU rendering")]
public sealed class FieldPresentationTests
{
    [Fact]
    public void Intermediate_actor_and_camera_samples_preserve_the_committed_state()
    {
        object scene = new();
        object actor = new();
        FieldActorSample first = Actor(actor, 10) with
        {
            Visual = Actor(actor, 10).Visual with
            {
                Y = 20,
                Z = 30,
                RootTranslationZ = 40,
                GroundShadowCentre = new Vector3(10, 20, 30),
                FirstControlRotation = new Vector3(0, 0, MathF.Tau - 0.2f),
                SkinTranslations = new Dictionary<int, Vector3> { [7] = new(10, 20, 30) }
            }
        };
        FieldActorSample last = first with
        {
            Visual = first.Visual with
            {
                X = 30,
                Y = 40,
                Z = 50,
                RootTranslationZ = 60,
                GroundShadowCentre = new Vector3(30, 40, 50),
                FirstControlRotation = new Vector3(0, 0, 0.2f),
                SkinTranslations = new Dictionary<int, Vector3> { [7] = new(30, 40, 50) }
            }
        };
        FieldPresentationFrame before = Frame(scene, 7, first, new(10, 20));
        FieldPresentationFrame after = Frame(scene, 8, last, new(30, 40));
        string original = JsonSerializer.Serialize(new[] { before, after });
        FieldPresentation presentation = Prime(before, after);
        FieldPresentationSample sample = presentation.Sample(after, 0.5f, true);
        ActorVisual visual = Assert.Single(sample.Actors);
        Assert.Equal(new Vector3(20, 30, 40), new Vector3(visual.X, visual.Y, visual.Z));
        Assert.Equal(50, visual.RootTranslationZ);
        Assert.Equal(new Vector3(20, 30, 40), visual.GroundShadowCentre);
        Assert.Equal(MathF.Tau, visual.FirstControlRotation!.Value.Z, 5);
        Assert.Equal(new Vector3(20, 30, 40), visual.SkinTranslations![7]);
        Assert.Equal(0.5f, sample.Background!.Fraction);
        Assert.Equal(before.Scroll.ActorOffset, sample.Background.Previous.ActorOffset);
        Assert.Equal(after.Scroll.ActorOffset, sample.Background.Current.ActorOffset);
        Assert.Equal(new Vector2(20, 30), Vector2.Lerp(sample.Background.Previous.LayerOffsets[0], sample.Background.Current.LayerOffsets[0], sample.Background.Fraction));
        Assert.Equal(original, JsonSerializer.Serialize(new[] { before, after }));
        Assert.Equal(first.Visual.X, Assert.Single(presentation.Sample(after, 0, false).Actors).X);
        Assert.Equal(last.Visual.X, Assert.Single(presentation.Sample(after, 1, false).Actors).X);
    }

    [Theory]
    [InlineData(250, 6)]
    [InlineData(6, 250)]
    public void Actor_heading_uses_the_shortest_direction_across_wrap(float first, float last)
    {
        object scene = new();
        object owner = new();
        FieldActorSample beforeActor = Actor(owner, 10) with { Visual = Actor(owner, 10).Visual with { Direction = first } };
        FieldPresentationFrame before = Frame(scene, 7, beforeActor);
        FieldPresentationFrame after = Frame(scene, 8, beforeActor with { Visual = beforeActor.Visual with { Direction = last } });
        ActorVisual middle = Assert.Single(Prime(before, after).Sample(after, 0.5f, false).Actors);
        Assert.Equal(0, middle.Direction % 256, 5);
    }

    [Theory]
    [InlineData(1.25f, 1.75f)]
    [InlineData(1.5f, 1.5f)]
    public void Native_fractional_animation_phase_is_retained_even_when_the_callback_holds_it(float first, float last)
    {
        object scene = new();
        object owner = new();
        FieldActorSample actor = Actor(owner, 10, first);
        FieldPresentationFrame before = Frame(scene, 7, actor);
        FieldPresentationFrame after = Frame(scene, 8, actor with { Visual = actor.Visual with { AnimationFrame = last } });
        FieldPresentation presentation = Prime(before, after);
        FieldAnimationBlend blend = Assert.Single(presentation.Sample(after, 0.5f, true).Actors).AnimationBlend!.Value;
        Assert.Equal(new FieldAnimationBlend(first, last, 0.5f, 0, 4, true), blend);
        Assert.Null(Assert.Single(presentation.Sample(after, 0.5f, false).Actors).AnimationBlend);
        Assert.Equal(last, Assert.Single(presentation.Sample(after, 0.5f, false).Actors).AnimationFrame);
    }

    [Theory]
    [InlineData(3.5f, 0, 1, 4)]
    [InlineData(0, 3.5f, -1, -0.5f)]
    public void Loop_boundaries_unroll_in_the_native_playback_direction(float first, float last, int step, float expectedCurrent)
    {
        object scene = new();
        object owner = new();
        FieldActorSample actor = Actor(owner, 10, first) with { AnimationStep = step };
        FieldPresentationFrame before = Frame(scene, 7, actor);
        FieldPresentationFrame after = Frame(scene, 8, actor with { Visual = actor.Visual with { AnimationFrame = last } });
        FieldAnimationBlend blend = Assert.Single(Prime(before, after).Sample(after, 0.5f, true).Actors).AnimationBlend!.Value;
        Assert.Equal(first, blend.PreviousFrame);
        Assert.Equal(expectedCurrent, blend.CurrentFrame);
        Assert.Equal(0.5f, blend.Fraction);
        Assert.True(blend.Loop);
    }

    [Theory]
    [InlineData("scene")]
    [InlineData("camera")]
    [InlineData("instant camera")]
    [InlineData("camera actor")]
    [InlineData("placement")]
    [InlineData("frame gap")]
    public void Discontinuous_scene_camera_placement_or_callbacks_snap_all_presentation(string change)
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), 10);
        FieldPresentationFrame before = Frame(scene, 7, actor, new(10, 20));
        FieldPresentationFrame after = Frame(scene, 8, actor with { Visual = actor.Visual with { X = 100 } }, new(30, 40));
        after = change switch
        {
            "scene" => after with { Owner = new object() },
            "camera" => after with { CameraIndex = 1 },
            "instant camera" => after with { CameraRevision = 1 },
            "camera actor" => after with { CameraActorId = 2 },
            "placement" => after with { Actors = [after.Actors[0] with { PositionRevision = 1 }] },
            _ => after with { Frame = 10 }
        };
        FieldPresentationSample sample = Prime(before, after).Sample(after, 0, true);
        Assert.Equal(100, Assert.Single(sample.Actors).X);
        Assert.Null(sample.Background);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("model")]
    [InlineData("visible")]
    public void Replaced_models_actors_and_visibility_changes_do_not_interpolate_old_actors(string change)
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), 10);
        FieldPresentationFrame before = Frame(scene, 7, actor);
        FieldActorSample afterActor = actor with { Visual = actor.Visual with { X = 100 } };
        afterActor = change switch
        {
            "owner" => afterActor with { Owner = new object() },
            "model" => afterActor with { Visual = afterActor.Visual with { ModelId = 1 } },
            _ => afterActor with { Visual = afterActor.Visual with { Visible = false } }
        };
        FieldPresentationFrame after = Frame(scene, 8, afterActor);
        Assert.Equal(afterActor.Visual, Assert.Single(Prime(before, after).Sample(after, 0.5f, false).Actors));
    }

    [Fact]
    public void Spawned_actors_snap_while_existing_actors_keep_their_history()
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), 10);
        FieldActorSample added = Actor(new object(), 200) with { Visual = Actor(new object(), 200).Visual with { ObjectId = 2 } };
        FieldPresentationFrame before = Frame(scene, 7, actor);
        FieldPresentationFrame after = Frame(scene, 8, actor) with { Actors = [actor with { Visual = actor.Visual with { X = 30 } }, added] };
        FieldPresentationSample sample = Prime(before, after).Sample(after, 0.5f, false);
        Assert.Equal(20, sample.Actors.Single(actor => actor.ObjectId == 1).X);
        Assert.Equal(added.Visual, sample.Actors.Single(actor => actor.ObjectId == 2));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Unrelated_actor_placement_changes_preserve_the_camera_and_other_actor_history(bool visible)
    {
        object scene = new();
        FieldActorSample followed = Actor(new object(), 10);
        FieldActorSample other = Actor(new object(), 100);
        other = other with { Visual = other.Visual with { ObjectId = 2, Visible = visible } };
        FieldPresentationFrame before = Frame(scene, 7, followed, new(10, 20)) with { Actors = [followed, other] };
        FieldPresentationFrame after = Frame(scene, 8, followed, new(30, 40)) with
        {
            Actors = [followed with { Visual = followed.Visual with { X = 30 } }, other with { PositionRevision = 1, Visual = other.Visual with { X = 5000 } }]
        };
        FieldPresentationSample sample = Prime(before, after).Sample(after, 0.5f, false);
        Assert.Equal(20, sample.Actors.Single(actor => actor.ObjectId == 1).X);
        Assert.Equal(5000, sample.Actors.Single(actor => actor.ObjectId == 2).X);
        Assert.NotNull(sample.Background);
        Assert.Equal(before.Scroll.ActorOffset, sample.Background.Previous.ActorOffset);
        Assert.Equal(after.Scroll.ActorOffset, sample.Background.Current.ActorOffset);
    }

    [Theory]
    [InlineData("restart")]
    [InlineData("clip")]
    [InlineData("control")]
    [InlineData("range")]
    [InlineData("loop")]
    public void Animation_discontinuities_snap_pose_overrides_without_discarding_continuous_movement(string change)
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), 10, 2) with
        {
            Visual = Actor(new object(), 10, 2).Visual with
            {
                FirstControlRotation = Vector3.Zero,
                SkinTranslations = new Dictionary<int, Vector3> { [7] = Vector3.Zero }
            }
        };
        FieldActorSample afterActor = actor with
        {
            Visual = actor.Visual with
            {
                X = 30,
                AnimationFrame = 0,
                FirstControlRotation = Vector3.One,
                SkinTranslations = new Dictionary<int, Vector3> { [7] = Vector3.One }
            }
        };
        afterActor = change switch
        {
            "restart" => afterActor with { AnimationRevision = 1 },
            "clip" => afterActor with { Visual = afterActor.Visual with { AnimationId = 1 } },
            "control" => afterActor with { Visual = afterActor.Visual with { AdditionalControlDisabled = true } },
            "range" => afterActor with { TerminalFrame = 3 },
            _ => afterActor with { Loop = false }
        };
        FieldPresentationFrame before = Frame(scene, 7, actor);
        FieldPresentationFrame after = Frame(scene, 8, afterActor);
        ActorVisual visual = Assert.Single(Prime(before, after).Sample(after, 0.5f, true).Actors);
        Assert.Equal(20, visual.X);
        Assert.Equal(Vector3.One, visual.FirstControlRotation);
        Assert.Equal(Vector3.One, visual.SkinTranslations![7]);
        Assert.Equal(0, visual.AnimationBlend!.Value.PreviousFrame);
        Assert.Equal(0, visual.AnimationBlend.Value.CurrentFrame);
        Assert.Equal(1, visual.AnimationBlend.Value.Fraction);
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("frame")]
    [InlineData("visual")]
    [InlineData("range")]
    [InlineData("loop")]
    [InlineData("animation step")]
    public void External_snapshot_replacement_cannot_reuse_stale_history(string change)
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), 10);
        FieldPresentationFrame before = Frame(scene, 7, actor);
        FieldPresentationFrame after = Frame(scene, 8, actor with { Visual = actor.Visual with { X = 30 } });
        FieldPresentation presentation = Prime(before, after);
        FieldPresentationFrame replaced = change switch
        {
            "owner" => after with { Owner = new object() },
            "frame" => after with { Frame = 9 },
            "range" => after with { Actors = [after.Actors[0] with { TerminalFrame = 3 }] },
            "loop" => after with { Actors = [after.Actors[0] with { Loop = false }] },
            "animation step" => after with { Actors = [after.Actors[0] with { AnimationStep = -1 }] },
            _ => after with { Actors = [after.Actors[0] with { Visual = after.Actors[0].Visual with { X = 100 } }] }
        };
        FieldPresentationSample sample = presentation.Sample(replaced, 0, false);
        Assert.Equal(replaced.Actors[0].Visual, Assert.Single(sample.Actors));
        Assert.Null(sample.Background);
    }

    [Fact]
    public void Multiple_accelerated_callbacks_retain_only_the_last_consecutive_pair_and_reset_discards_it()
    {
        object scene = new();
        FieldActorSample actor = Actor(new object(), 10);
        FieldPresentationFrame first = Frame(scene, 7, actor);
        FieldPresentation presentation = new();
        presentation.Sample(first, 1, true);
        FieldPresentationFrame last = first;
        for (int callback = 1; callback <= 3; callback++)
        {
            last = Frame(scene, 7 + callback, actor with { Visual = actor.Visual with { X = 10 + callback * 10 } });
            FieldPresentationFrame committed = last;
            presentation.Commit(scene, committed.Frame, () => committed);
        }
        Assert.Equal(35, Assert.Single(presentation.Sample(last, 0.5f, false).Actors).X);
        presentation.Reset();
        FieldPresentationSample reset = presentation.Sample(last, 0, false);
        Assert.Equal(40, Assert.Single(reset.Actors).X);
        Assert.Null(reset.Background);
    }

    [Fact]
    public void Preparation_failures_belong_to_drawing_and_reset_clears_them()
    {
        object scene = new();
        FieldPresentationFrame frame = Frame(scene, 7, Actor(new object(), 10));
        FieldPresentation presentation = new();
        presentation.Commit(scene, 7, () => throw new InvalidOperationException("Inactive drawing does not prepare samples."));
        presentation.Sample(frame, 1, false);
        presentation.Commit(scene, 8, () => throw new InvalidOperationException("Unsupported camera contract."));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => presentation.Sample(frame, 1, true));
        Assert.Equal("Unsupported camera contract.", exception.Message);
        presentation.Reset();
        Assert.Single(presentation.Sample(frame, 1, false).Actors);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Invalid_presentation_fraction_fails(float fraction)
    {
        FieldPresentationFrame frame = Frame(new object(), 7, Actor(new object(), 10));
        Assert.Throws<ArgumentOutOfRangeException>(() => new FieldPresentation().Sample(frame, fraction, true));
    }

    [Fact]
    public void Installed_intermediate_images_change_without_changing_events_or_the_source_save()
    {
        string source = TestResources.FixturePath("App.Tests.FieldPresentation.1");
        byte[] original = SHA256.HashData(File.ReadAllBytes(source));
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        world.SetRenderSize(640, 480);
        world.Render(TimeSpan.Zero);
        for (int callback = 0; callback < 2; callback++)
        {
            Assert.Equal(1, world.Apply(new(PlayerActionKind.Move, Horizontal: 1)).FramesAdvanced);
            world.CommitFieldPresentation();
        }
        string state = Serialize(session, world);
        byte[] first = world.Render(TimeSpan.Zero, fieldInterpolation: 0, smoothFieldAnimation: true).ToArray();
        byte[] middle = world.Render(TimeSpan.Zero, fieldInterpolation: 0.5f, smoothFieldAnimation: true).ToArray();
        byte[] last = world.Render(TimeSpan.Zero, fieldInterpolation: 1, smoothFieldAnimation: true).ToArray();
        Assert.NotEqual(first, middle);
        Assert.NotEqual(middle, last);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TEST_OUTPUT_DIRECTORY");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
        }
        using GpuTestDevice device = new();
        using GpuTriangleRenderer gpu = new(device.Device);
        Microsoft.Xna.Framework.Color[] RenderHardware(float fraction, string name)
        {
            world.Render(TimeSpan.Zero, triangleRenderer: gpu, fieldInterpolation: fraction, smoothFieldAnimation: true);
            Microsoft.Xna.Framework.Color[] pixels = GpuTestDevice.Read(gpu.Target);
            if (output is not null)
            {
                using FileStream image = new(Path.Combine(output, name + ".png"), FileMode.Create);
                gpu.Target.SaveAsPng(image, world.RenderWidth, world.RenderHeight);
            }
            return pixels;
        }
        Microsoft.Xna.Framework.Color[] hardwareFirst = RenderHardware(0, "field-before");
        Microsoft.Xna.Framework.Color[] hardwareMiddle = RenderHardware(0.5f, "field-middle");
        Microsoft.Xna.Framework.Color[] hardwareLast = RenderHardware(1, "field-current");
        Assert.NotEqual(hardwareFirst, hardwareMiddle);
        Assert.NotEqual(hardwareMiddle, hardwareLast);
        Assert.Equal(state, Serialize(session, world));
        Assert.Equal(original, SHA256.HashData(File.ReadAllBytes(source)));
        if (output is not null)
        {
            long snapshotFrame = world.Events.Frame;
            session.SetEventState(world.Events.Capture());
            string checkpoint = Path.Combine(output, "field-presentation-checkpoint.json");
            session.Save(checkpoint);
            GameSession restoredSession = new(GameSession.Load(checkpoint));
            using GameWorld restored = new(restoredSession, TestResources.GameDirectory);
            restored.EnsureField();
            restored.SetRenderSize(world.RenderWidth, world.RenderHeight);
            Assert.Equal(Serialize(session, world), Serialize(restoredSession, restored));
            Assert.Equal(last, restored.Render(TimeSpan.Zero, smoothFieldAnimation: true));
            world.Events.Step();
            restored.Events.Step();
            world.CommitFieldPresentation();
            restored.CommitFieldPresentation();
            Assert.Equal(snapshotFrame + 1, world.Events.Frame);
            Assert.Equal(Serialize(session, world), Serialize(restoredSession, restored));
            Assert.Equal(world.Render(TimeSpan.Zero, smoothFieldAnimation: true), restored.Render(TimeSpan.Zero, smoothFieldAnimation: true));
            File.WriteAllText(Path.Combine(output, "field-presentation.json"), JsonSerializer.Serialize(new { FieldId = world.Field!.Id, Frame = snapshotFrame, ContinuedFrame = world.Events.Frame, SourcePreserved = true, DrawPreservedState = true, CpuIntermediateChanged = true, GpuIntermediateChanged = true, ReloadedStateAndPixelsEqual = true, NextIdleCallbackEqual = true }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }

    [Fact]
    public void Installed_held_fractional_endpoint_keeps_the_next_source_pose_available()
    {
        GameSession session = new(GameSession.Load(TestResources.FixturePath("App.Tests.FieldPresentation.1")));
        using GameWorld world = new(session, TestResources.GameDirectory);
        world.EnsureField();
        ActorState original = world.Events.Actors.First(actor => actor.Active && actor.Visible && actor.RenderModelId >= 0 && world.Field!.GetAnimationFrameCount(actor.RenderModelId, actor.AnimationId) >= 3);
        JsonObject changed = JsonNode.Parse(JsonSerializer.Serialize(original))!.AsObject();
        changed["AnimationFirst"] = 0;
        changed["AnimationLast"] = 0;
        changed["AnimationFrame"] = 1.5f;
        changed["AnimationLoop"] = false;
        ActorState held = changed.Deserialize<ActorState>()!;
        string state = Serialize(session, world);
        FieldActorSample sample = world.CaptureFieldActor(held);
        Assert.Equal(0, sample.FirstFrame);
        Assert.Equal(3, sample.TerminalFrame);
        Assert.Equal(1.5f, sample.Visual.AnimationFrame);
        Assert.False(sample.Loop);
        FieldPresentationFrame frame = Frame(new object(), 0, sample);
        FieldAnimationBlend animation = Assert.Single(new FieldPresentation().Sample(frame, 0.5f, true).Actors).AnimationBlend!.Value;
        Assert.Equal(3, animation.TerminalFrame);
        Assert.Equal(1.5f, animation.CurrentFrame);
        Assert.Equal(state, Serialize(session, world));
    }

    [Fact]
    public void Installed_mod_and_debug_animation_samples_use_the_actual_selected_clip_bounds()
    {
        string source = TestResources.FixturePath("App.Tests.FieldPresentation.1");
        string directory = TestResources.GameDirectory;
        GameSession baselineSession = new(GameSession.Load(source));
        using GameWorld baseline = new(baselineSession, directory);
        baseline.EnsureField();
        ActorState original = baseline.Events.Actors.First(actor => actor.Active && actor.Visible && actor.RenderModelId >= 0);
        FieldScenePatch patch = new()
        {
            SchemaVersion = 1,
            FieldId = baseline.Field!.Id,
            FieldName = baseline.Field.Name,
            Actors = [new FieldActorPatch { ObjectId = original.ObjectId, Model = new FieldModelReference { FieldId = 175, ModelId = 9 }, AnimationId = 1 }]
        };
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(patch, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        ModFile file = new("Memoria/animation-example", ModFormat.Memoria, $"fields/{baseline.Field.Name}.json", 1, () => content);
        Assert.True(FieldSceneMods.TryPrepare(new ModCatalogSnapshot(1, [], [file]), out FieldSceneMods mods, out var diagnostics));
        Assert.Empty(diagnostics);
        GameSession session = new(GameSession.Load(source));
        using GameWorld world = new(session, directory, readSceneMods: () => mods);
        world.EnsureField();
        ActorState actor = world.Events.Actors.Single(actor => actor.ObjectId == original.ObjectId);
        JsonElement native = world.Events.Capture();
        FieldActorSample patched = world.CaptureFieldActor(actor);
        Assert.True(patched.Visual.ModelId >= world.Field!.Models.Count);
        Assert.Equal(0, patched.FirstFrame);
        Assert.Equal(40, patched.TerminalFrame);
        Assert.True(patched.Loop);
        world.ApplyDebug(new(DebugSceneTarget.Actor, ActorId: actor.ObjectId, AnimationId: 1, Frame: 39.5f));
        FieldActorSample debug = world.CaptureFieldActor(actor);
        Assert.Equal(39.5f, debug.Visual.AnimationFrame);
        Assert.Equal(0, debug.FirstFrame);
        Assert.Equal(40, debug.TerminalFrame);
        Assert.True(debug.Loop);
        Assert.True(JsonElement.DeepEquals(native, world.Events.Capture()));
        world.Render(TimeSpan.Zero, smoothFieldAnimation: true);
        Assert.True(JsonElement.DeepEquals(native, world.Events.Capture()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_30_and_60_presentations_preserve_the_same_events_rng_and_native_callback_count(bool accelerated)
    {
        string source = TestResources.FixturePath("App.Tests.FieldPresentation.1");
        GameState snapshot = GameSession.Load(source);
        string? expected = null;
        foreach (int displayFps in new[] { 30, 60 })
        {
            GameSession session = new(snapshot);
            using GameWorld world = new(session, TestResources.GameDirectory);
            world.EnsureField();
            world.SetRenderSize(160, 120);
            world.Render(TimeSpan.Zero);
            long initialFrame = world.Events.Frame;
            GameplayClock gameplay = new();
            PresentationTiming host = new();
            if (accelerated)
            {
                gameplay.ToggleSpeed();
            }
            double fieldSeconds = 0;
            int displays = displayFps / 5;
            for (int display = 0; display < displays; display++)
            {
                long start = TimeSpan.TicksPerSecond / 5 * display / displays;
                long end = TimeSpan.TicksPerSecond / 5 * (display + 1) / displays;
                host.Accumulate(TimeSpan.FromTicks(end - start));
                while (host.TryStep())
                {
                    fieldSeconds += gameplay.Advance(PresentationTiming.SimulationStep, SceneKind.Field).TotalSeconds;
                    while (fieldSeconds + 1e-9 >= 1d / EventEngine.SimulationFps)
                    {
                        fieldSeconds -= 1d / EventEngine.SimulationFps;
                        world.Events.Step();
                        world.CommitFieldPresentation();
                    }
                }
                world.Render(TimeSpan.Zero, fieldInterpolation: displayFps == 30 ? 1 : (float)Math.Clamp(fieldSeconds * EventEngine.SimulationFps, 0, 1), smoothFieldAnimation: displayFps == 60);
            }
            Assert.Equal(PresentationTiming.SimulationStep * 12, host.SimulationTotal);
            long expectedCallbacks = host.SimulationTotal.Ticks * (accelerated ? 3 : 1) * EventEngine.SimulationFps / TimeSpan.TicksPerSecond;
            Assert.Equal(initialFrame + expectedCallbacks, world.Events.Frame);
            string actual = Serialize(session, world);
            expected ??= actual;
            Assert.Equal(expected, actual);
        }
    }

    private static FieldPresentation Prime(FieldPresentationFrame before, FieldPresentationFrame after)
    {
        FieldPresentation presentation = new();
        presentation.Sample(before, 1, true);
        presentation.Commit(after.Owner, after.Frame, () => after);
        return presentation;
    }

    private static FieldActorSample Actor(object owner, float x, float phase = 1) => new(owner, 0, 0, new ActorVisual(1, 0, x, 0, 0, 0, 0, phase, true), 0, 4, true, 1);

    private static FieldPresentationFrame Frame(object owner, long frame, FieldActorSample actor, Vector2 cameraOffset = default) => new(owner, frame, 0, [actor], new BackgroundScrollFrame(new Dictionary<int, Vector2> { [0] = cameraOffset }, cameraOffset), CameraActorId: 1);

    private static string Serialize(GameSession session, GameWorld world) => JsonSerializer.Serialize(new { Session = session.State, Events = world.Events.Capture() });
}
