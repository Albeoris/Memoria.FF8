using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePoseTests
{
    [Fact]
    public void Slow_pose_replays_each_half_delta_with_signed_truncation_instead_of_interpolating_absolute_frames()
    {
        NativeBattlePoseSource source = Source();

        NativeBattleDecodedPose firstHalf = source.Sample(0, 2, 1);
        NativeBattleDecodedPose secondHalf = source.Sample(0, 3, 1);

        Assert.Equal(new NativeBattleVector(0, -2, 2), firstHalf.Translation);
        Assert.Equal(new NativeBattleVector(-1, -1, 2), secondHalf.Translation);
        Assert.Equal(new NativeBattleVector(-2, 0, 3), source.Sample(0, 2, 0).Translation);
        NativeBattleDecodedPose decoded = NativeBattlePoseDecoder.DecodeFrames(source.Animations.AsSpan(), 2)[0][1];
        Assert.Equal(source.Sample(0, 2, 0).Translation, decoded.Translation);
        Assert.Equal(source.Sample(0, 2, 0).Bones, decoded.Bones);
    }

    [Theory]
    [InlineData(0, 116, 152, 482)]
    [InlineData(2048, 84, 152, 118)]
    public void Native_anchor_uses_header_aliases_parent_lengths_world_rotation_and_model_scale(short yaw, short x, short y, short z)
    {
        NativeBattleGraphicsModel model = new(0, 3) { ModelId4 = 0, Rotation12 = 0, Rotation14 = yaw, Rotation16 = 0, Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 16384), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };

        model = NativeBattleBodyBoneMatrices.Publish508C90(model, Source());
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source()) };
        NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(model, Source());

        Assert.Equal(new(x, y, z, 8000), anchor);
        Assert.Equal(model.Transform!.Position28, NativeBattlePoseMatrices.ReadBone(model with { Flags0 = 0 }, Source(), 240, 0));
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ReadAnchor(model with { Transform = null }, Source()));
    }

    [Fact]
    public void Missing_or_out_of_range_pose_owners_fail_before_producing_an_anchor()
    {
        NativeBattlePoseSource source = Source();
        Assert.Throws<InvalidDataException>(() => source.Sample(0, 0, 0));
        Assert.Throws<InvalidDataException>(() => source.Sample(0, 3, 0));
        Assert.Throws<InvalidDataException>(() => source.Sample(1, 1, 0));
        Assert.Throws<InvalidDataException>(() => (source with { Skeleton = [] }).Validate());
    }

    [Theory]
    [InlineData(15, false, 110, 220, 330)]
    [InlineData(0, false, 126, 172, 362)]
    [InlineData(0, true, 106, 172, 362)]
    [InlineData(1, false, 126, 172, 662)]
    public void Actor_relative_camera_uses_the_model_or_aliased_bone_basis_and_mirrors_only_local_X(byte alias, bool mirror, short x, short y, short z)
    {
        NativeBattleGraphicsModel model = new(0, 3) { ModelId4 = 0, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 2048), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };

        model = NativeBattleBodyBoneMatrices.Publish508C90(model, Source());
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source()) };
        Assert.Equal(new NativeBattleVector(x, y, z), NativeBattlePoseMatrices.TransformCameraPoint(model, Source(), alias, new(10, 20, 30), mirror));
    }

    [Fact]
    public void BoneAndCameraReadRetainedMatrixUntilTheActualModelPublisherRuns()
    {
        NativeBattleGraphicsModel model = new(0, 3) { ModelId4 = 0, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 2048), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ReadBone(model, Source(), 240, 0));
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, Source());
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source()) };
        NativeBattleVector before = NativeBattlePoseMatrices.ReadBone(model, Source(), 240, 0);
        model = model with { Transform = model.Transform! with { Position28 = new(1000, 2000, 3000) } };
        Assert.Equal(before, NativeBattlePoseMatrices.ReadBone(model, Source(), 240, 0));
        Assert.Equal(new NativeBattleVector(110, 220, 330), NativeBattlePoseMatrices.TransformCameraPoint(model, Source(), 15, new(10, 20, 30), false));
    }

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        skeleton[4] = 0;
        skeleton[5] = 0x81;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 100);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), 200);
        List<int> bits = [];
        void Write(int value, int length)
        {
            for (int bit = 0; bit < length; bit++)
            {
                bits.Add(value >> bit & 1);
            }
        }
        void Frame(short x, short y, short z)
        {
            foreach (short value in new[] { x, y, z })
            {
                Write(3, 2);
                Write(value, 16);
            }
            Write(0, 1);
            Write(0, 6);
        }
        Frame(1, -3, 2);
        Frame(-3, 3, 1);
        byte[] animations = new byte[9 + (bits.Count + 7) / 8];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4), 8);
        animations[8] = 2;
        for (int bit = 0; bit < bits.Count; bit++)
        {
            animations[9 + bit / 8] |= (byte)(bits[bit] << (bit & 7));
        }
        return new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
    }
}
