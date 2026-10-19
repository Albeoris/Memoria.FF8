using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleRinoa87CameraResourcesTests
{
    [Fact]
    public void Original_sources_and_curves_initialize_the_proved_camera_kernel_independently()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleRinoa87CameraResources resources = game.ReadNativeRinoa87CameraResources();
        resources.Validate();
        Assert.Equal(new[] { 28, 15, 32 }, resources.Curves.Select(curve => curve.Values.Length));
        foreach (NativeBattleMoogleCameraSource source in resources.Sources)
        {
            uint pointer = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan());
            NativeBattleMoogleCameraCurve? curve = pointer == 0 ? null : resources.Curves.Single(item => item.Address == pointer);
            NativeBattleMoogleCameraStep initialized = NativeBattleMoogleCamera.Initialize732B10(NativeBattleMoogleCamera.Reset732AF0(), source, new byte[8].ToImmutableArray(), 0, curve);
            Assert.NotNull(initialized.Pose.FocalLength);
            NativeBattleMoogleCameraState restored = JsonSerializer.Deserialize<NativeBattleMoogleCameraState>(JsonSerializer.Serialize(initialized.State))!;
            Assert.Equal(JsonSerializer.Serialize(NativeBattleMoogleCamera.Advance732E00(initialized.State, curve)), JsonSerializer.Serialize(NativeBattleMoogleCamera.Advance732E00(restored, curve)));
        }
    }
}
