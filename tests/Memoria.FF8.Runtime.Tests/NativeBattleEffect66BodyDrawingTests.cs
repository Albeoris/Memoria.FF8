using System.Buffers.Binary;
using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect66Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect66BodyDrawingTests
{
    [Fact]
    public void Metadata_uses_saturated_current_bone_IR_with_known_zero_W_and_only_ORs_threshold_word()
    {
        NativeBattleEffect66Frame frame = Frame();
        frame = WriteBytes(frame, FlagAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x4000)), 2);
        NativeBattleEffectArena temporary = Temporary(0);
        byte[] geometry = Geometry(secondY: -6000);
        NativeBattlePackedMatrix32 bone = Packed(50000, 0, 0);
        NativeBattleEffect66BodyStep result = Draw(frame, geometry, temporary, bone);
        Assert.Equal(short.MaxValue, S16(result.Scratch, 0));
        Assert.Equal(0, S16(result.Scratch, 6));
        Assert.Equal(0x4000, U16(ReadBytes(result.Frame, FlagAddress, 2), 0));
        Assert.Equal(FlagAddress + 2, U32(result.Temporary176, 0x2C));
        Assert.Equal(frame.Random, result.Frame.Random);
        Assert.Throws<NotSupportedException>(() => result.Temporary176.Read(0x28, 3));
    }

    [Fact]
    public void Ordinary_body_emits_native_partial_UVs_signed_last_word_and_actual_depths_without_alpha()
    {
        List<NativeBattleEffect66Operation> operations = [];
        NativeBattleEffect66BodyStep result = Draw(Frame(), Geometry(), Temporary(1), Packed(), Inputs(operations));
        NativeBattleEffect66DeliverBatch batch = Assert.IsType<NativeBattleEffect66DeliverBatch>(Assert.Single(operations));
        NativeBattleGfGeometryAttempt attempt = Assert.Single(batch.Batch.Attempts);
        Assert.Equal(32, batch.Batch.AllocationBytes);
        Assert.Equal(0x1D8E054u, batch.CursorGlobal);
        Assert.Equal(0x26112233u, attempt.Primitive!.Words[1]);
        Assert.Equal(0x12341234u, attempt.Primitive.Words[3]);
        Assert.Equal(0x00204567u, attempt.Primitive.Words[5]);
        Assert.Equal(0xFFFFFAB2u, attempt.Primitive.Words[7]);
        Assert.Equal(new[] { 2000, 2000, 2000, 0 }, attempt.Primitive.VertexPayload4);
        Assert.All(attempt.WriteMask, value => Assert.Equal(NativeBattleGfPacketByteState.Known, value));
        Assert.Throws<NotSupportedException>(() => result.Temporary176.Read(0x28, 3));
        Assert.Throws<NotSupportedException>(() => result.Temporary176.Read(0x10, 4));
    }

    [Fact]
    public void Retired_face_skips_invalid_indices_and_keeps_arithmetic_state_after_raw_bone_producer()
    {
        NativeBattleEffect66Frame frame = Frame();
        frame = WriteBytes(frame, FlagAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x8000)), 2);
        byte[] geometry = Geometry();
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(52), 0xFFF);
        List<NativeBattleEffect66Operation> operations = [];
        NativeBattleEffect66BodyStep result = Draw(frame, geometry, Temporary(1), Packed(), Inputs(operations));
        Assert.Empty(operations);
        Assert.Equal(FlagAddress + 2, U32(result.Temporary176, 0x2C));
        Assert.Equal(frame.Random, result.Frame.Random);
    }

    [Fact]
    public void Final_shard_age_renders_pre_motion_corners_then_overlay_and_retires_flag_without_reading_indices()
    {
        NativeBattleEffect66Frame frame = Frame();
        frame = WriteBytes(frame, FlagAddress, NativeBattleEffectArena.Clear(), 2);
        uint shardAddress = frame.State.ReadDword(0x24C917C);
        NativeBattleEffectArena shard = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)63)).Write(2, BitConverter.GetBytes((short)16));
        short[] xy = [-100, -100, 100, -100, -100, 100];
        for (int corner = 0; corner < 3; corner++)
        {
            shard = shard.Write(4 + corner * 8, BitConverter.GetBytes(xy[corner * 2])).Write(6 + corner * 8, BitConverter.GetBytes(xy[corner * 2 + 1])).Write(0x24 + corner * 8, BitConverter.GetBytes((short)4096));
        }
        frame = WriteBytes(frame, shardAddress, shard, 68);
        byte[] geometry = Geometry();
        BinaryPrimitives.WriteUInt16LittleEndian(geometry.AsSpan(52), 0xFFF);
        List<NativeBattleEffect66Operation> operations = [];
        NativeBattleEffect66BodyStep result = Draw(frame, geometry, Temporary(100), Packed(), Inputs(operations));
        Assert.Equal(2, operations.Count);
        NativeBattleEffect66DeliverBatch body = Assert.IsType<NativeBattleEffect66DeliverBatch>(operations[0]);
        NativeBattleEffect66DeliverBatch overlay = Assert.IsType<NativeBattleEffect66DeliverBatch>(operations[1]);
        Assert.Equal(32, body.Batch.AllocationBytes);
        Assert.Equal(20, overlay.Batch.AllocationBytes);
        Assert.Equal(0x22FFFFFFu, overlay.Batch.Attempts[0].Primitive!.Words[1]);
        Assert.Equal(body.Batch.Attempts[0].Primitive!.Bucket - 1, overlay.Batch.Attempts[0].Primitive!.Bucket);
        Assert.Equal(new[] { 0, 0, 0, 0 }, overlay.Batch.Attempts[0].Primitive.VertexPayload4);
        Assert.Equal(0x8000, U16(ReadBytes(result.Frame, FlagAddress, 2), 0));
        Assert.Equal(0, S16(ReadBytes(result.Frame, shardAddress, 2), 0));
        Assert.Equal(-88, S16(ReadBytes(result.Frame, shardAddress + 4, 2), 0));
    }

    [Fact]
    public void Delivery_before_next_face_observes_current_flag_owner()
    {
        NativeBattleEffect66Frame frame = Frame();
        frame = WriteBytes(frame, FlagAddress + 2, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x4000)), 2);
        List<NativeBattleEffect66Operation> operations = [];
        NativeBattleEffect66Inputs inputs = Inputs(operations, current => WriteBytes(current, FlagAddress + 2, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x8000)), 2));
        NativeBattleEffect66BodyStep result = Draw(frame, Geometry(faces: 2), Temporary(1), Packed(), inputs);
        Assert.Single(operations);
        Assert.Equal(FlagAddress + 4, U32(result.Temporary176, 0x2C));
    }

    private const uint Common = 0x20DFAB8;
    private const uint FlagAddress = Common + 0x28000;
    private static NativeBattleEffect66Frame Frame()
    {
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear(), GeometryRegisters = NativeBattleGeometryRegisters.Unknown(), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown(), ScriptedGraphics = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), PrimitiveProcess = new(null, null, null, 0, null) { ModelRendererRegisters = new(null, null, null, null, null) } };
        NativeBattleEffect66State state = NativeBattleEffect66State.FromColdProcess().WriteDword(0x24C91A0, Common).WriteDword(0x24C917C, Common + 0x29184);
        NativeBattleEffect66Frame frame = new(memory, new(66, 65, new(66, 0x622610, 0x6225F0), null, null) { Pools = [] }, new(123), state);
        return WriteBytes(frame, FlagAddress, NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((ushort)0x43E8)), 2);
    }
    private static NativeBattleEffectArena Temporary(int age) => NativeBattleEffectArena.Clear().MarkUnknown(0, 176).Write(4, BitConverter.GetBytes(0x19682D8u)).Write(0x1C, BitConverter.GetBytes(0x00112233u)).Write(0x20, BitConverter.GetBytes(1u)).Write(0x2C, BitConverter.GetBytes(FlagAddress)).Write(0x44, BitConverter.GetBytes(age));
    private static NativeBattlePackedMatrix32 Packed(int x = 0, int y = 0, int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], x, y, z, 4096), null);
    private static NativeBattleEffect66Inputs Inputs(List<NativeBattleEffect66Operation> operations, Func<NativeBattleEffect66Frame, NativeBattleEffect66Frame>? execute = null) => new(NativeBattleGteColorInterpolation.CodeConstants45F270, null!, _ => 0, null!, _ => Packed(z: 2000), _ => new(null, 100, 160, 108), null!, null!, null!, null!, null!, null!, null!, (frame, operation) =>
    {
        operations.Add(operation);
        return execute is null ? frame : execute(frame);
    });
    private static NativeBattleEffect66BodyStep Draw(NativeBattleEffect66Frame frame, byte[] geometry, NativeBattleEffectArena temporary, NativeBattlePackedMatrix32 bone, NativeBattleEffect66Inputs? inputs = null) => NativeBattleEffect66BodyDrawing.Draw(frame, 0x100000, (address, count) => NativeBattleEffectArena.Clear().Write(0, geometry.AsSpan(checked((int)(address - 0x100000)), count)), [bone], Packed(z: 2000), temporary, NativeBattleEffectArena.Clear().MarkUnknown(0, 24), 24, null!, inputs ?? Inputs([]));
    private static byte[] Geometry(short secondY = -100, int faces = 1)
    {
        byte[] bytes = new byte[52 + faces * 16];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 16);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 1);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(20), 3);
        short[] xyz = [-100, -100, 0, 100, secondY, 0, -100, 100, 0];
        for (int index = 0; index < xyz.Length; index++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(22 + index * 2), xyz[index]);
        }
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(40), (short)faces);
        for (int face = 0; face < faces; face++)
        {
            int start = 52 + face * 16;
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(start + 2), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start + 4), 0xFAB20002u);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start + 8), 0x12341234u);
            BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(start + 12), 0x06204567u);
        }
        return bytes;
    }
    private static ushort U16(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(source.Read(offset, 2));
    private static short S16(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadInt16LittleEndian(source.Read(offset, 2));
    private static uint U32(NativeBattleEffectArena source, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(source.Read(offset, 4));
}
