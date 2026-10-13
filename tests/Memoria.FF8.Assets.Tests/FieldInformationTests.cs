using System.Numerics;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class FieldInformationTests
{
    [Fact]
    public void InfReaderPreservesEveryBackgroundWrapBit()
    {
        byte[] bytes = new byte[676];
        bytes[15] = 0xA5;
        Assert.Equal((byte)0xA5, FieldInformation.Read(bytes).BackgroundWrapMask);
    }

    [Fact]
    public void OriginalCorridorInfPreservesGatewaysAndSeparateAreaContactGeometry()
    {
        using GameAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? @"C:\Steam\steamapps\common\FINAL FANTASY VIII");
        FieldInformation information = Assert.IsType<FieldInformation>(assets.LoadField(139).Information);
        Assert.Equal(200, information.ControlDirection);
        Assert.Equal(200, information.CameraHeight);
        Assert.Equal(new FieldBounds(-112, 112, 160, -160), information.CameraBounds[0]);
        Assert.Equal(new ushort[] { 143, 232, 137 }, information.Gateways.Where(gateway => gateway.Defined).Select(gateway => gateway.DestinationField));
        Assert.Equal(new short[] { 37, 315, 72 }, information.Gateways.Where(gateway => gateway.Defined).Select(gateway => gateway.DestinationTriangle));
        FieldAreaTrigger area = information.AreaTriggers[0];
        Assert.Equal(new Vector3(-3866, -5965, 8341), area.Start);
        Assert.Equal(new Vector3(-4409, -5476, 8341), area.End);
        Assert.Equal(0, area.AreaId);
        Assert.Equal(0, area.Side);
        Assert.All(information.AreaTriggers.Skip(1), trigger => Assert.Equal(255, trigger.AreaId));
    }
}
