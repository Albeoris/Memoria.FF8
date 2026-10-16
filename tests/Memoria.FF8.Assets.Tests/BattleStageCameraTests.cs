using System.Numerics;
using Memoria.FF8.Assets;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class BattleStageCameraTests
{
    private static string GameDirectory => TestResources.GameDirectory;

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    [InlineData(31)]
    public void OriginalStageControlWaitsForTheSelectedCameraAndThenStops(int stageId)
    {
        using BattleAssets assets = new(GameDirectory);
        BattleStage stage = assets.LoadStage(stageId);
        NativeBattleCameraBank bank = Assert.IsType<NativeBattleCameraBank>(stage.NativeCameraBank);
        foreach (int selector in stage.IntroductionCameras.Keys)
        {
            NativeBattleCameraState camera = NativeBattleCameraState.Initialize() with { ControlCursor = 0, Variables = NativeBattleCameraState.Initialize().Variables.SetItem(0, (short)selector) };
            int callbacks = 0;
            while (camera.ControlCursor is not null && callbacks < 5000)
            {
                camera = camera.AdvanceControl(bank, stage.NativeCameraControl, new()).Tick(0);
                callbacks++;
            }
            Assert.Null(camera.ControlCursor);
            Assert.Empty(camera.Order);
            Assert.Equal((ushort)0, camera.Mask);
            Assert.Equal(1ul, camera.PoseCaptureCount);
            NativeBattleCameraPose pose = Assert.IsType<NativeBattleCameraPose>(camera.CurrentPose);
            if (selector == 0)
            {
                BattleStageCamera expected = stage.IntroductionCameras[selector];
                Assert.Equal(expected.NativeEye, new Vector3(pose.Eye.X, pose.Eye.Y, pose.Eye.Z));
                Assert.Equal(expected.NativeTarget, new Vector3(pose.Target.X, pose.Target.Y, pose.Target.Z));
                Assert.Equal((short?)expected.FocalLength, pose.FocalLength);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(23)]
    [InlineData(31)]
    public void OriginalCameraTracksDriveTheirOwnCompletionCallbacks(int stageId)
    {
        using BattleAssets assets = new(GameDirectory);
        BattleStage stage = assets.LoadStage(stageId);
        NativeBattleCameraBank bank = Assert.IsType<NativeBattleCameraBank>(stage.NativeCameraBank);
        foreach (int selector in stage.IntroductionCameras.Keys)
        {
            NativeBattleCameraState camera = NativeBattleCameraState.Initialize().Start(bank, checked((byte)selector));
            Assert.NotEmpty(camera.Order);
            int callbacks = 0;
            while (camera.Order.Length != 0 && callbacks < 5000)
            {
                camera = camera.Tick(0);
                callbacks++;
            }
            Assert.Empty(camera.Order);
            Assert.Equal((ushort)0, camera.Mask);
            Assert.InRange(callbacks, 1, 5000);
        }
    }

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
    [InlineData(0, 211.2f, 108f)]
    [InlineData(1024, 160f, 159.2f)]
    [InlineData(-1024, 160f, 56.8f)]
    [InlineData(2048, 108.8f, 108f)]
    public void NativeRollRotatesTheProjectedBasisAroundTheViewAxis(short roll, float x, float y)
    {
        BattleStageCamera native = new(new(0, 0, -1000), Vector3.Zero, 512);
        BattleRenderer renderer = new(320, 240);
        renderer.Render(new BattleStage(0, [], []), [], BattleCamera.FromNative(native, roll));
        Assert.True(renderer.TryProject(Vector3.UnitX, out Vector2 actual));
        Assert.InRange(Vector2.Distance(new(x, y), actual), 0, 0.001f);
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
