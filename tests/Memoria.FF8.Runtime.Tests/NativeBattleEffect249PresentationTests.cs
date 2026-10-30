using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect249PresentationTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private const uint Manager = 0x2797690;
    private static readonly NativeBattleWaterSlotBank Bank = new(0x2000, 0x400, 4, 0x2000);
    private static readonly NativeBattleActionMatrix View = NativeBattleCuragaMatrices.Identity8DD770() with { Z = 2000 };
    private static readonly NativeBattleCameraProjection Projection = new(View, 216, 160, 108);

    [Theory]
    [InlineData((short)8, 0x9B9710u, 0xB28110u, false)]
    [InlineData((short)18, 0x9B9A40u, 0xB28DD0u, false)]
    [InlineData((short)8, 0x9B9710u, 0xB28110u, true)]
    [InlineData((short)18, 0x9B9A40u, 0xB28DD0u, true)]
    public void Reached_wrapper2_triangle_and_quad_keep_actual_source_registers_and_native_depth(short type, uint selected, uint canonical, bool reject)
    {
        Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(249, "prim_table", type, selected));
        NativeBattleScriptedMeshSubmission native = Mesh(type, canonical, false, reject);
        string nativeFrame = NativeBattleTestSnapshots.Json(native.Frame);
        string nativeBatch = NativeBattleTestSnapshots.Json(native.Packets);
        string nativeRegisters = NativeBattleTestSnapshots.Json(native.Registers);
        string nativeGeometry = NativeBattleTestSnapshots.Json(native.Geometry);
        NativeBattleScriptedMeshSubmission captured = Mesh(type, canonical, true, reject);
        NativeBattleGfGeometryBatch batch = NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 2, captured.Packets);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(nativeBatch, NativeBattleTestSnapshots.Json(batch));
        Assert.Equal(nativeRegisters, NativeBattleTestSnapshots.Json(captured.Registers));
        Assert.Equal(nativeGeometry, NativeBattleTestSnapshots.Json(captured.Geometry));
        Assert.All(native.Packets.Attempts, attempt => Assert.Null(attempt.Primitive?.PilotGeometry));
        if (reject)
        {
            Assert.All(batch.Attempts, attempt => Assert.Null(attempt.Primitive));
            return;
        }
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal((byte)(type == 8 ? 3 : 4), geometry.CornerCount);
        Assert.Equal((ushort)249, geometry.Identity.Effect);
        Assert.Equal(Base + 0x2000, geometry.Identity.Bank);
        Assert.Equal(0, geometry.Identity.Slot);
        Assert.True(geometry.Identity.Generation > 0);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(Payload(primitive), geometry.SourceDepths);
        Assert.NotEqual(MathF.Truncate(geometry.A.Z), geometry.A.Z);
        Assert.Equal(new Vector3(-100, -100, 1000 - 1200f / 4096), geometry.A);
        string before = NativeBattleTestSnapshots.Json(captured.Frame);
        NativeBattlePilotVisualGeometry sampled = NativeBattlePilotVisualGeometry.Sample(geometry, geometry with { A = geometry.A + Vector3.UnitX * 4 }, 0.5f);
        Assert.Equal(geometry.A + Vector3.UnitX * 2, sampled.A);
        Assert.Equal(before, NativeBattleTestSnapshots.Json(captured.Frame));
    }

    [Theory]
    [InlineData((ushort)0x20)]
    [InlineData((ushort)0)]
    public void Reached_sequence5_mode9_keeps_native_rectangles_and_compound_E1_payload(ushort tpage)
    {
        var native = Sprite(tpage, false);
        string nativeFrame = NativeBattleTestSnapshots.Json(native.Frame);
        string nativeRegisters = NativeBattleTestSnapshots.Json(native.Registers);
        string nativeBatch = NativeBattleTestSnapshots.Json(native.Packets);
        var captured = Sprite(tpage, true);
        NativeBattleGfGeometryBatch batch = NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 5, captured.Packets);
        Assert.Equal(nativeFrame, NativeBattleTestSnapshots.Json(captured.Frame));
        Assert.Equal(nativeRegisters, NativeBattleTestSnapshots.Json(captured.Registers));
        Assert.Equal(nativeBatch, NativeBattleTestSnapshots.Json(batch));
        NativeBattleGeometryPrimitive primitive = Assert.Single(batch.Attempts).Primitive!;
        NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
        Assert.Equal((byte)4, geometry.CornerCount);
        Assert.True(geometry.UseSourceDepths);
        Assert.Equal(Payload(primitive), geometry.SourceDepths);
        Assert.Equal((ushort)249, geometry.Identity.Effect);
        Assert.Equal(tpage == 0 ? 44 : 40, batch.AllocationBytes);
        if (tpage == 0)
        {
            Assert.Equal(0xE1000220u, primitive.Words[^1]);
        }
        Assert.NotEqual(geometry.A, geometry.D);
        Assert.Null(Assert.Single(native.Packets.Attempts).Primitive!.PilotGeometry);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Actual_slot_recycle_prevents_cross_birth_sampling_and_keeps_each_source_domain(byte kind)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, Bank, 0, 3, 0, 0) };
        NativeBattlePilotVisualIdentity first = NativeBattleCinematicVisualIdentity.Identity(249, frame, Bank, 0);
        Assert.Equal(first, NativeBattleCinematicVisualIdentity.Identity(249, frame, Bank, 0));
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, Bank, 1, 3, 0, 0) };
        NativeBattlePilotVisualIdentity other = NativeBattleCinematicVisualIdentity.Identity(249, frame, Bank, 1);
        Assert.NotEqual(first, other);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, Bank, 0, 3, 0, 0) };
        NativeBattlePilotVisualIdentity recycled = NativeBattleCinematicVisualIdentity.Identity(249, frame, Bank, 0);
        Assert.NotEqual(first.Generation, recycled.Generation);
        NativeBattlePilotVisualGeometry geometry = new(first, Vector3.Zero, Vector3.UnitX, Vector3.UnitY, default, 3);
        NativeBattleGeometryPrimitive primitive = new(17, [0x6000000, 0x30000000, 0, 0, 0, 0, 0], [777, 1000, 1001, 0]) { PilotGeometry = geometry };
        NativeBattleGfGeometryBatch batch = Batch(primitive);
        NativeBattlePilotVisualGeometry captured = Assert.Single(NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, kind, batch).Attempts).Primitive!.PilotGeometry!;
        Assert.Same(captured, NativeBattlePilotVisualGeometry.Sample(captured, captured with { Identity = recycled, A = new(99) }, 0.5f));
        Assert.Equal(first, captured.Identity);
    }

    [Fact]
    public void Finalization_keeps_defaults_other_families_state_packets_and_existing_receipts_unchanged()
    {
        NativeBattleGeometryPrimitive native = new(17, [0x6000000, 0x30000000, 0, 0, 0, 0, 0], [1, 2, 3, 4]);
        NativeBattleGfGeometryBatch omitted = Batch(native);
        Assert.Same(omitted, NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 2, omitted));
        NativeBattlePilotVisualGeometry geometry = new(new(249, 1, 2, 3, 4), default, default, default, default, 3) { ScreenSpace = true, ScreenScale = 8, FocalLengthOverride = 300, ScreenOffsets = [new(1, 2), new(3, 4), new(5, 6)] };
        NativeBattleGfGeometryBatch batch = Batch(native with { PilotGeometry = geometry });
        Assert.Same(batch, NativeBattleEffect249Presentation.FinalizeCapturedBatch(248, 2, batch));
        Assert.Same(batch, NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 6, batch));
        NativeBattleGfGeometryBatch finalized = NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 2, batch);
        Assert.Equal(geometry with { SourceDepths = new Vector4(1, 2, 3, 4), UseSourceDepths = true }, Assert.Single(finalized.Attempts).Primitive!.PilotGeometry);
        Assert.Same(finalized, NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 2, finalized));
        NativeBattleGfGeometryBatch state = Batch(new(17, [0x1000000, 0xE1000220], []) { PilotGeometry = geometry });
        Assert.Same(state, NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 2, state));
    }

    [Fact]
    public void Actual_empty_payload_is_explicit_zero_and_unproved_arity_fails_at_capture_boundary()
    {
        NativeBattlePilotVisualGeometry geometry = new(new(249, 1, 2, 3, 4), default, default, default, default, 3);
        NativeBattleGeometryPrimitive primitive = new(17, [0x6000000, 0x30000000, 0, 0, 0, 0, 0], []) { PilotGeometry = geometry };
        NativeBattleGfGeometryBatch captured = NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 2, Batch(primitive));
        Assert.Equal(Vector4.Zero, Assert.Single(captured.Attempts).Primitive!.PilotGeometry!.SourceDepths);
        Assert.True(Assert.Single(captured.Attempts).Primitive!.PilotGeometry!.UseSourceDepths);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect249Presentation.FinalizeCapturedBatch(249, 2, Batch(primitive with { VertexPayload4 = [1, 2, 3] })));
    }

    private static NativeBattleScriptedMeshSubmission Mesh(short type, uint handler, bool capture, bool reject)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x14C, 0x5000);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, Bank, 0, 3, 0, 0) };
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x74, Base + 0x20000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x7C, 0x300000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xD8, Base + 0x10000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x00808080);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x4C, 1);
        foreach (int offset in new[] { 0x8C, 0x8E, 0x90 })
        {
            frame = WaterSchedulerFixture.SlotWord(frame, 0, offset, 256);
        }
        NativeBattleActionMatrix matrix = new([4096, 0, 0, 0, 4096, 0, 5, 7, 4096], 0, 0, reject ? -5000 : 2000, 4096);
        frame = frame with { Core = frame.Core.Write(0x27979E8, NativeBattlePackedMatrix32.FromMatrix(matrix, 0).Bytes.AsSpan()) };
        int corners = type == 8 ? 3 : 4;
        int stride = type == 8 ? 20 : 24;
        byte[] shape = new byte[0xC0];
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(8), 0x30);
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(0x14), 0xA0);
        BinaryPrimitives.WriteInt32LittleEndian(shape.AsSpan(0x18), corners);
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(0x1C), 0xC0);
        BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0x30), type);
        BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0x32), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(0x34), 0x808080);
        int indices = type == 8 ? 10 : 12;
        for (int corner = 0; corner < corners; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(shape.AsSpan(0x34 + indices + corner * 2), (ushort)(corner * 8));
            BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0xA0 + corner * 8), (short)(corner % 2 == 0 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0xA2 + corner * 8), (short)(corner < 2 ? -100 : 100));
            BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0xA4 + corner * 8), (short)(1000 + corner));
            BinaryPrimitives.WriteUInt16LittleEndian(shape.AsSpan(0xA6 + corner * 8), 0x4321);
        }
        BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0x34 + stride + 2), -1);
        frame = frame with { Arena = frame.Arena.Write(0x10000, shape) };
        NativeBattleGeometryRegisterStep step = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        var wrapper = NativeBattleWaterMeshPreparation.PrepareWrapper2(frame, Bank, 0, 216, step);
        NativeBattleWaterMeshPrepared prepared = wrapper.Prepared;
        step = wrapper.Registers;
        NativeBattlePilotVisualIdentity? identity = null;
        if (capture)
        {
            identity = NativeBattleCinematicVisualIdentity.Identity(249, frame, Bank, 0);
        }
        NativeBattleScriptedMeshProjected projected = NativeBattleScriptedMeshPreparation.Project(prepared, shape.Length, Projection, 0, 0, NativeBattleEffectArena.Clear(), NativeBattleScriptedLightingMemory.Unknown(), step, pilotIdentity: identity);
        return NativeBattleScriptedMeshPreparation.Submit(projected, shape.Length, _ => handler, _ => (byte)stride, pointer => (int)(pointer / 4));
    }

    private static NativeBattleWaterParticleRectangleResult Sprite(ushort tpage, bool capture)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryByte(0x145, 1).WriteTemporaryDword(0x14C, 0x5000);
        frame = frame with { Arena = NativeBattleWaterSlots.InitializeB65160(frame.Arena, Bank, 0, 3, 0, 0) };
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x7C, 0x300000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xD8, Base + 0x10000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xCC, 0x02000000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 256);
        frame = frame with { Arena = frame.Arena.Write(0x201E, [9]), Core = frame.Core.Write(0x27979E8, NativeBattlePackedMatrix32.FromMatrix(View, 0).Bytes.AsSpan()).Write(0x2797968, NativeBattlePackedMatrix32.FromMatrix(NativeBattleCuragaMatrices.Identity8DD770(), 0).Bytes.AsSpan()) };
        byte[] source = new byte[24];
        BinaryPrimitives.WriteInt32LittleEndian(source, 1);
        foreach (int offset in new[] { 8, 10 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(source.AsSpan(offset), 4096);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(14), 0x12F8);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(16), 0x0420);
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(18), 0x1234);
        source[20] = 0x33;
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(22), tpage);
        frame = frame with { Arena = frame.Arena.Write(0x10000, source) };
        NativeBattlePilotVisualIdentity? identity = capture ? NativeBattleCinematicVisualIdentity.Identity(249, frame, Bank, 0) : null;
        NativeBattleGeometryRegisterStep registers = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleWaterParticleRectangleResult? result = null;
        _ = NativeBattleWaterRectangleCommands.AdvanceDraw5(frame, (_, _) => 0x27979E8, (current, matrix) =>
        {
            result = NativeBattleWaterParticleRectanglePackets.DrawA9EE90(current, registers, Projection, 1365, 0, 0, _ => 17, matrix, identity);
            registers = result.Registers;
            return result.Frame;
        }, _ => registers, (current, published) =>
        {
            registers = published;
            return current;
        });
        return result ?? throw new InvalidDataException("Expected the actual sequence rectangle callback.");
    }

    private static NativeBattleGfGeometryBatch Batch(NativeBattleGeometryPrimitive primitive)
    {
        return new([new(0, new byte[primitive.Words.Length * 4].ToImmutableArray(), new NativeBattleGfPacketByteState[primitive.Words.Length * 4].ToImmutableArray(), primitive)], primitive.Words.Length * 4);
    }

    private static Vector4 Payload(NativeBattleGeometryPrimitive primitive)
    {
        return new(primitive.VertexPayload4[0], primitive.VertexPayload4[1], primitive.VertexPayload4[2], primitive.VertexPayload4[3]);
    }
}
