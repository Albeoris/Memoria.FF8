using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModel8FBoneAliasTests
{
    [Theory]
    [InlineData(240, 27)]
    [InlineData(241, 11)]
    [InlineData(255, 7)]
    [InlineData(2, 2)]
    public void Selectors_use_the_original_model_directory_and_direct_indices_remain_direct(byte selector, int bone)
    {
        NativeBattlePoseSource source = Source();
        NativeBattleGraphicsModel model = Model(source);
        NativeBattleBoneAnchorProducer result = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, selector, 0);
        Assert.Equal(100 + bone, result.Matrix!.X);
    }

    [Fact]
    public void Missing_directory_only_blocks_the_actual_model8F_alias_branch()
    {
        NativeBattlePoseSource source = Source() with { Model8FBoneAliases = [] };
        NativeBattleGraphicsModel model = Model(source);
        Assert.Equal(102, NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, 2, 0).Matrix!.X);
        Assert.Throws<NotSupportedException>(() => NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, 240, 0));
    }

    [Fact]
    public void Alias_high_bit_selects_the_full_bone_length_and_keeps_bone_bounds_strict()
    {
        NativeBattlePoseSource source = Source();
        source = source with { Model8FBoneAliases = source.Model8FBoneAliases.SetItem(0, 0x82) };
        NativeBattleGraphicsModel model = Model(source);
        NativeBattleBoneAnchorProducer result = NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, 240, 0);
        Assert.Equal(102, result.Matrix!.X);
        Assert.Equal((short)75, result.Distance);
        source = source with { Model8FBoneAliases = source.Model8FBoneAliases.SetItem(0, 99) };
        Assert.Throws<InvalidDataException>(() => NativeBattlePoseMatrices.ResolveBoneAnchor502170(model, source, 240, 0));
    }

    [Fact]
    public void Independent_JSON_retains_the_selected_directory_without_inventing_it_for_old_sources()
    {
        NativeBattlePoseSource source = Source();
        NativeBattlePoseSource restored = JsonSerializer.Deserialize<NativeBattlePoseSource>(JsonSerializer.Serialize(source))!;
        Assert.Equal(source.Model8FBoneAliases.ToArray(), restored.Model8FBoneAliases.ToArray());
        Assert.Equal(127, NativeBattlePoseMatrices.ResolveBoneAnchor502170(Model(restored), restored, 240, 0).Matrix!.X);
        Assert.Empty(JsonSerializer.Deserialize<NativeBattlePoseSource>("{\"Skeleton\":[1],\"Animations\":[1]}")!.Model8FBoneAliases);
    }

    private static NativeBattlePoseSource Source()
    {
        byte[] skeleton = new byte[16 + 28 * 48];
        skeleton[0] = 28;
        for (int bone = 0; bone < 28; bone++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16 + bone * 48), bone == 0 ? (short)-1 : (short)0);
            BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(18 + bone * 48), 75);
        }
        return new(skeleton.ToImmutableArray(), [1]) { Model8FBoneAliases = [27, 11, 0, 0, 4, 2, 3, 3, 2, 4, 7, 5, 5, 6, 9, 7] };
    }

    private static NativeBattleGraphicsModel Model(NativeBattlePoseSource source)
    {
        NativeBattleActionMatrix identity = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096);
        return new(0, 2)
        {
            ModelId4 = 0x8F,
            Transform = new(new(0, 0, 0), new(0, 0, 0), 0, 0, 4096),
            GeometryWorldMatrix = identity,
            SequenceState = new(),
            CurrentBodyBoneMatrices = new(NativeBattleBodyBoneMatrices.Identity(source), Enumerable.Range(0, 28).Select(bone => identity with { X = 100 + bone }).ToImmutableArray())
        };
    }
}
