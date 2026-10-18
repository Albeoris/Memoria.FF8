using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSearchMeshDrawingTests
{
    private static NativeBattleActionMatrix View => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 4096, 4096);

    [Fact]
    public void Transposed_view_precedes_local_Z_rotation_and_column_scale_before_camera_composition()
    {
        NativeBattleActionMatrix view = View with { Rotation = [4096, 0, 0, 0, 0, -4096, 0, 4096, 0] };
        NativeBattleModulePoolTask task = Task().WriteWord(0x48, 1024).WriteDword(0x30, 2048).WriteDword(0x38, 8192).WriteWord(0x1C, 100).WriteWord(0x1E, 200).WriteWord(0x20, 300);
        NativeBattleActionMatrix matrix = NativeBattleSearchMeshDrawing.ComposeMatrix(task, view);
        Assert.Equal(new short[] { 0, -4096, 0, 2048, 0, 0, 0, 0, 8192 }, matrix.Rotation.ToArray());
        Assert.Equal((100, -300, 4296), (matrix.X, matrix.Y, matrix.Z));
    }

    [Fact]
    public void FT4_preserves_direct_materials_and_source_RGB_and_appends_to_the_common_packet_owner()
    {
        NativeBattleCameraProjection projection = new(View, 216, 160, 108);
        NativeBattleGfGeometryBatch batch = NativeBattleSearchMeshDrawing.Draw755FC0(Task(), Mesh(), View, projection, NativeBattleGteColorInterpolation.CodeConstants45F270);
        Assert.Equal(40, batch.AllocationBytes);
        NativeBattleGeometryPrimitive packet = Assert.Single(batch.Attempts).Primitive!;
        Assert.Equal(0x2C112233u, packet.Words[1]);
        Assert.Equal(0x77880102u, packet.Words[3]);
        Assert.Equal(0x00B40304u, packet.Words[5]);
        Assert.Equal(273, packet.Bucket);
        NativeBattlePrimitiveProcess process = NativeBattlePrimitiveProcess.FromColdProcess().Initialize500400() with { ByteCursor = 12 };
        NativeBattleCommonPacketWrite appended = NativeBattleCommonPacketArena.Append(process, NativeBattleLogicalOrdering.Clear(), batch);
        Assert.Equal(52, appended.Process.ByteCursor);
        Assert.Equal(12, Assert.Single(appended.Ordering.Proxies).Packet.ByteOffset);
        Assert.Throws<InvalidDataException>(() => NativeBattleSearchMeshDrawing.Draw755FC0(Task().WriteDword(0x4C, 2), Mesh(), View, projection, NativeBattleGteColorInterpolation.CodeConstants45F270));
        Assert.Throws<NotSupportedException>(() => NativeBattleSearchMeshDrawing.Draw755FC0(Task().WriteWord(0x50, 1), Mesh(), View, projection, NativeBattleGteColorInterpolation.CodeConstants45F270));
    }

    private static NativeBattleModulePoolTask Task() => new NativeBattleModulePoolTask(1, 0, null, 0, new byte[0x8C - 12].ToImmutableArray()).WriteDword(0x30, 4096).WriteDword(0x34, 4096).WriteDword(0x38, 4096).WriteDword(0x4C, 1);

    private static NativeBattleStaticMeshResource Mesh()
    {
        byte[] bytes = new byte[0x60];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x28);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 4);
        for (int vertex = 0; vertex < 4; vertex++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8 + vertex * 8), (short)((vertex & 1) == 0 ? -10 : 10));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(10 + vertex * 8), (short)(vertex < 2 ? -10 : 10));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(52), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(56), 0x2C112233);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(62), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(64), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(66), 6);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(68), 0x77880102);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(72), 0x00B40304);
        return new(0, 1, bytes.ToImmutableArray());
    }
}
