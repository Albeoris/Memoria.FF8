using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleHeaderDisplacementTests
{
    [Theory]
    [InlineData(0, -1, 244, 503)]
    [InlineData(1024, 303, 244, 401)]
    [InlineData(2048, 201, 244, 97)]
    [InlineData(512, 172, 244, 515)]
    public void Signed_header_vector_is_rotated_into_world_translation(short yaw, int x, int y, int z)
    {
        NativeBattleGraphicsModel model = Model(yaw);
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source());
        Assert.Equal((x, y, z), (matrix.X, matrix.Y, matrix.Z));
        Assert.Equal(new NativeBattleVector(100, 200, 300), model.Transform!.Position28);
    }

    [Fact]
    public void Header_translation_precedes_pointer_and_axis_scales_and_root_pivot_correction()
    {
        NativeBattleGraphicsModel model = Model(0);
        model = model with
        {
            ScaleVectorAddress48 = 0x1000,
            Transform = model.Transform! with { Scale148 = new(8192, 12288, 12288) }
        };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source(), new(8192, 4096, 2048));
        // The fixture root is (16,-48,32). Horizontal correction is (16-64,32-48).
        // Signed header (-101,37,203) is unscaled; the vertical pivot is not corrected.
        Assert.Equal((-49, 244, 487), (matrix.X, matrix.Y, matrix.Z));
        Assert.Equal((short)16384, matrix.Rotation[0]);
        Assert.Equal((short)12288, matrix.Rotation[4]);
        Assert.Equal((short)6144, matrix.Rotation[8]);
    }

    [Fact]
    public void Header_publication_keeps_full_dword_mac_values_above_signed_word_range()
    {
        NativeBattleGraphicsModel model = Model(0);
        model = model with
        {
            Transform = model.Transform! with { Position28 = new(30000, -30000, 30000), Vertical34 = 0 },
            SequenceState = model.SequenceState! with { Header36 = 10000, Header38 = -10000, Header40 = 12000 }
        };
        NativeBattleActionMatrix matrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source());
        Assert.Equal((40000, -40000, 42000), (matrix.X, matrix.Y, matrix.Z));
    }

    [Fact]
    public void Json_restoration_retains_displacement_and_next_publication()
    {
        NativeBattleGraphicsModel model = Model(512);
        model = model with { GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source()) };
        NativeBattleGraphicsModel restored = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(model))!;
        Assert.Equal((-101, 37, 203), ((int)restored.SequenceState!.Header36, (int)restored.SequenceState.Header38, (int)restored.SequenceState.Header40));
        Assert.Equal((172, 244, 515), (restored.GeometryWorldMatrix!.X, restored.GeometryWorldMatrix.Y, restored.GeometryWorldMatrix.Z));
        restored = restored with { SequenceState = restored.SequenceState with { Header40 = 204 } };
        NativeBattleActionMatrix next = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(restored, Source());
        Assert.Equal((173, 244, 516), (next.X, next.Y, next.Z));
    }

    [Fact]
    public void Body_weapon_bone_and_camera_share_the_displaced_world_matrix()
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(0);
        model = model with
        {
            GeometryWorldMatrix = NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, source),
            Flags2 = 0,
            LinkedTransform = new(new(0, 0, 0), new(0, 0, 0)),
            SequenceState = model.SequenceState! with { LinkedPose = new(0, 1, 1, 0) }
        };
        NativeBattleActionMatrix view = new(ImmutableArray.Create<short>(4096, 0, 0, 0, 4096, 0, 0, 0, 4096), 0, 0, 0, 4096);
        NativeBattleActionMatrix body = NativeBattlePoseMatrices.GeometryMatrices(model, source, view)[0];
        NativeBattleActionMatrix weapon = NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, source, view)[0];
        Assert.Equal((15, 196, 535), (body.X, body.Y, body.Z));
        Assert.Equal((body.X, body.Y, body.Z), (weapon.X, weapon.Y, weapon.Z));
        Assert.Equal(new NativeBattleVector(15, 196, 535), NativeBattlePoseMatrices.ReadBone(model, source, 240, 0));
        Assert.Equal(new NativeBattleVector(-1, 244, 503), NativeBattlePoseMatrices.TransformCameraPoint(model, source, 15, new(0, 0, 0), false));
    }

    [Fact]
    public void Missing_header_owner_fails_before_publishing_world_matrix()
    {
        NativeBattleGraphicsModel model = Model(0) with { SequenceState = null };
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.PublishGeometryWorldMatrix502AB0(model, Source()));
    }

    private static NativeBattleGraphicsModel Model(short yaw)
    {
        NativeBattleGraphicsModel model = new(0, 3)
        {
            ScaleVectorAddress48 = 0,
            ModelId4 = 0,
            Rotation12 = 0,
            Rotation14 = yaw,
            Rotation16 = 0,
            Transform = new(new(100, 200, 300), new(100, 200, 300), 7, 0, 4096),
            SequenceState = new() { Pose = 0, PoseFrame114 = 1, Header36 = -101, Header38 = 37, Header40 = 203 }
        };
        return NativeBattleBodyBoneMatrices.Publish508C90(model, Source());
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
        foreach (short value in new short[] { 1, -3, 2 })
        {
            Write(3, 2);
            Write(value, 16);
        }
        Write(0, 1);
        Write(0, 6);
        byte[] animations = new byte[9 + (bits.Count + 7) / 8];
        BinaryPrimitives.WriteInt32LittleEndian(animations, 1);
        BinaryPrimitives.WriteInt32LittleEndian(animations.AsSpan(4), 8);
        animations[8] = 1;
        for (int bit = 0; bit < bits.Count; bit++)
        {
            animations[9 + bit / 8] |= (byte)(bits[bit] << (bit & 7));
        }
        return new(skeleton.ToImmutableArray(), animations.ToImmutableArray());
    }
}
