using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAnimationRetargetTests
{
    [Fact]
    public void Indexed_compilation_matches_serial_transfer_bytes_and_preserves_all_animation_metadata()
    {
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> animations =
        [
            [Pose(0, 0), Pose(3, 200), Pose(-7, -300)],
            [Pose(11, 1700)],
            [Pose(5, -1800), Pose(-4, 2010), Pose(8, -2010), Pose(13, 900)],
            [Pose(-19, -700), Pose(21, 1200)]
        ];
        ImmutableArray<ImmutableArray<bool>> contacts = [[true, false, true], [false], [false, true, false, true], [true, false]];
        NativeBattlePoseSource source = Source(animations[0], 512);
        NativeBattlePoseSource motion = source with { Animations = NativeBattleAnimationEncoder.Encode(animations) };
        NativeBattlePoseSource recipient = Source([Pose(7, 200)], 1024) with { Model8FBoneAliases = Enumerable.Range(0, 16).Select(index => (byte)(index & 1)).ToImmutableArray() };
        NativeBattlePoseRetargeter compiler = new(source, recipient, [new("hand-a", 0, 0), new("hand-b", 1, 1) { SourceBindAxes = Matrix4x4.CreateRotationY(MathF.PI / 2) }], 1.25f, primaryContactJoint: "hand-b", horizontalMotionScale: 0.75f);
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> decoded = NativeBattlePoseDecoder.DecodeFrames(motion.Animations.AsSpan(), motion.BoneCount);
        ImmutableArray<NativeBattleDecodedPose>[] serial = decoded.Select((animation, animationIndex) => animation.Select((pose, frameIndex) => compiler.Transfer(pose, contacts[animationIndex][frameIndex])).ToImmutableArray()).ToArray();
        ImmutableArray<byte> expected = NativeBattleAnimationEncoder.Encode(serial);
        NativeBattlePoseSource compiled = compiler.Compile(motion, contacts);
        Assert.Equal(expected, compiled.Animations);
        Assert.Equal(expected, compiler.Compile(motion, contacts).Animations);
        Assert.Equal(recipient.Skeleton, compiled.Skeleton);
        Assert.Equal(recipient.Model8FBoneAliases, compiled.Model8FBoneAliases);
        Assert.Equal(new byte[] { 3, 1, 4, 2 }, NativeBattlePoseDecoder.ReadFrameCounts(compiled.Animations.AsSpan()).ToArray());
        Assert.Equal(new byte[] { 0, 1 }, compiled.ActionBoneAliases.ToArray());
        Assert.Equal(new short[] { 0, 0, 4096, 0, 4096, 0, -4096, 0, 0 }, compiled.ActionBoneFrames[1].Rotation.ToArray());
        Assert.Same(motion, compiled.RetargetBinding!.SourceMotion);
        for (int animation = 0; animation < decoded.Length; animation++)
        {
            Assert.Equal(contacts[animation], compiled.SecondaryHandContactFrames[animation]);
            for (int frame = 0; frame < decoded[animation].Length; frame++)
            {
                ImmutableArray<NativeBattleActionMatrix> sourceBones = NativeBattlePoseMatrices.StageBoneMatrices(motion, decoded[animation][frame]);
                NativeBattleVector Point(int bone) => new(unchecked((short)sourceBones[bone].X), unchecked((short)sourceBones[bone].Y), unchecked((short)sourceBones[bone].Z));
                NativeBattleDecodedPose pose = compiled.Sample(checked((byte)animation), checked((byte)(frame + 1)), 0);
                Assert.Equal(Point(1), pose.HandContact!.SourcePosition);
                Assert.Equal(1, pose.HandContact.RecipientHand);
                Assert.Equal(Point(0), pose.HandAContact!.SourcePosition);
                Assert.Equal(0, pose.HandAContact.RecipientBone);
                Assert.Equal((byte)animation, pose.HandAContact.Animation);
                Assert.Equal((byte)(frame + 1), pose.HandAContact.Frame);
                Assert.Equal((byte)decoded[animation].Length, pose.HandAContact.Frames);
            }
        }
    }

    [Fact]
    public void Indexed_frame_compilation_rethrows_the_first_failure_in_source_animation_order()
    {
        NotSupportedException first = new("First source failure.");
        NotSupportedException later = new("Later source failure.");
        ImmutableArray<ImmutableArray<NativeBattleDecodedPose>> animations = [[Pose(0, 0), Pose(11, 0)], [Pose(21, 0)], [Pose(31, 0)], [Pose(41, 0)]];
        NativeBattleDecodedPose Transfer(NativeBattleDecodedPose pose, bool contact)
        {
            if (pose.Translation.X == 11)
            {
                Thread.Sleep(25);
                throw first;
            }
            if (pose.Translation.X >= 21)
            {
                throw later;
            }
            return pose;
        }
        NotSupportedException actual = Assert.Throws<NotSupportedException>(() => NativeBattlePoseRetargeter.CompileFrames(animations, default, Transfer));
        Assert.Same(first, actual.InnerException);
        Assert.Contains("animation 0, frame 2", actual.Message);
    }

    [Fact]
    public void Semantic_hand_a_contact_preserves_its_decoded_source_frame_independently_of_primary_hand_b()
    {
        NativeBattlePoseSource source = Source([Pose(0, 0), Pose(20, 150)], 512);
        NativeBattlePoseSource recipient = Source([Pose(0, 0), Pose(20, 150)], 1024);
        NativeBattlePoseSource compiled = new NativeBattlePoseRetargeter(source, recipient, [new("hand-a", 0, 0), new("hand-b", 1, 1)], 1, primaryContactJoint: "hand-b").Compile(source);
        NativeBattleDecodedPose first = compiled.Sample(0, 1, 0);
        NativeBattleDecodedPose second = compiled.Sample(0, 2, 0);
        Assert.Equal((byte)1, first.HandAContact!.Frame);
        Assert.Equal((byte)2, second.HandAContact!.Frame);
        Assert.Equal((byte)2, first.HandAContact.Frames);
        Assert.Equal(0, first.HandAContact.RecipientBone);
        Assert.Equal(1, first.HandContact!.RecipientHand);
        Assert.NotEqual(first.HandContact.SourcePosition, first.HandAContact.SourcePosition);
        Assert.Equal(compiled.RetargetBinding!.SourceHandAFrames[0][0], first.HandAContact.SourcePosition);
        NativeBattleDecodedPose restored = System.Text.Json.JsonSerializer.Deserialize<NativeBattleDecodedPose>(System.Text.Json.JsonSerializer.Serialize(first))!;
        Assert.Equal(first.HandAContact, restored.HandAContact);
        NativeBattleSequenceState carried = new() { PoseFrame114 = 2, DecodedPose = restored };
        Assert.Equal((byte)1, carried.DecodedPose.HandAContact!.Frame);
    }

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

    [Theory]
    [InlineData(-1024, 67)]
    [InlineData(1024, -1981)]
    [InlineData(-1023, 67)]
    [InlineData(1023, -1981)]
    public void Singular_limb_rotation_remains_equivalent_under_adjacent_parent_motion(short limbX, short limbY)
    {
        NativeBattleVector unit = new(1024, 1024, 1024);
        NativeBattleBonePose limb = new(new(limbX, limbY, 0), unit);
        ImmutableArray<NativeBattleDecodedPose> frames =
        [
            new(default, [new(new(999, 1514, -1517), unit), limb], false),
            new(default, [new(new(1000, 1483, -1548), unit), limb], false)
        ];
        NativeBattlePoseSource source = Source(frames, 512);
        NativeBattlePoseSource compiled = new NativeBattlePoseRetargeter(source, source, [new("root", 0, 0), new("limb", 1, 1)], 1).Compile(source);
        for (int frame = 0; frame < frames.Length; frame++)
        {
            ImmutableArray<NativeBattleActionMatrix> expected = NativeBattlePoseMatrices.StageBoneMatrices(source, frames[frame]);
            ImmutableArray<NativeBattleActionMatrix> actual = NativeBattlePoseMatrices.StageBoneMatrices(compiled, compiled.Sample(0, checked((byte)(frame + 1)), 0));
            for (int bone = 0; bone < expected.Length; bone++)
            {
                for (int lane = 0; lane < 9; lane++)
                {
                    Assert.InRange(Math.Abs(actual[bone].Rotation[lane] - expected[bone].Rotation[lane]), 0, 8);
                }
                Assert.InRange(Math.Abs(actual[bone].X - expected[bone].X), 0, 3);
                Assert.InRange(Math.Abs(actual[bone].Y - expected[bone].Y), 0, 3);
                Assert.InRange(Math.Abs(actual[bone].Z - expected[bone].Z), 0, 3);
            }
        }
    }

    [Fact]
    public void Linked_contact_uses_the_published_recipient_hand_without_accumulation_or_sequence_mutation()
    {
        NativeBattleDecodedPose neutral = new(default, [new(default, new(1024, 1024, 1024)), new(default, new(1024, 1024, 1024))], false);
        NativeBattlePoseSource source = Source([neutral], 512);
        NativeBattlePoseSource recipient = Source([neutral], 1024);
        NativeBattlePoseSource compiled = new NativeBattlePoseRetargeter(source, recipient, [new("root", 0, 0), new("hand-a", 1, 1)], 1).Compile(source);
        NativeBattleGraphicsModel model = new(2)
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
        NativeBattleGraphicsModel restored = first with { SequenceState = first.SequenceState! with { DecodedPose = recipient.Sample(0, 1, 0) } };
        Assert.Null(NativeBattleLinkedContact.Publish(restored, recipient).LinkedContactOffset);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Sequence_frame_publication_synchronizes_transferred_contact_without_changing_native_linked_motion(bool transferred)
    {
        NativeBattlePoseSource source = Source([Pose(0, 0), Pose(1, 300), Pose(2, 600)], 512);
        NativeBattlePoseSource recipient = Source([Pose(0, 0)], 1024);
        NativeBattlePoseSource body = transferred ? new NativeBattlePoseRetargeter(source, recipient, [new("root", 0, 0), new("hand-a", 1, 1)], 1).Compile(source) : source;
        NativeBattleGraphicsResource resource = new([1, 0, 4, 0, 0xA1]) { PoseSource = body, AnimationFrames = [3] };
        NativeBattleGraphicsModel model = new(2)
        {
            ModelId4 = 0,
            Flags2 = 4,
            Flags8 = 0,
            Sequence = 1,
            ResourceFlags44 = 8,
            SequenceState = new() { Pose = 0, PoseFrame114 = 1, PoseLimit115 = 3, DecodedPose = body.Sample(0, 1, 0) },
            LinkedTransform = new(new(100, 200, 300), new(10, 20, 30))
        };
        model = NativeBattleBodyBoneMatrices.Publish508F60(model, body, null);

        NativeBattleGraphicsModel advanced = NativeBattleSequenceMachine.Advance(Memoria.FF8.Tests.NativeBattleTypedFixture.OriginalOwner(0), model, resource, new(null, null)).Model;

        Assert.Equal((byte)2, advanced.SequenceState!.PoseFrame114);
        Assert.Equal((byte)1, advanced.SequenceState.Callback42);
        Assert.Equal((ushort)0, advanced.SequenceState.Offset46);
        Assert.Equal(model.LinkedTransform, advanced.LinkedTransform);
        NativeBattleGraphicsModel expected = NativeBattleLinkedContact.Publish(advanced with { LinkedContactOffset = null }, body);
        Assert.Equal(expected.LinkedContactOffset, advanced.LinkedContactOffset);
        Assert.Equal(advanced.LinkedContactOffset, NativeBattleLinkedContact.Publish(advanced, body).LinkedContactOffset);
        if (transferred)
        {
            Assert.NotEqual(model.LinkedContactOffset, advanced.LinkedContactOffset);
        }
        else
        {
            Assert.Null(advanced.LinkedContactOffset);
            Assert.Equal(model.ReadEffectiveLinkedTransform(), advanced.ReadEffectiveLinkedTransform());
        }
    }

    [Fact]
    public void Already_compiled_external_bank_restores_its_own_contact_metadata_after_native_row_resolution()
    {
        NativeBattlePoseSource source = Source([Pose(0, 0)], 512);
        NativeBattlePoseSource recipient = Source([Pose(0, 0)], 1024);
        NativeBattlePoseRetargeter compiler = new(source, recipient, [new("root", 0, 0), new("hand-a", 1, 1)], 1);
        NativeBattlePoseSource standing = compiler.Compile(source);
        NativeBattlePoseSource external = source with { Animations = NativeBattleAnimationEncoder.Encode([[Pose(0, 0), Pose(1, 10), Pose(2, 20)]]) };
        NativeBattlePoseSource compiled = compiler.Compile(external);
        NativeBattlePoseSource resolved = compiled with { RetargetBinding = standing.RetargetBinding };
        string fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(external.Animations.AsSpan()));
        NativeBattleGraphicsResource resource = new([1])
        {
            PoseSource = resolved,
            AnimationBankOverrides = [new("external", 1, false, fingerprint, compiled.Animations) { ActionBoneAliases = compiled.ActionBoneAliases, RetargetBinding = compiled.RetargetBinding }]
        };
        NativeBattlePoseSource active = resource.ApplyAnimationBankOverrides("external").PoseSource!;
        Assert.Same(compiled.RetargetBinding, active.RetargetBinding);
        NativeBattleGraphicsModel model = new(2)
        {
            SequenceState = new() { Pose = 0, PoseFrame114 = 3, DecodedPose = active.Sample(0, 3, 0) },
            LinkedTransform = new(default, default)
        };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, active);
        Assert.NotNull(NativeBattleLinkedContact.Publish(model, active).LinkedContactOffset);
    }

    [Fact]
    public void Foot_floor_uses_mesh_contacts_and_native_units_without_changing_recipient_bone_lengths()
    {
        NativeBattleBonePose unit = new(default, new(1024, 1024, 1024));
        NativeBattleDecodedPose first = new(default, [unit with { Rotation = new(1024, 0, 0) }, unit], false);
        NativeBattleDecodedPose second = new(default, [unit, unit], false);
        NativeBattlePoseSource source = Source([first, second], 512);
        NativeBattlePoseSource recipient = Source([first, second], 1024);
        NativeBattleFootContactCalibration feet = new([new(1, default)], [new(1, default)]);
        NativeBattlePoseSource compiled = new NativeBattlePoseRetargeter(source, recipient, [new("root", 0, 0), new("foot-a", 1, 1)], 1, feet).Compile(source);
        NativeBattleDecodedPose pose = compiled.Sample(0, 2, 0);
        Assert.Equal(-32, pose.Translation.Y);
        Assert.Equal(-512, NativeBattlePoseMatrices.StageBoneMatrices(compiled, pose)[1].Y);
        Assert.Equal(recipient.Skeleton, compiled.Skeleton);
        Assert.Equal(new byte[] { 2 }, NativeBattlePoseDecoder.ReadFrameCounts(compiled.Animations.AsSpan()).ToArray());
    }

    [Fact]
    public void Retained_pose_contact_keeps_its_original_bank_provenance_until_a_new_pose_is_decoded()
    {
        NativeBattlePoseSource source = Source([Pose(0, 0)], 512);
        NativeBattlePoseSource recipient = Source([Pose(0, 0)], 1024);
        NativeBattlePoseRetargeter compiler = new(source, recipient, [new("root", 0, 0), new("hand-a", 1, 1)], 1);
        NativeBattlePoseSource standing = compiler.Compile(source);
        NativeBattlePoseSource external = compiler.Compile(source with { Animations = NativeBattleAnimationEncoder.Encode([[Pose(0, 0), Pose(2, 100), Pose(4, 200)]]) });
        NativeBattleGraphicsModel model = new(2)
        {
            SequenceState = new() { Pose = 0, PoseFrame114 = 1, DecodedPose = standing.Sample(0, 1, 0) },
            LinkedTransform = new(default, default)
        };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, standing);
        NativeBattleGraphicsModel first = NativeBattleLinkedContact.Publish(model, standing);
        NativeBattleGraphicsModel retained = first with { SequenceState = first.SequenceState! with { PoseFrame114 = 3 } };
        Assert.Equal(first.LinkedContactOffset, NativeBattleLinkedContact.Publish(retained, external).LinkedContactOffset);
        NativeBattleGraphicsModel decoded = retained with { SequenceState = retained.SequenceState! with { DecodedPose = external.Sample(0, 3, 0) } };
        decoded = NativeBattleBodyBoneMatrices.Publish508C90(decoded, external);
        Assert.Equal(external.Sample(0, 3, 0).HandContact, decoded.SequenceState!.DecodedPose!.HandContact);
        Assert.NotNull(NativeBattleLinkedContact.Publish(decoded, external).LinkedContactOffset);
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
