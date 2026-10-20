using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleTornado146ResourcesTests
{
    [Fact]
    public void Original_Tornado_sources_include_the_complete_crater_models_morphs_and_exact_cold_module_extent()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleTornado146Resources resources = game.ReadNativeTornado146Resources();
        NativeExecutableMemory image = new(game.ReadNativeExecutable());
        NativeBattleTornado146State cold = NativeBattleTornado146State.FromColdProcess();
        Assert.Equal(image.ReadLoader(NativeBattleTornado146State.WorkAddress, NativeBattleTornado146State.WorkLength), cold.Storage.Read(0, NativeBattleTornado146State.WorkLength));
        Assert.Equal(image.Read(0xDD674C, 12), new byte[12]);
        Assert.Equal(0x388, resources.Camera.Data.Length);
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Spark.Bytes.AsSpan().Slice(8)));
        Assert.Equal(120, BinaryPrimitives.ReadInt32LittleEndian(resources.Cloud.Bytes.AsSpan().Slice(4)));
        Assert.Equal(960, resources.CloudTarget.Length);
        Assert.Equal(142, BinaryPrimitives.ReadInt32LittleEndian(resources.Crater.Bytes.AsSpan().Slice(4)));
        Assert.Equal(261, BinaryPrimitives.ReadInt32LittleEndian(resources.Crater.Bytes.AsSpan().Slice(0x478)));
        Assert.Equal(image.Read(0xDD6780, 24), resources.CapturePacket.ToArray());
        Assert.Equal(0x4940Fu, resources.WindSound);
        Assert.Equal(0x49472u, resources.LandSound);
        Assert.Equal(15, resources.Pieces.Length);
        for (int index = 0; index < 15; index++)
        {
            Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(image.Read(0xDD6644u + (uint)index * 4, 4)), resources.Pieces[index].Address);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(image.Read(0xDD6680u + (uint)index * 4, 4)), resources.LiftHeights[index]);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(image.Read(0xDD66BCu + (uint)index * 4, 4)), resources.PieceX[index]);
            Assert.Equal(BinaryPrimitives.ReadInt32LittleEndian(image.Read(0xDD66F8u + (uint)index * 4, 4)), resources.PieceZ[index]);
        }
    }
}
