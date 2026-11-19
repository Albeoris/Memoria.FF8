using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Numerics;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCinematicPropContactTests
{
    [Fact]
    public void Body_publication_captures_one_common_camera_basis_and_retains_the_drawn_pose_provenance()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 64);
        NativeBattleDecodedPose pose = new(default, [new(default, new(1024, 1024, 1024)), new(default, new(1024, 1024, 1024))], false);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), NativeBattleAnimationEncoder.Encode([[pose, pose, pose]]));
        NativeBattleDecodedSemanticContact contact = new(new(0, 0, 32), 1, 0, 1, 3, 0);
        NativeBattleGraphicsModel model = new(2)
        {
            Flags2 = 0,
            ActiveAnimationDirectory = 0x187C20C,
            GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096),
            SequenceState = new() { Pose = 0, PoseFrame114 = 2, DecodedPose = pose with { HandAContact = contact } }
        };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        NativeBattleActionMatrix view = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 7, 11, 13, 4096);
        NativeBattleCameraProjection projection = new(view, 320, 160, 112);
        model = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(model, source, view);
        NativeBattlePosedSemanticContact actual = NativeBattlePoseMatrices.CaptureHandAContact502D40(model, source, projection, null, 42)!;
        Assert.Equal(new Vector3(0, 0, 32), actual.WorldDelta);
        Assert.Equal(contact, actual.Pose);
        Assert.Equal((byte)1, actual.Pose.Frame);
        Assert.Equal(42L, actual.Callback);
        Assert.Null(NativeBattlePoseMatrices.CaptureHandAContact502D40(model with { SequenceState = model.SequenceState! with { DecodedPose = pose } }, source, projection, null, 42));
    }

    [Fact]
    public void Paired_prop_admission_uses_the_actual_drawn_source_clock_and_selected_bank_lengths()
    {
        NativeBattlePosedSemanticContact drawn = new(201, 0x187C20C, new(default, 7, 0, 83, 155, 0), new(10, -20, 30));
        Assert.True(NativeBattleCinematicPropContact.OwnsPairedTrack(drawn, 201, 0, 83, 155));
        Assert.False(NativeBattleCinematicPropContact.OwnsPairedTrack(drawn, 202, 0, 83, 155));
        Assert.False(NativeBattleCinematicPropContact.OwnsPairedTrack(drawn, 201, 0, 84, 155));
        Assert.False(NativeBattleCinematicPropContact.OwnsPairedTrack(drawn, 201, 0, 83, 154));
        Assert.False(NativeBattleCinematicPropContact.OwnsPairedTrack(drawn, 201, 1, 83, 155));
        Assert.False(NativeBattleCinematicPropContact.OwnsPairedTrack(drawn with { Pose = drawn.Pose with { Animation = 1 } }, 201, 0, 83, 155));
        Assert.False(NativeBattleCinematicPropContact.OwnsPairedTrack(drawn with { AnimationDirectory = 1 }, 201, 0, 83, 155));
        Assert.False(NativeBattleCinematicPropContact.OwnsPairedTrack(null, 201, 0, 83, 155));
        Assert.True(NativeBattleCinematicPropContact.OwnsPairedTrack(drawn with { Pose = drawn.Pose with { Frame = 17, Frames = 23 } }, 201, 0, 17, 23));
    }

    [Fact]
    public void Translation_uses_the_prop_draw_view_and_preserves_authored_shape_track_and_native_gap()
    {
        NativeBattleActionMatrix view = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 100, 200, 300, 4096);
        NativeBattleCameraProjection projection = new(view, 320, 160, 112);
        ImmutableArray<NativeBattleActionMatrix> original = [new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 40, 50, 60, 4096), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 70, 90, 110, 4096)];
        ImmutableArray<NativeBattleActionMatrix> shifted = NativeBattleCinematicPropContact.TranslateOriginal(original, new(10, -20, 30), projection);
        Assert.Equal((70, 30, 50), (shifted[0].X, shifted[0].Y, shifted[0].Z));
        Assert.Equal((30, 40, 50), (shifted[1].X - shifted[0].X, shifted[1].Y - shifted[0].Y, shifted[1].Z - shifted[0].Z));
        Assert.Equal(original[0].Rotation, shifted[0].Rotation);
        Assert.Equal(original[0].Scale, shifted[0].Scale);
        Assert.Equal((40, 50, 60), (original[0].X, original[0].Y, original[0].Z));
        Assert.Equal(shifted.ToArray(), NativeBattleCinematicPropContact.TranslateOriginal(original, new(10, -20, 30), projection).ToArray());
    }
}
