using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeGardenWorldTests
{
    [Fact]
    public void OriginalMotorKeepsRawYawSeamThresholdSignedDivisionAndReleaseBraking()
    {
        //557AEA uses raw signed WORD distance. After crossing zero, the next
        //steering callback halves again even though the shortest delta is small.
        NativeGardenMotorResult first = NativeGardenMotor.Step(0, 0, 0, 0, 0, 127, 127);
        Assert.Equal(new NativeGardenMotorResult(7, 4080, 4080, 0, 0), first);
        NativeGardenMotorResult second = NativeGardenMotor.Step(first.BodyYaw, first.CameraYaw, first.Speed, 0, first.CameraFollowSpeed, 127, 127);
        Assert.Equal(new NativeGardenMotorResult(14, 4072, 4072, 0, 0), second);
        Assert.Equal(-7, NativeGardenMotor.Step(0, 0, 0, 0, 0, 0, -127).Speed);
        Assert.Equal(10, NativeGardenMotor.Step(0, 0, 26, 0, 0, 0, 0).Speed);
        Assert.Equal(0, NativeGardenMotor.Step(0, 0, -16, 0, 0, 0, 0).Speed);
    }

    [Fact]
    public void OriginalMotorPublishesRawVectorYawBeforePriorTerrainCorrectionAndFollowsCamera()
    {
        NativeGardenMotorResult slope = NativeGardenMotor.Step(0, 0, 0, 512, 0, 0, 127);
        Assert.Equal(0, slope.MotionYaw);
        Assert.Equal(32, slope.BodyYaw);
        Assert.Equal(7, slope.Speed);
        int yaw = 0;
        int follow = 0;
        for (int frame = 0; frame < 16; frame++)
        {
            NativeGardenMotorResult result = NativeGardenMotor.Step(1024, yaw, 0, 0, follow, 0, 0);
            yaw = result.CameraYaw;
            follow = result.CameraFollowSpeed;
        }
        //Original signed WORD follow rises by8, then each callback adds follow/8.
        Assert.Equal(128, follow);
        Assert.Equal(136, yaw);
        Assert.Equal(2048, NativeGardenMotor.Delta(0, 2048));
        Assert.Equal(2048, NativeGardenMotor.Delta(2048, 0));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void ExactRuntimeRestorePreservesDistinctGardenObjectsParkingAndNextMotor(byte variant)
    {
        NativeGardenWorldRuntimeState original = Fixture() with { AnimationVariant = variant, ManualCameraVelocity = -37 };
        Assert.Equal((-1, -1), (original.PrimaryContact, original.BroadContact));
        NativeWorldPathPosition parked = new(20480, 200, 4600);
        NativeGardenWorldRuntimeState foot = original.PublishBody(new NativeWorldPathPosition(21180, -34, 4600), 0, 0, 0, gardenControlled: false);
        Assert.Equal(parked, foot.Objects[foot.Allocation.Garden64Index].Position);
        Assert.Equal(parked, foot.Objects[foot.Allocation.Garden65Index].Position);
        Assert.NotEqual(parked, foot.Objects[foot.Allocation.FootObjectIndex].Position);
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame().WithGardenParking(foot);
        Assert.Equal(new NativeSavedWorldPose(20480, -4600, 200, 0), saved.GardenPose3);
        Assert.Equal(new NativeSavedWorldPose(0, 0, 0, 0), saved.Slot4.Pose);
        NativeGardenWorldRuntimeState moving = original with { Speed = 42, GroundYaw = -256, CameraFollowSpeed = 24, Buttons = original.Buttons.Observe(0x80) };
        string json = JsonSerializer.Serialize(moving);
        NativeGardenWorldRuntimeState restored = JsonSerializer.Deserialize<NativeGardenWorldRuntimeState>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(NativeGardenMotor.Step(32, 8, moving.Speed, moving.GroundYaw, moving.CameraFollowSpeed, -127, -127), NativeGardenMotor.Step(32, 8, restored.Speed, restored.GroundYaw, restored.CameraFollowSpeed, -127, -127));
        Assert.Throws<InvalidDataException>(() => (restored with { CameraPitch = -120 }).Validate());
        Assert.Throws<NotSupportedException>(() => (restored with { BroadContact = restored.Objects.Length }).Validate());
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void NativeGardenFieldReturnOwnsPose3WithoutRetiringCarSlotsOrAliasingFootPose(byte variant)
    {
        NativeGardenWorldRuntimeState garden = Fixture() with { AnimationVariant = variant };
        WorldState world = new() { Frame = 31, X = 22000, Z = -3000, NativeHeight = -120, BodyYaw = 128, CameraYaw = 96, NativeGardenRuntime = garden };
        NativeSavedWorldGardenReturn receipt = NativeSavedWorldGardenReturn.Create(world, 107, 750, 3);
        NativeSavedWorldState saved = NativeSavedWorldState.CreateNewGame().WithGardenReturn(receipt);
        saved.Validate();
        Assert.Equal(new NativeSavedWorldPose(22000, -3000, -120, 128), saved.GardenPose3);
        Assert.Equal((byte)0, saved.Slot4.ShapeId);
        Assert.Equal((byte)0, saved.Slot5.ShapeId);
        Assert.Equal((ushort)96, saved.GardenReturn!.CameraYaw96);
        Assert.Null(saved.FootReturn);
        Assert.Equal(variant, saved.GardenReturn.AnimationVariant108);
        Assert.Throws<NotSupportedException>(() => NativeSavedWorldGardenReturn.Create(world with { NativeFootRuntime = NativeFootWorldRuntimeState.CreateFirstNormalLoad(NativeSavedWorldState.CreateNewGame()) }, 107, 750, 3));
    }

    private static NativeGardenWorldRuntimeState Fixture()
    {
        NativeSavedWorldPose pose = new(20480, -4600, 200, 0);
        NativeWorldPathPosition position = new(20480, 200, 4600);
        NativeGardenWorldObject[] objects = [new(0, 0, position, 0, 0, 0), new(2, 0, default, 0, 0, 0), new(64, 0, position, 0, 0, 0), new(65, 2, position, 0, 0, 0), new(94, 0, new(53625, -314, -59399), 0, 0, 0)];
        ushort[] words = [0xFF01, 0, 0xFF02, 740, 0xFF03, 900, 0xFF04, 0, 0xFF13, 0, 0xFF13, 2, 0xFF13, 3, 0xFF13, 64, 0xFF13, 65, 0xFF05, 0];
        string hash = new('A', 64);
        NativeGardenWorldAllocation allocation = new(1, NativeGardenWorldAllocation.AllocationRule, hash, hash, hash, 8, 740, 900, 750, 0, 0, words.ToImmutableArray(), objects.ToImmutableArray(), 0, 2, 3, new WorldTransition(12, 48, -1), pose, pose, 0, 0, 0) { GuardianForce13Flags = 0, CactuarLoadPosition = new(53625, -314, -59399) };
        NativeWorldGroundCacheState cache = NativeWorldGroundCacheState.CreateFresh(new NativeWorldGroundIdentity(0, 0, 0, 0));
        return NativeGardenWorldRuntimeState.CreateFresh(NativeSavedWorldState.CreateNewGame(), allocation, cache, NativeWorldProcess.FromColdProcess(), new NativeGardenCameraDefaults(NativeFieldRuntimeProfile.FF8EnExecutableHash, -112, 0, -341, -7168), currentGroundHeight: 0);
    }
}
