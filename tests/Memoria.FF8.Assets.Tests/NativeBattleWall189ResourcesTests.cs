using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleWall189ResourcesTests
{
    [Fact]
    public void Original_Wall_resources_retain_camera_shield_and_both_particle_meshes()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleWall189Resources resources = game.ReadNativeWall189Resources();
        NativeExecutableMemory image = new(game.ReadNativeExecutable());
        NativeBattleWall189State cold = NativeBattleWall189State.FromColdProcess();
        Assert.Equal(image.ReadLoader(0x2294DB0, 150 * 24), cold.Sprites.ToArray());
        Assert.Equal(image.ReadLoader(0x2293F90, 100 * 24), cold.Meshes.ToArray());
        Assert.Equal(image.ReadLoader(0x2295BC0, 64), cold.Environment.ToArray());
        Assert.Equal(image.ReadLoader(0x2293F50, 12), cold.Area.ToArray());
        Assert.Equal(image.ReadLoader(0x2294DA0, 8), cold.Offset.ToArray());
        Assert.Equal(image.ReadLoader(0xD1B490, 24), cold.ClearPacket.ToArray());
        Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(image.ReadLoader(0x2296A2C, 2)), cold.CaptureX);
        Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(image.ReadLoader(0x2296A2E, 2)), cold.CaptureY);
        Assert.Equal(image.ReadLoader(0x2296A30, 0x1C000), cold.Packets.Read(0, 0x1C000));
        Assert.Equal(350051u, resources.Sound);
        Assert.Equal(0xD8, resources.Camera.Data.Length);
        Assert.Equal((ushort)16, BinaryPrimitives.ReadUInt16LittleEndian(resources.Spark.Bytes.AsSpan().Slice(8)));
        Assert.Equal(696, resources.Burst.Bytes.Length);
        Assert.Equal(184, resources.Debris.Bytes.Length);
        Assert.Equal(6192, resources.Shield.Bytes.Length);
        Assert.Equal(56, resources.Selectors.Length);
        Assert.All(resources.Selectors, item => Assert.InRange(item, 0, 5));
        Assert.Equal(resources.ClearPacket.ToArray(), NativeBattleWall189State.FromColdProcess().ClearPacket.ToArray());
        int polygon = BinaryPrimitives.ReadInt32LittleEndian(resources.Shield.Bytes.AsSpan());
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(resources.Shield.Bytes.AsSpan().Slice(polygon)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(resources.Shield.Bytes.AsSpan().Slice(polygon + 4)));
        Assert.Equal(250, BinaryPrimitives.ReadInt32LittleEndian(resources.Shield.Bytes.AsSpan().Slice(polygon + 8)));
    }
}
