using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleDuel274BoneMatrixTests
{
    [Fact]
    public void Authored_forearm_b_18_uses_its_recipient_semantic_matrix_instead_of_the_other_arm()
    {
        ImmutableArray<NativeBattleActionMatrix> bones = Bones(35);
        byte[] aliases = Enumerable.Repeat(byte.MaxValue, 23).ToArray();
        aliases[18] = 22;

        NativeBattleActionMatrix actual = NativeBattleDuel274Body.ReadBoneMatrix68A790(bones, 18, aliases.ToImmutableArray());

        Assert.Same(bones[22], actual);
        Assert.NotEqual(bones[18], actual);
    }

    [Fact]
    public void Native_or_unscoped_physical_forearm_18_keeps_its_actual_matrix()
    {
        ImmutableArray<NativeBattleActionMatrix> bones = Bones(23);
        Assert.Same(bones[18], NativeBattleDuel274Body.ReadBoneMatrix68A790(bones, 18));
    }

    [Fact]
    public void Authored_forearm_matrix_uses_the_source_local_frame_without_moving_the_physical_joint()
    {
        NativeBattleActionMatrix original = NativeBattleGfMatrices.Euler56CE30(new(173, 397, 611)) with { X = 123, Y = -456, Z = 789, Scale = 164 };
        NativeBattleActionMatrix quarter = new([0, -4096, 0, 4096, 0, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix inverse = new([0, 4096, 0, -4096, 0, 0, 0, 0, 4096], 0, 0, 0, 4096);
        NativeBattleActionMatrix physical = NativeBattleGfMatrices.Compose56C2F0(original, quarter) with { Scale = original.Scale };
        ImmutableArray<byte> aliases = Enumerable.Repeat(byte.MaxValue, 23).ToImmutableArray().SetItem(18, 22);
        ImmutableArray<NativeBattleActionMatrix> frames = Enumerable.Repeat(inverse, 23).ToImmutableArray();
        ImmutableArray<NativeBattleActionMatrix> bones = Bones(35).SetItem(22, physical);

        NativeBattleActionMatrix actual = NativeBattleDuel274Body.ReadBoneMatrix68A790(bones, 18, aliases, frames);

        Assert.Equal(original.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((physical.X, physical.Y, physical.Z, physical.Scale), (actual.X, actual.Y, actual.Z, actual.Scale));
        Assert.Equal(NativeBattleGfMatrices.Transform460840(original, new(100, 200, 300)), NativeBattleGfMatrices.Transform460840(actual, new(100, 200, 300)));
        Assert.Same(bones[18], NativeBattleDuel274Body.ReadBoneMatrix68A790(bones, 18, actionBoneFrames: frames));
    }

    [Fact]
    public void Unsupported_authored_forearm_does_not_fall_back_to_a_recipient_physical_bone()
    {
        ImmutableArray<byte> aliases = Enumerable.Repeat(byte.MaxValue, 23).ToImmutableArray();
        Assert.Throws<NotSupportedException>(() => NativeBattleDuel274Body.ReadBoneMatrix68A790(Bones(35), 18, aliases));
    }

    [Fact]
    public void Resolved_matrix_must_belong_to_the_current_physical_skeleton()
    {
        byte[] aliases = Enumerable.Repeat(byte.MaxValue, 23).ToArray();
        aliases[18] = 35;
        Assert.Throws<InvalidDataException>(() => NativeBattleDuel274Body.ReadBoneMatrix68A790(Bones(35), 18, aliases.ToImmutableArray()));
    }

    [Fact]
    public void Source_frame_directory_cannot_silently_miss_a_mapped_selector()
    {
        ImmutableArray<byte> aliases = Enumerable.Repeat(byte.MaxValue, 23).ToImmutableArray().SetItem(18, 22);
        Assert.Throws<InvalidDataException>(() => NativeBattleDuel274Body.ReadBoneMatrix68A790(Bones(35), 18, aliases, [Bones(1)[0]]));
    }

    private static ImmutableArray<NativeBattleActionMatrix> Bones(int count)
    {
        return Enumerable.Range(0, count).Select(bone => NativeBattleGfMatrices.Euler56CE30(new(checked((short)(17 * bone)), checked((short)(31 * bone)), checked((short)(43 * bone)))) with { X = 100 * bone, Y = 200 * bone, Z = 300 * bone }).ToImmutableArray();
    }
}
