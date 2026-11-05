using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleStageOverlayPresentationTests
{
    [Theory]
    [InlineData(0x518440u, 0, 640, 480)]
    [InlineData(0x518440u, 1, 1280, 720)]
    [InlineData(0x518440u, 0, 1981, 1327)]
    [InlineData(0x518440u, 1, 3440, 1440)]
    [InlineData(0x5180C0u, 1, 640, 480)]
    [InlineData(0x5180C0u, 0, 1280, 720)]
    [InlineData(0x5180C0u, 1, 1981, 1327)]
    [InlineData(0x5180C0u, 0, 720, 1280)]
    public void Arena_tints_cover_the_output_without_changing_native_geometry_or_material(uint callback, byte buffer, int width, int height)
    {
        NativeBattleStageSpecializedState state = NativeBattleStageSpecializedState.FromColdProcess();
        NativeBattleStageSpecializedPacketStep built = NativeBattleStageSpecializedPackets.Build(state, callback, buffer, 0x204060);
        NativeBattleStageSpecializedPacket packet = built.Packets[0];
        Assert.Equal(NativeBattleScreenCoverage.Full, packet.ScreenCoverage);
        Assert.Null(built.Packets[1].ScreenCoverage);
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(packet.Bytes.AsSpan().Slice(8)));
        Assert.Equal(320u | 224u << 16, BinaryPrimitives.ReadUInt32LittleEndian(packet.Bytes.AsSpan().Slice(12)));
        NativeBattleLogicalCommand command = Command(packet);
        NativeBattleLogicalCommand restored = JsonSerializer.Deserialize<NativeBattleLogicalCommand>(JsonSerializer.Serialize(command))!;
        Assert.Equal(command.Words.ToArray(), restored.Words.ToArray());
        Assert.Equal(command.ScreenCoverage, restored.ScreenCoverage);
        Capture expanded = Render(restored, width, height);
        Capture native = Render(restored with { ScreenCoverage = null }, width, height);
        Assert.Equal(2, expanded.Triangles.Count);
        Assert.Equal(Vector2.Zero, expanded.Triangles[0].A.Position);
        Assert.Equal(new Vector2(width, height), expanded.Triangles[1].B.Position);
        for (int index = 0; index < expanded.Triangles.Count; index++)
        {
            Submission actual = expanded.Triangles[index];
            Submission original = native.Triangles[index];
            Assert.Equal(original.Material, actual.Material);
            Assert.Equal(TriangleBlend.Add, actual.Material.Blend);
            AssertVertex(original.A, actual.A);
            AssertVertex(original.B, actual.B);
            AssertVertex(original.C, actual.C);
        }
        BattleRenderer raster = new(480, 320, ViewportLayout.Fit(320, 240, 480, 320));
        byte[] pixels = raster.RenderNativePrimitiveFrame(Frame(restored), null);
        byte[] center = pixels.AsSpan((160 * 480 + 240) * 4, 4).ToArray();
        Assert.Contains(center.Take(3), value => value != 0);
        foreach (int pixel in new[] { 0, 479, 319 * 480, 320 * 480 - 1 })
        {
            Assert.Equal(center, pixels.AsSpan(pixel * 4, 4).ToArray());
        }
    }

    [Fact]
    public void Retained_stage_rectangle_and_draw_mode_never_acquire_screen_coverage()
    {
        NativeBattleStageSpecializedState state = NativeBattleStageSpecializedState.FromColdProcess();
        state = NativeBattleStageSpecializedPackets.Write(state, 0x518290, 8, [16, 0, 24, 0, 32, 0, 48, 0]);
        NativeBattleStageSpecializedPacketStep built = NativeBattleStageSpecializedPackets.Build(state, 0x518290, 0, 0x204060);
        Assert.All(built.Packets, packet => Assert.Null(packet.ScreenCoverage));
        Assert.Equal(new byte[] { 16, 0, 24, 0, 32, 0, 48, 0 }, built.Packets[0].Bytes.Skip(8));
        Capture rendered = Render(Command(built.Packets[0]), 1280, 720);
        Assert.NotEqual(Vector2.Zero, rendered.Triangles[0].A.Position);
        Assert.NotEqual(new Vector2(1280, 720), rendered.Triangles[1].B.Position);
    }

    private static NativeBattleLogicalCommand Command(NativeBattleStageSpecializedPacket packet)
    {
        ImmutableArray<uint> words = Enumerable.Range(1, 3).Select(index => BinaryPrimitives.ReadUInt32LittleEndian(packet.Bytes.AsSpan().Slice(index * 4))).ToImmutableArray();
        return new(new(packet.Bank, 0, packet.Offset), 1, 0x62, words, packet.Flags, [0, 0, 0, 0], null)
        {
            OrderingBucket = 1,
            ScreenCoverage = packet.ScreenCoverage
        };
    }

    private static NativeBattlePrimitiveFrame Frame(NativeBattleLogicalCommand command)
    {
        return new(NativeBattleLogicalOrdering.Clear(), [new(command, new(0x20), 2)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
    }

    private static Capture Render(NativeBattleLogicalCommand command, int width, int height)
    {
        Capture capture = new();
        BattleRenderer renderer = new(width, height, ViewportLayout.Fit(320, 240, width, height)) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(Frame(command), null);
        return capture;
    }

    private static void AssertVertex(TriangleVertex expected, TriangleVertex actual)
    {
        Assert.Equal(expected.Depth, actual.Depth);
        Assert.Equal(expected.ReciprocalW, actual.ReciprocalW);
        Assert.Equal(expected.Color, actual.Color);
        Assert.Equal(expected.Uv, actual.Uv);
    }

    private sealed record Submission(TriangleVertex A, TriangleVertex B, TriangleVertex C, TriangleMaterial Material);

    private sealed class Capture : ITriangleRenderer
    {
        public List<Submission> Triangles { get; } = [];

        public void Begin(int width, int height, Vector4 clearColor, float clearDepth = 1)
        {
            Triangles.Clear();
        }

        public void Draw(in TriangleVertex a, in TriangleVertex b, in TriangleVertex c, in TriangleMaterial material)
        {
            Triangles.Add(new(a, b, c, material));
        }

        public void End()
        {
        }
    }
}
