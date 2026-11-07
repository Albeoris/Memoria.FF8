using System.Numerics;
using System.Text.Json;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePilotCameraCompatibilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Singular_camera_intervals_use_exact_native_frames_and_report_the_camera_boundary(bool singularEarlier)
    {
        NativeBattleCameraPose valid = new(new(0, 0, -1000), new(0, 0, 0), 200, 0);
        NativeBattleCameraPose singular = valid with { Target = valid.Eye };
        NativeBattlePreparedPresentation earlier = Frame(1, 0x200000FF, singularEarlier ? singular : valid);
        NativeBattlePreparedPresentation later = Frame(2, 0x2000FF00, singularEarlier ? valid : singular);
        string frozen = JsonSerializer.Serialize(new[] { earlier, later });
        BattleRenderer renderer = new(320, 216);
        Assert.False(renderer.CanSampleNativePilotInterval(earlier, later));
        Assert.Equal(1, renderer.LastPilotFallbackCount);
        Assert.Contains("noninvertible native camera pose basis", renderer.LastPilotFallbackReason!);
        byte[] a = renderer.RenderNativePrimitiveFrame(earlier.Primitives, null).ToArray();
        byte[] b = renderer.RenderNativePrimitiveFrame(later.Primitives, null).ToArray();
        Assert.NotEqual(a, b);
        foreach (float alpha in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
        {
            Assert.Equal(alpha == 1 ? b : a, renderer.RenderNativePilotPrimitiveFrame(earlier, later, alpha).ToArray());
        }
        Assert.Equal(frozen, JsonSerializer.Serialize(new[] { earlier, later }));
    }

    private static NativeBattlePreparedPresentation Frame(long callback, uint color, NativeBattleCameraPose pose)
    {
        NativeBattleLogicalCommand command = new(new("camera-test", 0, 0), 1, 0x20, [color, Point(120, 80), Point(190, 80), Point(120, 150)], 0, [], null)
        {
            OrderingBucket = 50,
            PilotGeometry = new(new(327, 1, 0, 1, 0), Vector3.Zero, Vector3.UnitX, Vector3.UnitY, default, 3, false) { ScreenSpace = true }
        };
        NativeBattlePreparedPrimitiveFrame primitives = new([new(command, new(0), 1)], [])
        {
            LastDescriptorPerspective = false,
            LastDepthState = NativeBattlePrimitiveDepthState.FromColdProcess(),
            LastColorState = NativeBattlePrimitiveColorState.FromColdProcess()
        };
        return new(callback, callback, callback, false, primitives, null, null, null, pose, [], null, null, [], null)
        {
            PilotEffect = 327,
            PilotGeneration = 1,
            PilotCamera = pose,
            PilotProjection = new(NativeBattlePoseMatrices.CameraView(pose), 200, 160, 108)
        };
    }

    private static uint Point(int x, int y) => unchecked((ushort)(x * 8) | (uint)(ushort)(y * 8) << 16);
}
