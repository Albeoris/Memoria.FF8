using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHostedModelPacketsTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Linked_store_publishes_known_link_bytes_regardless_of_the_original_mask(bool changePrefix)
    {
        NativeBattleGeometryPrimitive triangle = Triangle(0x80);
        ImmutableArray<NativeBattleGfPacketByteState> original = triangle.PhysicalWriteMask!.Value;
        ImmutableArray<NativeBattleGfPacketByteState> input = changePrefix
            ? original.SetItem(0, NativeBattleGfPacketByteState.Unknown).SetItem(2, NativeBattleGfPacketByteState.Untouched)
            : original;
        NativeBattleHostedModelPackets packets = new();
        NativeBattlePrimitiveProcess process = Process();
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        int previousRoot = ordering.Table[triangle.Bucket].Index;
        uint link = checked(0x1D8E058u + 4u * (uint)previousRoot);
        ordering = packets.Append(process, ordering, [triangle with { PhysicalWriteMask = input }]);
        NativeBattlePrimitiveReference reference = Assert.Single(ordering.Packets).Reference;
        Assert.Equal(new[] { (byte)link, (byte)(link >> 8), (byte)(link >> 16), (byte)(triangle.Words[0] >> 24) }, packets.Read(reference, 4));
        if (changePrefix)
        {
            Assert.Equal(NativeBattleGfPacketByteState.Unknown, input[0]);
            Assert.Equal(NativeBattleGfPacketByteState.Untouched, input[2]);
        }
    }

    [Theory]
    [InlineData("off_B6D094", "hosted-model-094", (byte)1, 147456)]
    [InlineData("off_B6D070", "hosted-model-070", (byte)0, 229376)]
    public void Hosted_bodies_exceed_the_native_COMMON_capacity_without_losing_authored_commands(string nativeBank, string hostedBank, byte buffer, int nativeCapacity)
    {
        NativeBattleHostedModelPackets packets = new();
        NativeBattlePrimitiveProcess process = Process() with { PacketBank = nativeBank, PacketBuffer = buffer };
        Assert.Equal(nativeCapacity, process.CommonPacketWriteCapacity);
        NativeBattleModelTextureOwner material = new("extended-actor");
        NativeBattleGeometryPrimitive triangle = Triangle(0x80) with { PresentationActorId = "extended-actor", ModelTextureOwner = material };
        int byteLength = triangle.Words.Length * sizeof(uint);
        int count = nativeCapacity / byteLength + 1;
        NativeBattleLogicalOrdering ordering = packets.Append(process, NativeBattleLogicalOrdering.Clear(), Repeat(triangle, count), 19, "replacement");

        Assert.Equal(count * byteLength, packets.GetCursor(process));
        Assert.Equal(count, ordering.ProxyCursor);
        Assert.True(ordering.ProxyCursor < NativeBattlePrimitiveOrdering.ProxyCapacity);
        Assert.Equal(0, process.ByteCursor);
        Assert.Null(process.CommonPacketMemory);
        ImmutableArray<NativeBattleLogicalCommand> commands = ordering.Traverse(Lengths(), (_, _) => throw new InvalidOperationException("Current authored packets should not read their physical padding."));
        Assert.Equal(count, commands.Length);
        Assert.Equal(new(hostedBank, buffer, nativeCapacity), commands[0].Packet);
        Assert.Equal(new(hostedBank, buffer, 0), commands[^1].Packet);
        Assert.Equal(triangle.Words.Skip(1), commands[0].Words);
        Assert.Equal(triangle.VertexPayload4, commands[0].VertexPayload4);
        Assert.NotNull(triangle.ProjectedCoordinates);
        Assert.Equal(triangle.ProjectedCoordinates, commands[0].ProjectedCoordinates);
        Assert.Equal(triangle.PresentationActorId, commands[0].PresentationActorId);
        Assert.Equal(material, commands[0].ModelTextureOwner);
        Assert.Equal(19, commands[0].ModuleDraw);
        Assert.Equal("replacement", commands[0].TextureSourceOwner);
        Assert.True(NativeBattleHostedModelPackets.IsHostedReference(commands[0].Packet));
    }

    [Fact]
    public void Frozen_payload_pages_preserve_linked_bytes_order_and_retained_commands()
    {
        NativeBattleModelPrimitiveBatch.Builder builder = new();
        int emission = builder.BeginEmission(new("extended-actor", null, null, default, false, false));
        NativeBattleGeometryPrimitive[] originals = Enumerable.Range(0, 130).Select(index => Triangle((ushort)(0x80 + index))).ToArray();
        foreach (NativeBattleGeometryPrimitive primitive in originals)
        {
            builder.AddRaw(primitive.Bucket, primitive.Words.AsSpan(), primitive.VertexPayload4.AsSpan(), primitive.ProjectedCoordinates!.Value.AsSpan(),
                primitive.PhysicalWriteMask!.Value, default, emission);
        }
        NativeBattleModelPrimitiveBatch batch = builder.Freeze();

        NativeBattleHostedModelPackets packets = new();
        NativeBattlePrimitiveProcess process = Process();
        NativeBattleLogicalOrdering first = packets.Append(process, NativeBattleLogicalOrdering.Clear(), batch);
        NativeBattleLogicalOrdering fork = NativeBattleStorageSnapshots.Fork(first);
        ImmutableArray<NativeBattleLogicalCommand> commands = first.Traverse(Lengths());
        Assert.Equal(originals.Length, commands.Length);

        for (int index = 0; index < commands.Length; index++)
        {
            int source = originals.Length - 1 - index;
            NativeBattleGeometryPrimitive expected = originals[source];
            NativeBattleLogicalCommand command = commands[index];
            Assert.Equal(source * expected.Words.Length * sizeof(uint), command.Packet.ByteOffset);
            Assert.True(expected.Words.AsSpan().Slice(1, command.WordSpan.Length).SequenceEqual(command.WordSpan));
            Assert.True(expected.VertexPayload4.AsSpan().SequenceEqual(command.VertexPayloadSpan));
            Assert.True(expected.ProjectedCoordinates!.Value.AsSpan().SequenceEqual(command.ProjectedCoordinateSpan));
            Assert.Equal("extended-actor", command.PresentationActorId);

            int authoredBytes = expected.Words.Length * sizeof(uint) - sizeof(ushort);
            byte[] actual = packets.Read(command.Packet, authoredBytes);
            for (int offset = 3; offset < authoredBytes; offset++)
            {
                uint word = expected.Words[offset / sizeof(uint)];
                Assert.Equal((byte)(word >> (8 * (offset % sizeof(uint)))), actual[offset]);
            }
        }

        packets.ResetFrame();
        _ = packets.Append(process, NativeBattleLogicalOrdering.Clear(), [Triangle(0x40)]);
        ImmutableArray<NativeBattleLogicalCommand> continued = fork.Traverse(Lengths());
        Assert.Equal(commands.Length, continued.Length);
        for (int index = 0; index < commands.Length; index++)
        {
            Assert.True(commands[index].WordSpan.SequenceEqual(continued[index].WordSpan));
            Assert.True(commands[index].VertexPayloadSpan.SequenceEqual(continued[index].VertexPayloadSpan));
            Assert.True(commands[index].ProjectedCoordinateSpan.SequenceEqual(continued[index].ProjectedCoordinateSpan));
        }
        Assert.Equal(originals[^1].Words.Skip(1), commands[0].Words);
        Assert.True(originals[^1].ProjectedCoordinates!.Value.AsSpan().SequenceEqual(commands[0].ProjectedCoordinates!.Value.AsSpan()));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Rejected_shadow_tail_matches_native_and_reset_preserves_physical_links_for_the_native_pass(bool gouraud)
    {
        NativeBattleHostedModelPackets packets = new();
        NativeBattlePrimitiveProcess process = Process();
        NativeBattleGfGeometryBatch batch = Shadow(gouraud, rejected: true);
        NativeBattleCommonPacketWrite native = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), batch);
        NativeBattleLogicalOrdering ordering = packets.Append(process, NativeBattleLogicalOrdering.Clear(1), batch, moduleDraw: 9);
        NativeBattlePrimitiveReference first = new("hosted-model-070", 0, 0);
        NativeBattlePrimitiveReference second = first with { ByteOffset = 8 };
        NativeBattlePrimitiveReference tail = first with { ByteOffset = 16 };
        Assert.Equal(8, batch.Attempts.Count(attempt => attempt.Primitive is null));
        Assert.Equal(16, packets.GetCursor(process));
        Assert.Equal(new[] { first, second }, ordering.Proxies.Select(proxy => proxy.Packet));
        Assert.Equal(native.Process.ReadCommonPacket(16, 20), packets.Read(tail, 20));
        Assert.Throws<NotSupportedException>(() => packets.Read(tail with { ByteOffset = 36 }, 1));

        ordering = ordering.ResetAfterDraw45D080().SelectTable(0);
        packets.ResetFrame();
        Assert.Equal(0, packets.GetCursor(process));
        Assert.Equal(native.Process.ReadCommonPacket(16, 20), packets.Read(tail, 20));
        Assert.Empty(ordering.Traverse(Lengths(), packets.Read));
        ordering = ordering.SelectTable(1);
        Assert.All(ordering.Packets, packet => Assert.True(packet.RequiresPhysicalRead));
        List<NativeBattlePrimitiveReference> reads = [];
        var result = NativeBattleLogicalPasses.Execute(ordering, PassTables(0xE1), new(0), 0, readPhysical: Read);
        Assert.Equal(new[] { second, second, first, first }, reads);
        Assert.Equal(new[] { 0xE1000640u, 0xE1000620u }, result.Commands.Select(command => Assert.Single(command.Command.Words)));
        Assert.All(result.Commands, command =>
        {
            Assert.Equal(1, command.Pass);
            Assert.Equal(9, command.Command.ModuleDraw);
        });

        byte[] Read(NativeBattlePrimitiveReference reference, int count)
        {
            reads.Add(reference);
            return packets.Read(reference, count);
        }
    }

    [Fact]
    public void Four_hosted_owners_reuse_bounded_storage_without_touching_native_COMMON()
    {
        NativeBattleHostedModelPackets packets = new();
        NativeBattlePrimitiveProcess native = Process().ApplyCommonWrite(0, [11, 22, 33, 44], [NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known, NativeBattleGfPacketByteState.Known]) with { ByteCursor = 4 };
        NativeBattlePrimitiveProcess[] processes =
        [
            native,
            native with { PacketBuffer = 1 },
            native with { PacketBank = "off_B6D094" },
            native with { PacketBank = "off_B6D094", PacketBuffer = 1 }
        ];
        ImmutableArray<NativeBattleGeometryPrimitive>[] batches = Enumerable.Range(0, processes.Length).Select(index => Repeat(Triangle((ushort)(0x80 + index)), 32 * (index + 1))).ToArray();
        NativeBattlePrimitiveReference[] starts =
        [
            new("hosted-model-070", 0, 0),
            new("hosted-model-070", 1, 0),
            new("hosted-model-094", 0, 0),
            new("hosted-model-094", 1, 0)
        ];
        long firstAllocation = 0;
        long firstStored = 0;
        for (int frame = 0; frame < 20; frame++)
        {
            NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
            for (int index = 0; index < processes.Length; index++)
            {
                Assert.Equal(0, packets.GetCursor(processes[index]));
                int proxy = ordering.ProxyCursor!.Value;
                ordering = packets.Append(processes[index], ordering, batches[index]);
                Assert.Equal(starts[index], ordering.Proxies[proxy].Packet);
            }
            for (int index = 0; index < processes.Length; index++)
            {
                Assert.Equal(batches[index].Length * 32, packets.GetCursor(processes[index]));
                Assert.Equal(0x80 + index, BinaryPrimitives.ReadUInt16LittleEndian(packets.Read(starts[index] with { ByteOffset = 22 }, 2)));
                Assert.Equal(4, processes[index].ByteCursor);
            }
            if (frame == 0)
            {
                firstAllocation = packets.AllocatedBytes;
                firstStored = packets.StoredBytes;
                Assert.InRange(firstStored, 1, 10240);
                Assert.InRange(firstAllocation, firstStored, 20480);
            }
            else
            {
                Assert.Equal(firstAllocation, packets.AllocatedBytes);
                Assert.Equal(firstStored, packets.StoredBytes);
            }
            packets.ResetFrame();
        }
        Assert.Equal(new byte[] { 11, 22, 33, 44 }, native.ReadCommonPacket(0, 4));
        Assert.False(NativeBattleHostedModelPackets.IsHostedReference(new("off_B6D070", 0, 0)));
    }

    [Fact]
    public void Untouched_body_padding_keeps_prior_known_bytes_and_never_invents_fresh_values()
    {
        NativeBattleHostedModelPackets fresh = new();
        NativeBattleHostedModelPackets reused = new();
        NativeBattlePrimitiveProcess process = Process();
        NativeBattlePrimitiveReference padding = new("hosted-model-070", 0, 30);
        NativeBattleGeometryPrimitive triangle = Triangle(0x80);
        fresh.Append(process, NativeBattleLogicalOrdering.Clear(), [triangle]);
        Assert.Throws<NotSupportedException>(() => fresh.Read(padding, 2));

        reused.Append(process, NativeBattleLogicalOrdering.Clear(), Shadow(gouraud: false, rejected: true));
        byte[] prior = reused.Read(padding, 2);
        Assert.Contains(prior, value => value != 0);
        reused.ResetFrame();
        NativeBattleLogicalOrdering ordering = reused.Append(process, NativeBattleLogicalOrdering.Clear(), [triangle]);
        Assert.Equal(prior, reused.Read(padding, 2));
        reused.ResetFrame();
        ordering = ordering.ResetAfterDraw45D080();
        var result = NativeBattleLogicalPasses.Execute(ordering, PassTables(0x24), new(0), 0, readPhysical: reused.Read);
        NativeBattleLogicalCommandReference command = Assert.Single(result.Commands).Command;
        Assert.Equal(triangle.Words.Skip(1).Take(6), command.Words.Take(6));
        Assert.Equal(triangle.Words[^1] | (uint)BinaryPrimitives.ReadUInt16LittleEndian(prior) << 16, command.Words[^1]);
        Assert.Equal(triangle.ProjectedCoordinates, command.ProjectedCoordinates);
    }

    [Fact]
    public void Invalid_trailing_primitive_mask_and_range_leave_the_active_bytes_cursor_and_OT_unchanged()
    {
        NativeBattleHostedModelPackets packets = new();
        NativeBattlePrimitiveProcess process = Process();
        NativeBattleGfGeometryBatch batch = Shadow(gouraud: true, rejected: true);
        NativeBattleLogicalOrdering ordering = packets.Append(process, NativeBattleLogicalOrdering.Clear(), batch);
        NativeBattlePrimitiveReference reference = new("hosted-model-070", 0, 0);
        byte[] original = packets.Read(reference, 36);
        NativeBattleGeometryPrimitive valid = Triangle(0x80);
        NativeBattleGeometryPrimitive truncated = valid with { Words = valid.Words.RemoveAt(valid.Words.Length - 1) };
        NativeBattleGeometryPrimitive badMask = valid with { PhysicalWriteMask = valid.PhysicalWriteMask!.Value.SetItem(4, (NativeBattleGfPacketByteState)255) };
        NativeBattleGfGeometryBatch badRange = batch with { Attempts = batch.Attempts.SetItem(batch.Attempts.Length - 1, batch.Attempts[^1] with { ByteOffsetRelative = int.MaxValue & ~3 }) };
        NativeBattleLogicalPacket[] originalPackets = ordering.Packets.ToArray();
        NativeBattleLogicalProxy[] originalProxies = ordering.Proxies.ToArray();
        NativeBattlePrimitiveLink[] originalRoots = ordering.Table.ToArray();
        long allocated = packets.AllocatedBytes;
        long stored = packets.StoredBytes;

        Assert.Throws<InvalidDataException>(() => packets.Append(process, ordering, [valid, truncated]));
        AssertUnchanged();
        Assert.Throws<InvalidDataException>(() => packets.Append(process, ordering, [valid, badMask]));
        AssertUnchanged();
        Assert.Throws<InvalidDataException>(() => packets.Append(process, ordering, [valid, valid with { Bucket = int.MaxValue }]));
        AssertUnchanged();
        Assert.Throws<InvalidDataException>(() => packets.Append(process, ordering, badRange));
        AssertUnchanged();

        void AssertUnchanged()
        {
            Assert.Equal(16, packets.GetCursor(process));
            Assert.Equal(allocated, packets.AllocatedBytes);
            Assert.Equal(stored, packets.StoredBytes);
            Assert.Equal(original, packets.Read(reference, original.Length));
            Assert.Equal(2, ordering.ProxyCursor);
            Assert.Equal(originalPackets, ordering.Packets.ToArray());
            Assert.Equal(originalProxies, ordering.Proxies.ToArray());
            Assert.Equal(originalRoots, ordering.Table.ToArray());
        }
    }

    [Fact]
    public void Prepared_raw_batch_rolls_back_a_late_ordering_failure_and_keeps_prior_bytes_and_holes()
    {
        NativeBattleHostedModelPackets packets = new();
        NativeBattlePrimitiveProcess process = Process();
        NativeBattleGeometryPrimitive original = Triangle(0x80);
        NativeBattleGeometryPrimitive changed = Triangle(0x81);
        packets.Append(process, NativeBattleLogicalOrdering.Clear(), Raw(original, original));
        packets.ResetFrame();
        NativeBattleLogicalOrdering ordering = NativeBattleLogicalOrdering.Clear();
        string originalOrdering = JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering));
        NativeBattleHostedModelPackets.Snapshot before = packets.Capture();
        string originalPackets = JsonSerializer.Serialize(before);
        NativeBattlePrimitiveReference body = new("hosted-model-070", 0, 0);
        byte[] bytes = packets.Read(body, 30);
        Assert.Throws<NotSupportedException>(() => packets.Read(body with { ByteOffset = 30 }, 2));

        NativeBattleModelPrimitiveBatch rejected = Raw(changed, changed with { Bucket = int.MaxValue });
        Assert.Throws<InvalidDataException>(() => packets.Append(process, ordering, rejected));
        Assert.Equal(originalOrdering, JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(ordering)));
        Assert.Equal(originalPackets, JsonSerializer.Serialize(packets.Capture()));
        Assert.Equal(bytes, packets.Read(body, 30));
        Assert.Throws<NotSupportedException>(() => packets.Read(body with { ByteOffset = 30 }, 2));

        NativeBattleLogicalOrdering repair = packets.Append(process, ordering, Raw(changed, changed));
        Assert.Equal(2, repair.ProxyCursor);
        Assert.False(bytes.AsSpan().SequenceEqual(packets.Read(body, 30)));
        Assert.Throws<NotSupportedException>(() => packets.Read(body with { ByteOffset = 30 }, 2));
        NativeBattleHostedModelPackets fork = NativeBattleHostedModelPackets.Restore(packets.Capture());
        Assert.Equal(packets.Read(body, 30), fork.Read(body, 30));
        Assert.Throws<NotSupportedException>(() => fork.Read(body with { ByteOffset = 30 }, 2));

        static NativeBattleModelPrimitiveBatch Raw(NativeBattleGeometryPrimitive first, NativeBattleGeometryPrimitive second)
        {
            NativeBattleModelPrimitiveBatch.Builder builder = new();
            int emission = builder.BeginEmission(new("extended-actor", null, null, default, false, false));
            Append(first);
            Append(second);
            return builder.Freeze();

            void Append(NativeBattleGeometryPrimitive value)
            {
                builder.AddRaw(value.Bucket, value.Words.AsSpan(), value.VertexPayload4.AsSpan(), value.ProjectedCoordinates!.Value.AsSpan(), value.PhysicalWriteMask!.Value, default, emission);
            }
        }
    }

    [Fact]
    public void Append_rejects_reaching_the_native_proxy_ceiling_before_writing_any_packet()
    {
        NativeBattleHostedModelPackets packets = new();
        NativeBattlePrimitiveProcess process = Process();
        NativeBattleGeometryPrimitive triangle = Triangle(0x80);
        int finalRenderableProxyCount = NativeBattlePrimitiveOrdering.ProxyCapacity - 1;
        NativeBattleLogicalOrdering ordering = packets.Append(process, NativeBattleLogicalOrdering.Clear(), Repeat(triangle, finalRenderableProxyCount));
        NativeBattlePrimitiveReference last = ordering.Proxies[^1].Packet;
        int cursor = packets.GetCursor(process);
        byte[] original = packets.Read(last, 30);
        NativeBattlePrimitiveLink root = ordering.Table[triangle.Bucket];
        long allocated = packets.AllocatedBytes;
        long stored = packets.StoredBytes;

        Assert.Throws<InvalidOperationException>(() => packets.Append(process, ordering, [triangle]));

        Assert.Equal(cursor, packets.GetCursor(process));
        Assert.Equal(allocated, packets.AllocatedBytes);
        Assert.Equal(stored, packets.StoredBytes);
        Assert.Equal(original, packets.Read(last, original.Length));
        Assert.Throws<NotSupportedException>(() => packets.Read(last with { ByteOffset = cursor }, 1));
        Assert.Equal(finalRenderableProxyCount, ordering.ProxyCursor);
        Assert.Equal(finalRenderableProxyCount, ordering.Proxies.Length);
        Assert.Equal(finalRenderableProxyCount, ordering.Packets.Length);
        Assert.Equal(root, ordering.Table[triangle.Bucket]);
    }

    private static ImmutableArray<NativeBattleGeometryPrimitive> Repeat(NativeBattleGeometryPrimitive primitive, int count)
    {
        return Enumerable.Repeat(primitive, count).ToImmutableArray();
    }

    private static NativeBattlePrimitiveProcess Process()
    {
        return NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400();
    }

    private static NativeBattleGeometryPrimitive Triangle(ushort page)
    {
        byte[] polygon = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(polygon, 0x8000);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(polygon.AsSpan(14), page);
        NativeBattleRawMesh mesh = new([new(new(-10, -10, 1000), 0), new(new(10, -10, 1000), 0), new(new(0, 10, 1000), 0)], [new(false, polygon.ToImmutableArray())]);
        return Assert.Single(NativeBattleRawGeometryPackets.Emit5099D0([mesh], [Identity], new(Identity, 1000, 160, 108), 0x808080, 1, null, 17, 4, null, viewport: new(-100, -100, 420, 316)));
    }

    private static NativeBattleGfGeometryBatch Shadow(bool gouraud, bool rejected)
    {
        NativeBattleModuleProjectedVertex[] points = [Point(0, 0), Point(-8, -8), Point(8, -8), Point(16, 0), Point(8, 8), Point(0, 16), Point(-8, 8), Point(-16, 0), Point(-8, -8), Point(-8, -8)];
        if (rejected)
        {
            points[0] = points[0] with { Depth = 303 };
        }
        return NativeBattleShadowPackets.EmitBatch5088A0(points, 0x32A1C3E5, gouraud, 17, 16);

        static NativeBattleModuleProjectedVertex Point(short x, short y)
        {
            return new(unchecked((ushort)x) | unchecked((uint)y << 16), 1003, 0);
        }
    }

    private static ImmutableArray<byte> Lengths()
    {
        return new byte[256].ToImmutableArray().SetItem(0x24, 7).SetItem(0xE1, 1);
    }

    private static NativeBattlePrimitiveTables PassTables(byte opcode)
    {
        ImmutableArray<uint> empty = new uint[256].ToImmutableArray();
        return new(Lengths(), new byte[256].ToImmutableArray(), empty, empty.SetItem(opcode, 1), empty);
    }
}
