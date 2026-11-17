using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePoseTests
{
    [Fact]
    public void Model_scale_pointer_compounds_two_word_truncations_and_requires_its_live_vector()
    {
        NativeBattleGraphicsModel model = new(3)
        {
            ScaleVectorAddress48 = 0x269D008,
            ModelId4 = 0,
            Rotation12 = 0,
            Rotation14 = 512,
            Rotation16 = 0,
            Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 4096) { Scale148 = new(4095, 4096, 4096) },
            SequenceState = new() { Pose = 0, PoseFrame114 = 1 }
        };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, Source());
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source()));
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source(), new(8191, 4096, 4096));
        Assert.Equal((short)5789, matrix.Rotation[0]);
        Assert.NotEqual((short)5790, matrix.Rotation[0]);
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model with { ScaleVectorAddress48 = 0 }, Source(), new(4096, 4096, 4096)));
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model with { ScaleVectorAddress48 = null }, Source()));
    }

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
        NativeBattleGraphicsModel model = new(3) { ScaleVectorAddress48 = 0, ModelId4 = 0, Rotation12 = 0, Rotation14 = yaw, Rotation16 = 0, Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 16384), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };

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
        NativeBattleGraphicsModel model = new(3) { ScaleVectorAddress48 = 0, ModelId4 = 0, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 2048), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };

        model = NativeBattleBodyBoneMatrices.Publish508C90(model, Source());
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source()) };
        Assert.Equal(new NativeBattleVector(x, y, z), NativeBattlePoseMatrices.TransformCameraPoint(model, Source(), alias, new(10, 20, 30), mirror));
    }

    [Fact]
    public void BoneAndCameraReadRetainedMatrixUntilTheActualModelPublisherRuns()
    {
        NativeBattleGraphicsModel model = new(3) { ScaleVectorAddress48 = 0, ModelId4 = 0, Rotation12 = 0, Rotation14 = 0, Rotation16 = 0, Transform = new(new(100, 200, 300), new(100, 200, 300), 0, 0, 2048), SequenceState = new() { Pose = 0, PoseFrame114 = 1 } };
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ReadBone(model, Source(), 240, 0));
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, Source());
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source()) };
        NativeBattleVector before = NativeBattlePoseMatrices.ReadBone(model, Source(), 240, 0);
        model = model with { Transform = model.Transform! with { Position28 = new(1000, 2000, 3000) } };
        Assert.Equal(before, NativeBattlePoseMatrices.ReadBone(model, Source(), 240, 0));
        Assert.Equal(new NativeBattleVector(110, 220, 330), NativeBattlePoseMatrices.TransformCameraPoint(model, Source(), 15, new(10, 20, 30), false));
    }

    [Theory]
    [InlineData(0, 0, false, 1010, 2020, 3030)]
    [InlineData(0, 14, true, 990, 2020, 3030)]
    [InlineData(0, 15, false, 110, 220, 330)]
    [InlineData(2, 15, true, 90, 220, 330)]
    public void Camera_reads_retained_matrix_and_inactive_position_without_an_animation_resource(byte flags, byte alias, bool mirror, short x, short y, short z)
    {
        NativeBattleGraphicsModel model = new(flags)
        {
            ModelId4 = 16,
            Transform = new(new(100, 200, 300), new(1000, 2000, 3000), 0, 0, 2048),
            GeometryWorldMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 100, 200, 300, 4096)
        };
        NativeBattleVector expected = new(x, y, z);

        Assert.Equal(expected, NativeBattlePoseMatrices.TransformCameraPoint(model, null, alias, new(10, 20, 30), mirror));
        NativeBattleGraphicsModel restored = System.Text.Json.JsonSerializer.Deserialize<NativeBattleGraphicsModel>(System.Text.Json.JsonSerializer.Serialize(model))!;
        Assert.Equal(expected, NativeBattlePoseMatrices.TransformCameraPoint(restored, null, alias, new(10, 20, 30), mirror));
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.TransformCameraPoint(model with { Flags0 = 2 }, null, 0, new(10, 20, 30), false));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0x10010)]
    public void Decoder_borrows_large_compressed_sections_only_for_the_call_and_keeps_packed_progress(int clipPadding)
    {
        NativeBattlePoseSource source = Source();
        byte[] large = new byte[2 * NativeBattleModelCpuBank.Length];
        source.Animations.AsSpan(0, 8).CopyTo(large);
        BinaryPrimitives.WriteInt32LittleEndian(large.AsSpan(4), 8 + clipPadding);
        source.Animations.AsSpan()[8..].CopyTo(large.AsSpan(8 + clipPadding));
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(large, 2, 0);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(large, 2, 0, first.Pose, first.ByteOffset, first.BitOffset);
        Assert.Equal(source.Sample(0, 2, 0).Translation, next.Pose.Translation);
        Assert.Equal(source.Sample(0, 2, 0).Bones, next.Pose.Bones);
        NativeBattlePoseFirstFrame small = NativeBattlePoseDecoder.DecodeFirst509440(source.Animations.AsSpan(), 2, 0);
        Assert.Equal(small.ByteOffset, first.ByteOffset);
        Assert.Equal(small.BitOffset, first.BitOffset);
        Assert.Equal(source.Sample(0, 2, 1).Translation, NativeBattlePoseDecoder.Sample(large, 2, 0, 2, true).Translation);
        NativeBattlePoseDecoder.DecodeFirst509440(large, 2, 0);
        long before = GC.GetAllocatedBytesForCurrentThread();
        NativeBattlePoseDecoder.DecodeFirst509440(large, 2, 0);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated < NativeBattleModelCpuBank.Length, $"Single-frame decode allocated {allocated} bytes for a borrowed source.");
        large.AsSpan().Clear();
        Assert.Equal(small.Pose.Translation, first.Pose.Translation);
        Assert.Equal(small.Pose.Bones, first.Pose.Bones);
        Assert.Throws<InvalidDataException>(() => NativeBattlePoseDecoder.DecodeFirst509440(large, 2, 0));
    }

    [Theory]
    [InlineData(64)]
    [InlineData(128)]
    public void Differential_pose_preserves_owned_rows_wrap_scale_and_cursor(int bones)
    {
        byte[] section = ScaledSection(bones);
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(section, bones, 0);
        NativeBattlePoseFirstFrame next = NativeBattlePoseDecoder.AdvanceRegular508F90(section, bones, 0, first.Pose, first.ByteOffset, first.BitOffset);
        NativeBattleDecodedPose half = NativeBattlePoseDecoder.Sample(section, bones, 0, 2, slow: true);
        NativeBattleDecodedPose bothHalves = NativeBattlePoseDecoder.Sample(section, bones, 0, 3, slow: true);
        ImmutableArray<NativeBattleDecodedPose> frames = NativeBattlePoseDecoder.DecodeFrames(section, bones)[0];

        Assert.Equal(7 + 10 * bones, first.ByteOffset);
        Assert.Equal(7, first.BitOffset);
        int finalBits = 110 + 125 * bones;
        Assert.Equal(1 + (finalBits >> 3), next.ByteOffset);
        Assert.Equal(finalBits & 7, next.BitOffset);
        Assert.Equal(new NativeBattleVector(32767, -32768, 5), first.Pose.Translation);
        Assert.Equal(new NativeBattleVector(-32766, 32765, 6), next.Pose.Translation);
        Assert.Equal(new NativeBattleVector(-32768, 32767, 5), half.Translation);
        Assert.Equal(new NativeBattleVector(-32767, 32766, 5), bothHalves.Translation);
        Assert.True(first.Pose.HasScale);
        Assert.False(next.Pose.HasScale);
        Assert.False(half.HasScale);
        Assert.Equal(new NativeBattleBonePose(new(2047, -2048, 3), new(1300, -300, 1024)), first.Pose.Bones[^1]);
        Assert.Equal(new NativeBattleBonePose(new(2050, -2051, 4), new(1024, 1024, 1024)), next.Pose.Bones[^1]);
        Assert.Equal(new NativeBattleBonePose(new(2049, -2050, 3), new(1024, 1024, 1024)), bothHalves.Bones[^1]);
        Assert.Equal(first.Pose.Bones, frames[0].Bones);
        Assert.Equal(next.Pose.Bones, frames[1].Bones);
        Assert.NotSame(ImmutableCollectionsMarshal.AsArray(frames[0].Bones), ImmutableCollectionsMarshal.AsArray(frames[1].Bones));

        NativeBattleDecodedPose fork = NativeBattleStorageSnapshots.Fork(first.Pose);
        Assert.Same(ImmutableCollectionsMarshal.AsArray(first.Pose.Bones), ImmutableCollectionsMarshal.AsArray(fork.Bones));
        string json = JsonSerializer.Serialize(first.Pose);
        NativeBattleDecodedPose restored = JsonSerializer.Deserialize<NativeBattleDecodedPose>(json)!;
        Assert.Equal(first.Pose.Bones, restored.Bones);
        using JsonDocument fields = JsonDocument.Parse(json);
        Assert.Equal(1300, fields.RootElement.GetProperty("Bones")[0].GetProperty("Scale").GetProperty("X").GetInt32());
        Assert.Throws<InvalidDataException>(() => NativeBattlePoseDecoder.AdvanceRegular508F90(section.AsSpan(0, section.Length - 1), bones, 0, first.Pose, first.ByteOffset, first.BitOffset));
        Array.Clear(section);
        Assert.Equal(new NativeBattleVector(1300, -300, 1024), fork.Bones[^1].Scale);
    }

    [Fact]
    public void Regular_advance_allocates_only_the_retained_pose_bone_array_and_result_owners()
    {
        const int bones = 64;
        const int repetitions = 128;
        byte[] section = ScaledSection(bones);
        NativeBattlePoseFirstFrame first = NativeBattlePoseDecoder.DecodeFirst509440(section, bones, 0);
        _ = Advance();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int step = 0; step < repetitions; step++)
        {
            GC.KeepAlive(Advance());
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        const int posePayload = bones * 12;
        Assert.InRange(allocated, repetitions * posePayload, repetitions * (posePayload + 128));

        NativeBattlePoseFirstFrame Advance()
        {
            return NativeBattlePoseDecoder.AdvanceRegular508F90(section, bones, 0, first.Pose, first.ByteOffset, first.BitOffset);
        }
    }

    private static byte[] ScaledSection(int bones)
    {
        List<int> bits = [];
        Frame(new(32767, -32768, 5), new(2047, -2048, 3), scaled: true);
        Frame(new(3, -3, 1), new(3, -3, 1), scaled: false);

        byte[] section = new byte[9 + (bits.Count + 7) / 8];
        BinaryPrimitives.WriteInt32LittleEndian(section, 1);
        BinaryPrimitives.WriteInt32LittleEndian(section.AsSpan(4), 8);
        section[8] = 2;
        for (int index = 0; index < bits.Count; index++)
        {
            section[9 + index / 8] |= (byte)(bits[index] << (index & 7));
        }
        return section;

        void Write(int value, int count)
        {
            for (int bit = 0; bit < count; bit++)
            {
                bits.Add((value >> bit) & 1);
            }
        }

        void Frame(NativeBattleVector translation, NativeBattleVector rotation, bool scaled)
        {
            Position(translation.X);
            Position(translation.Y);
            Position(translation.Z);
            Write(scaled ? 1 : 0, 1);
            for (int bone = 0; bone < bones; bone++)
            {
                Rotation(rotation.X);
                Rotation(rotation.Y);
                Rotation(rotation.Z);
                if (scaled)
                {
                    Write(1, 1);
                    Write(276, 16);
                    Write(1, 1);
                    Write(-1324, 16);
                    Write(0, 1);
                }
            }
        }

        void Position(short value)
        {
            Write(3, 2);
            Write(value, 16);
        }

        void Rotation(short value)
        {
            Write(1, 1);
            Write(3, 2);
            Write(value, 12);
        }
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
