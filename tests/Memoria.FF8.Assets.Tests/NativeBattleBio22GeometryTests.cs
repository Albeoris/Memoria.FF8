using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBio22GeometryTests
{
    [Fact]
    public void Actual_four_shared_meshes_keep_their_own_vertex_banks_faces_and_warm_JSON_continuation()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        NativeBattleBio22Geometry first = NativeBattleBio22Geometry.FromColdProcess(resources.MeshBaseline);
        NativeBattleBio22Geometry second = NativeBattleBio22Geometry.FromColdProcess(resources.SecondMeshBaseline);
        NativeBattleBio22Geometry third = NativeBattleBio22Geometry.FromColdProcess(resources.ThirdMeshBaseline);
        NativeBattleBio22Geometry fourth = NativeBattleBio22Geometry.FromColdProcess(resources.FourthMeshBaseline);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Bio22Geometry = first, Bio22SecondaryGeometry = second, Bio22ThirdGeometry = third, Bio22FourthGeometry = fourth };
        memory = memory with { Bio22ThirdGeometry = third.Morph89B0D0(resources.SecondFamilyInitialSources[0].Data, resources.SecondFamilyInitialSources[1].Data, 2048) };
        Assert.Same(first, memory.Bio22Geometry);
        Assert.Same(second, memory.Bio22SecondaryGeometry);
        Assert.Same(fourth, memory.Bio22FourthGeometry);
        memory = memory with { Bio22FourthGeometry = fourth.Morph89B0D0(resources.SecondFamilyMorphSources[0].Data, resources.SecondFamilyMorphSources[1].Data, 2048) };
        foreach ((NativeBattleBio22Geometry original, NativeBattleBio22Geometry changed, int count) in new[] { (third, memory.Bio22ThirdGeometry!, 467), (fourth, memory.Bio22FourthGeometry!, 258) })
        {
            Assert.Equal(original.Bytes.AsSpan().Slice(0, 8).ToArray(), changed.Bytes.AsSpan().Slice(0, 8).ToArray());
            Assert.Equal(original.Bytes.AsSpan().Slice(8 + count * 8).ToArray(), changed.Bytes.AsSpan().Slice(8 + count * 8).ToArray());
            for (int vertex = 0; vertex < count; vertex++)
            {
                Assert.Equal(original.Bytes.AsSpan().Slice(8 + vertex * 8 + 6, 2).ToArray(), changed.Bytes.AsSpan().Slice(8 + vertex * 8 + 6, 2).ToArray());
            }
        }
        NativeBattleEffectMemory restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(NativeBattleSnapshotJson.Serialize(memory.BeginLoad(22)))!;
        Assert.Equal(first.Bytes.ToArray(), restored.Bio22Geometry!.Bytes.ToArray());
        Assert.Equal(second.Bytes.ToArray(), restored.Bio22SecondaryGeometry!.Bytes.ToArray());
        Assert.Equal(memory.Bio22FourthGeometry!.Morph89B0D0(resources.SecondFamilyMorphSources[3].Data, resources.SecondFamilyMorphSources[4].Data, 3072).Bytes.ToArray(), restored.Bio22FourthGeometry!.Morph89B0D0(resources.SecondFamilyMorphSources[3].Data, resources.SecondFamilyMorphSources[4].Data, 3072).Bytes.ToArray());
        Assert.Equal(memory.Bio22ThirdGeometry!.Bytes.ToArray(), restored.Bio22ThirdGeometry!.Bytes.ToArray());
    }

    [Fact]
    public void Actual_second_profile_morphs_independently_and_both_current_meshes_survive_cold_next()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        NativeBattleBio22Geometry primary = NativeBattleBio22Geometry.FromColdProcess(resources.MeshBaseline);
        for (int source = 0; source < 4; source++)
        {
            primary = primary.Morph89B0D0(resources.MorphSources[source].Data, resources.MorphSources[source + 1].Data, 2048);
        }
        NativeBattleBio22Geometry secondary = NativeBattleBio22Geometry.FromColdProcess(resources.SecondMeshBaseline);
        NativeBattleBio22Geometry changed = secondary.Morph89B0D0(resources.MorphSources[5].Data, resources.MorphSources[6].Data, 2048);
        Assert.Equal(secondary.Bytes.AsSpan().Slice(0, 8).ToArray(), changed.Bytes.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal(secondary.Bytes.AsSpan().Slice(0x158).ToArray(), changed.Bytes.AsSpan().Slice(0x158).ToArray());
        for (int vertex = 0; vertex < 42; vertex++)
        {
            Assert.Equal(secondary.Bytes.AsSpan().Slice(8 + vertex * 8 + 6, 2).ToArray(), changed.Bytes.AsSpan().Slice(8 + vertex * 8 + 6, 2).ToArray());
        }
        Assert.False(resources.MeshBaseline.Bytes.SequenceEqual(primary.Bytes));
        Assert.False(secondary.Bytes.SequenceEqual(changed.Bytes));
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Bio22Geometry = primary, Bio22SecondaryGeometry = changed };
        NativeBattleEffectMemory warm = memory.BeginLoad(22);
        Assert.Same(primary, warm.Bio22Geometry);
        Assert.Same(changed, warm.Bio22SecondaryGeometry);
        NativeBattleEffectMemory restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(NativeBattleSnapshotJson.Serialize(warm))!;
        NativeBattleBio22Geometry endpoint = restored.Bio22SecondaryGeometry!.Morph89B0D0(resources.MorphSources[5].Data, resources.MorphSources[6].Data, 4096);
        Assert.Equal(changed.Morph89B0D0(resources.MorphSources[5].Data, resources.MorphSources[6].Data, 4096).Bytes.ToArray(), endpoint.Bytes.ToArray());
        Assert.Equal(primary.Bytes.ToArray(), restored.Bio22Geometry!.Bytes.ToArray());
        for (int vertex = 0; vertex < 42; vertex++)
        {
            Assert.Equal(resources.MorphSources[6].Data.AsSpan().Slice(8 + vertex * 8, 6).ToArray(), endpoint.Bytes.AsSpan().Slice(8 + vertex * 8, 6).ToArray());
        }
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().Bio22SecondaryGeometry);
    }

    [Theory]
    [InlineData((short)0)]
    [InlineData((short)2048)]
    [InlineData((short)4096)]
    public void Actual_shared_mesh_uses_original_sources_and_retains_non_XYZ_bytes_across_JSON_next(short ratio)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        NativeBattleBio22Geometry cold = NativeBattleBio22Geometry.FromColdProcess(resources.MeshBaseline);
        byte[] currentBytes = cold.Bytes.ToArray();
        currentBytes[8 + 6] = 0xC7;
        NativeBattleBio22Geometry current = new(currentBytes.ToImmutableArray());
        NativeBattleBio22Geometry next = current.Morph89B0D0(resources.MorphFirst.Data, resources.MorphSecond.Data, ratio);
        Assert.Equal(NativeBattleEnhancementMorph.Morph(current.Bytes, resources.MorphFirst.Data, resources.MorphSecond.Data, ratio).ToArray(), next.Bytes.ToArray());
        Assert.Equal(current.Bytes.AsSpan().Slice(0, 8).ToArray(), next.Bytes.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal(current.Bytes.AsSpan().Slice(0x2E0).ToArray(), next.Bytes.AsSpan().Slice(0x2E0).ToArray());
        for (int vertex = 0; vertex < 91; vertex++)
        {
            int offset = 8 + vertex * 8;
            Assert.Equal(current.Bytes.AsSpan().Slice(offset + 6, 2).ToArray(), next.Bytes.AsSpan().Slice(offset + 6, 2).ToArray());
            if (ratio is 0 or 4096)
            {
                ImmutableArray<byte> selected = ratio == 0 ? resources.MorphFirst.Data : resources.MorphSecond.Data;
                Assert.Equal(selected.AsSpan().Slice(offset, 6).ToArray(), next.Bytes.AsSpan().Slice(offset, 6).ToArray());
            }
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Bio22Geometry = next };
        Assert.Same(next, memory.BeginLoad(22).Bio22Geometry);
        NativeBattleEffectMemory restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffectMemory>(NativeBattleSnapshotJson.Serialize(memory))!;
        NativeBattleBio22Geometry expectedNext = next.Morph89B0D0(resources.MorphFirst.Data, resources.MorphSecond.Data, 3072);
        NativeBattleBio22Geometry actualNext = restored.Bio22Geometry!.Morph89B0D0(resources.MorphFirst.Data, resources.MorphSecond.Data, 3072);
        Assert.Equal(expectedNext.Bytes.ToArray(), actualNext.Bytes.ToArray());
        Assert.Equal((byte)0xC7, actualNext.Bytes[14]);
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().Bio22Geometry);
    }
}
