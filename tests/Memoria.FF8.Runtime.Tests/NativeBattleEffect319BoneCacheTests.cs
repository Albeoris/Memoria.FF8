using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect319BoneCacheTests
{
    [Fact]
    public void Actual_skeleton_record_pointer_is_distinct_from_its_matrix_and_requires_current_extent()
    {
        const uint skeleton = 0x1939018;
        uint record = NativeBattleEffect319BoneCache.ResolveRecord(skeleton, 36, 25);
        Assert.Equal(skeleton + 0x10u + 25u * 48, record);
        Assert.Equal(25, NativeBattleEffect319BoneCache.ResolveIndex(skeleton, 36, record));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveIndex(skeleton, 36, record + 0x10));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveIndex(skeleton, 25, record));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect319BoneCache.ResolveRecord(skeleton, 36, -1));
    }

    [Fact]
    public void Scaling_mutates_only_current_selected_R_retains_T_and_continues_after_JSON()
    {
        NativeBattleActionMatrix matrix = new([1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000], 123456, -234567, 345678, 4096);
        NativeBattleGraphicsModel original = new(3, 2) { CurrentBodyBoneMatrices = new(new string('A', 64), [matrix, matrix]) };
        NativeBattleGraphicsModel result = NativeBattleEffect319BoneCache.ScaleCurrent56BEF0(original, 1, 2048, 1024);
        Assert.Equal(matrix, result.CurrentBodyBoneMatrices!.Matrices[0]);
        NativeBattleActionMatrix scaled = result.CurrentBodyBoneMatrices.Matrices[1];
        Assert.Equal(new short[] { 500, 1000, 750, 2000, 2500, 1500, 3500, 4000, 2250 }, scaled.Rotation);
        Assert.Equal((matrix.X, matrix.Y, matrix.Z), (scaled.X, scaled.Y, scaled.Z));
        Assert.Equal(matrix, original.CurrentBodyBoneMatrices!.Matrices[1]);
        NativeBattleGraphicsModel restored = NativeBattleStorageSnapshots.Restore(JsonSerializer.Deserialize<NativeBattleGraphicsModel>(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(result)))!);
        Assert.Equal(JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect319BoneCache.ScaleCurrent56BEF0(result, 1, 4096, 2048))), JsonSerializer.Serialize(NativeBattleStorageSnapshots.Capture(NativeBattleEffect319BoneCache.ScaleCurrent56BEF0(restored, 1, 4096, 2048))));
    }
}
