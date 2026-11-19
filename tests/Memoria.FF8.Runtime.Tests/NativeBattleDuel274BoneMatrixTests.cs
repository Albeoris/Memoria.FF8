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

    private static ImmutableArray<NativeBattleActionMatrix> Bones(int count)
    {
        return Enumerable.Range(0, count).Select(bone => NativeBattleGfMatrices.Euler56CE30(new(checked((short)(17 * bone)), checked((short)(31 * bone)), checked((short)(43 * bone)))) with { X = 100 * bone, Y = 200 * bone, Z = 300 * bone }).ToImmutableArray();
    }
}
