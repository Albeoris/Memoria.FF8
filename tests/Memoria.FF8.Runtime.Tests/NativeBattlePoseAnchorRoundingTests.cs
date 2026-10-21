using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePoseAnchorRoundingTests
{
    [Theory]
    [InlineData(3072, 1)]
    [InlineData(-3072, -1)]
    [InlineData(1024, 0)]
    [InlineData(-1024, 0)]
    [InlineData(2048, 0)]
    [InlineData(-2048, 0)]
    public void Fractional_anchor_transform_uses_native_biased_rounding_instead_of_arithmetic_floor(short coefficient, short expected)
    {
        var fixture = Fixture(coefficient, 1);
        NativeBattleVector actual = NativeBattlePoseMatrices.ReadBone(fixture.Model, fixture.Source, 1, 4096);
        Assert.Equal(new NativeBattleVector(expected, expected, 1), actual);
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(fixture.Model, fixture.Source, 1, 4096);
        Assert.True(producer.Enabled);
        Assert.Equal((short)1, producer.Distance);
        Assert.Equal(coefficient, producer.Matrix!.Rotation[2]);
    }

    [Fact]
    public void Fraction_times_selected_length_keeps_signed_shift_before_rounded_transform()
    {
        var fixture = Fixture(3072, 5);
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(fixture.Model, fixture.Source, 1, 2048);
        Assert.Equal((short)2, producer.Distance);
        Assert.Equal(new NativeBattleVector(2, 2, 2), NativeBattlePoseMatrices.ReadBone(fixture.Model, fixture.Source, 1, 2048));
        var negative = Fixture(3072, -1);
        Assert.Equal((short)-1, NativeBattlePoseMatrices.ResolveBoneAnchor502170(negative.Model, negative.Source, 1, 2048).Distance);
        Assert.Equal(new NativeBattleVector(-1, -1, -1), NativeBattlePoseMatrices.ReadBone(negative.Model, negative.Source, 1, 2048));
    }

    [Fact]
    public void Scratch_distance_is_stored_as_a_WORD_before_the_full_MAC_transform()
    {
        var fixture = Fixture(1, 32767);
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(fixture.Model, fixture.Source, 1, 32767);
        Assert.Equal((short)-16, producer.Distance);
        Assert.Equal(new NativeBattleVector(0, 0, -16), NativeBattlePoseMatrices.ReadBone(fixture.Model, fixture.Source, 1, 32767));
    }

    [Fact]
    public void Anchor_copies_low_full_MAC_WORDs_without_IR_saturation_and_keeps_anchor_W()
    {
        var fixture = Fixture(3072, 1);
        NativeBattleGraphicsModel model = fixture.Model with { GeometryWorldMatrix = Identity() with { X = 40000, Y = -40000, Z = 70000 } };
        Assert.Equal(new NativeBattleVector(unchecked((short)40001), unchecked((short)-39999), unchecked((short)70001)), NativeBattlePoseMatrices.ReadBone(model, fixture.Source, 1, 4096));
        byte[] skeleton = fixture.Source.Skeleton.ToArray();
        skeleton[4] = 0x81;
        skeleton[5] = 0x81;
        NativeBattlePoseSource source = fixture.Source with { Skeleton = skeleton.ToImmutableArray() };
        model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(model, source);
        NativeBattleModuleAnchor anchor = NativeBattlePoseMatrices.ReadAnchor(model, source);
        Assert.Equal(unchecked((short)40001), anchor.X);
        Assert.Equal(unchecked((short)-39999), anchor.Y);
        Assert.Equal(unchecked((short)70001), anchor.Z);
        Assert.Equal((short)4000, anchor.W);
        Assert.Equal((short)8192, model.Transform!.Scale38);
    }

    [Fact]
    public void Disabled_anchor_exposes_only_current_position_and_requires_no_pose_or_alias_owner()
    {
        NativeBattleGraphicsModel model = new(0, 0) { Transform = new(new(0, 0, 0), new(1, -2, 3), 123, 456, 4096) };
        NativeBattleBoneAnchorProducer producer = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, new([], []), 255, -32768);
        Assert.False(producer.Enabled);
        Assert.Null(producer.Matrix);
        Assert.Equal((short)0, producer.Distance);
        Assert.Equal(new NativeBattleVector(1, -2, 3), producer.DisabledPosition);
        Assert.Equal(producer.DisabledPosition, NativeBattlePoseMatrices.ReadBone(model, new([], []), 255, -32768));
    }

    private static (NativeBattleGraphicsModel Model, NativeBattlePoseSource Source) Fixture(short coefficient, short selectedLength)
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        skeleton[4] = 0;
        skeleton[5] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18), 3);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(64), 0);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(66), selectedLength);
        foreach (int start in new[] { 32, 80 })
        {
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(start), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(start + 8), 4096);
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(start + 16), 4096);
        }
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(84), coefficient);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(90), coefficient);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleGraphicsModel model = new(0, 2) { Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 8192), SequenceState = new(), GeometryWorldMatrix = Identity() };
        model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(model, source);
        return (model, source);
    }

    private static NativeBattleActionMatrix Identity() => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
}
