using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314FaceCaptureTests
{
    private const uint Context = NativeBattleTemporaryMemory.Address + 0x100;
    private const uint Vertices = NativeBattleTemporaryMemory.Address + 0x200;
    private const uint Faces = NativeBattleTemporaryMemory.Address + 0x400;

    [Theory]
    [InlineData(false, false, "ordinary")]
    [InlineData(false, true, "ordinary")]
    [InlineData(true, false, "ordinary")]
    [InlineData(true, true, "ordinary")]
    [InlineData(false, true, "unused-gray")]
    [InlineData(false, false, "backface")]
    [InlineData(false, false, "depth-reject")]
    [InlineData(true, true, "depth-reject")]
    [InlineData(true, true, "uniform")]
    public void Captured_corner_rows_match_public_lazy_arena_rasterization_and_ordered_publications(bool expanded, bool quad, string mode)
    {
        Outcome expected = Run(false, expanded, quad, mode);
        Outcome actual = Run(true, expanded, quad, mode);
        Assert.Null(expected.Error);
        Assert.Null(actual.Error);
        Assert.Equal(expected.Frame, actual.Frame);
        Assert.Equal(expected.Trace, actual.Trace);
        Assert.Equal(expected.StagingVersion, actual.StagingVersion);
        Assert.Equal(expected.Random, actual.Random);
        if (mode == "ordinary")
        {
            Assert.Contains(actual.Trace, entry => entry.StartsWith("draw:", StringComparison.Ordinal));
            if (expanded)
            {
                Assert.Contains(actual.Trace, entry => entry.StartsWith("birth:", StringComparison.Ordinal));
            }
        }
    }

    [Theory]
    [InlineData("numeric")]
    [InlineData("opaque")]
    [InlineData("null")]
    [InlineData("partial")]
    [InlineData("padding-null")]
    public void Pointer_overlays_partial_descriptors_and_unknown_padding_follow_the_public_arena_oracle(string metadata)
    {
        Outcome expected = Run(false, true, true, "uniform", metadata);
        Outcome actual = Run(true, true, true, "uniform", metadata);
        Assert.Equal(expected.Error?.GetType(), actual.Error?.GetType());
        Assert.Equal(expected.Frame, actual.Frame);
        Assert.Equal(expected.Trace, actual.Trace);
        Assert.Equal(expected.StagingVersion, actual.StagingVersion);
        Assert.Equal(expected.Random, actual.Random);
        if (metadata is "opaque" or "null")
        {
            Assert.IsType<NotSupportedException>(actual.Error);
        }
        else
        {
            Assert.Null(actual.Error);
        }
    }

    [Fact]
    public void Canonical_span_reads_match_arena_bytes_knownness_and_source_transaction_authority_across_domains()
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown().Write(Vertices, Enumerable.Range(0, 32).Select(index => (byte)index).ToArray()) };
        frame = frame with { Staging = frame.Staging.WriteResourcePointer(Vertices + 4, null) };
        using NativeBattleFileStagingTransaction transaction = new(frame.Staging);
        frame = frame with { Staging = transaction.View };
        transaction.View.WriteByte(Vertices, 99);
        foreach ((uint address, int length) in new (uint, int)[] { (Vertices, 16), (Vertices + 5, 8), (CommonAddress + MainOffset + 12, 16), (BaseAddress + 8, 8), (Vertices, 0) })
        {
            NativeBattleEffectArena previous = ReadBytes(frame, address, length);
            byte[] data = new byte[length];
            bool[] known = new bool[length];
            ReadWithKnownness(frame, address, data, known);
            Assert.Equal(previous.ReadSnapshot(0, length).Data, data);
            Assert.Equal(previous.ReadSnapshot(0, length).Known, known);
            long version = previous.Version;
            transaction.View.WriteByte(Vertices + 31, 98);
            Assert.Equal(version, previous.Version);
        }
    }

    [Fact]
    public void Canonical_span_reads_reject_invalid_pointer_encoding_before_any_corner_is_consumed()
    {
        NativeBattleResourcePointer pointer = new(null, 0x12345678, 4);
        NativeBattleEffectArena corrupt = new([new(0, ImmutableArray.Create<byte>(1, 2, 3, 4))]) { ResourcePointers = ImmutableDictionary<int, NativeBattleResourcePointer?>.Empty.Add(0, pointer) };
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame();
        frame = frame with { Effect = frame.Effect with { Pools = [] }, Memory = frame.Memory with { Arena = corrupt } };
        InvalidDataException previous = Assert.Throws<InvalidDataException>(() => ReadBytes(frame, CommonAddress, 4));
        InvalidDataException actual = Assert.Throws<InvalidDataException>(() => ReadWithKnownness(frame, CommonAddress, new byte[4], new bool[4]));
        Assert.Equal(previous.Message, actual.Message);
    }

    [Fact]
    public void Corner_span_capture_allocates_less_than_the_public_detached_arena_reader()
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame() with { Staging = NativeBattleFileStaging.Unknown().Write(Vertices, new byte[16]) };
        Span<byte> data = stackalloc byte[16];
        Span<bool> known = stackalloc bool[16];
        for (int index = 0; index < 16; index++)
        {
            _ = ReadBytes(frame, Vertices, 16).ReadByte(0);
            ReadWithKnownness(frame, Vertices, data, known);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            _ = ReadBytes(frame, Vertices, 16).ReadByte(0);
        }
        long previous = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            ReadWithKnownness(frame, Vertices, data, known);
            _ = NativeBattleEffect314ReadOnlyCapture.FromBytes(data, known).ReadByte(0);
        }
        long current = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(current < previous, $"Captured rows allocated {current} bytes; detached arenas allocated {previous} bytes.");
    }

    private sealed record Outcome(string Frame, string[] Trace, long StagingVersion, NativeCrtRandomState Random, Exception? Error);

    private static Outcome Run(bool capture, bool expanded, bool quad, string mode, string? metadata = null)
    {
        int corners = quad ? 4 : 3;
        int stride = expanded ? 16 : 8;
        byte[] context = new byte[expanded ? 0x90 : 0x64];
        BinaryPrimitives.WriteUInt32LittleEndian(context, Faces);
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(4), Vertices);
        BinaryPrimitives.WriteUInt16LittleEndian(context.AsSpan(quad ? 10 : 8), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(context.AsSpan(0x14), 0x808080);
        byte[] face = new byte[quad ? 20 : 16];
        byte[] rows = new byte[corners * stride];
        for (int corner = 0; corner < corners; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(corner * 2), (ushort)corner);
            short x = corner is 1 or 3 ? (short)200 : (short)100;
            short y = corner >= 2 ? (short)200 : (short)100;
            if (mode == "backface")
            {
                (x, y) = (y, x);
            }
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(corner * stride), x);
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(corner * stride + 2), y);
            BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(corner * stride + 4), mode == "depth-reject" ? (short)0 : (short)1000);
            rows[corner * stride + 6] = mode is "uniform" or "unused-gray" ? (byte)128 : (byte)(32 + corner * 16);
            rows[corner * stride + stride - 1] = 0xD7;
            if (expanded)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    BinaryPrimitives.WriteInt16LittleEndian(rows.AsSpan(corner * stride + 8 + axis * 2), unchecked((short)(corner * 12345 - axis * 23456)));
                }
            }
        }
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown().Write(Context, context).Write(Vertices, rows).Write(Faces, face);
        for (int corner = 0; corner < corners; corner++)
        {
            MakeUnknown(Vertices + (uint)(corner * stride + stride - 1), 1);
        }
        if (mode == "unused-gray")
        {
            MakeUnknown(Vertices + (uint)(3 * stride + 6), 1);
        }
        if (mode is "backface" or "depth-reject")
        {
            for (int corner = 0; corner < corners; corner++)
            {
                MakeUnknown(Vertices + (uint)(corner * stride + 6), 1);
                if (mode == "depth-reject")
                {
                    MakeUnknown(Vertices + (uint)(corner * stride), 4);
                }
            }
        }
        if (metadata is not null)
        {
            uint field = Vertices + (metadata == "partial" ? checked((uint)((corners - 1) * stride + 14)) : metadata == "padding-null" ? 12u : 4u);
            NativeBattleResourcePointer? pointer = metadata == "numeric" ? new(null, 0x008003E8, 4) : metadata == "opaque" ? new(new(0, 70000) { ResourceOnly = true }, null, 0) : null;
            staging = staging.WriteResourcePointer(field, pointer);
        }
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame();
        frame = frame with { Staging = staging, Memory = frame.Memory with { GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() } };
        List<string> trace = [];
        NativeBattleEffect314FaceRasterServices<NativeBattleEffect314Frame> services = new(Read,
            (current, address, value, length) =>
            {
                trace.Add($"write:{address}:{value.Version}:" + NativeBattleTestSnapshots.Json(value));
                return WriteBytes(current, address, value, length);
            },
            current => current.Memory, (current, memory) => current with { Memory = memory },
            (current, batch, cursor) =>
            {
                trace.Add($"draw:{cursor}:" + JsonSerializer.Serialize(batch));
                return current;
            },
            current =>
            {
                NativeCrtRandomState random = current.Random.Draw(out ushort value);
                trace.Add($"random:{value}");
                return new(current with { Random = random }, value);
            },
            (current, address, value) =>
            {
                trace.Add($"birth:{address}:{value.Version}:" + NativeBattleTestSnapshots.Json(value));
                return current;
            });
        if (capture)
        {
            services = services with { ReadBytes = (current, address, data, known) =>
            {
                trace.Add($"read:{address}:{data.Length}");
                ReadWithKnownness(current, address, data, known);
            } };
        }
        Exception? error = Record.Exception(() => frame = NativeBattleEffect314FaceRaster661720.RasterFaces(frame, services, Context, 0, 0, expanded, CursorGlobal));
        return new(NativeBattleTestSnapshots.Json(frame), trace.ToArray(), frame.Staging!.StorageVersion, frame.Random, error);

        NativeBattleEffectArena Read(NativeBattleEffect314Frame current, uint address, int length)
        {
            trace.Add($"read:{address}:{length}");
            return ReadBytes(current, address, length);
        }

        void MakeUnknown(uint address, int length)
        {
            (byte[] data, bool[] known) = staging.ReadSnapshot(address, length);
            Array.Fill(known, false);
            staging = staging.WriteWithKnownness(address, data, known);
        }
    }
}
