using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class DrawPointRenderInterpolationTests
{
    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.NegativeInfinity)]
    [InlineData(float.PositiveInfinity)]
    public void Invalid_blend_fractions_are_rejected(float fraction)
    {
        DrawPointParticleBlend blend = new(Particles(7, 0), fraction);
        Assert.Throws<ArgumentOutOfRangeException>(() => blend.Validate());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Field_presentation_propagates_particle_history_independently_of_actor_smoothing(bool smoothAnimation)
    {
        object owner = new();
        FieldPresentationFrame before = Frame(owner, 7, Particles(7, 0));
        FieldPresentationFrame after = Frame(owner, 8, Particles(8, 101));
        FieldPresentation presentation = Prime(before, after);
        DrawPointParticleBlend blend = Assert.IsType<DrawPointParticleBlend>(presentation.Sample(after, 0.25f, smoothAnimation).DrawPointBlend);
        Assert.Same(before.DrawPointParticles, blend.Previous);
        Assert.Equal(0.25f, blend.Fraction);
        Assert.Null(presentation.Sample(after, 1, smoothAnimation).DrawPointBlend);
    }

    [Theory]
    [InlineData("reset")]
    [InlineData("skipped callback")]
    [InlineData("replaced current particles")]
    public void Field_history_discontinuities_clear_particle_blending(string change)
    {
        object owner = new();
        FieldPresentationFrame before = Frame(owner, 7, Particles(7, 0));
        FieldPresentationFrame after = Frame(owner, 8, Particles(8, 101));
        FieldPresentation presentation = Prime(before, after);
        Assert.NotNull(presentation.Sample(after, 0.5f, true).DrawPointBlend);
        if (change == "reset")
        {
            presentation.Reset();
        }
        else if (change == "skipped callback")
        {
            after = after with { Frame = 10, DrawPointParticles = Particles(10, 202) };
            presentation.Commit(owner, after.Frame, () => after);
        }
        else
        {
            after = after with { DrawPointParticles = after.DrawPointParticles! with { } };
        }
        Assert.Null(presentation.Sample(after, 0.5f, true).DrawPointBlend);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ring_wrap_interpolates_each_trail_age_with_fractional_precision(bool secondEdge)
    {
        NativeDrawPointParticles previous = Particles(7, 0);
        NativeDrawPointParticles current = Particles(8, 101);
        string before = JsonSerializer.Serialize(new[] { previous, current });
        DrawPointParticleBlend blend = new(previous, 0.25f);
        NativeDrawPointSpark spark = current.Drawings[0];
        for (int age = 0; age <= 5; age++)
        {
            ImmutableArray<NativeSparkPoint> oldEdge = secondEdge ? previous.Drawings[0].SecondEdge : previous.Drawings[0].FirstEdge;
            ImmutableArray<NativeSparkPoint> newEdge = secondEdge ? spark.SecondEdge : spark.FirstEdge;
            Vector3 expected = Vector3.Lerp(oldEdge[(7 - age) & 7].Vector, newEdge[(8 - age) & 7].Vector, 0.25f);
            Assert.Equal(expected, blend.Point(current, spark, age, secondEdge));
        }
        Assert.Equal(before, JsonSerializer.Serialize(new[] { previous, current }));
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void Endpoints_preserve_exact_native_ring_coordinates(float fraction)
    {
        NativeDrawPointParticles previous = Particles(7, -31);
        NativeDrawPointParticles current = Particles(8, 101);
        DrawPointParticleBlend blend = new(previous, fraction);
        NativeDrawPointSpark expected = fraction == 0 ? previous.Drawings[0] : current.Drawings[0];
        for (int age = 0; age <= 5; age++)
        {
            int index = (expected.RenderCounter - age) & 7;
            Assert.Equal(expected.FirstEdge[index].Vector, blend.Point(current, current.Drawings[0], age, false));
            Assert.Equal(expected.SecondEdge[index].Vector, blend.Point(current, current.Drawings[0], age, true));
        }
    }

    [Theory]
    [InlineData("birth")]
    [InlineData("status")]
    [InlineData("counter")]
    [InlineData("anchor")]
    [InlineData("control")]
    [InlineData("target")]
    [InlineData("duration")]
    public void Lifecycle_discontinuities_snap_to_current_geometry(string change)
    {
        NativeDrawPointParticles previous = Particles(7, 0);
        NativeDrawPointParticles current = Particles(8, 101);
        NativeDrawPointSpark old = previous.Sparks[3];
        old = change switch
        {
            "birth" => old with { RenderStatus = 0 },
            "status" => old with { RenderStatus = 2 },
            "counter" => old with { RenderCounter = 2 },
            "anchor" => old with { Anchor = new(1, 2, 3) },
            "control" => old with { Control = new(4, 5, 6) },
            "target" => old with { Target = new(7, 8, 9) },
            "duration" => old with { Duration = 30 },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        previous = previous with { Sparks = previous.Sparks.SetItem(3, old), Drawings = change == "birth" ? [] : [old] };
        DrawPointParticleBlend blend = new(previous, 0.5f);
        NativeDrawPointSpark spark = current.Drawings[0];
        for (int age = 0; age <= 5; age++)
        {
            int index = (spark.RenderCounter - age) & 7;
            Assert.Equal(spark.FirstEdge[index].Vector, blend.Point(current, spark, age, false));
            Assert.Equal(spark.SecondEdge[index].Vector, blend.Point(current, spark, age, true));
        }
    }

    [Theory]
    [InlineData("App.Tests.DrawPointRenderInterpolationTests.1", false)]
    [InlineData("App.Tests.DrawPointRenderInterpolationTests.2", true)]
    public void Installed_draw_point_has_intermediate_pixels_without_advancing_events(string resource, bool visiblePoint)
    {
        string directory = TestResources.GameDirectory;
        string fixture = TestResources.FixturePath(resource);
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(fixture), "Draw-point snapshot and installed resources are required.");
        using GameResourceHost resources = new(directory, watch: false);
        GameSession session = new(PlayerStateLoader.Load(fixture, directory, resources: resources.Resources, modelOverrides: resources, readGameplayPolicy: () => resources.GameplayPolicy));
        using GameWorld world = new(session, directory, resources: resources.Resources, resolveGameFile: resources.ResolveGameFile, readSceneMods: () => resources.SceneMods, modelOverrides: resources);
        world.EnsureField();
        world.Render(TimeSpan.Zero, smoothFieldAnimation: true);
        long initialFrame = world.Events.Frame;
        // Gameplay saves restore the field initialization boundary before publishing particle drawings.
        for (int step = 0; step < 4; step++)
        {
            Assert.True(world.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 1)).Accepted);
            Assert.Null(world.Events.LastFault);
            world.CommitFieldPresentation();
        }
        Assert.Equal(initialFrame + 4, world.Events.Frame);
        NativeDrawPointParticles particles = Assert.IsType<NativeDrawPointParticles>(world.Events.DrawPointParticleFrame);
        if (visiblePoint)
        {
            Assert.NotEmpty(particles.Drawings);
            Assert.Contains(particles.Drawings, spark => spark.RenderStatus == 2 || spark.RenderStatus == 1 && world.Events.Scene.NativeDrawPointDisplayMask == 1);
        }
        JsonElement state = world.Events.Capture();
        byte[] first = world.Render(TimeSpan.Zero, fieldInterpolation: 0.25f, smoothFieldAnimation: true).ToArray();
        byte[] middle = world.Render(TimeSpan.Zero, fieldInterpolation: 0.5f, smoothFieldAnimation: true).ToArray();
        byte[] last = world.Render(TimeSpan.Zero, fieldInterpolation: 0.75f, smoothFieldAnimation: true).ToArray();
        if (visiblePoint)
        {
            Assert.False(first.AsSpan().SequenceEqual(middle));
            Assert.False(middle.AsSpan().SequenceEqual(last));
        }
        Assert.True(JsonElement.DeepEquals(state, world.Events.Capture()));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_DRAW_INTERPOLATION_OUTPUT") ?? TestResources.ArtifactFile("draw-point-interpolation-save.json");
        if (visiblePoint)
        {
            output = Path.Combine(Path.GetDirectoryName(output)!, Path.GetFileNameWithoutExtension(output) + "-draw.json");
        }
        session.SetEventState(world.Events.Capture());
        session.Save(output, compact: true);
        GameSession restoredSession = new(GameSession.Load(output));
        using GameWorld restored = new(restoredSession, directory, resources: resources.Resources, resolveGameFile: resources.ResolveGameFile, readSceneMods: () => resources.SceneMods, modelOverrides: resources);
        restored.EnsureField();
        restored.Render(TimeSpan.Zero);
        Assert.Equal(world.Events.FieldId, restored.Events.FieldId);
        Assert.Equal(world.Events.Frame, restored.Events.Frame);
        File.WriteAllText(output + ".summary.json", JsonSerializer.Serialize(new { Field = world.Events.FieldId, world.Events.Frame, Drawings = particles.Drawings.Length, world.Events.Scene.DrawPointEnabled }));
        Console.WriteLine($"Draw-point interpolation checkpoint: field {world.Field!.Id}, frame {world.Events.Frame}, {output}");
    }

    private static NativeDrawPointParticles Particles(short counter, int offset)
    {
        ImmutableArray<NativeSparkPoint> first = Enumerable.Range(0, 8).Select(index => NativeSparkPoint.From(offset + index * 3, offset - index * 5, offset + index * 7)).ToImmutableArray();
        ImmutableArray<NativeSparkPoint> second = Enumerable.Range(0, 8).Select(index => NativeSparkPoint.From(offset + index * 11, offset - index * 13, offset + index * 17)).ToImmutableArray();
        NativeDrawPointSpark selected = new(1, default, new(20, 30, 40), new(50, 60, 70), 16, counter, 0, 0, first, second, 1, counter);
        ImmutableArray<NativeDrawPointSpark> sparks = Enumerable.Range(0, 8).Select(index => index == 3 ? selected : selected with { Anchor = NativeSparkPoint.From(index + 100, 0, 0), RenderStatus = 0 }).ToImmutableArray();
        return new NativeDrawPointParticles(1, sparks, 0) { Drawings = [selected] };
    }

    private static FieldPresentationFrame Frame(object owner, long frame, NativeDrawPointParticles particles)
    {
        return new FieldPresentationFrame(owner, frame, 0, [], new BackgroundScrollFrame(new Dictionary<int, Vector2>(), Vector2.Zero), DrawPointParticles: particles);
    }

    private static FieldPresentation Prime(FieldPresentationFrame before, FieldPresentationFrame after)
    {
        FieldPresentation presentation = new();
        presentation.Sample(before, 1, true);
        presentation.Commit(after.Owner, after.Frame, () => after);
        return presentation;
    }
}
