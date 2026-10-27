using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicStageShatter49Tests
{
    private const int RecordOffset = 0x10000;
    private const uint Records = NativeBattleEffectNodeMemory.ArenaAddress + RecordOffset;
    private const uint Palette = NativeBattleEffectNodeMemory.ArenaAddress + 0x3000;

    [Fact]
    public void Initialization_assigns_only_active_and_lifetime_bytes_in_the_actual_allocated_bank()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        frame = frame with { Arena = frame.Arena.MarkUnknown(RecordOffset, NativeBattleCinematicStageShatter49.AllocationBytes) };
        frame = NativeBattleCinematicStageShatter49.InitializeRecords(frame, Records, 0x1234);
        for (int index = 0; index < NativeBattleCinematicStageShatter49.Capacity; index++)
        {
            var snapshot = frame.Arena.ReadSnapshot(RecordOffset + index * 44, 44);
            Assert.Equal(new[] { 6, 7 }, Enumerable.Range(0, 44).Where(offset => snapshot.Known[offset]));
            Assert.Equal((byte)0, snapshot.Data[6]);
            Assert.Equal((byte)0x34, snapshot.Data[7]);
        }
    }

    [Fact]
    public void Capture_stores_low_MAC_WORDs_without_saturating_to_IR_range()
    {
        NativeBattleActionMatrix matrix = Camera() with { X = 40000, Y = -40000, Z = 65536 };
        NativeBattleVector result = NativeBattleCinematicStageShatter49.CaptureVertex(matrix, new(10, -10, 20));
        Assert.Equal(new NativeBattleVector(unchecked((short)40010), unchecked((short)-40010), 20), result);
    }

    [Fact]
    public void Capture_keeps_origin_relative_centroid_local_vertices_lifetime_and_four_ordered_random_draws()
    {
        NativeBattleWaterExecutionFrame frame = Initialize(2);
        frame = frame with { Arena = frame.Arena.Write(RecordOffset + 36, Enumerable.Repeat((byte)0xA5, 8).ToArray()) };
        NativeCrtRandomState random = frame.Random;
        for (int draw = 0; draw < 4; draw++)
        {
            random = random.Draw(out _);
        }
        NativeBattleCinematicStageShatter49Capture inputs = new(2, 0, 0, 100, 5, 0, -17, 0);
        frame = NativeBattleCinematicStageShatter49.CaptureFace(frame, Records, [new(0, 0, 0), new(10, 0, 0), new(0, 10, 0)], inputs);
        Assert.Equal((short)1, Word(frame, 0));
        Assert.Equal((short)3, Word(frame, 2));
        Assert.Equal((short)0, Word(frame, 4));
        Assert.Equal((byte)1, Byte(frame, 6));
        Assert.Equal((byte)2, Byte(frame, 7));
        Assert.Equal((sbyte)-2, unchecked((sbyte)Byte(frame, 9)));
        Assert.Equal((short)-1, Word(frame, 12));
        Assert.Equal((short)-3, Word(frame, 14));
        Assert.Equal((short)9, Word(frame, 20));
        foreach (int offset in new[] { 0x12, 0x1A, 0x22 })
        {
            Assert.Equal((byte)0, Byte(frame, offset));
            Assert.Equal((byte)5, Byte(frame, offset + 1));
        }
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 8), frame.Arena.Read(RecordOffset + 36, 8));
        Assert.Equal(random, frame.Random);
        Assert.Same(frame, NativeBattleCinematicStageShatter49.CaptureFace(frame, Records, [new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)], inputs));
    }

    [Fact]
    public void Radius_uses_strict_signed_unchecked_squared_distance()
    {
        NativeBattleWaterExecutionFrame frame = Initialize(1);
        ImmutableArray<NativeBattleVector> boundary = [new(3, 4, 0), new(3, 4, 0), new(3, 4, 0)];
        NativeBattleCinematicStageShatter49Capture inputs = new(0, 0, 0, 25, 0, 0, 0, 0);
        Assert.Same(frame, NativeBattleCinematicStageShatter49.CaptureFace(frame, Records, boundary, inputs));
        NativeBattleWaterExecutionFrame entered = NativeBattleCinematicStageShatter49.CaptureFace(frame, Records, boundary, inputs with { RadiusSquared = 26 });
        Assert.Equal((byte)1, Byte(entered, 6));
        ImmutableArray<NativeBattleVector> overflow = [new(32767, 32767, 32767), new(32767, 32767, 32767), new(32767, 32767, 32767)];
        Assert.Equal((byte)1, Byte(NativeBattleCinematicStageShatter49.CaptureFace(frame, Records, overflow, inputs with { RadiusSquared = 1 }), 6));
    }

    [Theory]
    [InlineData(false, 27)]
    [InlineData(true, 33)]
    public void Inactive_faces_preserve_original_color_and_use_native_signed_quad_depth_asymmetry(bool quad, int stores)
    {
        NativeBattleCinematicStageShatter49Emission emission = NativeBattleCinematicStageShatter49.RenderFace(Initialize(1), Records, Face(quad), Projected(quad), Camera(), Projection(), Palette);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(emission.Packets.Attempts);
        Assert.NotNull(attempt.Primitive);
        Assert.Equal(quad ? 0x100Cu : 0x1014u, emission.OrderingAddress);
        Assert.Equal(0x01223344u, attempt.Primitive!.Words[1]);
        Assert.Equal(stores, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        Assert.Equal(quad ? 40 : 32, emission.Packets.AllocationBytes);
    }

    [Theory]
    [InlineData(false, false, 15)]
    [InlineData(true, false, 17)]
    [InlineData(false, true, 11)]
    [InlineData(true, true, 13)]
    public void Culled_inactive_and_dead_active_faces_keep_their_original_partial_packet_stores(bool quad, bool dead, int stores)
    {
        NativeBattleWaterExecutionFrame frame = Initialize(-1);
        if (dead)
        {
            frame = frame with { Arena = frame.Arena.Write(RecordOffset + 6, new byte[] { 1 }) };
        }
        ImmutableArray<NativeBattleModuleProjectedVertex> vertices = Projected(quad).SetItem(1, new(0x000A0000, 80, 0)).SetItem(2, new(10, 120, 0));
        NativeBattleCinematicStageShatter49Emission emission = NativeBattleCinematicStageShatter49.RenderFace(frame, Records, Face(quad), vertices, Camera(), Projection(), Palette);
        NativeBattleGfGeometryAttempt attempt = Assert.Single(emission.Packets.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, emission.Packets.AllocationBytes);
        Assert.Equal(stores, attempt.WriteMask.Count(value => value == NativeBattleGfPacketByteState.Known));
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[3]);
        Assert.Equal(dead ? NativeBattleGfPacketByteState.Untouched : NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
    }

    [Fact]
    public void Active_fragment_uses_predecrement_palette_third_vertex_depth_and_retained_WORD_lifetimes()
    {
        NativeBattleWaterExecutionFrame frame = Initialize(0).WriteTemporaryDword(0xFC, 0xA5A5FFFF);
        frame = NativeBattleCinematicStageShatter49.CaptureFace(frame, Records, [new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), new(0, 0, 1000)], new(0, 0, 0, 1000000, 0, 0, 0, 0));
        NativeBattleCinematicStageShatter49Emission emission = NativeBattleCinematicStageShatter49.RenderFace(frame, Records, Face(true), Projected(true), Camera(), Projection(), Palette);
        Assert.Equal((byte)255, Byte(emission.Frame, 7));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(emission.Packets.Attempts);
        Assert.Equal(0x2E030201u, attempt.Primitive!.Words[1]);
        Assert.Equal(0x20, (int)(BinaryPrimitives.ReadUInt16LittleEndian(attempt.Data.AsSpan().Slice(22)) & 0x20));
        Assert.Equal(0xA5A57530u, emission.Frame.ReadTemporaryDword(0xFC));
        Assert.Equal(0x2D4Cu, emission.OrderingAddress);
        NativeBattleWaterExecutionFrame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(emission.Frame))!;
        NativeBattleCinematicStageShatter49Emission dead = NativeBattleCinematicStageShatter49.RenderFace(restored, Records, Face(true), Projected(true), Camera(), Projection(), Palette);
        Assert.Equal(0, dead.Packets.AllocationBytes);
        Assert.Equal((byte)255, Byte(dead.Frame, 7));
        Assert.Equal(emission.Frame.Random, restored.Random);
    }

    [Fact]
    public void Whole_draw_allocates_one_retained_bank_and_restores_before_each_capture_and_render()
    {
        NativeBattleWaterExecutionFrame frame = Fixture();
        uint payload = NativeBattleEffectNodeMemory.ArenaAddress + 0x4000;
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xB8, payload);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xBC, 0);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 1000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797704, Records);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x279770C, NativeBattleEffectNodeMemory.ArenaAddress + 0x50000);
        frame = frame with { Arena = frame.Arena.Write(0x4000, new byte[] { 0x00, 0x01, 0x01, 0x00, 0x00, 0x00 }).Write(0x4100, Enumerable.Repeat((byte)0x80, 8).ToArray()) };
        NativeBattleStageState stage = NativeBattleStageState.FromColdProcess();
        stage = stage with { Groups = stage.Groups.SetItem(0, stage.Groups[0] with { Flags = 3, ColorFlags = 0x123456, Matrices = [Camera() with { Z = 0 }] }) };
        NativeBattleRawMesh mesh = new([new(new(-100, -100, 0), 0), new(new(100, -100, 0), 0), new(new(0, 100, 0), 0)], [Face(false)]) { StageGroup = 0 };
        int restored = 0;
        int cameras = 0;
        List<int> allocations = [];
        NativeBattleCinematicStageShatter49Services services = new((_, _) => NativeBattleEffectNodeMemory.ArenaAddress + 0x2100, (_, group) =>
        {
            restored++;
            return group with { Matrices = [Camera() with { Z = 0 }] };
        }, current =>
        {
            cameras++;
            return new(current, 0x27979E8, Camera(), Projection());
        }, (current, _, _, packets) =>
        {
            allocations.Add(packets.AllocationBytes);
            return current;
        });
        NativeBattleCinematicStageShatter49Result first = NativeBattleCinematicStageShatter49.Draw(frame, stage, [mesh], services);
        Assert.Equal(Records, WaterSchedulerFixture.ReadSlotDword(first.Frame, 0, 0xBC));
        Assert.Equal(Records + 0xB000u, WaterSchedulerFixture.ReadCoreDword(first.Frame, 0x2797704));
        Assert.Equal((byte)1, first.Stage.Groups[0].Flags);
        Assert.Equal((byte)2, first.Stage.Groups[3].Flags);
        Assert.Equal(0x123456u, first.BackColor);
        NativeBattleCinematicStageShatter49Result second = NativeBattleCinematicStageShatter49.Draw(first.Frame, first.Stage, [mesh], services);
        NativeBattleCinematicStageShatter49Result third = NativeBattleCinematicStageShatter49.Draw(second.Frame, second.Stage, [mesh], services);
        Assert.Equal(new[] { 32, 32, 0 }, allocations);
        Assert.Equal(6, restored);
        Assert.Equal(3, cameras);
        Assert.Equal(Records + 0xB000u, WaterSchedulerFixture.ReadCoreDword(third.Frame, 0x2797704));
        Assert.Equal((byte)255, Byte(third.Frame, 7));
        Assert.Equal(NativeBattleEffectNodeMemory.ArenaAddress + 0x50040u, WaterSchedulerFixture.ReadCoreDword(third.Frame, 0x279770C));
    }

    [Fact]
    public void Freeze_preserves_lifetime_motion_and_angles_while_still_emitting_current_fragment()
    {
        NativeBattleWaterExecutionFrame frame = Initialize(3).WriteTemporaryByte(0x145, 1);
        frame = NativeBattleCinematicStageShatter49.CaptureFace(frame, Records, [new(0, 0, 0), new(100, 0, 0), new(0, 100, 0)], new(0, 0, 0, 1000000, 7, 0, -32, 0));
        byte[] before = frame.Arena.Read(RecordOffset, 44);
        NativeBattleCinematicStageShatter49Emission emission = NativeBattleCinematicStageShatter49.RenderFace(frame, Records, Face(false), Projected(false), Camera(), Projection(), Palette);
        Assert.NotNull(Assert.Single(emission.Packets.Attempts).Primitive);
        Assert.Equal(before, emission.Frame.Arena.Read(RecordOffset, 44));
    }

    private static NativeBattleWaterExecutionFrame Initialize(short lifetime) => NativeBattleCinematicStageShatter49.InitializeRecords(Fixture(), Records, lifetime);

    private static NativeBattleWaterExecutionFrame Fixture()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x1000);
        byte[] colors = new byte[4 * 128];
        for (int index = 0; index < 128; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(colors.AsSpan(index * 4), 0x00030201u + (uint)index);
        }
        return frame with { Arena = frame.Arena.Write(0x3000, colors) };
    }

    private static NativeBattleActionMatrix Camera() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
    private static NativeBattleCameraProjection Projection() => new(Camera(), 256, 160, 120);
    private static byte Byte(NativeBattleWaterExecutionFrame frame, int offset) => frame.Arena.Read(RecordOffset + offset, 1)[0];
    private static short Word(NativeBattleWaterExecutionFrame frame, int offset) => BinaryPrimitives.ReadInt16LittleEndian(frame.Arena.Read(RecordOffset + offset, 2));

    private static ImmutableArray<NativeBattleModuleProjectedVertex> Projected(bool quad)
    {
        ImmutableArray<NativeBattleModuleProjectedVertex> vertices = [new(0, 40, 0), new(10, 80, 0), new(0x000A0000, 120, 0)];
        return quad ? vertices.Add(new(0x000A000A, 65535, 0)) : vertices;
    }

    private static NativeBattleRawPolygon Face(bool quad)
    {
        byte[] bytes = new byte[quad ? 24 : 20];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 2);
        if (quad)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 3);
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(quad ? 20 : 16), 0x01223344);
        return new(quad, bytes.ToImmutableArray());
    }
}
