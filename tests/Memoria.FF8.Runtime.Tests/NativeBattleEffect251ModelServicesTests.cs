using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect251ModelServicesTests
{
    [Fact]
    public void Bone_world_truncates_current_translation_before_rotation_and_preserves_position_padding()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Loaded();
        NativeBattleEffectArena retained = NativeBattleEffectArena.Clear().Write(6, new byte[] { 0xEF, 0xBE });
        NativeBattleEffect251BoneWorldOutput result = NativeBattleEffect251ModelServices.BoneWorld5FC0(model, source, 1, NativeBattleEffectArena.Clear().MarkUnknown(0, 12), retained);
        Assert.Equal(new[] { 0, 0, -4096 }, Direction(result));
        Assert.Equal(new short[] { 4465, -4467, -24302 }, Position(result));
        Assert.Equal(new byte[] { 0xEF, 0xBE }, result.Position8.Read(6, 2));
    }

    [Fact]
    public void Bone_alias_reads_current_matrices_and_normalizes_both_rotation_results()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Loaded();
        NativeBattleActionMatrix current = new([0, 0, 4096, 0, 4096, 0, -4096, 0, 0], 2, 3, 4, 4096);
        model = model with { CurrentBodyBoneMatrices = model.CurrentBodyBoneMatrices! with { Matrices = model.CurrentBodyBoneMatrices.Matrices.SetItem(1, current) } };
        NativeBattleEffect251BoneWorldOutput result = NativeBattleEffect251ModelServices.BoneWorld5FC0(model, source, 240, NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear().MarkUnknown(6, 2));
        Assert.Equal(new[] { -4096, 0, 0 }, Direction(result));
        Assert.Equal(new short[] { 4465, -4461, -25532 }, Position(result));
        Assert.Equal(new[] { false, false }, result.Position8.ReadSnapshot(6, 2).Known);
    }

    [Fact]
    public void Missing_current_rig_and_invalid_bone_are_not_replaced_with_a_static_anchor()
    {
        (NativeBattleGraphicsModel model, NativeBattlePoseSource source) = Loaded();
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect251ModelServices.BoneWorld5FC0(model with { CurrentBodyBoneMatrices = null }, source, 1, NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear()));
        Assert.Throws<InvalidDataException>(() => NativeBattleEffect251ModelServices.BoneWorld5FC0(model, source, 2, NativeBattleEffectArena.Clear(), NativeBattleEffectArena.Clear()));
    }

    private static (NativeBattleGraphicsModel Model, NativeBattlePoseSource Source) Loaded()
    {
        byte[] skeleton = new byte[112];
        skeleton[0] = 2;
        skeleton[4] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        for (int bone = 0; bone < 2; bone++)
        {
            int offset = 32 + bone * 48;
            foreach (int diagonal in new[] { 0, 8, 16 })
            {
                BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(offset + diagonal), 4096);
            }
        }
        BinaryPrimitives.WriteInt32LittleEndian(skeleton.AsSpan(100), 65538);
        BinaryPrimitives.WriteInt32LittleEndian(skeleton.AsSpan(104), -65539);
        BinaryPrimitives.WriteInt32LittleEndian(skeleton.AsSpan(108), 1234);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [0]);
        NativeBattleGraphicsModel model = NativeBattleBodyBoneMatrices.PublishLoadedSkeleton(new(4, 2), source);
        model = model with { GeometryWorldMatrix = new([2048, 0, 0, 0, 4096, 0, 0, 0, 4096], 70000, -70000, 40000, 4096) };
        return (model, source);
    }

    private static int[] Direction(NativeBattleEffect251BoneWorldOutput result)
    {
        byte[] bytes = result.Direction12.Read(0, 12);
        return Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(index * 4))).ToArray();
    }

    private static short[] Position(NativeBattleEffect251BoneWorldOutput result)
    {
        byte[] bytes = result.Position8.Read(0, 6);
        return Enumerable.Range(0, 3).Select(index => BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * 2))).ToArray();
    }
}
