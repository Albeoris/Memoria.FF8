using System.Numerics;
using System.Text.Json;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class FieldParticleInterpolationTests
{
    private static NativeFieldParticleSprite Sprite => new(0, 0, 0, 0, 0, 400, 0, 8, 4, 0, 0, 16, 16, 4, 128, 128, 128, 0, false);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_forwards_completed_pool_and_sprite_identity_independently_of_actor_smoothing(bool smoothAnimation)
    {
        object owner = new();
        NativeFieldParticleSprite previous = Sprite;
        FieldPresentationFrame before = Frame(owner, 7, State(previous), [previous]);
        FieldPresentationFrame after = Frame(owner, 8, State(Sprite), [Sprite with { X = 19 }]);
        FieldPresentation presentation = Prime(before, after);
        FieldParticleBlend blend = Assert.IsType<FieldParticleBlend>(presentation.Sample(after, 0.25f, smoothAnimation).FieldParticles);
        Assert.Same(before.FieldParticles, blend.PreviousState);
        Assert.Same(before.FieldParticleSprites, blend.Previous);
        Assert.Equal(0.25f, blend.Fraction);
        Assert.True(blend.TryPrevious(after.FieldParticleSprites![0], out NativeFieldParticleSprite matched));
        Assert.Same(previous, matched);
        Assert.Null(presentation.Sample(after, 1, smoothAnimation).FieldParticles);
    }

    [Theory]
    [InlineData("pool")]
    [InlineData("sprites")]
    [InlineData("frame")]
    [InlineData("skipped callback")]
    [InlineData("scene")]
    [InlineData("reset")]
    public void Presentation_clears_particle_history_at_discontinuities(string change)
    {
        object owner = new();
        FieldPresentationFrame before = Frame(owner, 7, State(Sprite), [Sprite]);
        FieldPresentationFrame after = Frame(owner, 8, State(Sprite), [Sprite with { X = 19 }]);
        FieldPresentation presentation = Prime(before, after);
        Assert.NotNull(presentation.Sample(after, 0.5f, true).FieldParticles);
        switch (change)
        {
            case "pool":
                after = after with { FieldParticles = after.FieldParticles! with { } };
                break;
            case "sprites":
                after = after with { FieldParticleSprites = after.FieldParticleSprites!.ToArray() };
                break;
            case "frame":
                after = after with { Frame = 9 };
                break;
            case "skipped callback":
                after = after with { Frame = 10 };
                presentation.Commit(owner, after.Frame, () => after);
                break;
            case "scene":
                after = after with { Owner = new object(), Frame = 9 };
                presentation.Commit(after.Owner, after.Frame, () => after);
                break;
            case "reset":
                presentation.Reset();
                break;
        }
        Assert.Null(presentation.Sample(after, 0.5f, true).FieldParticles);
    }

    [Theory]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Invalid_fractions_are_rejected(float fraction)
    {
        FieldParticleBlend blend = new(NativeFieldParticlesState.Create(new byte[NativeFieldParticlesState.DefinitionSize]), [], fraction);
        Assert.Throws<ArgumentOutOfRangeException>(() => blend.Validate());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Retired_pool_slots_never_blend_with_reused_slots(bool sameType)
    {
        NativeFieldParticlesState state = State(Sprite);
        state.Memory[10016 + 27] = 0;
        FieldParticleBlend blend = new(state, [Sprite], 0.5f);
        NativeFieldParticleSprite current = Sprite with { TypeIndex = sameType ? (byte)0 : (byte)1 };
        Assert.False(blend.TryPrevious(current, out _));
    }

    [Fact]
    public void Occupied_slots_follow_stage_changes_but_do_not_match_other_types_or_births()
    {
        FieldParticleBlend blend = new(State(Sprite), [Sprite], 0.5f);
        Assert.True(blend.TryPrevious(Sprite with { StageIndex = 1 }, out NativeFieldParticleSprite previous));
        Assert.Equal(Sprite, previous);
        Assert.False(blend.TryPrevious(Sprite with { TypeIndex = 1 }, out _));
        Assert.False(blend.TryPrevious(Sprite with { PoolIndex = 1 }, out _));
    }

    [Fact]
    public void Half_turn_preserves_size_and_angle_wrap_takes_shortest_arc()
    {
        FieldParticleBlend blend = new(State(Sprite), [Sprite], 0.5f);
        Vector2 turned = blend.Offset(new Vector2(10, 0), new Vector2(-10, 0), 0, 2048);
        Assert.InRange(turned.Length(), 9.999f, 10.001f);
        Assert.InRange(turned.Y, -10.001f, -9.999f);
        float radians = MathF.Tau * 4090 / 4096;
        Vector2 before = new(MathF.Cos(radians) * 10, MathF.Sin(radians) * 10);
        radians = MathF.Tau * 6 / 4096;
        Vector2 after = new(MathF.Cos(radians) * 10, MathF.Sin(radians) * 10);
        Vector2 wrapped = blend.Offset(before, after, 4090, 6);
        Assert.InRange(wrapped.X, 9.999f, 10.001f);
        Assert.InRange(wrapped.Y, -0.001f, 0.001f);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(1f)]
    public void Renderer_preserves_exact_native_geometry_and_color_at_endpoints(float fraction)
    {
        NativeFieldParticleSprite previous = Sprite with { X = -17, Angle = 4090, Red = 63 };
        NativeFieldParticleSprite current = Sprite with { X = 27, Angle = 6, Width = 11, Red = 231 };
        Recorder expected = Render(fraction == 0 ? previous : current);
        Recorder actual = Render(current, new FieldParticleBlend(State(previous), [previous], fraction));
        Assert.Equal(expected.Vertices, actual.Vertices);
    }

    [Fact]
    public void Renderer_blends_float_geometry_size_color_and_depth_but_keeps_current_texture_frame()
    {
        NativeFieldParticleSprite previous = Sprite with { X = -17, Width = 8, Red = 63 };
        NativeFieldParticleSprite current = Sprite with { X = 27, Z = 500, Width = 13, Red = 231, U = 32, V = 48 };
        NativeFieldParticlesState state = State(previous);
        string original = JsonSerializer.Serialize(state);
        Recorder first = Render(previous);
        Recorder last = Render(current);
        Recorder middle = Render(current, new FieldParticleBlend(state, [previous], 0.25f));
        for (int index = 0; index < middle.Vertices.Count; index++)
        {
            TriangleVertex expected = first.Vertices[index];
            TriangleVertex after = last.Vertices[index];
            Assert.Equal(Vector2.Lerp(expected.Position, after.Position, 0.25f), middle.Vertices[index].Position);
            Assert.Equal(expected.Depth + (after.Depth - expected.Depth) * 0.25f, middle.Vertices[index].Depth);
            Assert.Equal(after.Uv, middle.Vertices[index].Uv);
            Assert.Equal(Vector4.Lerp(expected.Color, after.Color, 0.25f), middle.Vertices[index].Color);
        }
        Assert.Equal(original, JsonSerializer.Serialize(state));
        Assert.NotEqual(first.Vertices[0], middle.Vertices[0]);
        Assert.NotEqual(last.Vertices[0], middle.Vertices[0]);
    }

    [Fact]
    public void Renderer_snaps_reused_pool_slot_to_current_native_geometry()
    {
        NativeFieldParticleSprite previous = Sprite with { X = -40 };
        NativeFieldParticleSprite current = Sprite with { X = 40 };
        NativeFieldParticlesState state = State(previous);
        state.Memory[10016 + 27] = 0;
        state.Memory[348] = 0;
        Recorder expected = Render(current);
        Recorder actual = Render(current, new FieldParticleBlend(state, [previous], 0.5f));
        Assert.Equal(expected.Vertices, actual.Vertices);
    }

    [Fact]
    public void Installed_particles_render_intermediate_frames_without_advancing_native_state()
    {
        string directory = TestResources.GameDirectory;
        string fixture = TestResources.FixturePath("App.Tests.NativeFieldParticleContinuationTests.1");
        Assert.SkipUnless(Directory.Exists(directory) && File.Exists(fixture), "Configured particle fixture and installed resources are required.");
        GameSession session = new(GameSession.Load(fixture));
        using GameWorld world = new(session, directory, validateWorldPresentation: false);
        world.EnsureField();
        world.SetRenderSize(320, 224);
        world.Render(TimeSpan.Zero);
        Assert.True(world.Apply(new(PlayerActionKind.Confirm)).Accepted);
        for (int tick = 0; tick < 500 && world.Events.FieldParticleSprites.Count < 20; tick++)
        {
            Assert.True(world.Apply(world.NextTrigger.Kind == PlayerTriggerKind.Dialogue ? new(PlayerActionKind.Confirm) : new(PlayerActionKind.Wait)).Accepted);
            Assert.Null(world.Events.LastFault);
            world.CommitFieldPresentation();
        }
        Assert.True(world.Events.FieldParticleSprites.Count >= 20);
        Assert.True(world.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
        world.CommitFieldPresentation();
        JsonElement before = world.Events.Capture();
        byte[] first = world.Render(TimeSpan.Zero, fieldInterpolation: 0.25f).ToArray();
        byte[] middle = world.Render(TimeSpan.Zero, fieldInterpolation: 0.5f).ToArray();
        byte[] last = world.Render(TimeSpan.Zero, fieldInterpolation: 0.75f).ToArray();
        Assert.False(first.AsSpan().SequenceEqual(middle));
        Assert.False(middle.AsSpan().SequenceEqual(last));
        Assert.True(JsonElement.DeepEquals(before, world.Events.Capture()));
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_FIELD_PARTICLE_INTERPOLATION_OUTPUT") ?? TestResources.ArtifactFile("field-particle-interpolation.ff8");
        session.SetEventState(before);
        session.Save(output, compact: true);
        using GameWorld restored = new(new GameSession(GameSession.Load(output)), directory, validateWorldPresentation: false);
        restored.EnsureField();
        restored.SetRenderSize(320, 224);
        Assert.Equal(world.Render(TimeSpan.Zero), restored.Render(TimeSpan.Zero));
        Assert.True(JsonElement.DeepEquals(before, restored.Events.Capture()));
        File.WriteAllText(output + ".summary.json", JsonSerializer.Serialize(new { Field = world.Events.FieldId, world.Events.Frame, Particles = world.Events.FieldParticleSprites.Count }));
        Console.WriteLine($"Particle interpolation checkpoint: field {world.Events.FieldId}, frame {world.Events.Frame}, {output}");
    }

    private static FieldPresentationFrame Frame(object owner, long frame, NativeFieldParticlesState state, IReadOnlyList<NativeFieldParticleSprite> sprites)
    {
        return new FieldPresentationFrame(owner, frame, 0, [], new BackgroundScrollFrame(new Dictionary<int, Vector2>(), Vector2.Zero), FieldParticles: state, FieldParticleSprites: sprites);
    }

    private static FieldPresentation Prime(FieldPresentationFrame before, FieldPresentationFrame after)
    {
        FieldPresentation presentation = new();
        presentation.Sample(before, 1, true);
        presentation.Commit(after.Owner, after.Frame, () => after);
        return presentation;
    }

    private static NativeFieldParticlesState State(NativeFieldParticleSprite sprite)
    {
        NativeFieldParticlesState state = NativeFieldParticlesState.Create(new byte[NativeFieldParticlesState.DefinitionSize]);
        state.Memory[10] = sprite.U;
        state.Memory[11] = sprite.V;
        state.Memory[12] = sprite.TextureWidth;
        state.Memory[13] = sprite.TextureHeight;
        state.Memory[15] = sprite.BlendMode;
        state.Memory[348] = 1;
        state.Memory[10016 + 27] = 1;
        return state;
    }

    private static Recorder Render(NativeFieldParticleSprite sprite, FieldParticleBlend? blend = null)
    {
        FieldAssets field = new(0, "particle-fixture", [], [], [], new FieldCamera(Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero, Vector2.Zero, 100), new FieldWalkmesh([]), []);
        Recorder recorder = new();
        FieldRenderer renderer = new(field, 320, 224, nativeMovieViewport: true) { TriangleRenderer = recorder };
        PixelImage texture = new(256, 256, Enumerable.Repeat(new Rgba32(40, 40, 40), 256 * 256).ToArray());
        renderer.Render(new Dictionary<int, int>(), [], TimeSpan.Zero, fieldParticles: [sprite], fieldParticleTexture: texture, fieldParticleBlend: blend);
        return recorder;
    }

    private sealed class Recorder : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
        }

        public void End()
        {
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.AddRange([a, b, c]);
        }
    }
}
