using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleActorBoundsTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Actor_bounds_follow_rendered_coordinates_and_clear_with_the_next_frame(bool captureBackend)
    {
        NativeBattleLogicalCommand body = Triangle("target", 80, 54) with { ProjectedCoordinates = [new(640, 432), new(800, 432), new(640, 576)] };
        NativeBattleLogicalCommand background = Triangle(null, 0, 0);
        NativeBattlePrimitiveFrame frame = Frame(body, background);
        CaptureBackend backend = new();
        BattleRenderer renderer = new(480, 240, ViewportLayout.Fit(320, 240, 480, 240)) { TriangleRenderer = captureBackend ? backend : null };
        renderer.RenderNativePrimitiveFrame(frame, null);
        Assert.True(renderer.TryGetActorBounds("target", out Vector4 bounds));
        Assert.Equal(new Vector4(160, 60, 180, 80), bounds);
        Assert.False(renderer.TryGetActorBounds("absent", out _));
        if (captureBackend)
        {
            Assert.Equal(new Vector2(bounds.X, bounds.Y), backend.Vertices[0].Position);
        }
        renderer.RenderNativePrimitiveFrame(Frame(background), null);
        Assert.False(renderer.TryGetActorBounds("target", out _));
    }

    [Fact]
    public void Optional_actor_identity_survives_prepared_command_serialization()
    {
        NativeBattlePreparedPrimitiveFrame frame = NativeBattlePreparedPrimitiveFrame.From(Frame(Triangle("target", 80, 54)));
        NativeBattlePreparedPrimitiveFrame restored = JsonSerializer.Deserialize<NativeBattlePreparedPrimitiveFrame>(JsonSerializer.Serialize(frame))!;
        Assert.Equal("target", Assert.Single(restored.LastCommands).Command.PresentationActorId);
        BattleRenderer renderer = new(320, 240);
        renderer.RenderNativePrimitiveFrame(restored, null);
        Assert.True(renderer.TryGetActorBounds("target", out _));
    }

    [Fact]
    public void Actor_identity_flows_through_pending_ordering_snapshot_and_execution()
    {
        NativeBattleLogicalCommand body = Triangle("target", 80, 54);
        ImmutableArray<uint> packetWords = ImmutableArray.Create(0x04000000u).AddRange(body.Words);
        NativeBattlePrimitiveReference reference = new("off_B6D070", 0, 0);
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear().Prepend(100, reference, packetWords, 0, [], presentationActorId: "target");
        string json = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));
        NativeBattleLogicalOrdering restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleLogicalOrdering>(json)!);
        NativeBattlePrimitiveTables tables = Tables();
        NativeBattleLogicalResolvedCommand resolved = Assert.Single(NativeBattleLogicalPasses.Execute(restored, tables, new(0), 1).Commands);
        Assert.Equal("target", resolved.Command.PresentationActorId);
        Assert.Equal(body.Words, resolved.Command.Words);
        BattleRenderer renderer = new(320, 240);
        renderer.RenderNativePrimitiveFrame(Frame(resolved.Command), null);
        Assert.True(renderer.TryGetActorBounds("target", out Vector4 bounds));
        Assert.Equal(new Vector4(80, 60, 100, 80), bounds);

        using NativeBattleLogicalOrdering.Builder builder = new(restored);
        builder.InvalidatePacketRange(new(reference.Bank, reference.Buffer, 8), 4);
        NativeBattleLogicalPacket invalidated = Assert.Single(builder.ToImmutable().Packets);
        Assert.Null(invalidated.PresentationActorId);
        Assert.True(invalidated.RequiresPhysicalRead);
        Assert.Equal("target", Assert.Single(ordering.Packets).PresentationActorId);
    }

    [Fact]
    public void Component_bounds_follow_posed_body_transform_and_exclude_unlabelled_weapons()
    {
        BattleModel model = BattleDebugModels.CreateDummy();
        BattleCamera camera = new(Vector3.Zero, -Vector3.UnitZ, 1, 0.45f);
        BattleRenderer renderer = new(320, 240);
        BattleActorDraw body = new(model, new(10, 0, -100), AttackOffset: new(5, 3, 0), GroundOffset: 2) { ActorId = "target" };
        BattleActorDraw weapon = body with { ActorId = null, Position = new(1000, 0, -100) };
        renderer.Render(new BattleStage(0, [], []), [body, weapon], camera);
        Assert.True(renderer.TryGetActorBounds("target", out Vector4 bounds));
        List<Vector2> expected = [];
        foreach (Vector3 vertex in model.Pose(0, 0).SelectMany(mesh => mesh))
        {
            Assert.True(renderer.TryProject(vertex + new Vector3(15, 1, -100), out Vector2 screen));
            expected.Add(screen);
        }
        Assert.Equal(new Vector4(expected.Min(point => point.X), expected.Min(point => point.Y), expected.Max(point => point.X), expected.Max(point => point.Y)), bounds);
        renderer.Render(new BattleStage(0, [], []), [weapon], camera);
        Assert.False(renderer.TryGetActorBounds("target", out _));
    }

    private static NativeBattlePrimitiveTables Tables()
    {
        byte[] lengths = new byte[256];
        lengths[0x20] = 4;
        uint[] handlers = new uint[256];
        handlers[0x20] = 1;
        ImmutableArray<uint> empty = new uint[256].ToImmutableArray();
        return new(lengths.ToImmutableArray(), new byte[256].ToImmutableArray(), empty, handlers.ToImmutableArray(), empty);
    }

    private static NativeBattleLogicalCommand Triangle(string? actorId, int x, int y)
    {
        uint Point(int px, int py) => unchecked((ushort)(px * 8)) | (uint)unchecked((ushort)(py * 8)) << 16;
        return new(new("triangle", 0, 0), 1, 0x20, [0x200000A0, Point(x, y), Point(x + 20, y), Point(x, y + 18)], 0, [], null) { OrderingBucket = 100, PresentationActorId = actorId };
    }

    private static NativeBattlePrimitiveFrame Frame(params NativeBattleLogicalCommand[] commands)
    {
        return new(NativeBattleLogicalOrdering.Clear(), commands.Select(command => new NativeBattleLogicalResolvedCommand(command, new(0), 1)).ToImmutableArray(), [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
    }

    private sealed class CaptureBackend : ITriangleRenderer
    {
        public List<TriangleVertex> Vertices { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Vertices.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Vertices.Add(a);
            Vertices.Add(b);
            Vertices.Add(c);
        }

        public void End()
        {
        }
    }
}
