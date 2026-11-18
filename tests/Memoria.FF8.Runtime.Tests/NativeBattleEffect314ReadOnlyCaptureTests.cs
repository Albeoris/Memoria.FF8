using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect314Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect314Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect314ReadOnlyCaptureTests
{
    [Theory]
    [InlineData(3, 0, false)]
    [InlineData(4, 97, false)]
    [InlineData(3, 1, true)]
    public void Captured_fragment_matches_public_arena_inputs_including_native_versions_random_sparks_and_marker_order(int corners, uint seed, bool spark)
    {
        NativeBattleEffect314Frame initial = Frame(seed);
        NativeBattleEffect314Frame previous = NativeBattleTestSnapshots.Independent(initial);
        NativeBattleEffect314Frame captured = NativeBattleTestSnapshots.Independent(initial);
        NativeBattleEffectArena face = Face(corners);
        NativeBattleEffectArena capturedSource = NativeBattleTestSnapshots.Independent(face);
        ImmutableArray<NativeBattleEffectArena> vertices = Vertices(corners);
        Span<NativeBattleEffect314ReadOnlyCapture> points = stackalloc NativeBattleEffect314ReadOnlyCapture[4];
        for (int index = 0; index < corners; index++)
        {
            points[index] = NativeBattleEffect314ReadOnlyCapture.Capture(vertices[index], 0, 8);
        }
        List<string> previousMarkers = [];
        List<string> capturedMarkers = [];
        long faceVersion = face.Version;
        long capturedSourceVersion = capturedSource.Version;
        string capturedSourceBefore = NativeBattleTestSnapshots.Json(capturedSource);
        uint main = CommonAddress + MainOffset;
        var oldResult = CreateFragment6646A0(previous, null!, main, face, vertices, main + 0x4728, 127, 13, 0x314, Marker(previousMarkers));
        var newResult = CreateCapturedFragment6646A0(captured, null!, main, NativeBattleEffect314ReadOnlyCapture.Capture(capturedSource, 0, corners == 3 ? 16 : 20), points[..corners], main + 0x4728, 127, 13, 0x314, Marker(capturedMarkers));
        Assert.Equal(oldResult.CapacityAvailable, newResult.CapacityAvailable);
        Assert.Equal(NativeBattleTestSnapshots.Json(oldResult.Frame), NativeBattleTestSnapshots.Json(newResult.Frame));
        Assert.Equal(previousMarkers, capturedMarkers);
        Assert.Single(previousMarkers);
        Assert.Equal(spark ? (short)11 : (short)0, ReadBytes(newResult.Frame, main + SparkOffset + 6, 2).ReadInt16(0));
        Assert.Equal(faceVersion + 1, face.Version);
        Assert.Equal((byte)(capturedSource.ReadByte(15) | 0x80), face.ReadByte(15));
        Assert.Equal(capturedSourceVersion, capturedSource.Version);
        Assert.Equal(capturedSourceBefore, NativeBattleTestSnapshots.Json(capturedSource));
        Assert.Equal((byte)0x12, capturedSource.ReadByte(15));

        Func<NativeBattleEffect314Frame, byte, NativeBattleEffect314Frame> Marker(List<string> observations)
        {
            return (frame, marker) =>
            {
                observations.Add(marker + ":" + frame.State.ReadDword(RandomGlobal) + ":" + NativeBattleTestSnapshots.Json(ReadBytes(frame, main + FragmentOffset, FragmentStride)));
                return frame;
            };
        }
    }

    [Fact]
    public void Capture_detaches_unknown_raw_padding_and_copy_matches_the_existing_value_only_copy_contract()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [0x80, 0xFF, 0xA2, 0xB3, 4, 5, 1, 0]).MarkUnknown(2, 2);
        source.WriteResourcePointer(12, null);
        source.WriteExecutionReference(4, 2, new BattleExecutionReference.Model(Memoria.FF8.Tests.NativeBattleTypedFixture.OriginalOwner(3)), [4, 5]);
        NativeBattleEffectArenaSnapshot frozen = source.Capture();
        long version = source.Version;
        NativeBattleEffect314ReadOnlyCapture captured = NativeBattleEffect314ReadOnlyCapture.Capture(source, 0, 8);
        NativeBattleEffect314ReadOnlyCapture pinned = NativeBattleEffect314ReadOnlyCapture.Capture(frozen, 0, 8);
        Assert.Equal(version, source.Version);
        NativeBattleEffectArena oldPoint = NativeBattleEffectNodeContextMemory.Copy(source, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8);
        NativeBattleEffectArena previous = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xEE, 32).ToArray());
        previous.WriteResourcePointer(4, null);
        NativeBattleEffectArena current = NativeBattleTestSnapshots.Independent(previous);
        previous = NativeBattleEffectNodeContextMemory.Copy(oldPoint, 0, previous, 4, 8);
        current = captured.CopyTo(0, current, 4, 8);
        Assert.Equal(NativeBattleTestSnapshots.Json(previous), NativeBattleTestSnapshots.Json(current));
        source.Write(0, new byte[8]);
        Assert.Equal((byte)0x80, captured.ReadByte(0));
        Assert.Equal(captured.ReadUInt16(0), pinned.ReadUInt16(0));
        Assert.Throws<NotSupportedException>(() => captured.ReadUInt16(2));
        Assert.Equal((ushort)1, captured.ReadUInt16(6));
        var raw = current.ReadSnapshot(4, 8);
        Assert.Equal((byte)0xA2, raw.Data[2]);
        Assert.Equal((byte)0xB3, raw.Data[3]);
        Assert.False(raw.Known[2]);
        Assert.False(raw.Known[3]);
        Assert.False(current.TryGetResourcePointer(4, out _));
        Assert.False(current.TryReadExecutionReference<BattleExecutionReference.Model>(8, 2, out _));
    }

    [Fact]
    public void Capture_preserves_zero_fill_and_frozen_source_without_hydration_or_version_changes()
    {
        NativeBattleEffectArena source = NativeBattleEffectArena.Clear().Write(0, [1, 2]);
        NativeBattleEffectArenaSnapshot frozen = source.Capture();
        long version = source.Version;
        NativeBattleEffect314ReadOnlyCapture zero = NativeBattleEffect314ReadOnlyCapture.Capture(frozen, 100, 8);
        Assert.Equal((ushort)0, zero.ReadUInt16(0));
        Assert.Equal((ushort)0, zero.ReadUInt16(6));
        Assert.True(frozen.IsSnapshot);
        Assert.Equal(version, source.Version);
        Assert.Throws<InvalidDataException>(() => zero.ReadByte(8));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect314ReadOnlyCapture.Capture(source, 0, 21));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Public_and_captured_inputs_preserve_early_vertex_flag_returns_before_unknown_coordinates(bool mismatch)
    {
        NativeBattleEffect314Frame previous = Frame(1);
        NativeBattleEffect314Frame current = NativeBattleTestSnapshots.Independent(previous);
        ImmutableArray<NativeBattleEffectArena> vertices = Enumerable.Range(0, 3).Select(index => NativeBattleEffectArena.Clear().MarkUnknown(0, 6).Write(6, BitConverter.GetBytes((ushort)(mismatch && index == 1 ? 2 : mismatch ? 1 : 0)))).ToImmutableArray();
        Span<NativeBattleEffect314ReadOnlyCapture> points = stackalloc NativeBattleEffect314ReadOnlyCapture[3];
        for (int index = 0; index < 3; index++)
        {
            points[index] = NativeBattleEffect314ReadOnlyCapture.Capture(vertices[index], 0, 8);
        }
        var oldResult = CreateFragment6646A0(previous, null!, CommonAddress + MainOffset, Face(3), vertices, 0, 0, 0, 0, UnexpectedMarker);
        var newResult = CreateCapturedFragment6646A0(current, null!, CommonAddress + MainOffset, NativeBattleEffect314ReadOnlyCapture.Known(Face(3).Read(0, 16)), points, 0, 0, 0, 0, UnexpectedMarker);
        Assert.True(oldResult.CapacityAvailable);
        Assert.True(newResult.CapacityAvailable);
        Assert.Equal(NativeBattleTestSnapshots.Json(oldResult.Frame), NativeBattleTestSnapshots.Json(newResult.Frame));
    }

    [Fact]
    public void Public_adapter_preserves_lazy_face_reads_vertex_count_validation_and_frozen_mutation_guard()
    {
        ImmutableArray<NativeBattleEffectArena> unavailable = [null!, null!, null!];
        NativeBattleEffectArena consumed = NativeBattleEffectArena.Clear().MarkUnknown(0, 20).Write(15, [0x80]);
        Assert.True(CreateFragment6646A0(null!, null!, 0, consumed, unavailable, 0, 0, 0, 0, UnexpectedMarker).CapacityAvailable);
        Assert.Throws<InvalidDataException>(() => CreateFragment6646A0(null!, null!, 0, null!, [null!, null!], 0, 0, 0, 0, UnexpectedMarker));
        NativeBattleEffectArena face = Face(3).MarkUnknown(0, 6);
        Assert.True(CreateFragment6646A0(Frame(0), null!, CommonAddress + MainOffset, face, Vertices(3), 0x1000, 127, 0, 0, (frame, _) => frame).CapacityAvailable);
        NativeBattleEffectArenaSnapshot frozen = Face(3).Capture();
        Assert.Throws<InvalidOperationException>(() => CreateFragment6646A0(Frame(0), null!, CommonAddress + MainOffset, frozen, Vertices(3), 0x1000, 127, 0, 0, UnexpectedMarker));
        Assert.Equal((byte)0x12, frozen.ReadByte(15));
    }

    [Fact]
    public void Full_fragment_bank_returns_before_random_draws_and_marker_publication_for_both_input_kinds()
    {
        NativeBattleEffect314Frame initial = Frame(97);
        uint main = CommonAddress + MainOffset;
        byte[] occupied = new byte[FragmentCapacity * FragmentStride];
        for (int index = 0; index < FragmentCapacity; index++)
        {
            occupied[index * FragmentStride + 6] = 1;
        }
        initial = WriteBytes(initial, main + FragmentOffset, NativeBattleEffectArena.Clear().Write(0, occupied), occupied.Length);
        NativeBattleEffect314Frame previous = NativeBattleTestSnapshots.Independent(initial);
        NativeBattleEffect314Frame current = NativeBattleTestSnapshots.Independent(initial);
        ImmutableArray<NativeBattleEffectArena> vertices = Vertices(3);
        Span<NativeBattleEffect314ReadOnlyCapture> points = stackalloc NativeBattleEffect314ReadOnlyCapture[3];
        for (int index = 0; index < 3; index++)
        {
            points[index] = NativeBattleEffect314ReadOnlyCapture.Capture(vertices[index], 0, 8);
        }
        var oldResult = CreateFragment6646A0(previous, null!, main, Face(3), vertices, 0, 0, 0, 0, UnexpectedMarker);
        var newResult = CreateCapturedFragment6646A0(current, null!, main, NativeBattleEffect314ReadOnlyCapture.Known(Face(3).Read(0, 16)), points, 0, 0, 0, 0, UnexpectedMarker);
        Assert.False(oldResult.CapacityAvailable);
        Assert.False(newResult.CapacityAvailable);
        Assert.Equal(97u, newResult.Frame.State.ReadDword(RandomGlobal));
        Assert.Equal(NativeBattleTestSnapshots.Json(oldResult.Frame), NativeBattleTestSnapshots.Json(newResult.Frame));
    }

    [Fact]
    public void Geometry_face_capture_keeps_eager_knownness_and_current_pinned_reload_visibility()
    {
        NativeBattleModelCpuResources cpu = NativeBattleModelCpuResources.Unknown();
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, cpu.Cpu.Banks[0].Write(0, Enumerable.Range(0, 32).Select(index => (byte)index).ToArray())) } };
        NativeBattleFileStaging staging = NativeBattleFileStaging.Unknown();
        NativeBattleResourcePointer pointer = new(new(0, 0), null, 32);
        NativeBattleResourcePointer pinnedPointer = pointer.Pin(cpu);
        NativeBattleEffect314GeometryReadScope scope = new();
        byte[] ReadCurrent(int offset, int length) => scope.Read(cpu, staging, pointer, 32, offset, length);
        byte[] ReadPinned(int offset, int length) => scope.Read(cpu, staging, pinnedPointer, 32, offset, length);
        NativeBattleModelGeometrySource live = NativeBattleModelGeometrySource.OpenEditor(cpu, staging, pointer, ReadCurrent, (offset, length) => NativeBattleEffect314TemporaryReads.Known(ReadCurrent(offset, length)));
        NativeBattleModelGeometrySource pinned = NativeBattleModelGeometrySource.OpenEditor(cpu, staging, pinnedPointer, ReadPinned, (offset, length) => NativeBattleEffect314TemporaryReads.Known(ReadPinned(offset, length)));
        NativeBattleEffect314ReadOnlyCapture earlier = NativeBattleEffect314ReadOnlyCapture.Known(live.Read(0, 20));
        NativeBattleModelCpuResources before = cpu;
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 15, [0x80]);
        scope.ObserveWrite(before, cpu, pointer, 15, [0x80]);
        Check(0x80);
        cpu = NativeBattleModelResourceView.WritePartySection(cpu, staging, pointer, 15, [0x40]);
        Check(0x40);
        cpu = NativeBattleTestSnapshots.Independent(cpu);
        Check(0x40);
        Assert.Equal((byte)15, earlier.ReadByte(15));
        cpu = cpu with { Cpu = cpu.Cpu with { Banks = cpu.Cpu.Banks.SetItem(0, NativeBattleModelCpuBank.Unknown().Write(15, [0x80])) } };
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect314ReadOnlyCapture.Known(live.Read(0, 20)));
        Assert.Equal((byte)15, NativeBattleEffect314ReadOnlyCapture.Known(pinned.Read(0, 20)).ReadByte(15));

        void Check(byte marker)
        {
            Assert.Equal(live.ReadArena(0, 20).ReadByte(15), NativeBattleEffect314ReadOnlyCapture.Known(live.Read(0, 20)).ReadByte(15));
            Assert.Equal(marker, NativeBattleEffect314ReadOnlyCapture.Known(live.Read(0, 20)).ReadByte(15));
            Assert.Equal((byte)15, NativeBattleEffect314ReadOnlyCapture.Known(pinned.Read(0, 20)).ReadByte(15));
        }
    }

    [Fact]
    public void Warm_readonly_vertex_captures_avoid_mutable_arena_graph_allocations()
    {
        NativeBattleEffectArena source = Vertices(3)[0];
        int previousSum = 0;
        int currentSum = 0;
        for (int index = 0; index < 16; index++)
        {
            _ = NativeBattleEffectNodeContextMemory.Copy(source, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8).ReadUInt16(6);
            _ = NativeBattleEffect314ReadOnlyCapture.Capture(source, 0, 8).ReadUInt16(6);
        }
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 256; index++)
        {
            previousSum += NativeBattleEffectNodeContextMemory.Copy(source, 0, NativeBattleEffectArena.Clear().MarkUnknown(0, 8), 0, 8).ReadUInt16(6);
        }
        long previous = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 256; index++)
        {
            currentSum += NativeBattleEffect314ReadOnlyCapture.Capture(source, 0, 8).ReadUInt16(6);
        }
        long current = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(previousSum, currentSum);
        Assert.True(previous > 0 && current < previous / 4, $"Readonly captures allocated {current} bytes; mutable copies allocated {previous} bytes.");
    }

    private static NativeBattleEffect314Frame Frame(uint seed)
    {
        NativeBattleEffect314Frame frame = NativeBattleEffect314ControllerTests.Frame();
        uint main = CommonAddress + MainOffset;
        frame = WriteBytes(frame, main + FragmentOffset, NativeBattleEffectArena.Clear().Write(0, new byte[FragmentCapacity * FragmentStride]), FragmentCapacity * FragmentStride);
        frame = WriteBytes(frame, main + SparkOffset, NativeBattleEffectArena.Clear().Write(0, new byte[SparkCapacity * SparkStride]), SparkCapacity * SparkStride);
        frame = WriteBytes(frame, main + 0x4724, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(main + FragmentOffset)), 4);
        frame = WriteBytes(frame, main + 0xF20, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(main + SparkOffset)), 4);
        return frame with { State = frame.State.WriteDword(RandomGlobal, seed) };
    }

    private static NativeBattleEffectArena Face(int corners)
    {
        byte[] data = Enumerable.Range(0, corners == 3 ? 16 : 20).Select(index => (byte)(index + 1)).ToArray();
        data[15] = 0x12;
        return NativeBattleEffectArena.Clear().Write(0, data);
    }

    private static ImmutableArray<NativeBattleEffectArena> Vertices(int count)
    {
        short[][] coordinates = [[short.MinValue, short.MaxValue, -1], [short.MaxValue, short.MinValue, 1], [0, 0, short.MaxValue], [-3, 2, short.MinValue]];
        return coordinates.Take(count).Select(point =>
        {
            byte[] data = new byte[8];
            for (int axis = 0; axis < 3; axis++)
            {
                BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(axis * 2), point[axis]);
            }
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6), 0x101);
            return NativeBattleEffectArena.Clear().Write(0, data);
        }).ToImmutableArray();
    }

    private static NativeBattleEffect314Frame UnexpectedMarker(NativeBattleEffect314Frame frame, byte marker)
    {
        throw new InvalidOperationException("This branch must not publish a marker.");
    }
}
