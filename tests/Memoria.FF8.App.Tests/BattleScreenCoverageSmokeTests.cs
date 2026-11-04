using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleScreenCoverageSmokeTests
{
    [Fact]
    public void Explicit_screen_coverage_preserves_native_packets_and_material_at_the_configured_resolution()
    {
        string? resolution = Environment.GetEnvironmentVariable("MEMORIA_FF8_VISIBLE_RESOLUTION");
        Assert.SkipUnless(resolution is not null, "Configure the current visible resolution.");
        int[] size = resolution!.Split('x').Select(int.Parse).ToArray();
        Assert.Equal(2, size.Length);
        int width = size[0];
        int height = size[1];
        Assert.True(width > 0 && height > 0);
        NativeBattleLogicalCommand quad = new(new("overlay", 0, 0), 1, 0x2A, [0x2A405060, 0, 320, 216u << 16, 320u | 216u << 16], 0, [0, 0, 0, 0], null)
        {
            OrderingBucket = 17,
            ScreenCoverage = NativeBattleScreenCoverage.Full
        };
        NativeBattleLogicalCommand tile = new(new("overlay", 0, 24), 1, 0x62, [0x62405060, 0, 320u | 216u << 16], 1, [0, 0, 0, 0], null)
        {
            OrderingBucket = 17,
            ScreenCoverage = NativeBattleScreenCoverage.Full
        };
        List<NativeBattleLogicalCommand> commands = [quad, tile];
        for (int quadrant = 0; quadrant < 4; quadrant++)
        {
            float left = (quadrant & 1) * 0.5f;
            float top = (quadrant >> 1) * 0.5f;
            int x = (quadrant & 1) * 160;
            int y = (quadrant >> 1) * 120;
            uint Point(int px, int py) => unchecked((ushort)px) | (uint)unchecked((ushort)py) << 16;
            commands.Add(quad with
            {
                Packet = new("quadrants", 0, quadrant * 24),
                Words = [quad.Words[0], Point(x, y), Point(x + 160, y), Point(x, y + 120), Point(x + 160, y + 120)],
                ScreenCoverage = new(left, top, left + 0.5f, top + 0.5f)
            });
        }
        foreach (NativeBattleLogicalCommand command in commands)
        {
            ImmutableArray<uint> nativeWords = command.Words;
            NativeBattleLogicalCommand restored = JsonSerializer.Deserialize<NativeBattleLogicalCommand>(JsonSerializer.Serialize(command))!;
            Assert.Equal(command.ScreenCoverage, restored.ScreenCoverage);
            Assert.Equal(nativeWords.ToArray(), restored.Words.ToArray());
            Capture expanded = Render(restored, width, height);
            Capture original = Render(restored with { ScreenCoverage = null }, width, height);
            Assert.Equal(2, expanded.Triangles.Count);
            Assert.Equal(original.Triangles.Count, expanded.Triangles.Count);
            NativeBattleScreenCoverage coverage = restored.ScreenCoverage!;
            Assert.Equal(new Vector2(coverage.Left * width, coverage.Top * height), expanded.Triangles[0].A.Position);
            Assert.Equal(new Vector2(coverage.Right * width, coverage.Bottom * height), expanded.Triangles[1].B.Position);
            for (int index = 0; index < expanded.Triangles.Count; index++)
            {
                Submission actual = expanded.Triangles[index];
                Submission native = original.Triangles[index];
                Assert.Equal(native.Material, actual.Material);
                Assert.Equal(TriangleBlend.Add, actual.Material.Blend);
                AssertVertex(native.A, actual.A);
                AssertVertex(native.B, actual.B);
                AssertVertex(native.C, actual.C);
            }
            Assert.Equal(nativeWords.ToArray(), command.Words.ToArray());
        }
    }

    private static void AssertVertex(TriangleVertex expected, TriangleVertex actual)
    {
        Assert.Equal(expected.Depth, actual.Depth);
        Assert.Equal(expected.ReciprocalW, actual.ReciprocalW);
        Assert.Equal(expected.Color, actual.Color);
        Assert.Equal(expected.Uv, actual.Uv);
    }

    private static Capture Render(NativeBattleLogicalCommand command, int width, int height)
    {
        NativeBattlePrimitiveFrame frame = new(NativeBattleLogicalOrdering.Clear(), [new(command, new(0x20), 2)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        Capture capture = new();
        BattleRenderer renderer = new(width, height, ViewportLayout.Fit(320, 240, width, height)) { TriangleRenderer = capture };
        renderer.RenderNativePrimitiveFrame(frame, null);
        return capture;
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
