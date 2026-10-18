using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleCactuarAuxiliaryDrawingTests
{
    [Fact]
    public void All_original_projected_particle_frames_keep_full_MAC_billboard_translation_and_return_native_frame_result()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        NativeBattleActionMatrix view = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 90000, -80000, 8192, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleModuleResource sprite = new(0, resources.Sprite.Address, 0, 0, resources.Sprite.Bytes);
        byte[] particle = new byte[0x18];
        BinaryPrimitives.WriteUInt32LittleEndian(particle, 1);
        BinaryPrimitives.WriteInt16LittleEndian(particle.AsSpan(6), 2048);
        BinaryPrimitives.WriteInt16LittleEndian(particle.AsSpan(0xC), 100);
        BinaryPrimitives.WriteInt16LittleEndian(particle.AsSpan(0xE), -200);
        BinaryPrimitives.WriteInt16LittleEndian(particle.AsSpan(0x10), 300);
        for (short frame = 0; frame < 16; frame++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(particle.AsSpan(4), frame);
            ImmutableArray<byte> before = particle.ToImmutableArray();
            NativeBattleCactuarParticleDraw draw = Assert.IsType<NativeBattleCactuarParticleDraw>(NativeBattleCactuarAuxiliaryDrawing.Particle5A9290(before, resources.Sprite, view, projection));
            Assert.Equal(new short[] { 2048, 0, 0, 0, 2048, 0, 0, 0, 2048 }, draw.CameraMatrix.Rotation.ToArray());
            Assert.Equal(90200, draw.CameraMatrix.X);
            Assert.Equal(-79900, draw.CameraMatrix.Y);
            Assert.Equal(8492, draw.CameraMatrix.Z);
            Assert.Equal(sprite.ProjectedFrame571C80(frame).NextOffset, draw.FrameResult);
            Assert.NotEmpty(draw.Packets.Primitives);
            Assert.All(draw.Packets.Primitives, primitive => Assert.Equal((ushort)0, primitive.Flags20));
            Assert.Equal(particle, before.ToArray());
            ImmutableArray<byte> cold = JsonSerializer.Deserialize<ImmutableArray<byte>>(JsonSerializer.Serialize(before));
            Assert.Equal(JsonSerializer.Serialize(draw), JsonSerializer.Serialize(NativeBattleCactuarAuxiliaryDrawing.Particle5A9290(cold, resources.Sprite, view, projection)));
        }
        BinaryPrimitives.WriteUInt32LittleEndian(particle, 0);
        Assert.Null(NativeBattleCactuarAuxiliaryDrawing.Particle5A9290(particle.ToImmutableArray(), resources.Sprite, view, projection));
    }

    [Theory]
    [InlineData(0x5A9140, 5)]
    [InlineData(0x5A9140, 8)]
    [InlineData(0x5A9570, 2)]
    [InlineData(0x5A9570, 5)]
    [InlineData(0x5A9730, 3)]
    [InlineData(0x5A9730, 10)]
    [InlineData(0x5A9730, 16)]
    public void Original_three_mesh_leaves_use_existing_common_packet_kernel_and_preserve_retained_task(int callback, int counter)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleCactuar199Resources resources = game.ReadNativeCactuar199Resources();
        NativeBattleSummonColors colors = game.ReadNativeBattleGraphicsRules().SummonColors!;
        NativeBattleActionMatrix view = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 8192, 4096);
        NativeBattleCameraProjection projection = new(view, 216, 160, 108);
        NativeBattleModulePoolTask task = new NativeBattleModulePoolTask(1, 0, null, (uint)callback, new byte[0x20].ToImmutableArray()).WriteWord(0xC, checked((ushort)counter)).WriteWord(0x18, 512).WriteWord(0x1C, 4096);
        string before = JsonSerializer.Serialize(task);
        NativeBattleGfGeometryBatch batch = NativeBattleCactuarAuxiliaryDrawing.Mesh(task, resources, -1590, view, projection, colors);
        Assert.NotEmpty(batch.Attempts);
        Assert.True(batch.AllocationBytes > 0);
        NativeBattleStaticMeshResource mesh = callback == 0x5A9140 ? resources.MeshC : callback == 0x5A9570 ? resources.MeshA : resources.MeshB;
        NativeBattleCactuarMeshDraw description = NativeBattleCactuarAuxiliaryDrawing.DescribeMesh(task, mesh, -1590, view);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleModulePackets.EmitBatch572200(mesh.Bytes, 0, default, description.Matrix, projection, description.Context, colors)), JsonSerializer.Serialize(batch));
        Assert.Equal(before, JsonSerializer.Serialize(task));
        NativeBattleModulePoolTask cold = JsonSerializer.Deserialize<NativeBattleModulePoolTask>(before)!;
        Assert.Equal(JsonSerializer.Serialize(batch), JsonSerializer.Serialize(NativeBattleCactuarAuxiliaryDrawing.Mesh(cold, resources, -1590, view, projection, colors)));
    }
}
