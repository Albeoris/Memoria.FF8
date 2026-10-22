using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBillboardGeometryTests
{
    private static readonly NativeBattleActionMatrix Identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);

    [Fact]
    public void Current_eye_drives_billboard_rotation_without_rebuilding_world_or_pose()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Fixture();
        NativeBattleGraphicsModel drawn = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(model, source, Identity, new(0, 999, -4096));
        NativeBattleActionMatrix matrix = Assert.Single(NativeBattleBodyBoneMatrices.Require(drawn, source));
        Assert.Equal(new short[] { 0, 0, -4096, 0, 4096, 0, 4096, 0, 0 }, matrix.Rotation);
        Assert.Same(model.GeometryWorldMatrix, drawn.GeometryWorldMatrix);
        Assert.Same(model.SequenceState, drawn.SequenceState);
        Assert.Equal(model.Rotation14, drawn.Rotation14);
        Assert.Equal(Identity.Rotation, Assert.Single(NativeBattleBodyBoneMatrices.Require(model, source)).Rotation);
    }

    [Fact]
    public void Signed_model_rotation_is_subtracted_after_angle_and_prior_view_remains_independent()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Fixture();
        model = model with { Rotation14 = -1024 };
        NativeBattleActionMatrix view = Identity with { Rotation = [0, 0, 4096, 0, 4096, 0, -4096, 0, 0], X = 12, Y = 34, Z = 56 };
        NativeBattleGraphicsModel drawn = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(model, source, view, new(0, 0, -4096));
        NativeBattleActionMatrix matrix = Assert.Single(NativeBattleBodyBoneMatrices.Require(drawn, source));
        Assert.Equal(view.Rotation, matrix.Rotation);
        Assert.Equal((12, 34, 56), (matrix.X, matrix.Y, matrix.Z));
    }

    [Fact]
    public void Point_translation_is_low_word_but_difference_retains_full_signed_dword()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Fixture();
        model = model with { GeometryWorldMatrix = Identity with { X = 65536 - 32768 } };
        NativeBattleActionMatrix matrix = Assert.Single(NativeBattlePoseMatrices.GeometryMatrices(model, source, Identity, new(32767, 0, -1)));
        Assert.Equal(new short[] { -4096, 0, 0, 0, 4096, 0, 0, 0, -4096 }, matrix.Rotation);
        Assert.Equal(32768, matrix.X);
    }

    [Fact]
    public void Generic_5095B0_does_not_apply_billboard_and_only_actual_branch_requires_eye_and_rotation()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Fixture();
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.GeometryMatrices(model, source, Identity));
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.GeometryMatrices(model with { Rotation14 = null }, source, Identity, new(0, 0, -1)));
        Assert.Equal(Identity.Rotation, Assert.Single(NativeBattleBodyBoneMatrices.Require(NativeBattlePoseMatrices.ComposeGeometryMatrices5095B0(model, source, Identity), source)).Rotation);
        Assert.Equal(Identity.Rotation, Assert.Single(NativeBattlePoseMatrices.GeometryMatrices(model with { Flags2 = 0, Rotation14 = null }, source, Identity)).Rotation);
    }

    [Fact]
    public void Independent_linked_local_translation_uses_the_same_adjusted_parent_basis()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Fixture();
        model = model with { LinkedTransform = new(new(0, 0, 0), new(100, 0, 0)), SequenceState = model.SequenceState! with { LinkedPose = new(0, 0, 1, 0), LinkedDecodedPose = model.SequenceState.DecodedPose } };
        NativeBattleActionMatrix actual = Assert.Single(NativeBattlePoseMatrices.LinkedDrawGeometryMatrices502D40(model, source, Identity, new(0, 0, -4096)));
        Assert.Equal((0, 0, 100), (actual.X, actual.Y, actual.Z));
        Assert.Equal(new short[] { 0, 0, -4096, 0, 4096, 0, 4096, 0, 0 }, actual.Rotation);
        NativeBattleActionMatrix generic = Assert.Single(NativeBattlePoseMatrices.LinkedGeometryMatrices502D40(model, source, Identity));
        Assert.Equal((100, 0, 0), (generic.X, generic.Y, generic.Z));
    }

    [Fact]
    public void Independent_snapshot_keeps_current_world_and_repeats_same_billboard_composition()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Fixture();
        NativeBattleGraphicsModel imported = JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(model))!;
        NativeBattleGraphicsModel expected = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(model, source, Identity, new(-4096, 0, 0));
        NativeBattleGraphicsModel actual = NativeBattlePoseMatrices.ComposeGeometryMatrices502D40(imported, source, Identity, new(-4096, 0, 0));
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    private static (NativeBattleGraphicsModel Model, NativeBattlePoseSource Source) Fixture()
    {
        byte[] skeleton = new byte[0x40];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(0x10), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1]);
        NativeBattleGraphicsModel model = new(3, 3) { Flags2 = 1, Rotation14 = 0, GeometryWorldMatrix = Identity, SequenceState = new() { DecodedPose = new(new(0, 0, 0), [new(new(0, 0, 0), new(1024, 1024, 1024))], false) } };
        model = NativeBattleBodyBoneMatrices.Publish508C90(model, source);
        model = model with { CurrentBodyBoneMatrices = model.CurrentBodyBoneMatrices! with { Matrices = [Identity] } };
        return (model, source);
    }
}
