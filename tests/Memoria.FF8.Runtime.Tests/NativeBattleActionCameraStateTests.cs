using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleActionCameraStateTests
{
    [Fact]
    public void NativeInitializersOwnModeAndExistingSelectorWords()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize();
        Assert.Equal((byte?)1, camera.Mode);
        Assert.False(camera.ForceCamera);
        camera = camera.SetActionSelector(65535).SetSecondarySelector(1);
        Assert.Equal((short)-1, camera.Variables[0]);
        Assert.Equal((short)1, camera.Variables[1]);
        string json = JsonSerializer.Serialize(camera);
        Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<NativeBattleCameraState>(json)));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 0x8123)]
    public void OriginalActionCleanupHonorsCameraModeWithoutReplacingItsTaskBank(byte mode, ushort expectedMask)
    {
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize() with { Mode = mode, Mask = 0x8123, ForceCamera = true, BlendProgress = 0 };
        camera = camera.CompleteAction();
        Assert.Equal(expectedMask, camera.Mask);
        Assert.False(camera.ForceCamera);
        Assert.Equal(mode == 3 ? (ushort)0 : (ushort)4096, camera.BlendProgress);
        Assert.Empty(camera.Order);
    }

    [Fact]
    public void NullOriginalCameraPointerIsAnExact503520NoopButUnknownModeIsRejectedAtCleanup()
    {
        NativeBattleCameraState camera = NativeBattleCameraState.Initialize();
        Assert.Same(camera, camera.Start(null, 0));
        Assert.Throws<NotSupportedException>(() => (camera with { Mode = null }).CompleteAction());
    }

    [Theory]
    [InlineData(0u, 0, true)]
    [InlineData(2u, 0, false)]
    [InlineData(8u, 0, false)]
    [InlineData(16u, 0, false)]
    [InlineData(0u, 64, false)]
    [InlineData(0u, 256, true)]
    public void BusyPredicateUsesAllDecimal26StatusBitsAndTheActualResourceMask(uint flags, ushort resource, bool expected)
    {
        NativeBattleGraphicsModel model = new(3, 3) { Flags8 = flags, Kind5 = 0, ResourceFlags44 = resource };
        Assert.Equal(expected, NativeBattleActionGraphics.IsModelBusy(model, 64));
    }
}
