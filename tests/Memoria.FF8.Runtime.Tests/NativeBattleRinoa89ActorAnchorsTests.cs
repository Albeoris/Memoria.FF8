using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa89ActorAnchorsTests
{
    [Theory]
    [InlineData(23, 19, 20)]
    [InlineData(25, 21, 22)]
    [InlineData(41, 37, 38)]
    public void Authored_Rinoa_foot_selectors_use_recipient_semantic_anchors(int count, byte firstFoot, byte secondFoot)
    {
        ImmutableArray<NativeBattleActionMatrix> bones = Enumerable.Range(0, count).Select(index => Matrix(index * 10, -index, index + 1)).ToImmutableArray();
        ImmutableArray<byte> aliases = Enumerable.Repeat(byte.MaxValue, 32).ToImmutableArray().SetItem(24, firstFoot).SetItem(25, secondFoot);
        NativeBattleActionMatrix world = Matrix(100, 200, -300);
        foreach ((short authored, byte recipient) in new[] { ((short)24, firstFoot), ((short)25, secondFoot) })
        {
            NativeBattleActionMatrix actual = NativeBattleRinoa89BodyDrawing.ReadActorBoneMatrix6FD2D0(Reference(authored), bones, world, aliases);
            EqualMatrix(NativeBattleGfMatrices.Compose56C2F0(world, bones[recipient]), actual);
        }
    }

    [Fact]
    public void Physical_owner_without_action_aliases_retains_its_own_bone_index()
    {
        ImmutableArray<NativeBattleActionMatrix> bones = Enumerable.Range(0, 32).Select(index => Matrix(index, 0, 0)).ToImmutableArray();
        NativeBattleActionMatrix world = Matrix(10, 20, 30);
        EqualMatrix(NativeBattleGfMatrices.Compose56C2F0(world, bones[24]), NativeBattleRinoa89BodyDrawing.ReadActorBoneMatrix6FD2D0(Reference(24), bones, world));
    }

    [Fact]
    public void Authored_point_keeps_native_local_units_under_the_current_actor_matrix()
    {
        NativeBattleActionMatrix world = new([2048, 0, 0, 0, 2048, 0, 0, 0, 2048], 100, 200, 300, 4096);
        ImmutableArray<byte> aliases = Enumerable.Repeat(byte.MaxValue, 32).ToImmutableArray().SetItem(24, 0);
        NativeBattleActionMatrix matrix = NativeBattleRinoa89BodyDrawing.ReadActorBoneMatrix6FD2D0(Reference(24), [Matrix(0, 0, 0)], world, aliases);
        Assert.Equal(new NativeBattleVector(-1492, 343, -470), NativeBattleGfMatrices.Transform460840(matrix, new(-3184, 286, -1539)));
    }

    [Fact]
    public void Signed_selector_extent_and_unmapped_alias_remain_required()
    {
        ImmutableArray<NativeBattleActionMatrix> bones = [Matrix(0, 0, 0)];
        NativeBattleActionMatrix world = Matrix(0, 0, 0);
        Assert.Throws<InvalidDataException>(() => NativeBattleRinoa89BodyDrawing.ReadActorBoneMatrix6FD2D0(new byte[7], bones, world));
        Assert.Throws<InvalidDataException>(() => NativeBattleRinoa89BodyDrawing.ReadActorBoneMatrix6FD2D0(Reference(-1), bones, world));
        Assert.Throws<InvalidDataException>(() => NativeBattleRinoa89BodyDrawing.ReadActorBoneMatrix6FD2D0(Reference(256), bones, world));
        Assert.Throws<NotSupportedException>(() => NativeBattleRinoa89BodyDrawing.ReadActorBoneMatrix6FD2D0(Reference(24), bones, world, Enumerable.Repeat(byte.MaxValue, 32).ToImmutableArray()));
    }

    private static NativeBattleActionMatrix Matrix(int x, int y, int z) => new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], x, y, z, 4096);

    private static void EqualMatrix(NativeBattleActionMatrix expected, NativeBattleActionMatrix actual)
    {
        Assert.Equal(expected.Rotation.ToArray(), actual.Rotation.ToArray());
        Assert.Equal((expected.X, expected.Y, expected.Z, expected.Scale), (actual.X, actual.Y, actual.Z, actual.Scale));
    }

    private static byte[] Reference(short selector)
    {
        byte[] reference = new byte[8];
        BinaryPrimitives.WriteInt16LittleEndian(reference, -3184);
        BinaryPrimitives.WriteInt16LittleEndian(reference.AsSpan(2), 286);
        BinaryPrimitives.WriteInt16LittleEndian(reference.AsSpan(4), -1539);
        BinaryPrimitives.WriteInt16LittleEndian(reference.AsSpan(6), selector);
        return reference;
    }
}
