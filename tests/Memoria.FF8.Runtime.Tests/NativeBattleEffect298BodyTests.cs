using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect298BodyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Offset_pose_uses_decoded_angles_WORD_wrap_and_preserves_unwritten_temporary_bytes(bool scaled)
    {
        var source = Source();
        var decoded = new NativeBattleDecodedPose(new(10, 20, 30), [new(new(32760, 0, 0), new(512, 1024, 2048)), new(new(0, 0, 0), new(1024, 512, 2048))], scaled);
        var model = new NativeBattleGraphicsModel(0, 3) { SequenceState = new() { DecodedPose = decoded } };
        var offsets = NativeBattleEffectArena.Clear().Write(8, BitConverter.GetBytes((short)8));
        var temporary = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 96).ToArray());
        var actual = NativeBattleEffect298Pose.Prepare662C00(model, source, offsets, temporary);
        var bones = NativeBattleBodyBoneMatrices.Require(actual.Model, source);
        Assert.Equal(new[] { 160, 320, 480 }, new[] { bones[0].X, bones[0].Y, bones[0].Z });
        Assert.Equal(scaled ? 680 : 580, bones[1].Z);
        Assert.Equal(scaled ? new short[] { 2048, 0, 0, 0, 2048, 0, 0, 0, 16384 } : new short[] { 4096, 0, 0, 0, 4096, 0, 0, 0, 4096 }, bones[1].Rotation);
        Assert.Equal(new byte[] { 0xA5, 0xA5 }, actual.Temporary96.Read(0x5E, 2));
        Assert.Equal(new byte[6], actual.Temporary96.Read(0x58, 6));
        Assert.Equal(new byte[2], actual.Temporary96.Read(18, 2));
        Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5 }, actual.Temporary96.Read(0x4C, 4));
        Assert.Same(decoded, actual.Model.SequenceState!.DecodedPose);
        Assert.Null(model.CurrentBodyBoneMatrices);
        if (scaled)
        {
            Assert.Equal(new[] { 0, 0, 200 }, Enumerable.Range(0, 3).Select(axis => BinaryPrimitives.ReadInt32LittleEndian(actual.Temporary96.Read(0x40 + axis * 4, 4))));
            Assert.Equal(new byte[] { 0, 0, 0, 0, 100, 0 }, actual.Temporary96.Read(0x50, 6));
        }
        else
        {
            Assert.Equal(new byte[] { 0xA5, 0xA5, 0xA5, 0xA5, 0xA5, 0xA5 }, actual.Temporary96.Read(0x50, 6));
            Assert.Equal(100, BinaryPrimitives.ReadInt32LittleEndian(actual.Temporary96.Read(0x1C, 4)));
        }
    }

    [Fact]
    public void Missing_offset_angle_bytes_are_not_replaced_from_current_matrices()
    {
        var source = Source();
        var model = new NativeBattleGraphicsModel(0, 3) { SequenceState = new() { DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024)), new(new(0, 0, 0), new(1024, 1024, 1024))], false) } };
        var offsets = NativeBattleEffectArena.Clear().MarkUnknown(24, 2);
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect298Pose.Prepare662C00(model, source, offsets, NativeBattleEffectArena.Clear()));
    }

    [Fact]
    public void Body_outer_calls_shadow_even_when_model_flag20_is_set_and_restores_after_camera_compose()
    {
        var matrix = Identity().ReadMatrix() with { X = 10, Y = 20, Z = 30 };
        var model = new NativeBattleGraphicsModel(0x20, 3) { Color40 = 0x123456, GeometryWorldMatrix = Identity().ReadMatrix() with { X = 100, Y = 200, Z = 300 }, SequenceState = new() { MeshMask124 = 7 }, CurrentBodyBoneMatrices = new(new string('A', 64), [matrix]) };
        var body = new NativeBattleEffect298BodyFrame(Memory(), NativeBattleFileStaging.Unknown(), model, NativeBattleEffectArena.Clear().MarkUnknown(0, 152));
        var offsets = NativeBattleEffectArena.Clear().Write(0, BitConverter.GetBytes((short)1)).Write(2, BitConverter.GetBytes((short)2)).Write(4, BitConverter.GetBytes((short)3));
        List<string> calls = [];
        var services = new NativeBattleEffect298BodyServices(current =>
        {
            calls.Add("shadow");
            return current;
        }, current =>
        {
            calls.Add("pose");
            return current;
        }, current =>
        {
            calls.Add("draw");
            var bone = current.Model.CurrentBodyBoneMatrices!.Matrices[0];
            Assert.Equal(new[] { 1111, 122, 833 }, new[] { bone.X, bone.Y, bone.Z });
            Assert.Equal(0x123456u, BinaryPrimitives.ReadUInt32LittleEndian(current.Temporary152.Read(0x14, 4)));
            Assert.Equal(7u, BinaryPrimitives.ReadUInt32LittleEndian(current.Temporary152.Read(0x18, 4)));
            Assert.Equal(new byte[] { 0, 0, 0, 0, 64, 1, 216, 0 }, current.Temporary152.Read(12, 8));
            return current;
        }, current =>
        {
            calls.Add("restore");
            return current with { Model = model };
        });
        var camera = NativeBattlePackedMatrix32.FromMatrix(Identity().ReadMatrix() with { X = 1000, Y = -100, Z = 500 }, null);
        var actual = NativeBattleEffect298BodyDrawing.DrawOuter(body, camera, 0x1000, offsets, 0x2000, services);
        Assert.Equal(new[] { "shadow", "pose", "draw", "restore" }, calls);
        Assert.Same(model, actual.Model);
        Assert.False(actual.Temporary152.ReadSnapshot(0x50, 1).Known[0]);
    }

    [Fact]
    public void Projected_body_uses_quarterdepth_rows_for_AVSZ_and_includes_physical_tag()
    {
        byte[] bytes = Geometry();
        var temporary = Temporary();
        var controls = NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0() with { Zsf3 = 4096 };
        var actual = NativeBattleEffect298BodyDrawing.DrawBody(Memory(), temporary, NativeBattleEffectArena.Clear(), Read, 0x1000, [Identity(8000)], NativeBattleEffectArena.Clear(), new(null, 100, 160, 108), controls, 0x2000, 24);
        var attempt = Assert.Single(actual.Batch.Attempts);
        var primitive = Assert.IsType<NativeBattleGeometryPrimitive>(attempt.Primitive);
        Assert.Equal(32, actual.Batch.AllocationBytes);
        Assert.Equal(8, primitive.Words.Length);
        Assert.Equal(0x07000000u, primitive.Words[0]);
        Assert.Equal(0x24u, primitive.Words[1] >> 24);
        Assert.Equal(new[] { 2000, 2000, 2000, 0 }, primitive.VertexPayload4);
        Assert.Equal(6017, primitive.Bucket);
        Assert.Equal(6000u, actual.Memory.GeometryRegisters!.Otz);
        Assert.Equal((ushort)2000, BinaryPrimitives.ReadUInt16LittleEndian(actual.VertexScratch.Read(4, 2)));
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[30]);
        Assert.Equal(NativeBattleGfPacketByteState.Untouched, attempt.WriteMask[31]);
        Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(actual.Temporary152.Read(8, 2)));

        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan().Slice(checked((int)(address - 0x1000)), length));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Skipped_or_nonpositive_projection_preserves_unwritten_XYZ_and_selector(bool skipped)
    {
        byte[] bytes = Geometry();
        var offsets = NativeBattleEffectArena.Clear().Write(6, BitConverter.GetBytes((short)(skipped ? 2 : 0)));
        var scratch = NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0xA5, 24).ToArray());
        var actual = NativeBattleEffect298BodyDrawing.DrawBody(Memory(), Temporary(), scratch, Read, 0x1000, [Identity(1)], offsets, new(null, 100, 160, 108), NativeBattleScriptedGraphicsMemory.FromColdProcess().InitializeGeom56BAA0(), 0x2000, 24);
        Assert.Empty(actual.Batch.Attempts);
        for (int vertex = 0; vertex < 3; vertex++)
        {
            Assert.Equal(Enumerable.Repeat((byte)0xA5, 6), actual.VertexScratch.Read(vertex * 8, 6));
            Assert.Equal(new byte[] { 0x10, 0xA5 }, actual.VertexScratch.Read(vertex * 8 + 6, 2));
        }

        NativeBattleEffectArena Read(uint address, int length) => NativeBattleEffectArena.Clear().Write(0, bytes.AsSpan().Slice(checked((int)(address - 0x1000)), length));
    }

    private static NativeBattleEffectArena Temporary() => NativeBattleEffectArena.Clear().Write(4, BitConverter.GetBytes(0x2000u)).Write(0x10, BitConverter.GetBytes((short)320)).Write(0x12, BitConverter.GetBytes((short)216)).Write(0x18, BitConverter.GetBytes(1u));
    private static NativeBattleEffectMemory Memory() => NativeBattleEffectMemory.FromColdProcess() with { GeometryRegisters = NativeBattleGeometry.LoadMatrix(NativeBattleGeometryRegisters.Unknown(), Identity().ReadMatrix(), true, true, null), ScriptedColorRegisters = NativeBattleScriptedColorRegisters.Unknown() };
    private static NativeBattlePackedMatrix32 Identity(int z = 0) => NativeBattlePackedMatrix32.FromMatrix(new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, z, 4096), null);
    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 100);
        return new(skeleton.ToImmutableArray(), [1]);
    }
    private static byte[] Geometry()
    {
        byte[] data = new byte[60];
        BinaryPrimitives.WriteInt32LittleEndian(data, 1);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4), 8);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(8), 1);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(12), 3);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(20), 100);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(28), 100);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(32), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(46), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(48), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(50), 0x8078);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(52), 0x3D948048);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(56), 0x00B68078);
        return data;
    }
}
