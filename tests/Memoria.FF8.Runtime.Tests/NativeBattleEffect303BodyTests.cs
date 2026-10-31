using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect303BodyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Presentation_body_forwards_each_actual_pass_without_mutating_native_storage(bool quad)
    {
        byte[] source = Geometry(quad);
        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, source.AsSpan(checked((int)(address - 0x1000)), length));
        var controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096, Zsf4 = 4096 };
        var plain = NativeBattleEffect303BodyDrawing.DrawBody(Memory(), Temporary(), Read, 0x1000, [Identity(8000)], new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), controls, Scratch, 48);
        string frozen = System.Text.Json.JsonSerializer.Serialize(new { Memory = NativeBattleStorageSnapshots.Capture(plain.Memory), Temporary188 = NativeBattleStorageSnapshots.Capture(plain.Temporary188) });
        var captured = NativeBattleEffect303BodyDrawing.DrawBodyWithPresentation(Memory(), Temporary(), Read, 0x1000, [Identity(8000)], new(NativeBattleCuragaMatrices.Identity8DD770(), 100, 160, 108), controls, Scratch, 48, pilotIdentity: new(303, 0x123400, 1, 7, 0) { ResourceGeneration = 29 });
        Assert.Equal(frozen, System.Text.Json.JsonSerializer.Serialize(new { Memory = NativeBattleStorageSnapshots.Capture(captured.Memory), Temporary188 = NativeBattleStorageSnapshots.Capture(captured.Temporary188) }));
        Assert.Equal(plain.Batch.Writes.Length, captured.Batch.Writes.Length);
        List<NativeBattlePilotVisualIdentity> identities = [];
        for (int index = 0; index < plain.Batch.Writes.Length; index++)
        {
            var expected = plain.Batch.Writes[index];
            var actual = captured.Batch.Writes[index];
            Assert.Equal(expected.Bank, actual.Bank);
            Assert.Equal(expected.Attempt.Data.ToArray(), actual.Attempt.Data.ToArray());
            Assert.Equal(expected.Attempt.WriteMask.ToArray(), actual.Attempt.WriteMask.ToArray());
            if (actual.Attempt.Primitive is { Words.Length: > 1 } primitive && primitive.Words[1] >> 24 is >= 0x20 and < 0x80)
            {
                NativeBattlePilotVisualGeometry geometry = Assert.IsType<NativeBattlePilotVisualGeometry>(primitive.PilotGeometry);
                Assert.Equal((ushort)303, geometry.Identity.Effect);
                Assert.Equal(quad ? 4 : 3, geometry.CornerCount);
                Assert.False(geometry.ScreenSpace);
                Assert.True(geometry.UseSourceDepths);
                Assert.Equal(primitive.VertexPayload4[0], geometry.SourceDepths!.Value.X);
                identities.Add(geometry.Identity);
            }
        }
        Assert.Equal(2, identities.Count);
        Assert.Equal(2, identities.Distinct().Count());
    }

    [Fact]
    public void Outer_uses_recipient_shadow_composed_basis_dual_cursors_and_actual_restore()
    {
        NativeBattleGraphicsModel model = new(4, 0x20) { Color40 = 0x123456, GeometryWorldMatrix = Identity().ReadMatrix() with { X = 100, Y = 200, Z = 300 }, SequenceState = new() { MeshMask124 = 7 } };
        var body = new NativeBattleEffect303BodyFrame(Memory(), NativeBattleFileStaging.Unknown(), model, NativeBattleEffectArena.Clear().MarkUnknown(0, 188), 200, Fx);
        NativeBattleEffectArena rectangle = NativeBattleEffectArena.Clear().Write(0, new byte[] { 0xC0, 2, 0, 1, 128, 0, 128, 0 });
        List<string> calls = [];
        var services = new NativeBattleEffect303BodyServices(current =>
        {
            calls.Add("shadow");
            Assert.Equal(0x20, current.Model.Flags0 & 0x20);
            return current with { CommonCursor = 320 };
        }, (current, basis) =>
        {
            calls.Add("compose");
            Assert.Equal(new[] { 1100, 100, 800 }, new[] { basis.ReadMatrix().X, basis.ReadMatrix().Y, basis.ReadMatrix().Z });
            Assert.DoesNotContain(true, current.Temporary188.ReadSnapshot(18, 2).Known);
            return current;
        }, current =>
        {
            calls.Add("draw");
            Assert.Equal(320u, U32(current.Temporary188, 0xB4));
            Assert.Equal(Fx, U32(current.Temporary188, 0xB8));
            Assert.Equal(0x123456u, U32(current.Temporary188, 0x34));
            Assert.Equal(7u, U32(current.Temporary188, 0x38));
            Assert.Equal((ushort)0xBB, U16(current.Temporary188, 0x82));
            Assert.Equal((ushort)0x3DD4, U16(current.Temporary188, 0x80));
            Assert.Equal(40u, U32(current.Temporary188, 0x84));
            Assert.Equal(20u, U32(current.Temporary188, 0x88));
            Assert.Equal(0x00443322u, U32(current.Temporary188, 0x7C));
            return current with { Temporary188 = current.Temporary188.Write(0xB4, BitConverter.GetBytes(352u)).Write(0xB8, BitConverter.GetBytes(Fx + 56)) };
        }, current =>
        {
            calls.Add("restore");
            Assert.Equal(352u, current.CommonCursor);
            Assert.Equal(Fx + 56, current.FxCursor);
            return current with { Model = model };
        });
        NativeBattlePackedMatrix32 camera = NativeBattlePackedMatrix32.FromMatrix(Identity().ReadMatrix() with { X = 1000, Y = -100, Z = 500 }, null);
        var actual = NativeBattleEffect303BodyDrawing.DrawOuter(body, camera, rectangle, 0x3DD4, 20, 40, NativeBattleEffectArena.Clear().Write(0, new byte[] { 0x22, 0x33, 0x44, 0 }), Scratch, services);
        Assert.Equal(new[] { "shadow", "compose", "draw", "restore" }, calls);
        Assert.Same(model, actual.Model);
        Assert.DoesNotContain(true, actual.Temporary188.ReadSnapshot(0x45, 3).Known);
        Assert.Equal(0xE2000210u, U32(actual.Temporary188, 0xA4));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Projected_body_keeps_four_packet_order_separate_advances_and_opcode_FIFO(bool quad)
    {
        byte[] bytes = Geometry(quad);
        var controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096, Zsf4 = 4096 };
        var actual = NativeBattleEffect303BodyDrawing.DrawBody(Memory(), Temporary(), Read, 0x1000, [Identity(8000)], new(null, 100, 160, 108), controls, Scratch, 48);
        var packets = actual.Batch.Writes.Where(write => write.Bank != NativeBattleEffect303BodyBank.ProjectionScratch).ToArray();
        Assert.Equal(new[] { NativeBattleEffect303BodyBank.Effect, NativeBattleEffect303BodyBank.Common, NativeBattleEffect303BodyBank.Effect, NativeBattleEffect303BodyBank.Effect }, packets.Select(write => write.Bank));
        Assert.Equal(new[] { 0, 0, 12, 24 }, packets.Select(write => write.Attempt.ByteOffsetRelative));
        Assert.Equal(quad ? 40 : 32, actual.Batch.CommonAllocationBytes);
        Assert.Equal(quad ? 64 : 56, actual.Batch.EffectAllocationBytes);
        Assert.Equal(200u + (uint)actual.Batch.CommonAllocationBytes, U32(actual.Temporary188, 0xB4));
        Assert.Equal(Fx + (uint)actual.Batch.EffectAllocationBytes, U32(actual.Temporary188, 0xB8));
        Assert.Equal(new uint[] { 0xE2, quad ? 0x2Eu : 0x26u, 0xE2, quad ? 0x2Cu : 0x24u }, packets.Select(write => write.Attempt.Primitive!.Words[1] >> 24));
        Assert.Equal(new[] { 0, 0, 0, 0 }, packets[0].Attempt.Primitive!.VertexPayload4);
        Assert.Equal(quad ? new[] { 2000, 2000, 2000, 2000 } : new[] { 2000, 2000, 2000, 0 }, packets[1].Attempt.Primitive!.VertexPayload4);
        Assert.Equal(quad ? 8017 : 6017, packets[3].Attempt.Primitive!.Bucket);
        Assert.Equal(quad ? 8000u : 6000u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal((ushort)1, U16(actual.Temporary188, quad ? 0x2A : 0x28));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packets[3].Attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, packets[3].Attempt.WriteMask[31]);
        foreach (var row in actual.Batch.Writes.Where(write => write.Bank == NativeBattleEffect303BodyBank.ProjectionScratch))
        {
            Assert.Equal(12, row.Attempt.Data.Length);
            Assert.Equal(new[] { NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown, NativeBattleGfPacketByteState.Unknown }, row.Attempt.WriteMask.Skip(9));
            Assert.Equal((byte)0, row.Attempt.Data[6]);
        }

        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan().Slice(checked((int)(address - 0x1000)), length));
    }

    [Fact]
    public void Overlay_wraps_all_U_coordinates_before_offset_and_preserves_axis_argument_order()
    {
        byte[] bytes = Geometry(false);
        W(bytes, 14, 160);
        W(bytes, 20, 1600);
        W(bytes, 26, 160);
        W(bytes, 28, 1200);
        var controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0();
        var actual = NativeBattleEffect303BodyDrawing.DrawBody(Memory(), Temporary().Write(0x88, BitConverter.GetBytes(127u)), Read, 0x1000, [Identity(8000)], new(null, 100, 160, 108), controls, Scratch, 48);
        var overlay = Assert.Single(actual.Batch.Writes.Where(write => write.Bank == NativeBattleEffect303BodyBank.Common));
        Assert.Equal(new byte[] { 147, 71, 147 }, new[] { overlay.Attempt.Data[12], overlay.Attempt.Data[20], overlay.Attempt.Data[28] });
        Assert.Equal(new byte[] { 168, 168, 62 }, new[] { overlay.Attempt.Data[13], overlay.Attempt.Data[21], overlay.Attempt.Data[29] });
        Assert.Equal((ushort)0x3DD4, BinaryPrimitives.ReadUInt16LittleEndian(overlay.Attempt.Data.AsSpan().Slice(14)));
        Assert.Equal((ushort)0xBB, BinaryPrimitives.ReadUInt16LittleEndian(overlay.Attempt.Data.AsSpan().Slice(22)));

        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan().Slice(checked((int)(address - 0x1000)), length));
    }

    [Fact]
    public void Zero_area_is_accepted_without_reading_unowned_fade_or_projection_padding()
    {
        byte[] bytes = Geometry(false);
        bytes.AsSpan(14, 18).Clear();
        var actual = NativeBattleEffect303BodyDrawing.DrawBody(Memory(), Temporary(), Read, 0x1000, [Identity(8000)], new(null, 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), Scratch, 48);
        Assert.Equal(4, actual.Batch.Writes.Count(write => write.Attempt.Primitive is not null));
        Assert.Equal(0u, U32(actual.Temporary188, 0x6C));
        Assert.Equal(499u, actual.Memory.GeometryRegisters!.Mac0);
        Assert.Equal(32, actual.Batch.CommonAllocationBytes);
        Assert.Equal(56, actual.Batch.EffectAllocationBytes);

        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan().Slice(checked((int)(address - 0x1000)), length));
    }

    [Fact]
    public void Nonpositive_depth_stores_only_outcode_and_retains_prior_projection_bytes()
    {
        byte[] bytes = Geometry(false);
        var memory = Memory() with { Arena = NativeBattleEffectArena.Clear().Write(NativeBattleEffect303Profile.ScratchOffset, Enumerable.Repeat((byte)0xA5, 48).ToArray()) };
        var actual = NativeBattleEffect303BodyDrawing.DrawBody(memory, Temporary(), Read, 0x1000, [Identity(1)], new(null, 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), Scratch, 48);
        Assert.Equal(0, actual.Batch.CommonAllocationBytes);
        Assert.Equal(0, actual.Batch.EffectAllocationBytes);
        Assert.Equal(3, actual.Batch.Writes.Length);
        Assert.All(actual.Batch.Writes, write =>
        {
            Assert.Equal(NativeBattleEffect303BodyBank.ProjectionScratch, write.Bank);
            Assert.Equal(6, write.Attempt.ByteOffsetRelative % 12);
            Assert.Equal(new byte[] { 0x10 }, write.Attempt.Data);
        });
        Assert.Equal(200u, U32(actual.Temporary188, 0xB4));
        Assert.Equal(Fx, U32(actual.Temporary188, 0xB8));
        Assert.Equal(Enumerable.Repeat((byte)0xA5, 48), memory.Arena!.Read(NativeBattleEffect303Profile.ScratchOffset, 48));

        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan().Slice(checked((int)(address - 0x1000)), length));
    }

    [Fact]
    public void Projection_capacity_and_current_recipient_bone_are_required_without_fallback()
    {
        byte[] bytes = Geometry(false);
        var controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0();
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect303BodyDrawing.DrawBody(Memory(), Temporary(), Read, 0x1000, [Identity(8000)], new(null, 100, 160, 108), controls, Scratch, 24));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect303BodyDrawing.DrawBody(Memory(), Temporary(), Read, 0x1000, [], new(null, 100, 160, 108), controls, Scratch, 48));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect303BodyDrawing.DrawBody(Memory(), Temporary(), Read, 0x1000, [Identity(8000)], new(null, 100, 160, 108), controls, Scratch, 8193));

        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan().Slice(checked((int)(address - 0x1000)), length));
    }

    private const uint Scratch = NativeBattleEffect303Profile.ArenaAddress + NativeBattleEffect303Profile.ScratchOffset;
    private const uint Fx = NativeBattleEffect303Profile.ArenaAddress + NativeBattleEffect303Profile.FirstPacketOffset;
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear().MarkUnknown(NativeBattleEffect303Profile.ScratchOffset, 48), GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity().ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), null);
    private static ushort U16(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(value.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena value, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(value.Read(offset, 4));

    private static NativeBattleEffectArena Temporary()
    {
        NativeBattleEffectArena temporary = NativeBattleEffectArena.Clear().MarkUnknown(0, 188).Write(0x24, BitConverter.GetBytes(Scratch)).Write(0x2C, new byte[4]).Write(0x30, BitConverter.GetBytes((short)320)).Write(0x32, BitConverter.GetBytes((short)216)).Write(0x34, BitConverter.GetBytes(0x123456u)).Write(0x38, BitConverter.GetBytes(1u));
        temporary = temporary.Write(0x7C, BitConverter.GetBytes(0x223344u)).Write(0x80, BitConverter.GetBytes((ushort)0x3DD4)).Write(0x82, BitConverter.GetBytes((ushort)0xBB));
        temporary = temporary.Write(0x84, BitConverter.GetBytes(40u)).Write(0x88, BitConverter.GetBytes(20u)).Write(0x8C, BitConverter.GetBytes(128u)).Write(0x90, BitConverter.GetBytes(128u));
        temporary = temporary.Write(0x94, BitConverter.GetBytes(0x02000000u)).Write(0x98, BitConverter.GetBytes(0xE2000000u)).Write(0x9C, new byte[4]).Write(0xA0, BitConverter.GetBytes(0x02000000u)).Write(0xA4, BitConverter.GetBytes(0xE2000210u)).Write(0xA8, new byte[4]);
        return temporary.Write(0xB4, BitConverter.GetBytes(200u)).Write(0xB8, BitConverter.GetBytes(Fx));
    }

    private static byte[] Geometry(bool quad)
    {
        int vertices = quad ? 4 : 3;
        int countOffset = (14 + vertices * 6 + 3) & ~3;
        byte[] bytes = new byte[countOffset + 12 + (quad ? 20 : 16)];
        D(bytes, 0, 1);
        D(bytes, 4, 8);
        W(bytes, 8, 1);
        W(bytes, 12, vertices);
        W(bytes, 20, 100);
        W(bytes, 28, 100);
        if (quad)
        {
            W(bytes, 32, 100);
            W(bytes, 34, 100);
        }
        W(bytes, countOffset + (quad ? 2 : 0), 1);
        int face = countOffset + 12;
        W(bytes, face + 2, 1);
        W(bytes, face + 4, 2);
        W(bytes, face + 6, quad ? 3 : 0x8078);
        D(bytes, face + 8, 0x3D948048);
        D(bytes, face + 12, 0x00B68078);
        if (quad)
        {
            W(bytes, face + 16, 0x8078);
            W(bytes, face + 18, 0x8048);
        }
        return bytes;
    }

    private static void W(byte[] data, int offset, int value) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset), checked((ushort)value));
    private static void D(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(offset), value);
}
