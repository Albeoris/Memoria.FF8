using System.Buffers.Binary;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGuestBodyCameraTests
{
    [Fact]
    public void Special_body_loader_retains_camera_section_instead_of_sound_directory()
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        var source = assets.ReadNativeModelDatSource(7, false, 0);
        BattleModel model = assets.LoadPlayer(7);
        Assert.Equal(10, BinaryPrimitives.ReadInt32LittleEndian(source.Bytes.AsSpan()));
        byte[] camera = source.Bytes.Slice(source.Section(4), source.Section(5) - source.Section(4)).ToArray();
        byte[] sound = source.Bytes.Slice(source.Section(6), source.Section(7) - source.Section(6)).ToArray();
        Assert.Equal(1904, camera.Length);
        Assert.Equal(480, sound.Length);
        Assert.Equal(camera, model.NativeActionCameraBank!.Data.ToArray());
        Assert.NotEqual(sound, model.NativeActionCameraBank.Data.ToArray());
    }
}
