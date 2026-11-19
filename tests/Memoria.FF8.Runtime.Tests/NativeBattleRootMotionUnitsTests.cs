using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRootMotionUnitsTests
{
    [Fact]
    public void Native_world_horizontal_deltas_preserve_recipient_neutral_and_unchanged_vertical_foot_calibration()
    {
        NativeBattleDecodedPose neutral = Pose(new(21, -100, 111));
        NativeBattleDecodedPose moving = Pose(new(150, -140, -1281));
        NativeBattlePoseSource source = Source(175, 100, [neutral, moving]);
        NativeBattlePoseSource recipient = Source(164, 150, [Pose(new(-17, -160, 20))]);
        NativeBattleSemanticBonePair[] joints = [new("root", 0, 0), new("foot-a", 1, 1)];
        NativeBattleFootContactCalibration feet = new([new(1, new(0, 16, 0))], [new(1, new(0, 40, 0))]);
        NativeBattlePoseRetargeter proportion = new(source, recipient, joints, 1.25f, feet, primaryContactJoint: null);
        NativeBattlePoseRetargeter nativeUnits = new(source, recipient, joints, 1.25f, feet, primaryContactJoint: null, horizontalMotionScale: 175f / 164);
        NativeBattleDecodedPose oldPose = proportion.Transfer(moving);
        NativeBattleDecodedPose actualPose = nativeUnits.Transfer(moving);
        var sourceOrigin = NativeBattlePoseMatrices.StageBoneMatrices(source, neutral);
        var recipientOrigin = NativeBattlePoseMatrices.StageBoneMatrices(recipient, recipient.Sample(0, 1, 0));
        var expected = NativeBattlePoseMatrices.StageBoneMatrices(source, moving);
        var actual = NativeBattlePoseMatrices.StageBoneMatrices(recipient, actualPose);
        var old = NativeBattlePoseMatrices.StageBoneMatrices(recipient, oldPose);

        Assert.InRange(Math.Abs((actual[0].X - recipientOrigin[0].X) - (expected[0].X - sourceOrigin[0].X)), 0, 3);
        Assert.InRange(Math.Abs((actual[0].Z - recipientOrigin[0].Z) - (expected[0].Z - sourceOrigin[0].Z)), 0, 3);
        Assert.True(Math.Abs((old[0].Z - recipientOrigin[0].Z) - (expected[0].Z - sourceOrigin[0].Z)) > 100);
        Assert.Equal(oldPose.Translation.Y, actualPose.Translation.Y);
        Assert.Equal(oldPose.Bones.ToArray(), actualPose.Bones.ToArray());
        for (int bone = 0; bone < actual.Length; bone++)
        {
            Assert.Equal(old[bone].Y, actual[bone].Y);
            Assert.Equal(old[bone].Rotation, actual[bone].Rotation);
        }
        Assert.Equal(recipient.Skeleton, nativeUnits.Compile(source).Skeleton);
    }

    [Fact]
    public void Omitted_horizontal_override_keeps_explicit_source_absolute_coordinate_contract()
    {
        NativeBattlePoseSource source = Source(175, 100, [Pose(new(21, -100, 111))]);
        NativeBattlePoseSource recipient = Source(164, 150, [Pose(new(-17, -160, 20))]);
        NativeBattlePoseRetargeter retargeter = new(source, recipient, [new("root", 0, 0), new("foot-a", 1, 1)], 2, primaryContactJoint: null, translationMode: NativeBattleRootTranslationMode.SourceAbsolute);
        Assert.Equal(new NativeBattleVector(42, -200, 222), retargeter.Transfer(source.Sample(0, 1, 0)).Translation);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Invalid_horizontal_native_unit_calibration_is_rejected(float scale)
    {
        NativeBattlePoseSource source = Source(175, 100, [Pose(default)]);
        Assert.Throws<InvalidDataException>(() => new NativeBattlePoseRetargeter(source, source, [new("root", 0, 0), new("foot-a", 1, 1)], 1, horizontalMotionScale: scale));
    }

    private static NativeBattleDecodedPose Pose(NativeBattleVector translation)
    {
        return new(translation, [new(new(128, 64, 32), new(1024, 1024, 1024)), new(new(-64, 128, 32), new(1024, 1024, 1024))], false);
    }

    private static NativeBattlePoseSource Source(short scale, short length, ImmutableArray<NativeBattleDecodedPose> frames)
    {
        byte[] skeleton = new byte[16 + 2 * 48];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), scale);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), length);
        return new(skeleton.ToImmutableArray(), NativeBattleAnimationEncoder.Encode([frames]));
    }
}
