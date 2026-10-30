using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTheEnd85NodeDrawingTests
{
    private const uint Context = 0x2793DF0;
    private const uint Node = 0x25BC8A8;
    private const uint Template = 0x17151EC;
    private const uint Resource = 0x156C7FC;

    [Fact]
    public void Mesh_instances_publish_exact_signed_position_DWORDs_and_append_separate_packet_extents()
    {
        Fixture fixture = new(Mesh());
        fixture.Node[472] = 2;
        fixture.Position(412, new(10, 20, 4000));
        fixture.Position(420, new(-30, -40, 5000));
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Context + 8, new byte[] { 3, 5, 7 }).Write(Context + 12, BitConverter.GetBytes(0x12345678u));
        NativeBattleTheEnd85NodeDrawingStep result = NativeBattleTheEnd85NodeDrawing.Draw7897F0(staging, Context, Node, Template, Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, fixture.Services());
        Assert.Equal(6, fixture.Writes.Count);
        Assert.Equal(-30, BinaryPrimitives.ReadInt32LittleEndian(fixture.Node.AsSpan(192)));
        Assert.Equal(-40, BinaryPrimitives.ReadInt32LittleEndian(fixture.Node.AsSpan(196)));
        Assert.Equal(5000, BinaryPrimitives.ReadInt32LittleEndian(fixture.Node.AsSpan(200)));
        Assert.Equal(104, result.Packets.AllocationBytes);
        Assert.Equal(new[] { 0, 52 }, result.Packets.Attempts.Select(attempt => attempt.ByteOffsetRelative));
        Assert.True(result.PublishedFarColor);
        Assert.Equal((uint?)48, result.FarRed);
        Assert.Equal(0x12345678u, Dword(result.Staging, 12));
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Single_instance_keeps_existing_matrix_translation_and_does_not_read_position_array()
    {
        Fixture fixture = new(Mesh());
        fixture.Node[472] = 1;
        NativeBattleTheEnd85NodeDrawingStep result = NativeBattleTheEnd85NodeDrawing.Draw7897F0(NativeBattleFileStaging.Unknown(), Context, Node, Template, Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, fixture.Services());
        Assert.Empty(fixture.Writes);
        Assert.DoesNotContain(fixture.Reads, read => read.Address >= Node + 412 && read.Address < Node + 460);
        Assert.Equal(52, result.Packets.AllocationBytes);
        Assert.True(result.PublishedFarColor);
        Assert.Null(result.FarRed);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void Zero_or_negative_signed_count_retains_context_stores_without_projection_or_far_publication(byte count)
    {
        Fixture fixture = new(Mesh());
        fixture.Node[472] = count;
        NativeBattleTheEnd85NodeDrawingStep result = NativeBattleTheEnd85NodeDrawing.Draw7897F0(NativeBattleFileStaging.Unknown(), Context, Node, Template, Projection(), NativeBattleGteColorInterpolation.CodeConstants45F270, fixture.Services());
        Assert.Empty(result.Packets.Attempts);
        Assert.False(result.PublishedFarColor);
        Assert.Equal(Resource, Dword(result.Staging, 0));
        Assert.Equal(0x30u, Dword(result.Staging, 20));
        Assert.DoesNotContain(fixture.Reads, read => read.Address >= Node + 172 && read.Address < Node + 204);
    }

    [Fact]
    public void Sprite_reuses_original_kernel_and_retains_descriptor_corners_matrix_padding_and_first_average()
    {
        Fixture fixture = new(NativeBattleCuragaProjectedSpritesTests.Resource(true, NativeBattleCuragaProjectedSpritesTests.Quad()).ToArray());
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        foreach (int offset in new[] { 74, 82, 90, 98, 118, 150 })
        {
            staging = staging.Write(Context + (uint)offset, BitConverter.GetBytes((ushort)0xCAFE));
        }
        NativeBattleTheEnd85NodeDrawingStep result = NativeBattleTheEnd85NodeDrawing.Draw789A10(staging, Context, Node, Projection(), 0, fixture.Services());
        Assert.Equal(52, result.Packets.AllocationBytes);
        Assert.False(result.PublishedFarColor);
        Assert.Equal((ushort)0x100, Word(result.Staging, 36));
        Assert.Equal((ushort)1, Word(result.Staging, 38));
        Assert.Equal(Resource + 20, Dword(result.Staging, 44));
        Assert.Equal(1u, Dword(result.Staging, 48));
        Assert.Equal(1000u, Dword(result.Staging, 60));
        Assert.Equal(0u, Dword(result.Staging, 64));
        Assert.Equal(0x808080u, Dword(result.Staging, 28));
        foreach (int offset in new[] { 74, 82, 90, 98, 118 })
        {
            Assert.Equal((ushort)0xCAFE, Word(result.Staging, offset));
        }
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(Context + 150, 2));
        foreach (int offset in new[] { 72, 80, 88, 96 })
        {
            Assert.Equal((ushort)0, Word(result.Staging, offset));
        }
        Assert.Equal(4000, unchecked((int)Dword(result.Staging, 160)));
        Assert.Throws<NotSupportedException>(() => result.Staging.Read(NativeBattleTemporaryMemory.Cursor, 4));
    }

    [Fact]
    public void Rejected_sprite_keeps_unknown_cursor_owner_and_retained_projection_flags_without_average()
    {
        Fixture fixture = new(NativeBattleCuragaProjectedSpritesTests.Resource(false, NativeBattleCuragaProjectedSpritesTests.Quad()).ToArray());
        BinaryPrimitives.WriteInt32LittleEndian(fixture.Node.AsSpan(200), -1000);
        NativeBattleTheEnd85NodeDrawingStep result = NativeBattleTheEnd85NodeDrawing.Draw789A10(NativeBattleFileStaging.Unknown(), Context, Node, Projection(), 0, fixture.Services());
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.Null(Assert.Single(result.Packets.Attempts).Primitive);
        Assert.Equal(uint.MaxValue, Dword(result.Staging, 60));
        Assert.True((Dword(result.Staging, 64) & 0x60000) != 0);
        Assert.Equal((short)-128, unchecked((short)Word(result.Staging, 68)));
    }

    private static uint Dword(NativeBattleFileStaging staging, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(staging.Read(Context + (uint)offset, 4));
    private static ushort Word(NativeBattleFileStaging staging, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(staging.Read(Context + (uint)offset, 2));
    private static NativeBattleCameraProjection Projection() => new(null, 160, 160, 108);

    private sealed class Fixture
    {
        public byte[] Node { get; } = new byte[480];
        private readonly byte[] template = new byte[64];
        private readonly byte[] resource;
        public List<(uint Address, int Count)> Reads { get; } = [];
        public List<(uint Address, int Count)> Writes { get; } = [];

        public Fixture(byte[] resource)
        {
            this.resource = resource;
            BinaryPrimitives.WriteUInt32LittleEndian(Node.AsSpan(368), Resource);
            for (int index = 0; index < 9; index++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(Node.AsSpan(172 + index * 2), (short)(index % 4 == 0 ? 4096 : 0));
            }
            BinaryPrimitives.WriteInt32LittleEndian(Node.AsSpan(200), 4000);
        }

        public NativeBattleTheEnd85DrawServices Services() => new(Read, Write);
        public void Position(int offset, NativeBattleVector value)
        {
            BinaryPrimitives.WriteInt16LittleEndian(Node.AsSpan(offset), value.X);
            BinaryPrimitives.WriteInt16LittleEndian(Node.AsSpan(offset + 2), value.Y);
            BinaryPrimitives.WriteInt16LittleEndian(Node.AsSpan(offset + 4), value.Z);
        }

        private byte[] Read(uint address, int count)
        {
            Reads.Add((address, count));
            if (address >= NativeBattleTheEnd85NodeDrawingTests.Node && address - NativeBattleTheEnd85NodeDrawingTests.Node <= Node.Length - count)
            {
                return Node.AsSpan(checked((int)(address - NativeBattleTheEnd85NodeDrawingTests.Node)), count).ToArray();
            }
            if (address >= Template && address - Template <= template.Length - count)
            {
                return template.AsSpan(checked((int)(address - Template)), count).ToArray();
            }
            if (address >= Resource && address - Resource <= resource.Length - count)
            {
                return resource.AsSpan(checked((int)(address - Resource)), count).ToArray();
            }
            throw new NotSupportedException("Test source address is not retained.");
        }

        private void Write(uint address, ImmutableArray<byte> bytes)
        {
            Writes.Add((address, bytes.Length));
            bytes.AsSpan().CopyTo(Node.AsSpan(checked((int)(address - NativeBattleTheEnd85NodeDrawingTests.Node)), bytes.Length));
        }
    }

    [Fact]
    public void Presentation_node_mesh_authored_instances_keep_source_position_and_packet_write_order()
    {
        (NativeBattleTheEnd85NodeDrawingStep Step, Fixture Fixture) Run(bool capture)
        {
            Fixture fixture = new(Mesh());
            fixture.Node[472] = 2;
            fixture.Position(412, new(10, 20, 4000));
            fixture.Position(420, new(-30, -40, 5000));
            var services = fixture.Services() with { PilotIdentity = capture ? new(85, 1, 2, 3, 4) : null };
            var staging = NativeBattleFileStaging.Unknown().Write(Context + 8, new byte[] { 3, 5, 7 }).Write(Context + 12, BitConverter.GetBytes(0x12345678u));
            return (NativeBattleTheEnd85NodeDrawing.Draw7897F0(staging, Context, Node, Template, new(NativeBattleCuragaMatrices.Identity8DD770(), 160, 160, 108), NativeBattleGteColorInterpolation.CodeConstants45F270, services), fixture);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native.Step);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured.Step));
        Assert.Equal(native.Fixture.Reads, captured.Fixture.Reads);
        Assert.Equal(native.Fixture.Writes.Select(write => write.Address), captured.Fixture.Writes.Select(write => write.Address));
        var geometries = captured.Step.Packets.Attempts.Select(attempt => attempt.Primitive!.PilotGeometry!).ToArray();
        Assert.Equal(2, geometries.Select(geometry => geometry.Identity).Distinct().Count());
        Assert.NotEqual(geometries[0].A, geometries[1].A);
        Assert.All(native.Step.Packets.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
    }

    [Fact]
    public void Presentation_node_sprite_keeps_native_reads_writes_quad_corners_and_actual_payload()
    {
        (NativeBattleTheEnd85NodeDrawingStep Step, Fixture Fixture) Run(bool capture)
        {
            Fixture fixture = new(NativeBattleCuragaProjectedSpritesTests.Resource(true, NativeBattleCuragaProjectedSpritesTests.Quad()).ToArray());
            var staging = NativeBattleFileStaging.Unknown();
            foreach (int offset in new[] { 74, 82, 90, 98, 118, 150 })
            {
                staging = staging.Write(Context + (uint)offset, BitConverter.GetBytes((ushort)0xCAFE));
            }
            var services = fixture.Services() with { PilotIdentity = capture ? new(85, 1, 2, 3, 4) : null };
            var projection = new NativeBattleCameraProjection(NativeBattleCuragaMatrices.Identity8DD770(), 160, 160, 108);
            return (NativeBattleTheEnd85NodeDrawing.Draw789A10(staging, Context, Node, projection, 0, services), fixture);
        }
        var native = Run(false);
        string baseline = NativeBattleTestSnapshots.Json(native.Step);
        var captured = Run(true);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(captured.Step));
        Assert.Equal(native.Fixture.Reads, captured.Fixture.Reads);
        Assert.Equal(native.Fixture.Writes, captured.Fixture.Writes);
        var primitive = Assert.Single(captured.Step.Packets.Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        Assert.Equal(4, primitive.PilotGeometry!.CornerCount);
        var final = Assert.Single(NativeBattleTheEnd85Presentation.Depths(captured.Step.Packets).Attempts, attempt => attempt.Primitive?.PilotGeometry is not null).Primitive!;
        Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), final.PilotGeometry!.SourceDepths);
    }

    [Fact]
    public void Presentation_node_identity_preserves_actual_task_and_each_authored_copy_but_breaks_on_native_row_rebirth()
    {
        var pool = NativeBattleEffectTaskPool.Initialize(0x25C1748, 0x300000, 676, 1);
        (pool, int slot) = pool.Append(0x789700);
        var task = pool.Slots[slot];
        NativeBattleOriginalEffectState effect = new(85, 85, new(85, 0x7844A0, 0x784480), null, null) { Pools = [pool] };
        var frame = new NativeBattleTheEnd85Frame(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, effect, NativeCrtRandomState.FromColdProcess());
        string baseline = NativeBattleTestSnapshots.Json(frame);
        var first = NativeBattleTheEnd85Presentation.NodeIdentity(frame, task, Node, Template, 0);
        Assert.Equal(first, NativeBattleTheEnd85Presentation.NodeIdentity(frame, task, Node, Template, 0));
        Assert.NotEqual(first, NativeBattleTheEnd85Presentation.NodeIdentity(frame, task, Node, Template, 1));
        Assert.NotEqual(first, NativeBattleTheEnd85Presentation.NodeIdentity(frame, task, Node + 672, Template, 0));
        Assert.NotEqual(first, NativeBattleTheEnd85Presentation.NodeIdentity(frame, task, Node, Template + 4, 0));
        NativeBattlePilotVisualLifetimes.Allocated(frame.Memory.Arena!, Node, 0);
        var next = NativeBattleTheEnd85Presentation.NodeIdentity(frame, task, Node, Template, 0);
        Assert.Equal(first.Generation, next.Generation);
        Assert.Equal(first.Primitive, next.Primitive);
        Assert.NotEqual(first.ResourceGeneration, next.ResourceGeneration);
        Assert.Equal(baseline, NativeBattleTestSnapshots.Json(frame));
    }

    private static byte[] Mesh()
    {
        byte[] bytes = new byte[108];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 40);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 4);
        NativeBattleVector[] points = [new(-100, -100, 0), new(100, -100, 0), new(-100, 100, 0), new(100, 100, 0)];
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + corner * 8), points[corner].X);
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + corner * 8), points[corner].Y);
        }
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(68), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x3C808080);
        for (int corner = 0; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(76 + corner * 2), (ushort)(corner * 2));
        }
        for (int corner = 1; corner < 4; corner++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(92 + corner * 4), 0x3C808080);
        }
        return bytes;
    }
}
