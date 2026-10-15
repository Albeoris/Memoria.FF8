using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleStageCameraTests
{
    private const string GameDirectory = @"C:\Steam\steamapps\common\FINAL FANTASY VIII";

    [Theory]
    [InlineData(0, 4602, -2830, 8263, -453, -293, 260, 512)]
    [InlineData(23, 4086, -2084, 4246, -510, -629, 145, 288)]
    [InlineData(31, 1397, -676, 7307, -543, -751, 82, 512)]
    public void Installed_introduction_resolves_the_final_absolute_pose_and_native_lens(int stageId, int eyeX, int eyeY, int eyeZ, int targetX, int targetY, int targetZ, int focal)
    {
        using BattleAssets assets = new(GameDirectory);
        BattleStageCamera camera = assets.LoadStage(stageId).IntroductionCameras[0];
        Assert.Equal(new Vector3(eyeX, eyeY, eyeZ), camera.NativeEye);
        Assert.Equal(new Vector3(targetX, targetY, targetZ), camera.NativeTarget);
        Assert.Equal(focal, camera.FocalLength);
    }

    [Fact]
    public void Scene_camera_byte_selects_the_native_group_and_subtrack()
    {
        using BattleAssets assets = new(GameDirectory);
        BattleStage stage = assets.LoadStage(0);
        Assert.Equal(new Vector3(2593, -2363, -7284), stage.IntroductionCameras[1].NativeEye);
        Assert.Equal(new Vector3(6508, -2060, 5110), stage.IntroductionCameras[0x12].NativeEye);
        Assert.NotEqual(stage.IntroductionCameras[0], stage.IntroductionCameras[0x12]);
    }

    [Theory]
    [InlineData(0, 0, 0, 1800)]
    [InlineData(0, -1200, 0, 2100)]
    [InlineData(23, 800, -1500, -1200)]
    [InlineData(31, 0, -500, 0)]
    public void Renderer_projection_matches_native_positive_down_camera_basis(int stageId, int x, int y, int z)
    {
        using BattleAssets assets = new(GameDirectory);
        BattleStageCamera native = assets.LoadStage(stageId).IntroductionCameras[0];
        Vector3 forward = Vector3.Normalize(native.NativeTarget - native.NativeEye);
        Vector3 right = Vector3.Normalize(new Vector3(forward.Z, 0, -forward.X));
        Vector3 down = Vector3.Cross(forward, right);
        Vector3 relative = new Vector3(x, y, z) - native.NativeEye;
        float depth = Vector3.Dot(relative, forward);
        Vector2 expected = new(160 + native.FocalLength * Vector3.Dot(relative, right) / depth, 108 + native.FocalLength * Vector3.Dot(relative, down) / depth);
        BattleRenderer renderer = new(320, 240);
        renderer.Render(new BattleStage(0, [], []), [], BattleCamera.FromNative(native));
        Assert.True(renderer.TryProject(new Vector3(x, -y, -z) / 100f, out Vector2 actual));
        Assert.InRange(Vector2.Distance(expected, actual), 0, 0.001f);
    }
}
