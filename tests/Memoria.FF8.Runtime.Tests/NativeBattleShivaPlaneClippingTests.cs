using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaPlaneClippingTests
{
    [Fact]
    public void Plane_constructor_wraps_its_integer_dot_but_distance_uses_native_rounding_and_IR_clamp()
    {
        NativeBattleShivaPlane plane = NativeBattleShivaPlaneClipping.CreatePlane5C3130(new(1, 2, 3), new(0, -4096, 0));
        Assert.Equal(new NativeBattleShivaPlane(0, -4096, 0, 2), plane);
        Assert.Equal((short)0, NativeBattleShivaPlaneClipping.Distance460860(new(1, 2, 3), plane));
        Assert.Equal(short.MaxValue, NativeBattleShivaPlaneClipping.Distance460860(new(0, short.MinValue, 0), plane));
        Assert.Equal((short)0, NativeBattleShivaPlaneClipping.Distance460860(new(-1, 0, 0), new(1, 0, 0, 0)));
        Assert.Equal((short)48, NativeBattleShivaPlaneClipping.CreatePlane5C3130(new(32767, 32767, 32767), new(32767, 32767, 32767)).W);
    }

    [Fact]
    public void Edge_intersection_preserves_packed_UV_math_and_the_native_zero_denominator_substitution()
    {
        NativeBattleShivaClipVertex intersection = NativeBattleShivaPlaneClipping.Intersect5C35F0(new(new(-10, -10, 0), 0x1020), new(new(10, 10, 0), 0x3040), new(0, -4096, 0, 0));
        Assert.Equal(new NativeBattleShivaClipVertex(new(0, 0, 0), 0x2030), intersection);
        NativeBattleShivaClipVertex parallel = NativeBattleShivaPlaneClipping.Intersect5C35F0(new(new(1, 2, 3), 0), new(new(2, 4, 6), 0), new(0, 0, 0, 1));
        Assert.Equal(new NativeBattleVector(0, 0, 0), parallel.Position);
    }

    [Theory]
    [InlineData(-100, -100, -100, 0)]
    [InlineData(100, 100, 100, 3)]
    [InlineData(100, -100, -100, 3)]
    [InlineData(-100, 100, -100, 3)]
    [InlineData(-100, -100, 100, 3)]
    [InlineData(100, -100, 100, 4)]
    [InlineData(-100, 100, 100, 4)]
    [InlineData(100, 100, -100, 4)]
    public void Actual_selector_cases_keep_the_native_retained_side_and_triangle_or_quad_winding(short first, short second, short third, int expectedCount)
    {
        ImmutableArray<NativeBattleShivaClipVertex> result = NativeBattleShivaPlaneClipping.Clip5C34B0(Triangle(first, second, third), new(0, -4096, 0, 0), Selectors());
        Assert.Equal(expectedCount, result.Length);
        Assert.All(result, vertex => Assert.True(vertex.Position.Y <= 0));
        if (first == 100 && second == -100 && third == -100)
        {
            Assert.Equal(new ushort[] { 0, 0x0020, 0x2000 }, result.Select(vertex => vertex.Uv));
        }
        if (first == 100 && second == -100 && third == 100)
        {
            Assert.Equal(new ushort[] { 0, 0x0020, 0x4000, 0x2020 }, result.Select(vertex => vertex.Uv));
        }
    }

    [Fact]
    public void Zero_endpoint_asymmetry_preserves_native_duplicate_output_vertices_without_simplification()
    {
        ImmutableArray<NativeBattleShivaClipVertex> result = NativeBattleShivaPlaneClipping.Clip5C34B0(Triangle(-100, 0, 0), new(0, -4096, 0, 0), Selectors());
        Assert.Equal(3, result.Length);
        Assert.Equal(result[0], result[1]);
        Assert.Equal(new ushort[] { 0x40, 0x40, 0x4000 }, result.Select(vertex => vertex.Uv));
    }

    [Fact]
    public void Model_local_MVMVA_clamps_XYZ_and_writes_only_six_bytes_of_each_actual_workspace_record()
    {
        NativeBattleEffectArena arena = NativeBattleEffectArena.Clear().MarkUnknown(0x33000, 16);
        NativeBattleRawMesh mesh = new([new(new(100, -100, 5), 0), new(new(-100, 100, -5), 0)], []);
        NativeBattleActionMatrix matrix = Matrix(40000, -40000, 3);
        arena = NativeBattleShivaPlaneClipping.TransformVertices5C50D0(arena, mesh, [matrix]);
        byte[] first = arena.Read(0x33000, 6);
        Assert.Equal(short.MaxValue, BinaryPrimitives.ReadInt16LittleEndian(first));
        Assert.Equal(short.MinValue, BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(2)));
        Assert.Equal((short)8, BinaryPrimitives.ReadInt16LittleEndian(first.AsSpan(4)));
        Assert.Throws<NotSupportedException>(() => arena.Read(0x33006, 2));
        Assert.Throws<NotSupportedException>(() => arena.Read(0x3300E, 2));
        NativeBattleActionMatrix fractional = Matrix(1, 0, 0) with { Rotation = [2048, 0, 0, 0, 4096, 0, 0, 0, 4096] };
        NativeBattleEffectArena rounded = NativeBattleShivaPlaneClipping.TransformVertices5C50D0(arena, new([new(new(1, 0, 0), 0)], []), [fractional]);
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(rounded.Read(0x33000, 2)));
    }

    [Theory]
    [InlineData(false, 32, 110)]
    [InlineData(true, 40, 79)]
    public void Clipped_output_emits_without_any_geometric_retest_and_preserves_the_last_unwritten_high_word(bool quad, int packetBytes, int bucket)
    {
        ImmutableArray<NativeBattleShivaClipVertex> vertices = quad
            ? [new(new(-20000, 0, 0), 0x123), new(new(-20000, 10, 0), 0x456), new(new(-20000, 20, 0), 0x789), new(new(-20000, 30, 0), 0xABC)]
            : [new(new(-20000, 0, 0), 0x123), new(new(-20000, 10, 0), 0x456), new(new(-20000, 20, 0), 0x789)];
        NativeBattleGfGeometryAttempt attempt = NativeBattleShivaPlaneClipping.Emit5C3410(vertices, Matrix(0, 0, 1000), Projection(), 0x00808080, 0xAABB0000, 0xCCDD0000, 0x200, 0x100, 0, 0);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(packetBytes, attempt.PacketLength);
        Assert.Equal(bucket, attempt.Primitive.Bucket);
        Assert.Equal(quad ? 0x2C808080u : 0x24808080u, attempt.Primitive.Words[1]);
        Assert.Equal(0xAABB0123u, attempt.Primitive.Words[3]);
        Assert.Equal(0xCCDD0456u, attempt.Primitive.Words[5]);
        for (int offset = 0; offset < packetBytes; offset++)
        {
            bool unwritten = offset is 30 or 31 || quad && offset is 38 or 39;
            Assert.Equal(unwritten ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, attempt.WriteMask[offset]);
        }
        Assert.Equal(attempt.WriteMask.ToArray(), attempt.Primitive.PhysicalWriteMask!.Value.ToArray());
    }

    [Fact]
    public void First_quad_triangle_gate_skips_both_halves_but_the_second_triangle_has_no_independent_gate()
    {
        var fixture = Model(true, false);
        NativeBattleShivaPlane plane = new(0, 0, 4096, 5000);
        NativeBattleShivaPlaneDraw result = NativeBattleShivaPlaneClipping.Draw5C5060(fixture.Arena, fixture.Model, fixture.Resource, Matrix(0, 0, 1000), Projection(), plane, Selectors(), 0x155, 0x100, 0, 0);
        Assert.Equal(2, result.Batch.Attempts.Length);
        Assert.Equal(64, result.Batch.AllocationBytes);
        Assert.Equal(0, result.Batch.Attempts[1].Primitive!.VertexPayload4[0]);
        Assert.Equal(4, result.VerticesWritten);
        Assert.Throws<NotSupportedException>(() => result.Arena.Read(0x33006, 2));
        var rejected = Model(true, true);
        NativeBattleShivaPlaneDraw skipped = NativeBattleShivaPlaneClipping.Draw5C5060(rejected.Arena, rejected.Model, rejected.Resource, Matrix(0, 0, 1000), Projection(), plane, Selectors(), 0x155, 0x100, 0, 0);
        Assert.Empty(skipped.Batch.Attempts);
        Assert.Equal(0, skipped.Batch.AllocationBytes);
    }

    [Fact]
    public void Plane_sampling_uses_model_local_vertices_before_the_separate_world_view_projection()
    {
        var fixture = Model(false, false);
        byte[] model = fixture.Model.Model.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(model.AsSpan(0x58), -50);
        NativeBattleShivaPlaneDraw result = NativeBattleShivaPlaneClipping.Draw5C5060(fixture.Arena, fixture.Model with { Model = model.ToImmutableArray() }, fixture.Resource, Matrix(0, 500, 1000), Projection(), new(0, -4096, 0, 0), Selectors(), 0x155, 0x100, 0, 0);
        Assert.Single(result.Batch.Attempts);
        Assert.Equal(40, result.Batch.AllocationBytes);
        Assert.Equal((short)-150, BinaryPrimitives.ReadInt16LittleEndian(result.Arena.Read(0x33002, 2)));
    }

    private static ImmutableArray<NativeBattleShivaClipVertex> Triangle(short first, short second, short third)
    {
        return [new(new(-100, (short)-first, 0), 0), new(new(100, (short)-second, 0), 0x40), new(new(0, (short)-third, 100), 0x4000)];
    }

    private static ImmutableArray<int> Selectors()
    {
        return [0, 1, 2, 0, 0, 1, 2, 0, 0, 3, 2, 3, 3, 1, 2, 0, 0, 1, 4, 4, 0, 4, 2, 0, 3, 1, 4, 4, 0, 3, 2, 4, 5, 1, 2, 5, 0, 1, 5, 0, 0, 3, 5, 3, 5, 3, 2, 1, 5, 4, 2, 5, 0, 1, 5, 4];
    }

    private static (NativeBattleEffectArena Arena, NativeBattleCactuarRawModelState Model, NativeBattleEffectOwnedModelResource Resource) Model(bool quad, bool rejectFirst)
    {
        byte[] bytes = new byte[104];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 4);
        int[] sections = [24, 88, 96, 100, 104];
        for (int index = 0; index < sections.Length; index++)
        {
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4 + index * 4), sections[index]);
        }
        bytes[24] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(26), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), -1);
        NativeBattlePoseSource poseSource = new(bytes.AsSpan(24, 64).ToArray().ToImmutableArray(), [1]);
        byte[] face = new byte[quad ? 20 : 16];
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(4), 2);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 3);
            BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(16), 0x00440033);
        }
        else
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(6), 0x33);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(8), 0xAAAA0011);
        BinaryPrimitives.WriteUInt32LittleEndian(face.AsSpan(12), 0xBBBB0022);
        ImmutableArray<NativeBattleRawVertex> vertices = quad
            ? [new(new(-100, -100, rejectFirst ? (short)-2000 : (short)0), 0), new(new(100, -100, 0), 0), new(new(-100, 100, 0), 0), new(new(100, -100, -2000), 0)]
            : [new(new(-100, -100, 0), 0), new(new(100, -100, 0), 0), new(new(-100, 100, 0), 0)];
        NativeBattleEffectOwnedModelResource resource = new(NativeBattleEffectNodeMemory.ArenaAddress, bytes.ToImmutableArray(), poseSource, [new(vertices, [new(quad, face.ToImmutableArray())])]);
        NativeBattleCactuarRawModelState model = NativeBattleCactuarRawModel.Initialize5A8F40(NativeBattleCactuarRawModelState.FromColdProcess(), resource, NativeBattleEffectNodeMemory.ArenaAddress, 3, NativeBattleShivaModelTask.Binding);
        byte[] modelBytes = model.Model.ToArray();
        BinaryPrimitives.WriteInt16LittleEndian(modelBytes.AsSpan(0x40), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(modelBytes.AsSpan(0x48), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(modelBytes.AsSpan(0x50), 4096);
        model = model with { Model = modelBytes.ToImmutableArray(), DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024))], false) };
        return (NativeBattleEffectArena.Clear().MarkUnknown(0x33000, vertices.Length * 8), model, resource);
    }

    private static NativeBattleActionMatrix Matrix(int x, int y, int z)
    {
        return new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], x, y, z, 4096);
    }

    private static NativeBattleCameraProjection Projection()
    {
        return new(Matrix(0, 0, 0), 216, 160, 108);
    }
}
