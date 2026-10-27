using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleEffect69Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect69StageTests
{
    private const uint GeometryAddress = 0x193B000;
    private const uint ScratchAddress = 0x1D98B3C;
    private const uint Arena = 0x20DFAB8;
    private const uint Metric = Arena + 0x5A50;
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Fact]
    public void Capture_uses_shifted_squared_IR_and_actual_masked_faces_not_average_Z()
    {
        Harness harness = new([new(64, 0, 0), new(0, 128, 0), new(0, 0, 256)]);
        NativeBattleEffect69Frame frame = harness.Frame();
        NativeBattlePackedMatrix32 cpu = NativeBattlePackedMatrix32.FromMatrix(Identity, null);
        NativeBattleEffect69StageStep result = NativeBattleEffect69StageCapture.StageCapture6FE300(frame, 1, cpu, Metric, harness.Inputs());
        Assert.Equal(Metric + 4, result.NextMetric);
        Assert.Equal(7u, U(result.Frame.Memory.Arena!, 0x5A50));
        Assert.Equal(new[] { 1, 4, 16 }, Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadInt32LittleEndian(result.Frame.Staging!.Read(ScratchAddress + (uint)(index * 4), 4))));
        Assert.Equal(new int?[] { 0, 0, 16 }, result.Frame.Memory.ScriptedColorRegisters!.Mac);
        Assert.All(result.Frame.Memory.ScriptedColorRegisters.NormalInput!.Value.AsSpan().Slice(6, 2).ToArray(), value => Assert.Equal((byte)0, value));
        Assert.Same(cpu, harness.LastCpu);
        Assert.Equal((ushort)0xFFFF, BinaryPrimitives.ReadUInt16LittleEndian(result.Frame.Staging!.Read(NativeBattleTemporaryMemory.Address + 12, 2)));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Shifted_SQR_publishes_full_MAC_saturated_IR_and_lane_flags()
    {
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown() with { Ir = [-32768, 32767, 64], Ir0 = 123 });
        registers = NativeBattleEffect69StageCapture.Square45F9E0(registers);
        Assert.Equal(new int?[] { 262144, 262128, 1 }, registers.Colors.Mac);
        Assert.Equal(new int?[] { 32767, 32767, 1 }, registers.Colors.Ir);
        Assert.Equal(0x81800000u, registers.Colors.Flags);
        Assert.Equal(123u, registers.Colors.Ir0);
    }

    [Fact]
    public void Stage_faces_detach_once_with_five_private_draws_and_retain_triangle_record_holes()
    {
        Harness harness = new();
        NativeBattleEffect69Frame frame = harness.Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x36, BitConverter.GetBytes((ushort)0xBEEF)) } };
        NativeBattleEffect69StageStep result = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(frame, 1, 1, 63, Metric, harness.Inputs());
        NativeBattleEffectArena debris = result.Frame.Memory.Arena!;
        Assert.Equal((short)33, S(debris, 0));
        Assert.Equal((short)33, S(debris, 2));
        Assert.Equal((ushort)1, W(debris, 6));
        Assert.Equal((short)-17, S(debris, 8));
        Assert.Equal((short)-388, S(debris, 10));
        Assert.Equal((short)5, S(debris, 12));
        Assert.Equal((ushort)12, W(debris, 14));
        Assert.Equal(Arena + 0x5708, U(debris, 16));
        Assert.Equal((ushort)0xBEEF, W(debris, 0x36));
        Assert.Equal(uint.MaxValue, U(debris, 0x5A50));
        Assert.Equal(10582u, result.Frame.State.ReadDword(RandomGlobal));
        Assert.Equal(Arena + 0x3C, result.Frame.State.ReadDword(DebrisCursorGlobal));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Empty(harness.LastBatch!.Attempts);
        result = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(result.Frame, 1, 1, 63, Metric, harness.Inputs());
        Assert.Equal(10582u, result.Frame.State.ReadDword(RandomGlobal));
    }

    [Fact]
    public void Exhausted_debris_bank_suppresses_face_without_consuming_random_or_marking_metric()
    {
        Harness harness = new();
        NativeBattleEffect69Frame frame = harness.Frame();
        NativeBattleEffectArena arena = frame.Memory.Arena!;
        for (int index = 0; index < 364; index++)
        {
            arena = arena.Write(index * 0x3C + 6, BitConverter.GetBytes((ushort)1));
        }
        frame = frame with { Memory = frame.Memory with { Arena = arena } };
        NativeBattleEffect69StageStep result = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(frame, 1, 1, 63, Metric, harness.Inputs());
        Assert.Equal(0u, result.Frame.State.ReadDword(RandomGlobal));
        Assert.Equal(0u, result.Frame.State.ReadDword(DebrisCursorGlobal));
        Assert.Equal(0u, U(result.Frame.Memory.Arena!, 0x5A50));
        Assert.Empty(harness.LastBatch!.Attempts);
    }

    [Theory]
    [InlineData(false, 32, 499)]
    [InlineData(true, 40, 500)]
    public void Draw_uses_current_quarter_depth_AVSZ_and_raw_UV_material_with_retained_holes(bool quad, int bytes, int depth)
    {
        Harness harness = new(quad: quad);
        NativeBattleEffect69Frame frame = harness.Frame();
        frame = frame with { Memory = frame.Memory with { Arena = frame.Memory.Arena!.Write(0x5A50, BitConverter.GetBytes(100)) } };
        NativeBattleEffect69StageStep result = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(frame, 1, 0, 63, Metric, harness.Inputs());
        NativeBattleGfGeometryAttempt attempt = Assert.Single(harness.LastBatch!.Attempts);
        Assert.Equal(bytes, harness.LastBatch.AllocationBytes);
        Assert.Equal(17 + depth, attempt.Primitive!.Bucket);
        Assert.Equal(quad ? 0x2E123456u : 0x26123456u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(4)));
        Assert.Equal(0x00B68078u, BinaryPrimitives.ReadUInt32LittleEndian(attempt.Data.AsSpan().Slice(20)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
        Assert.Equal(new[] { 2000, 2000, 2000, quad ? 2000 : 0 }, attempt.Primitive.VertexPayload4);
        Assert.Equal((short)0, BinaryPrimitives.ReadInt16LittleEndian(result.Frame.Staging!.Read(ScratchAddress + 12, 2)));
        Assert.DoesNotContain(true, result.Frame.Staging.ReadSnapshot(ScratchAddress + 14, 2).Known);
        Assert.Equal((uint)depth, result.Frame.Memory.GeometryRegisters!.Otz);
        result = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(result.Frame, 1, 0, 63, Metric, harness.Inputs(), 4122, 16);
        Assert.Equal(4122, Assert.Single(harness.LastBatch!.Attempts).Primitive!.Bucket);
    }

    [Fact]
    public void Zero_area_is_admitted_negative_area_rejected_and_current_byte_continuation_roundtrips()
    {
        Harness zero = new([new(0, 0, 0), new(0, 0, 0), new(0, 0, 0)]);
        NativeBattleEffect69Frame zeroFrame = zero.Frame();
        NativeBattleEffect69StageDrawing.StageDraw6FF6E0(zeroFrame, 1, 0, 63, Metric, zero.Inputs());
        Assert.Single(zero.LastBatch!.Attempts);
        Harness negative = new([new(0, 0, 0), new(0, 100, 0), new(100, 0, 0)]);
        NativeBattleEffect69Frame frame = negative.Frame();
        NativeBattleEffect69StageStep result = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(frame, 1, 0, 63, Metric, negative.Inputs());
        Assert.Empty(negative.LastBatch!.Attempts);
        Assert.True(unchecked((int)result.Frame.Memory.GeometryRegisters!.Mac0!.Value) < 0);
        NativeBattleEffect69Frame restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleEffect69Frame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result.Frame)))!);
        NativeBattleEffect69Frame first = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(result.Frame, 1, 1, 63, Metric, negative.Inputs()).Frame;
        NativeBattleEffect69Frame second = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(restored, 1, 1, 63, Metric, negative.Inputs()).Frame;
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(first)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(second)));
    }

    [Fact]
    public void Stage_projection_loads_each_retained_W_byte_with_its_actual_knownness()
    {
        Harness harness = new();
        NativeBattleEffect69Frame frame = harness.Frame();
        frame = frame with { Staging = frame.Staging!.Write(ScratchAddress + 2 * 16 + 14, [(byte)0xCA]) };
        NativeBattleEffect69Frame result = NativeBattleEffect69StageDrawing.StageDraw6FF6E0(frame, 1, 0, 63, Metric, harness.Inputs()).Frame;
        NativeBattleScriptedColorRegisters registers = result.Memory.ScriptedColorRegisters!;
        Assert.Equal((byte)0xCA, registers.NormalInput!.Value[6]);
        Assert.True(registers.NormalInputKnown!.Value[6]);
        Assert.False(registers.NormalInputKnown.Value[7]);
        (byte[] bytes, bool[] known) = result.Staging!.ReadSnapshot(ScratchAddress + 2 * 16 + 14, 2);
        Assert.Equal((byte)0xCA, bytes[0]);
        Assert.True(known[0]);
        Assert.False(known[1]);
    }

    [Fact]
    public void Stage_scratch_and_source_indices_fail_at_the_actual_selected_bounds()
    {
        Harness harness = new();
        NativeBattleEffect69Frame frame = harness.Frame();
        NativeBattleEffect69Inputs inputs = harness.Inputs() with { ReadStage = (_, _) => harness.Source with { ScratchLength = 47 } };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect69StageDrawing.StageDraw6FF6E0(frame, 1, 0, 63, Metric, inputs));
        byte[] invalid = harness.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(invalid.AsSpan(harness.FaceOffset), 3);
        frame = frame with { Staging = frame.Staging!.Write(GeometryAddress, invalid) };
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect69StageCapture.StageCapture6FE300(frame, 1, NativeBattlePackedMatrix32.FromMatrix(Identity, null), Metric, harness.Inputs()));
    }

    private sealed class Harness
    {
        public byte[] Bytes { get; }
        public int FaceOffset { get; }
        public NativeBattleEffect69StageSource Source { get; }
        public NativeBattlePackedMatrix32? LastCpu { get; private set; }
        public NativeBattleGfGeometryBatch? LastBatch { get; private set; }

        public Harness(NativeBattleVector[]? vertices = null, bool quad = false)
        {
            vertices ??= quad ? [new(0, 0, 0), new(100, 0, 0), new(0, 100, 0), new(100, 100, 0)] : [new(0, 0, 0), new(100, 0, 0), new(0, 100, 0)];
            int counts = (14 + vertices.Length * 6 + 3) & ~3;
            FaceOffset = counts + 12;
            Bytes = new byte[FaceOffset + (quad ? 24 : 20)];
            BinaryPrimitives.WriteInt32LittleEndian(Bytes, 1);
            BinaryPrimitives.WriteInt32LittleEndian(Bytes.AsSpan(4), 8);
            Put(Bytes, 8, 1);
            Put(Bytes, 12, vertices.Length);
            for (int index = 0; index < vertices.Length; index++)
            {
                Put(Bytes, 14 + index * 6, vertices[index].X);
                Put(Bytes, 16 + index * 6, vertices[index].Y);
                Put(Bytes, 18 + index * 6, vertices[index].Z);
            }
            Put(Bytes, counts + (quad ? 6 : 4), 1);
            for (int corner = 0; corner < (quad ? 4 : 3); corner++)
            {
                Put(Bytes, FaceOffset + corner * 2, 0xF000 | corner);
            }
            if (!quad)
            {
                Put(Bytes, FaceOffset + 6, 0x8078);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(Bytes.AsSpan(FaceOffset + 8), 0x3D948048);
            BinaryPrimitives.WriteUInt32LittleEndian(Bytes.AsSpan(FaceOffset + 12), 0x02B68078);
            if (quad)
            {
                Put(Bytes, FaceOffset + 16, 0x8078);
                Put(Bytes, FaceOffset + 18, 0x8048);
            }
            BinaryPrimitives.WriteUInt32LittleEndian(Bytes.AsSpan(FaceOffset + (quad ? 20 : 16)), quad ? 0x2C123456u : 0x24123456u);
            ImmutableArray<NativeBattleRawMesh> meshes = NativeBattleRawGeometryReader.Read(Bytes, 1);
            Source = new(new(7, 0, 0, 0, 0, 0, 0, null, [Identity]), meshes, ScratchAddress, vertices.Length * 16) { GeometryAddress = GeometryAddress };
        }

        public NativeBattleEffect69Frame Frame()
        {
            NativeBattleOriginalEffectState effect = new(69, 68, new(69, Entry, Initializer), new(4, 0, 0x7F, 0, 0, 0), null);
            NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() };
            NativeBattleFileStaging staging = NativeBattleTemporaryMemory.Reset5082F0(NativeBattleFileStaging.Unknown().Write(GeometryAddress, Bytes));
            return new(memory, effect, new(1), NativeBattleEffect69State.FromColdProcess().WriteDword(ArenaGlobal, Arena)) { Staging = staging };
        }

        public NativeBattleEffect69Inputs Inputs() => new(NativeBattleGteColorInterpolation.CodeConstants45F270, _ => NativeBattlePackedMatrix32.FromMatrix(Identity with { Z = 8000 }, null), _ => new(null, 100, 160, 108), (_, _) => throw new NotSupportedException(), (_, _) => throw new NotSupportedException(), _ => 0, _ => 0, (_, _) => 0, (frame, operation) =>
        {
            LastBatch = Assert.IsType<NativeBattleEffect69DeliverCommonBatch>(operation).Batch;
            return frame;
        })
        {
            ReadStage = (_, _) => Source,
            PrepareStage = (frame, _, cpu) =>
            {
                LastCpu = cpu;
                return frame;
            }
        };
    }

    private static void Put(byte[] data, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), unchecked((ushort)value));
    private static ushort W(NativeBattleEffectArena data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.Read(offset, 2));
    private static short S(NativeBattleEffectArena data, int offset) => BinaryPrimitives.ReadInt16LittleEndian(data.Read(offset, 2));
    private static uint U(NativeBattleEffectArena data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.Read(offset, 4));
}
