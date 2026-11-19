using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAnimationRetargetTests
{
    [Fact]
    public void Encoded_motion_preserves_frame_counts_and_incremental_native_decoding()
    {
        ImmutableArray<NativeBattleDecodedPose> frames = [Pose(0, 2000), Pose(7, -2000), Pose(-11, 300)];
        ImmutableArray<byte> encoded = NativeBattleAnimationEncoder.Encode([frames]);
        Assert.Equal(new byte[] { 3 }, NativeBattlePoseDecoder.ReadFrameCounts(encoded.AsSpan()).ToArray());
        NativeBattlePoseFirstFrame current = NativeBattlePoseDecoder.DecodeFirst509440(encoded.AsSpan(), 2, 0);
        AssertEquivalent(frames[0], current.Pose);
        for (int frame = 1; frame < frames.Length; frame++)
        {
            current = NativeBattlePoseDecoder.AdvanceRegular508F90(encoded.AsSpan(), 2, 0, current.Pose, current.ByteOffset, current.BitOffset);
            AssertEquivalent(frames[frame], current.Pose);
            AssertEquivalent(frames[frame], NativeBattlePoseDecoder.Sample(encoded.AsSpan(), 2, 0, checked((byte)(frame + 1)), false));
        }
    }

    [Fact]
    public void Identity_semantic_mapping_preserves_native_rotation_matrices_and_recipient_skeleton()
    {
        ImmutableArray<NativeBattleDecodedPose> frames = [Pose(0, 200), Pose(19, -500), Pose(-17, 1100)];
        NativeBattlePoseSource source = Source(frames, 512);
        NativeBattlePoseSource recipient = Source(frames, 1024);
        NativeBattlePoseRetargeter retargeter = new(source, recipient, [new("root", 0, 0), new("limb", 1, 1)], 1);
        NativeBattlePoseSource compiled = retargeter.Compile(source);
        Assert.Equal(recipient.Skeleton, compiled.Skeleton);
        Assert.Equal(NativeBattlePoseDecoder.ReadFrameCounts(source.Animations.AsSpan()), NativeBattlePoseDecoder.ReadFrameCounts(compiled.Animations.AsSpan()));
        for (int frame = 0; frame < frames.Length; frame++)
        {
            NativeBattleDecodedPose actual = compiled.Sample(0, checked((byte)(frame + 1)), 0);
            Assert.Equal(frames[frame].Translation, actual.Translation);
            ImmutableArray<NativeBattleActionMatrix> matrices = NativeBattlePoseMatrices.StageBoneMatrices(compiled, actual);
            ImmutableArray<NativeBattleActionMatrix> expected = NativeBattlePoseMatrices.StageBoneMatrices(recipient, frames[frame]);
            for (int bone = 0; bone < matrices.Length; bone++)
            {
                for (int lane = 0; lane < 9; lane++)
                {
                    Assert.InRange(Math.Abs(matrices[bone].Rotation[lane] - expected[bone].Rotation[lane]), 0, 8);
                }
                Assert.InRange(Math.Abs(matrices[bone].X - expected[bone].X), 0, 3);
                Assert.InRange(Math.Abs(matrices[bone].Y - expected[bone].Y), 0, 3);
                Assert.InRange(Math.Abs(matrices[bone].Z - expected[bone].Z), 0, 3);
            }
            Assert.Equal(1024, BinaryPrimitives.ReadInt16LittleEndian(compiled.Skeleton.AsSpan()[18..]));
            Assert.Equal(2, matrices.Length);
        }
    }

    [Fact]
    public void Invalid_semantic_bindings_fail_before_motion_publication()
    {
        NativeBattlePoseSource source = Source([Pose(0, 0)], 512);
        Assert.Throws<InvalidDataException>(() => new NativeBattlePoseRetargeter(source, source, [new("root", 0, 0), new("duplicate", 1, 0)], 1));
        Assert.Throws<InvalidDataException>(() => new NativeBattlePoseRetargeter(source, source, [new("root", 0, 0)], float.NaN));
        Assert.Throws<InvalidDataException>(() => NativeBattleAnimationEncoder.Encode([[]]));
    }

    [Fact]
    public void Linked_contact_uses_the_published_recipient_hand_without_accumulation_or_sequence_mutation()
    {
        NativeBattleDecodedPose neutral = new(default, [new(default, new(1024, 1024, 1024)), new(default, new(1024, 1024, 1024))], false);
        NativeBattlePoseSource source = Source([neutral], 512);
        NativeBattlePoseSource recipient = Source([neutral], 1024);
        NativeBattlePoseSource compiled = new NativeBattlePoseRetargeter(source, recipient, [new("root", 0, 0), new("hand-a", 1, 1)], 1).Compile(source);
        NativeBattleGraphicsModel model = new()
        {
            SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = compiled.Sample(0, 1, 0) },
            LinkedTransform = new(default, new(10, 20, 30))
        };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, compiled);
        NativeBattleGraphicsModel first = NativeBattleLinkedContact.Publish(model, compiled);
        NativeBattleGraphicsModel second = NativeBattleLinkedContact.Publish(first, compiled);
        Assert.Equal(new NativeBattleVector(0, 0, 512), first.LinkedContactOffset);
        Assert.Equal(new NativeBattleVector(10, 20, 542), first.ReadEffectiveLinkedTransform()!.Position);
        Assert.Equal(first.LinkedContactOffset, second.LinkedContactOffset);
        Assert.Equal(model.LinkedTransform, first.LinkedTransform);
        Assert.Same(model.SequenceState, first.SequenceState);
        Assert.Null(NativeBattleLinkedContact.Publish(first, recipient).LinkedContactOffset);
    }

    private static NativeBattleDecodedPose Pose(short translation, short angle) => new(new(translation, -30, 60), [new(new(angle, 170, -100), new(1024, 1024, 1024)), new(new(-70, angle, 20), new(1024, 1024, 1024))], false);

    private static NativeBattlePoseSource Source(ImmutableArray<NativeBattleDecodedPose> frames, short length)
    {
        byte[] skeleton = new byte[16 + 2 * 48];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), length);
        return new(skeleton.ToImmutableArray(), NativeBattleAnimationEncoder.Encode([frames]));
    }

    private static void AssertEquivalent(NativeBattleDecodedPose expected, NativeBattleDecodedPose actual)
    {
        Assert.Equal(expected.Translation, actual.Translation);
        Assert.Equal(expected.Bones.Length, actual.Bones.Length);
        for (int bone = 0; bone < expected.Bones.Length; bone++)
        {
            Assert.Equal(expected.Bones[bone].Scale, actual.Bones[bone].Scale);
            Assert.Equal(0, (expected.Bones[bone].Rotation.X - actual.Bones[bone].Rotation.X) & 4095);
            Assert.Equal(0, (expected.Bones[bone].Rotation.Y - actual.Bones[bone].Rotation.Y) & 4095);
            Assert.Equal(0, (expected.Bones[bone].Rotation.Z - actual.Bones[bone].Rotation.Z) & 4095);
        }
    }
}
