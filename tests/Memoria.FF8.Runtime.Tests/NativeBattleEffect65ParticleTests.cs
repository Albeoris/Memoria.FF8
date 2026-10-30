using System.Buffers.Binary;
using static Memoria.FF8.Runtime.NativeBattleEffect65Controller;
using static Memoria.FF8.Runtime.NativeBattleEffect65Profile;
using static Memoria.FF8.Runtime.Tests.NativeBattleEffect65LowerFixture;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect65ParticleTests
{
    [Fact]
    public void Initialization_changes_only_native_free_markers_and_preserves_random()
    {
        NativeBattleEffect65Frame frame = Frame();
        uint first = ArenaAddress + 0x3B530;
        frame = Write(frame, first, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 128 * 22).ToArray()), 128 * 22);
        NativeBattleEffect65Frame result = NativeBattleEffect65Particles.Initialize6E2E20(frame);
        Assert.Equal(first, result.State.ReadDword(0x2544FC4));
        Assert.Equal(0xA5, Read(result, first, 1).Read(0, 1)[0]);
        Assert.Equal(0, Read(result, first + 6, 1).Read(0, 1)[0]);
        Assert.Equal(0xA5, Read(result, first + 7, 1).Read(0, 1)[0]);
        Assert.Equal(0, Read(result, first + 127 * 22 + 6, 1).Read(0, 1)[0]);
        Assert.Equal(frame.Random, result.Random);
    }

    [Fact]
    public void Full_billboard_pool_does_not_consume_CRT_then_success_draws_exactly_four()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65Particles.Initialize6E2E20(Frame());
        uint start = ArenaAddress + 0x3B530;
        for (int index = 0; index < 128; index++)
        {
            frame = Write(frame, start + (uint)(index * 22 + 6), NativeBattleEffectArena.Clear().Write(0, new byte[] { 1 }), 1);
        }
        (NativeBattleEffect65Frame full, uint missing) = NativeBattleEffect65Particles.Allocate6E2E50(frame);
        Assert.Equal(0u, missing);
        Assert.Equal(frame.Random, full.Random);
        uint last = start + 127 * 22;
        frame = Write(frame, last + 6, NativeBattleEffectArena.Clear(), 1);
        NativeCrtRandomState random = frame.Random.Draw(out ushort first).Draw(out ushort second).Draw(out ushort third).Draw(out ushort fourth);
        (NativeBattleEffect65Frame result, uint address) = NativeBattleEffect65Particles.Allocate6E2E50(frame);
        Assert.Equal(last, address);
        Assert.Equal(start, result.State.ReadDword(0x2544FC4));
        Assert.Equal(random, result.Random);
        NativeBattleEffectArena row = Read(result, last, 22);
        Assert.Equal(unchecked((short)first), Word(row, 16));
        Assert.Equal(unchecked((short)second), Word(row, 18));
        Assert.Equal(new[] { unchecked((byte)third), unchecked((byte)fourth) }, row.Read(20, 2));
    }

    [Fact]
    public void First_zero_life_retires_before_velocity_reads_and_other_rows_wrap_words()
    {
        NativeBattleEffect65Frame frame = NativeBattleEffect65Particles.Initialize6E3580(Frame());
        uint start = ArenaAddress + 0x27690;
        frame = Write(frame, start, NativeBattleEffectArena.Clear().MarkUnknown(0, 32).Write(6, BitConverter.GetBytes((short)1)).Write(0, BitConverter.GetBytes((short)123)), 32);
        NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)30000)).Write(6, BitConverter.GetBytes((short)2)).Write(0x1A, BitConverter.GetBytes((short)10000));
        frame = Write(frame, start + 32, row, 32);
        NativeBattleEffect65Frame result = NativeBattleEffect65Particles.Advance6E38C0(frame);
        Assert.Equal(start, result.State.ReadDword(0x2544FC8));
        Assert.Equal(123, Word(Read(result, start, 2), 0));
        Assert.Equal(unchecked((short)40000), Word(Read(result, start + 32, 2), 0));
        Assert.Equal(1, Word(Read(result, start + 38, 2), 0));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Projected_GT_faces_keep_exact_partial_stores_and_source_progress(bool quad)
    {
        (NativeBattleEffect65Frame frame, uint temporary, uint source, _) = FaceFrame(quad, -1);
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Frame result = NativeBattleEffect65Particles.DrawFaces(frame, temporary, 120, 0, 4, quad, CursorGlobal, 2, Inputs(operations));
        NativeBattleEffect65DeliverBatch delivery = Assert.IsType<NativeBattleEffect65DeliverBatch>(Assert.Single(operations));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(delivery.Batch.Attempts);
        Assert.Null(attempt.Primitive);
        Assert.Equal(0, delivery.Batch.AllocationBytes);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[4]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[8]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[16]);
        Assert.Equal(NativeBattleGfPacketByteState.Known, attempt.WriteMask[36]);
        Assert.Equal(source + 4 + (uint)(quad ? 36 : 28), BinaryPrimitives.ReadUInt32LittleEndian(Context(result, temporary, 120).Read(4, 4)));
    }

    [Fact]
    public void Quad_accepts_negative_area_without_writing_NCLIP_context()
    {
        (NativeBattleEffect65Frame frame, uint temporary, uint source, _) = FaceFrame(true, 2000);
        NativeBattleEffectArena face = Read(frame, source + 4, 36).Write(6, BitConverter.GetBytes((ushort)4)).Write(8, BitConverter.GetBytes((ushort)2));
        frame = Write(frame, source + 4, face, 36);
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Frame result = NativeBattleEffect65Particles.DrawFaces(frame, temporary, 120, 0, 4, true, CursorGlobal, 4, Inputs(operations));
        NativeBattleEffect65DeliverBatch delivery = Assert.IsType<NativeBattleEffect65DeliverBatch>(Assert.Single(operations));
        Assert.Equal(52, delivery.Batch.AllocationBytes);
        Assert.NotNull(Assert.Single(delivery.Batch.Attempts).Primitive);
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(Context(result, temporary, 120).Read(8, 4)));
    }

    [Fact]
    public void Attempt_delivery_precedes_next_faces_current_vertex_reads()
    {
        (NativeBattleEffect65Frame frame, uint temporary, uint source, uint vertices) = FaceFrame(false, 0);
        frame = Write(frame, source + 32, Read(frame, source + 4, 28), 28);
        frame = Write(frame, source, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(2)), 4);
        for (int index = 0; index < 4; index++)
        {
            frame = Write(frame, vertices + (uint)(index * 8 + 4), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)-1)), 2);
        }
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Inputs inputs = Inputs(operations, (current, operation) =>
        {
            if (operations.Count == 1)
            {
                for (int index = 0; index < 4; index++)
                {
                    current = Write(current, vertices + (uint)(index * 8 + 4), NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)2000)), 2);
                }
            }
            return current;
        });
        NativeBattleEffect65Particles.DrawFaces(frame, temporary, 120, 0, 4, false, CursorGlobal, 2, inputs);
        Assert.Equal(new[] { 0, 40 }, operations.Cast<NativeBattleEffect65DeliverBatch>().Select(delivery => delivery.Batch.AllocationBytes));
    }

    [Fact]
    public void Face_vertices_accept_unknown_W_but_reject_reached_unknown_XYZ()
    {
        (NativeBattleEffect65Frame frame, uint temporary, _, uint vertices) = FaceFrame(false, 2000);
        for (int index = 0; index < 3; index++)
        {
            frame.Memory.Arena!.MarkUnknown(checked((int)(vertices - ArenaAddress)) + index * 8 + 6, 2);
        }
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Particles.DrawFaces(frame, temporary, 120, 0, 4, false, CursorGlobal, 2, Inputs(operations));
        Assert.Single(operations);
        (frame, temporary, _, vertices) = FaceFrame(false, 2000);
        frame.Memory.Arena!.MarkUnknown(checked((int)(vertices - ArenaAddress)) + 2, 1);
        operations.Clear();
        NotSupportedException error = Assert.Throws<NotSupportedException>(() => NativeBattleEffect65Particles.DrawFaces(frame, temporary, 120, 0, 4, false, CursorGlobal, 2, Inputs(operations)));
        Assert.Contains("reached current XYZ", error.Message);
        Assert.Empty(operations);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(2000, true)]
    public void Fourth_quad_vertex_is_read_only_after_the_original_projection_guard(int z, bool reached)
    {
        (NativeBattleEffect65Frame frame, uint temporary, _, uint vertices) = FaceFrame(true, z);
        frame.Memory.Arena!.MarkUnknown(checked((int)(vertices - ArenaAddress)) + 24, 6);
        List<NativeBattleEffect65Operation> operations = [];
        if (reached)
        {
            Assert.Throws<NotSupportedException>(() => NativeBattleEffect65Particles.DrawFaces(frame, temporary, 120, 0, 4, true, CursorGlobal, 2, Inputs(operations)));
            Assert.Empty(operations);
        }
        else
        {
            NativeBattleEffect65Particles.DrawFaces(frame, temporary, 120, 0, 4, true, CursorGlobal, 2, Inputs(operations));
            Assert.Equal(0, Assert.IsType<NativeBattleEffect65DeliverBatch>(Assert.Single(operations)).Batch.AllocationBytes);
        }
    }

    private static (NativeBattleEffect65Frame Frame, uint Temporary, uint Source, uint Vertices) FaceFrame(bool quad, int z)
    {
        NativeBattleEffect65Frame frame = Frame(z);
        (frame, uint temporary) = Allocate(frame, 120);
        uint vertices = ArenaAddress + 0x50000;
        uint source = ArenaAddress + 0x51000;
        short[] xy = [-100, -100, 100, -100, -100, 100, 100, 100];
        for (int index = 0; index < 4; index++)
        {
            NativeBattleEffectArena row = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(xy[index * 2])).Write(2, BitConverter.GetBytes(xy[index * 2 + 1])).Write(6, BitConverter.GetBytes((short)0x1234));
            frame = Write(frame, vertices + (uint)(index * 8), row, 8);
        }
        NativeBattleEffectArena face = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(quad ? 0x3C332211u : 0x34332211u));
        for (int index = 0; index < (quad ? 4 : 3); index++)
        {
            face = face.Write(4 + index * 2, BitConverter.GetBytes((ushort)(index * 2)));
        }
        frame = Write(frame, source, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes(1)), 4);
        frame = Write(frame, source + 4, face, quad ? 36 : 28);
        frame = PublishContext(frame, temporary, Context(frame, temporary, 120).Write(0, BitConverter.GetBytes(vertices)).Write(4, BitConverter.GetBytes(source)).Write(8, BitConverter.GetBytes(0x12345678u)), 120);
        return (frame, temporary, source, vertices);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_faces_capture_reached_corners_and_native_FIFO_without_changing_packet_or_context(bool quad)
    {
        (NativeBattleEffect65Frame Frame, List<NativeBattleEffect65Operation> Operations) Run(bool capture)
        {
            var fixture = FaceFrame(quad, 2000);
            List<NativeBattleEffect65Operation> operations = [];
            NativeBattleEffect65Inputs inputs = Inputs(operations) with { ReadProjection = _ => new(Identity().ReadMatrix(), 100, 160, 108) };
            NativeBattleEffect65Frame result = NativeBattleEffect65Particles.DrawFaces(fixture.Frame, fixture.Temporary, 120, 0, 4, quad, CursorGlobal, 2, inputs, capture ? new(65, ArenaAddress + 0x3B530, 127, 43, 0) { ResourceGeneration = fixture.Source } : null);
            return (result, operations);
        }
        var native = Run(false);
        var captured = Run(true);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(native.Frame)), System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured.Frame)));
        NativeBattleGfGeometryBatch original = Assert.IsType<NativeBattleEffect65DeliverBatch>(Assert.Single(native.Operations)).Batch;
        NativeBattleGfGeometryBatch batch = Assert.IsType<NativeBattleEffect65DeliverBatch>(Assert.Single(captured.Operations)).Batch;
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(original)), System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(batch)));
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal(new System.Numerics.Vector3(-100, -100, 2000), geometry.A);
        Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
        if (quad)
        {
            Assert.Equal(new System.Numerics.Vector3(100, 100, 2000), geometry.D);
        }
        NativeBattlePilotVisualGeometry retained = Assert.Single(NativeBattleEffect65Presentation.FinalizeCapturedBatch(batch).Attempts).Primitive!.PilotGeometry!;
        Assert.True(retained.UseSourceDepths);
        Assert.Equal(new System.Numerics.Vector4(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]), retained.SourceDepths);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_reject_guard_keeps_fourth_corner_unread(bool capture)
    {
        var fixture = FaceFrame(true, -1);
        fixture.Frame.Memory.Arena!.MarkUnknown(checked((int)(fixture.Vertices - ArenaAddress)) + 24, 8);
        List<NativeBattleEffect65Operation> operations = [];
        NativeBattleEffect65Particles.DrawFaces(fixture.Frame, fixture.Temporary, 120, 0, 4, true, CursorGlobal, 2, Inputs(operations) with { ReadProjection = _ => new(Identity().ReadMatrix(), 100, 160, 108) }, capture ? new(65, 1, 2, 3, 4) : null);
        Assert.Null(Assert.Single(Assert.IsType<NativeBattleEffect65DeliverBatch>(Assert.Single(operations)).Batch.Attempts).Primitive);
    }

    [Fact]
    public void Presentation_particle_allocation_preserves_CRT_and_records_a_new_real_slot_birth()
    {
        NativeBattleEffect65Frame native = NativeBattleEffect65Particles.Allocate6E2E50(NativeBattleEffect65Particles.Initialize6E2E20(Frame())).Frame;
        NativeBattleEffect65Frame captured = NativeBattleEffect65Particles.Allocate6E2E50(NativeBattleEffect65Particles.Initialize6E2E20(Frame()), true).Frame;
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(native)), System.Text.Json.JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(captured)));
        NativeBattleEffect65Inputs inputs = Inputs() with { CapturePresentation = true };
        NativeBattlePilotVisualIdentity first = NativeBattleEffect65Presentation.Row(captured, inputs, 0x3B530, 0, 100)!.Value;
        captured = captured with { State = captured.State.WriteDword(0x2544FC4, ArenaAddress + 0x3B530) };
        captured = NativeBattleEffect65Particles.Allocate6E2E50(captured, true).Frame;
        NativeBattlePilotVisualIdentity second = NativeBattleEffect65Presentation.Row(captured, inputs, 0x3B530, 0, 100)!.Value;
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.NotEqual(second, NativeBattleEffect65Presentation.Row(captured, inputs, 0x3B530, 0, 101));
    }

}
