using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeSoftwarePolygonClipTests
{
    private static NativeSoftwareClipBounds Bounds => new(0, 0, 10, 10);

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(10, 10, 0)]
    [InlineData(-1, 5, 1)]
    [InlineData(5, -1, 2)]
    [InlineData(11, 5, 4)]
    [InlineData(5, 11, 8)]
    [InlineData(-1, -1, 3)]
    [InlineData(11, 11, 12)]
    public void Outcodes_use_signed_inclusive_boundaries_and_the_exact_four_bits(int x, int y, int expected)
    {
        Assert.Equal((byte)expected, NativeSoftwarePolygonClip.Outcode466320(x, y, Bounds));
        Assert.Equal((byte)0, NativeSoftwarePolygonClip.Outcode466320(-65536, -5, new(-65536, -5, 0, 0)));
        Assert.Equal((byte)1, NativeSoftwarePolygonClip.Outcode466320(5, 0, new(10, 0, 0, 10)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Each_plane_emits_crossing_intersections_before_the_current_inside_vertex(int plane)
    {
        NativeSoftwareClipVertex[] vertices = plane switch
        {
            0 => [V(2, 0), V(-2, 2), V(2, 4)],
            1 => [V(0, 2), V(2, -2), V(4, 2)],
            2 => [V(8, 0), V(12, 2), V(8, 4)],
            _ => [V(0, 8), V(2, 12), V(4, 8)]
        };
        (int, int)[] expected = plane switch
        {
            0 => [(2, 0), (0, 1), (0, 3), (2, 4)],
            1 => [(0, 2), (1, 0), (3, 0), (4, 2)],
            2 => [(8, 0), (10, 1), (10, 3), (8, 4)],
            _ => [(0, 8), (1, 10), (3, 10), (4, 8)]
        };
        NativeSoftwareClippedPolygon result = NativeSoftwarePolygonClip.Clip466360(Polygon(0, vertices), Bounds);
        Assert.Equal(expected, result.Vertices.Select(vertex => (vertex.X16_16, vertex.Y)).ToArray());
    }

    [Fact]
    public void Four_plane_corner_clipping_preserves_native_pass_order_and_header_bytes()
    {
        NativeSoftwareClippedPolygon input = Polygon(0, V(-1, -1), V(3, -1), V(3, 3), V(-1, 3));
        NativeSoftwareClippedPolygon result = NativeSoftwarePolygonClip.Clip466360(input, new(0, 0, 2, 2));
        Assert.Equal(new[] { (0, 2), (0, 0), (2, 0), (2, 2) }, result.Vertices.Select(vertex => (vertex.X16_16, vertex.Y)).ToArray());
        Assert.Equal(input.HeaderTail.ToArray(), result.HeaderTail.ToArray());
    }

    [Fact]
    public void Current_anchor_half_ties_differ_from_a_rearranged_previous_anchor_for_geometry_and_attributes()
    {
        NativeSoftwareClipVertex previous = new(1, 0, 0, 0, 0, 0, 0);
        NativeSoftwareClipVertex current = new(-1, 1, 1, 1, 1, 1, 1);
        NativeSoftwareClipVertex intersection = NativeSoftwarePolygonClip.Intersection(current, previous, 0, true, 3);
        Assert.Equal(new NativeSoftwareClipVertex(0, 1, 1, 1, 1, 1, 1), intersection);
        int rearrangedY = previous.Y + NativePrecision53.RoundInteger((current.Y - previous.Y) * 0.5);
        Assert.Equal(0, rearrangedY);
        Assert.NotEqual(rearrangedY, intersection.Y);
        NativeSoftwareClippedPolygon result = NativeSoftwarePolygonClip.Clip466360(Polygon(3, previous, current, new(1, 2, 2, 2, 2, 2, 2)), new(0, -10, 10, 10));
        Assert.Equal(new[] { (1, 0), (0, 1), (0, 2), (1, 2) }, result.Vertices.Select(vertex => (vertex.X16_16, vertex.Y)).ToArray());
    }

    [Fact]
    public void Delta_and_boundary_subtractions_wrap_as_DWORDs_before_double_division()
    {
        NativeSoftwareClipVertex result = NativeSoftwarePolygonClip.Intersection(V(int.MaxValue, 1), V(int.MinValue, 0), 0, true, 0);
        Assert.Equal(0, result.X16_16);
        Assert.Equal(int.MinValue, result.Y);
        NativeSoftwareClipVertex wrappedNumerator = NativeSoftwarePolygonClip.Intersection(V(int.MinValue, 1), V(int.MaxValue, 0), int.MaxValue, true, 0);
        Assert.Equal(0, wrappedNumerator.Y);
        NativeSoftwareClipVertex attribute = NativeSoftwarePolygonClip.Intersection(new(-1, 1, int.MaxValue, 5, null, null, null), new(1, 0, int.MinValue, 3, null, null, null), 0, true, 2);
        Assert.Equal(int.MaxValue, attribute.U1024);
        Assert.Equal(4, attribute.V1024);
    }

    [Fact]
    public void Enabled_attributes_require_actual_owners_and_disabled_attributes_remain_unclaimed()
    {
        NativeSoftwareClippedPolygon untextured = Polygon(0, V(-1, 1), V(1, 0), V(1, 2));
        Assert.All(NativeSoftwarePolygonClip.Clip466360(untextured, Bounds).Vertices, vertex =>
        {
            Assert.Null(vertex.U1024);
            Assert.Null(vertex.Red);
        });
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePolygonClip.Clip466360(Polygon(2, untextured.Vertices.ToArray()), Bounds));
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePolygonClip.Clip466360(Polygon(1, untextured.Vertices.ToArray()), Bounds));
        NativeSoftwareClippedPolygon textured = Polygon(2, new(-1, 1, 1024, 2048, null, null, null), new(1, 0, 2048, 3072, null, null, null), new(1, 2, 3072, 4096, null, null, null));
        Assert.All(NativeSoftwarePolygonClip.Clip466360(textured, Bounds).Vertices, vertex =>
        {
            Assert.NotNull(vertex.U1024);
            Assert.NotNull(vertex.V1024);
            Assert.Null(vertex.Red);
        });
    }

    [Fact]
    public void Duplicate_boundary_vertices_and_flat_edges_are_not_deduplicated()
    {
        NativeSoftwareClippedPolygon input = Polygon(0, V(0, 0), V(0, 0), V(5, 0), V(5, 5));
        NativeSoftwareClippedPolygon result = NativeSoftwarePolygonClip.Clip466360(input, Bounds);
        Assert.Equal(input.Vertices.ToArray(), result.Vertices.ToArray());
        Assert.Equal(4, result.Vertices.Length);
        Assert.Empty(NativeSoftwarePolygonClip.Clip466360(Polygon(0, V(-2, 0), V(-1, 1), V(-2, 2)), Bounds).Vertices);
        Assert.Empty(NativeSoftwarePolygonClip.Clip466360(Polygon(3), Bounds).Vertices);
    }

    [Fact]
    public void Fourteen_vertex_capacity_is_checked_on_input_and_each_output_append()
    {
        NativeSoftwareClipVertex[] alternating = Enumerable.Range(0, 14).Select(index => V(index % 2 == 0 ? -1 : 1, index)).ToArray();
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePolygonClip.Clip466360(Polygon(0, alternating), new(0, -100, 100, 100)));
        Assert.Throws<InvalidDataException>(() => NativeSoftwarePolygonClip.Clip466360(Polygon(0, alternating.Append(V(0, 20)).ToArray()), Bounds));
    }

    [Fact]
    public void Admission_retains_direct_raw_identity_and_clips_different_outside_planes_instead_of_rejecting()
    {
        NativeSoftwarePolygonDescriptor inside = Raw(Polygon(0, V(0, 0), V(5, 0)));
        NativeSoftwarePolygonDescriptor? direct = null;
        NativeSoftwareClippedPolygon? clipped = null;
        NativeSoftwarePolygonClip.Submit466880(inside, Bounds, item => direct = item, item => clipped = item);
        Assert.Same(inside, direct);
        Assert.Null(clipped);
        NativeSoftwarePolygonDescriptor mixed = Raw(Polygon(0, V(-2, 5), V(5, -2), V(12, 5)));
        direct = null;
        NativeSoftwarePolygonClip.Submit466880(mixed, Bounds, item => direct = item, item => clipped = item);
        Assert.Null(direct);
        Assert.NotNull(clipped);
        Assert.NotEmpty(clipped!.Vertices);
        Assert.Equal(mixed.Data.Slice(4, 8).ToArray(), clipped.HeaderTail.ToArray());
        Assert.All(clipped.Vertices, vertex => Assert.Equal((byte)0, NativeSoftwarePolygonClip.Outcode466320(vertex.X16_16, vertex.Y, Bounds)));
        NativeSoftwarePolygonClip.Submit466880(Raw(Polygon(0)), Bounds, _ => Assert.Fail("Empty input must not publish."), _ => Assert.Fail("Empty input must not clip."));
        NativeSoftwarePolygonClip.Submit466880(Raw(Polygon(0, V(-3, 1), V(-2, 2), V(-1, 3))), Bounds, _ => Assert.Fail("Common outside plane must reject."), _ => Assert.Fail("Common outside plane must not clip."));
    }

    [Fact]
    public void Consumed_JSON_restores_exact_next_clipping_without_claiming_raw_Z_or_residue()
    {
        NativeSoftwarePolygonDescriptor original = Raw(Polygon(3, new(-2, 4, 100, 200, 300, 400, 500), new(5, -2, 200, 300, 400, 500, 600), new(12, 8, 300, 400, 500, 600, 700)));
        ImmutableArray<byte> before = original.Data;
        NativeSoftwareClippedPolygon first = NativeSoftwarePolygonClip.Clip466360(original, Bounds);
        string json = JsonSerializer.Serialize(first);
        NativeSoftwareClippedPolygon cold = JsonSerializer.Deserialize<NativeSoftwareClippedPolygon>(json)!;
        Assert.Equal(json, JsonSerializer.Serialize(cold));
        Assert.Equal(JsonSerializer.Serialize(NativeSoftwarePolygonClip.Clip466360(first, new(2, 2, 8, 8))), JsonSerializer.Serialize(NativeSoftwarePolygonClip.Clip466360(cold, new(2, 2, 8, 8))));
        Assert.Equal(before, original.Data);
        Assert.DoesNotContain("Residue", json);
        Assert.DoesNotContain("\"Z\"", json);
    }

    [Fact]
    public void Typed_packet_producers_share_raw_admission_decisions_without_fabricating_descriptor_bytes()
    {
        NativeSoftwareClippedPolygon[] cases = [Polygon(0, V(1, 1), V(2, 1), V(2, 2)), Polygon(0, V(-2, 5), V(5, -2), V(12, 5)), Polygon(0, V(-3, 1), V(-2, 2), V(-1, 3)), Polygon(0)];
        foreach (NativeSoftwareClippedPolygon input in cases)
        {
            string rawRoute = "rejected";
            string typedRoute = "rejected";
            NativeSoftwareClippedPolygon? rawOutput = null;
            NativeSoftwareClippedPolygon? typedOutput = null;
            NativeSoftwarePolygonClip.Submit466880(Raw(input), Bounds, polygon =>
            {
                rawRoute = "direct";
                rawOutput = NativeSoftwarePolygonClip.ReadConsumedFields(polygon);
            }, polygon =>
            {
                rawRoute = "clipped";
                rawOutput = polygon;
            });
            NativeSoftwarePolygonClip.Submit466880(input, Bounds, polygon =>
            {
                typedRoute = "direct";
                typedOutput = polygon;
            }, polygon =>
            {
                typedRoute = "clipped";
                typedOutput = polygon;
            });
            Assert.Equal(rawRoute, typedRoute);
            Assert.Equal(JsonSerializer.Serialize(rawOutput), JsonSerializer.Serialize(typedOutput));
            if (typedRoute == "direct")
            {
                Assert.Same(input, typedOutput);
            }
        }
    }

    private static NativeSoftwareClipVertex V(int x, int y) => new(x, y, null, null, null, null, null);
    private static NativeSoftwareClippedPolygon Polygon(byte flags, params NativeSoftwareClipVertex[] vertices) => new([flags, 0xA5, 0xB6, 0xC7, 3, 4, 5, 9], vertices.ToImmutableArray());
    private static NativeSoftwarePolygonDescriptor Raw(NativeSoftwareClippedPolygon polygon)
    {
        byte[] bytes = Enumerable.Repeat((byte)0xCC, 12 + polygon.Vertices.Length * 32).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes, polygon.Vertices.Length);
        polygon.HeaderTail.AsSpan().CopyTo(bytes.AsSpan(4));
        for (int index = 0; index < polygon.Vertices.Length; index++)
        {
            NativeSoftwareClipVertex vertex = polygon.Vertices[index];
            int start = 12 + 32 * index;
            Put(0, vertex.X16_16);
            Put(4, vertex.Y);
            Put(0xC, vertex.U1024);
            Put(0x10, vertex.V1024);
            Put(0x14, vertex.Red);
            Put(0x18, vertex.Green);
            Put(0x1C, vertex.Blue);
            void Put(int offset, int? value)
            {
                if (value is int known)
                {
                    BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(start + offset), known);
                }
            }
        }
        return new(bytes.ToImmutableArray());
    }
}
