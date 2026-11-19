using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314ProjectionCaptureTests
{
    private const uint Context = NativeBattleTemporaryMemory.Address + 0x500;
    private const uint Output = NativeBattleTemporaryMemory.Address + 0x700;
    private const uint Raw = NativeBattleTemporaryMemory.Address + 0x900;

    [Theory]
    [InlineData(false, "mixed")]
    [InlineData(true, "mixed")]
    [InlineData(false, "edges")]
    [InlineData(true, "edges")]
    [InlineData(false, "zero")]
    [InlineData(true, "zero")]
    [InlineData(false, "boundary")]
    public void Captured_projection_matches_public_two_pass_math_payload_versions_and_ordered_stores(bool expanded, string mode)
    {
        Outcome expected = Run(false, expanded, mode);
        Outcome actual = Run(true, expanded, mode);
        AssertEquivalent(expected, actual);
        Assert.Null(actual.Error);
        if (mode != "zero")
        {
            Assert.Contains(actual.Trace, item => item.StartsWith("write:4:6:", StringComparison.Ordinal));
            Assert.True(actual.SpanWrites > 0);
        }
    }

    [Theory]
    [InlineData(false, "positive", "numeric-start")]
    [InlineData(false, "rejected", "numeric-start")]
    [InlineData(true, "positive", "numeric-start")]
    [InlineData(true, "rejected", "numeric-start")]
    [InlineData(true, "positive", "numeric-point")]
    [InlineData(true, "positive", "numeric-padding")]
    [InlineData(true, "positive", "null-start")]
    [InlineData(true, "rejected", "null-start")]
    [InlineData(true, "positive", "opaque-start")]
    [InlineData(true, "rejected", "opaque-start")]
    [InlineData(false, "positive", "partial")]
    [InlineData(true, "positive", "partial")]
    public void Metadata_rows_keep_the_canonical_fallback_retention_invalidation_and_partial_descriptor_rules(bool expanded, string mode, string metadata)
    {
        Outcome expected = Run(false, expanded, mode, metadata);
        Outcome actual = Run(true, expanded, mode, metadata);
        AssertEquivalent(expected, actual);
        Assert.Null(actual.Error);
        if (metadata != "partial")
        {
            Assert.True(actual.FallbackRows > 0);
        }
        if (mode == "rejected" && metadata.EndsWith("-start", StringComparison.Ordinal))
        {
            Assert.NotEmpty(actual.Pointers);
        }
        if (mode == "positive" && metadata.EndsWith("-start", StringComparison.Ordinal) || metadata == "partial")
        {
            Assert.Empty(actual.Pointers);
        }
        if (metadata is "numeric-point" or "numeric-padding")
        {
            Assert.NotEmpty(actual.Pointers);
        }
    }

    [Theory]
    [InlineData("null-point")]
    [InlineData("opaque-point")]
    public void Unobserved_point_components_fail_in_the_same_access_order_without_publishing_the_row(string metadata)
    {
        Outcome expected = Run(false, true, "positive", metadata);
        Outcome actual = Run(true, true, "positive", metadata);
        AssertEquivalent(expected, actual);
        Assert.IsType<NotSupportedException>(actual.Error);
    }

    [Fact]
    public void Special_owner_fallback_keeps_the_actual_arena_and_optional_callbacks_require_a_complete_pair()
    {
        Outcome expected = Run(false, true, "mixed");
        Outcome fallback = Run(true, true, "mixed", forceFallback: true);
        AssertEquivalent(expected, fallback);
        Assert.True(fallback.FallbackRows > 0);
        Assert.Null(fallback.Error);
        Outcome incomplete = Run(true, true, "mixed", missingWriter: true);
        AssertEquivalent(expected, incomplete);
        Assert.Equal(0, incomplete.SpanWrites);
        Assert.Equal(0, incomplete.FallbackRows);
    }

    [Fact]
    public void Owned_span_write_source_is_detached_before_publication_and_preserves_ordered_domain_slices()
    {
        NativeBattleEffect314Frame expected = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown() };
        NativeBattleEffect314Frame actual = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown() };
        byte[] bytes = [0, 0, 0, 0, 0xE8, 3, 0, 0];
        bool[] known = Enumerable.Repeat(true, 8).ToArray();
        NativeBattleEffect314WriteSourceSnapshot detached = NativeBattleEffect314WriteSourceSnapshot.Capture(bytes, known);
        NativeBattleEffectArena previous = NativeBattleEffectArena.CreateOwnedRead(bytes.ToArray(), known, null, 4);
        bytes.AsSpan().Fill(99);
        uint address = CommonAddress + MainOffset - 4;
        expected = WriteBytes(expected, address, previous, 8);
        actual = WriteBytes(actual, address, detached);
        Assert.Equal(NativeBattleTestSnapshots.Json(expected), NativeBattleTestSnapshots.Json(actual));
        Assert.Equal(expected.Memory.Arena!.Version, actual.Memory.Arena!.Version);
        Assert.Equal(previous.Read(0, 8), detached.Data);
    }

    [Fact]
    public void Captured_projection_row_storage_allocates_less_than_the_same_public_read_mutate_write_sequence()
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown().Write(Output, new byte[16]) };
        for (int index = 0; index < 16; index++)
        {
            Legacy();
            Fast();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            Legacy();
        }
        long previous = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            Fast();
        }
        long current = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(current < previous, $"Captured rows allocated {current} bytes; public rows allocated {previous} bytes.");

        void Legacy()
        {
            NativeBattleEffectArena row = ReadBytes(frame, Output, 16);
            row.Write(0, [1, 2, 3, 4]).Write(4, [5, 6]).WriteByte(6, 7);
            _ = NativeBattleEffect314WriteSourceSnapshot.Capture(row, 0, 16);
        }

        void Fast()
        {
            Span<byte> localData = stackalloc byte[16];
            Span<bool> localKnown = stackalloc bool[16];
            Assert.Null(ReadProjectionRow(frame, Output, localData, localKnown, out _));
            ReadOnlySpan<byte> patch = [1, 2, 3, 4, 5, 6, 7];
            patch.CopyTo(localData);
            localKnown[..7].Fill(true);
            _ = NativeBattleEffect314WriteSourceSnapshot.Capture(localData, localKnown);
        }
    }

    private sealed record Outcome(string Frame, string[] Trace, string[] Pointers, long ArenaVersion, long StagingVersion, int SpanWrites, int FallbackRows, Exception? Error);

    private static void AssertEquivalent(Outcome expected, Outcome actual)
    {
        Assert.Equal(expected.Error?.GetType(), actual.Error?.GetType());
        Assert.Equal(expected.Error?.Message, actual.Error?.Message);
        Assert.Equal(expected.Frame, actual.Frame);
        Assert.Equal(expected.Trace, actual.Trace);
        Assert.Equal(expected.Pointers, actual.Pointers);
        Assert.Equal(expected.ArenaVersion, actual.ArenaVersion);
        Assert.Equal(expected.StagingVersion, actual.StagingVersion);
    }

    private static Outcome Run(bool captured, bool expanded, string mode, string? metadata = null, bool forceFallback = false, bool missingWriter = false)
    {
        int count = mode == "zero" ? 0 : mode is "boundary" or "positive" or "rejected" ? 1 : 3;
        int stride = expanded ? 16 : 8;
        uint output = mode == "boundary" ? CommonAddress + MainOffset - 4 : Output;
        byte[] raw = new byte[2 + count * 6];
        BinaryPrimitives.WriteInt16LittleEndian(raw, (short)count);
        for (int index = 0; index < count; index++)
        {
            short x = mode == "boundary" ? (short)0 : mode == "edges" ? index == 0 ? short.MinValue : short.MaxValue : (short)(index * 17 - 12);
            short y = mode == "boundary" ? (short)0 : (short)(index * 11 - 9);
            short z = mode == "rejected" ? (short)-1000 : index == 1 ? (short)0 : index == 2 ? (short)-1000 : (short)1000;
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(2 + index * 6), x);
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(4 + index * 6), y);
            BinaryPrimitives.WriteInt16LittleEndian(raw.AsSpan(6 + index * 6), z);
        }
        byte[] context = new byte[expanded ? 0x90 : 0x64];
        int cursor = expanded ? 0x74 : 0x54;
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(cursor), Raw);
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(cursor + 4), output);
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown().Write(Context, context).Write(Raw, raw).Write(Output, Enumerable.Repeat((byte)0xA7, Math.Max(1, count * stride)).ToArray()) };
        for (int index = 0; index < count; index++)
        {
            uint padding = Output + checked((uint)(index * stride + stride - 1));
            frame = frame with { Staging = frame.Staging!.WriteWithKnownness(padding, [0xA7], [false]) };
        }
        NativeBattleActionMatrix identity = NativeBattleCuragaMatrices.Identity8DD770();
        frame = frame with { Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), identity, true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() } };
        List<string> trace = [];
        int spanWrites = 0;
        int fallbackRows = 0;
        NativeBattleEffect314ProjectionServices<NativeBattleEffect314Frame> services = new(Read, Write, current => current.Memory, (current, memory) => current with { Memory = memory }, _ => NativeBattlePackedMatrix32.FromMatrix(identity, null), _ => new(identity, 100, 160, 108))
        {
            CaptureVertex = (address, point) => trace.Add($"point:{address}:" + JsonSerializer.Serialize(new { point.X, point.Y, point.Z }))
        };
        if (captured)
        {
            services = services with { ReadRow = ReadRow, WriteRow = missingWriter ? null : WriteRow };
        }
        Exception? error = Record.Exception(() => frame = NativeBattleEffect314Projection663760.ProjectGroup(frame, services, Context, expanded));
        string[] pointers = frame.Staging!.EnumerateResourcePointers().Where(entry => entry.Key >= output && entry.Key < output + count * stride).Select(entry => entry.Key + ":" + JsonSerializer.Serialize(entry.Value)).ToArray();
        return new(NativeBattleTestSnapshots.Json(frame), trace.ToArray(), pointers, frame.Memory.Arena!.Version, frame.Staging.StorageVersion, spanWrites, fallbackRows, error);

        NativeBattleEffectArena Read(NativeBattleEffect314Frame current, uint address, int length)
        {
            trace.Add($"read:{address}:{length}");
            return ReadBytes(current, address, length);
        }

        NativeBattleEffectArena? ReadRow(NativeBattleEffect314Frame current, uint address, Span<byte> data, Span<bool> known, out long version)
        {
            trace.Add($"read:{address}:{data.Length}");
            if (forceFallback)
            {
                version = 0;
                fallbackRows++;
                return ReadBytes(current, address, data.Length);
            }
            NativeBattleEffectArena? result = ReadProjectionRow(current, address, data, known, out version);
            if (result is not null)
            {
                fallbackRows++;
            }
            return result;
        }

        NativeBattleEffect314Frame WriteRow(NativeBattleEffect314Frame current, uint address, ReadOnlySpan<byte> data, ReadOnlySpan<bool> known, long version)
        {
            spanWrites++;
            NativeBattleEffectArena observed = NativeBattleEffectArena.CreateOwnedRead(data.ToArray(), known, null, version);
            ObserveWrite(address, observed, data.Length);
            current = WriteBytes(current, address, NativeBattleEffect314WriteSourceSnapshot.Capture(data, known));
            return InjectMetadata(current, address, data.Length);
        }

        NativeBattleEffect314Frame Write(NativeBattleEffect314Frame current, uint address, NativeBattleEffectArena value, int length)
        {
            ObserveWrite(address, value, length);
            current = WriteBytes(current, address, value, length);
            return InjectMetadata(current, address, length);
        }

        void ObserveWrite(uint address, NativeBattleEffectArena value, int length)
        {
            trace.Add($"write:{value.Version}:{length}:{address}:" + NativeBattleTestSnapshots.Json(value));
        }

        NativeBattleEffect314Frame InjectMetadata(NativeBattleEffect314Frame current, uint address, int length)
        {
            if (metadata is null || length != 6 || address < output || address >= output + count * stride)
            {
                return current;
            }
            uint row = expanded ? address - 8 : address;
            uint field = row + (metadata.EndsWith("-point", StringComparison.Ordinal) ? 8u : metadata == "numeric-padding" ? 12u : metadata == "partial" ? (uint)(stride - 2) : 0u);
            NativeBattleResourcePointer? pointer = metadata.StartsWith("numeric", StringComparison.Ordinal) ? new(null, BinaryPrimitives.ReadUInt32LittleEndian(current.Staging!.ReadSnapshot(field, 4).Data), 4) : metadata.StartsWith("opaque", StringComparison.Ordinal) ? new(new(0, 70000) { ResourceOnly = true }, null, 0) : null;
            return current with { Staging = current.Staging!.WriteResourcePointer(field, pointer) };
        }
    }
}
