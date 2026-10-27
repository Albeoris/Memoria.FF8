using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect238MeshStateTests
{
    private const uint Base = NativeBattleEffectNodeMemory.ArenaAddress;
    private static readonly NativeBattleActionMatrix Matrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 10, 20, 30, 4096);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Wrapper2_preserves_source_padding_knownness_and_global_focal_after_actual_anchor_transform(bool unknownPadding)
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        NativeBattleWaterSlotBank bank = new(0, 0x10000, 8, 0x2000);
        byte[] packed = new byte[32];
        for (int lane = 0; lane < 9; lane++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(packed.AsSpan(lane * 2), Matrix.Rotation[lane]);
        }
        BinaryPrimitives.WriteUInt16LittleEndian(packed.AsSpan(18), 0xA55A);
        BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(20), 10);
        BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(24), 20);
        BinaryPrimitives.WriteInt32LittleEndian(packed.AsSpan(28), 30);
        frame = frame with { Core = frame.Core.Write(0x27979E8, packed) };
        if (unknownPadding)
        {
            frame = frame with { Core = frame.Core.MarkUnknown(0x27979FA, 2) };
        }
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x94, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x96, 2);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x98, 1000);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8C, 256);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x8E, 512);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x90, 128);
        NativeBattleGeometryRegisterStep initial = new(NativeBattleGeometryRegisters.Unknown(), NativeBattleScriptedColorRegisters.Unknown());
        var result = NativeBattleWaterMeshPreparation.PrepareWrapper2(frame, bank, 0, 216, initial);
        Assert.Equal(216, result.Prepared.Focal);
        Assert.Equal(new int?[] { 11, 22, 1030 }, result.Registers.Geometry.Translation);
        Assert.Equal(4096, BinaryPrimitives.ReadInt16LittleEndian(result.Prepared.Frame.ReadTemporary(0xE0, 2)));
        Assert.Equal(8192, BinaryPrimitives.ReadInt16LittleEndian(result.Prepared.Frame.ReadTemporary(0xE8, 2)));
        Assert.Equal(2048, BinaryPrimitives.ReadInt16LittleEndian(result.Prepared.Frame.ReadTemporary(0xF0, 2)));
        Assert.Equal(!unknownPadding, result.Prepared.Frame.TemporaryFrame.IsKnown(0xF2, 1));
        Assert.Equal(!unknownPadding, result.Prepared.Frame.TemporaryFrame.IsKnown(0xF3, 1));
        Assert.Equal(unknownPadding ? null : (byte?)0x5A, result.Registers.Geometry.Rotation20[18]);
        Assert.Equal(unknownPadding ? null : (byte?)0xA5, result.Registers.Geometry.Rotation20[19]);
        Assert.Equal(0xA55A, BinaryPrimitives.ReadUInt16LittleEndian(NativeBattleStorageSnapshots.Capture(result.Prepared.Frame.TemporaryFrame).Bytes.AsSpan().Slice(0xF2, 2)));
        Assert.Equal(10u, result.Prepared.Frame.ReadTemporaryDword(0xF4));
    }

    [Fact]
    public void Projection_and_empty_face_list_publish_FIFO_MAC_cue_and_known_zero_vector_tail_after_json_restore()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create().WriteTemporaryDword(0x40, 0).WriteTemporaryDword(0x44, 0).WriteTemporaryDword(0x48, 0).WriteTemporaryDword(0x4C, 0).WriteTemporaryDword(0x14C, 0x600000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x74, Base + 0x20000);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797690 + 0x7C, 0x5000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0xD8, Base + 0x10000);
        byte[] shape = new byte[0x58];
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(8), 0x24);
        BinaryPrimitives.WriteUInt32LittleEndian(shape.AsSpan(0x14), 0x40);
        BinaryPrimitives.WriteInt32LittleEndian(shape.AsSpan(0x18), 3);
        BinaryPrimitives.WriteUInt16LittleEndian(shape.AsSpan(0x26), 0xFFFF);
        for (int vertex = 0; vertex < 3; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0x40 + vertex * 8), (short)(vertex * 10));
            BinaryPrimitives.WriteInt16LittleEndian(shape.AsSpan(0x44 + vertex * 8), 100);
        }
        frame = frame with { Arena = frame.Arena.Write(0x10000, shape).MarkUnknown(0x10046, 2).MarkUnknown(0x1004E, 2).MarkUnknown(0x10056, 2) };
        NativeBattleActionMatrix matrix = Matrix with { X = 0, Y = 0, Z = 1000 };
        NativeBattleGeometryRegisters geometry = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), matrix, true, true, null) with { Sxy = [11, 22, 33], Sz = [44, 55, 66, 77] };
        NativeBattleGeometryRegisterStep step = new(geometry, NativeBattleScriptedColorRegisters.Unknown());
        NativeBattleScriptedMeshSubmission Run(NativeBattleWaterExecutionFrame input)
        {
            NativeBattleScriptedMeshProjected projected = NativeBattleScriptedMeshPreparation.Project(new(input, matrix, 216), shape.Length, new(null, 216, 160, 108), 1, 0, NativeBattleEffectArena.Clear().Write(12, [0xA5]), NativeBattleScriptedLightingMemory.Unknown(), step);
            Assert.Equal(new uint?[] { 77, 1100, 1100, 1100 }, projected.Geometry!.Sz);
            Assert.Equal(new int?[] { 20, 0, 1100 }, projected.Registers.Mac);
            Assert.Equal(3u, projected.Registers.Ir0);
            Assert.Equal(0u, projected.Registers.Flags);
            Assert.Equal(new byte[] { 0, 0 }, projected.Registers.NormalInput!.Value.AsSpan().Slice(6, 2).ToArray());
            Assert.True(projected.Registers.NormalInputKnown is null || projected.Registers.NormalInputKnown.Value[6]);
            Assert.Equal(0xA5, projected.DepthOwner.Read(12, 1)[0]);
            return NativeBattleScriptedMeshPreparation.Submit(projected, shape.Length, _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException(), _ => throw new InvalidOperationException());
        }
        NativeBattleScriptedMeshSubmission result = Run(frame);
        Assert.Empty(result.Packets.Attempts);
        Assert.Equal(3u, result.Registers.Ir0);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(Run(NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleWaterExecutionFrame>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(frame)))!)))));
    }

    [Theory]
    [InlineData(7, 0xB27E90u, 20, 12, 3)]
    [InlineData(8, 0xB28110u, 20, 10, 3)]
    [InlineData(16, 0xB105B0u, 12, 4, 4)]
    [InlineData(17, 0xB28B00u, 24, 16, 4)]
    [InlineData(18, 0xB28DD0u, 24, 12, 4)]
    public void Rejected_selected_face_retains_actual_SXY_MAC0_and_flag_feedback_before_material_shading(short type, uint handler, int stride, int indexOffset, int corners)
    {
        NativeBattleWaterExecutionFrame frame = NativeBattleWaterMeshPacketTests.Frame();
        byte[] face = new byte[stride];
        byte[] records = new byte[corners * 8];
        uint[] screens = [0, 10u << 16, 10, (10u << 16) | 10];
        for (int corner = 0; corner < corners; corner++)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(face.AsSpan(indexOffset + corner * 2), (ushort)(corner * 8));
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(corner * 8), screens[corner]);
            BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(corner * 8 + 4), 100);
        }
        NativeBattleScriptedMeshSubmission result = NativeBattleScriptedMeshPackets.EmitSelected(frame, new(type, 1, face.ToImmutableArray()), handler, 0, records.ToImmutableArray(), Enumerable.Repeat(100u, corners).ToImmutableArray(), 0x5000, NativeBattleScriptedLightingMemory.Unknown(), NativeBattleScriptedColorRegisters.Unknown(), _ => throw new InvalidOperationException(), NativeBattleGeometryRegisters.Unknown());
        Assert.Equal(0, result.Packets.AllocationBytes);
        Assert.Null(Assert.Single(result.Packets.Attempts).Primitive);
        Assert.Equal(new uint?[] { 0, 10u << 16, 10 }, result.Geometry!.Sxy);
        Assert.Equal(unchecked((uint)-100), result.Geometry.Mac0);
        Assert.Equal(0u, result.Registers.Flags);
        Assert.Null(result.Registers.Rgb[2]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, result.Packets.Attempts[0].WriteMask[3]);
    }
}
