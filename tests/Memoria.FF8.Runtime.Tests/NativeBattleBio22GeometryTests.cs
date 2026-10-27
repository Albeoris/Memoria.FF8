using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22GeometryTests
{
    [Theory]
    [InlineData((short)0)]
    [InlineData((short)2048)]
    [InlineData((short)4096)]
    [InlineData((short)-1)]
    public void Shared_kernel_replaces_only_XYZ_and_preserves_current_mesh_stores(short ratio)
    {
        NativeBattleBio22Geometry current = Cold();
        ImmutableArray<byte> first = Source(-200, 17);
        ImmutableArray<byte> second = Source(300, 29);
        NativeBattleBio22Geometry next = current.Morph89B0D0(first, second, ratio);
        Assert.Equal(NativeBattleEnhancementMorph.Morph(current.Bytes, first, second, ratio).ToArray(), next.Bytes.ToArray());
        Assert.Equal(current.Bytes.AsSpan().Slice(0, 8).ToArray(), next.Bytes.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal(current.Bytes.AsSpan().Slice(0x2E0).ToArray(), next.Bytes.AsSpan().Slice(0x2E0).ToArray());
        for (int vertex = 0; vertex < 91; vertex++)
        {
            int offset = 8 + vertex * 8;
            Assert.Equal(current.Bytes.AsSpan().Slice(offset + 6, 2).ToArray(), next.Bytes.AsSpan().Slice(offset + 6, 2).ToArray());
            if (ratio is 0 or 4096)
            {
                ImmutableArray<byte> selected = ratio == 0 ? first : second;
                Assert.Equal(selected.AsSpan().Slice(offset, 6).ToArray(), next.Bytes.AsSpan().Slice(offset, 6).ToArray());
            }
        }
    }

    [Fact]
    public void Shared_owner_is_retained_across_loads_and_independent_JSON_next_step()
    {
        NativeBattleBio22Geometry firstTask = Cold().Morph89B0D0(Source(-50, 7), Source(90, 13), 1024);
        byte[] retained = firstTask.Bytes.ToArray();
        retained[0x300] = 0xD3;
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Bio22Geometry = new(retained.ToImmutableArray()) };
        NativeBattleEffectMemory nextLoad = memory.BeginLoad(22);
        Assert.Same(memory.Bio22Geometry, nextLoad.Bio22Geometry);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(nextLoad))!;
        NativeBattleBio22Geometry nextTask = nextLoad.Bio22Geometry!.Morph89B0D0(Source(200, 23), Source(-300, 41), 3072);
        NativeBattleBio22Geometry restoredTask = restored.Bio22Geometry!.Morph89B0D0(Source(200, 23), Source(-300, 41), 3072);
        Assert.Equal(nextTask.Bytes.ToArray(), restoredTask.Bytes.ToArray());
        Assert.Equal((byte)0xD3, nextTask.Bytes[0x300]);
        Assert.Equal(retained, memory.Bio22Geometry!.Bytes.ToArray());
    }

    [Fact]
    public void Missing_import_does_not_seed_a_baseline_and_cold_ingress_checks_identity_and_extent()
    {
        NativeBattleEffectMemory missing = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(NativeBattleEffectMemory.FromColdProcess()))!;
        Assert.Null(missing.Bio22Geometry);
        Assert.Null(missing.BeginLoad(22).Bio22Geometry);
        Assert.DoesNotContain("Bio22Geometry", NativeBattleTestSnapshots.Json(missing));
        Assert.Throws<InvalidDataException>(() => NativeBattleBio22Geometry.FromColdProcess(new(0, 1, Cold().Bytes)));
        Assert.Throws<InvalidDataException>(() => new NativeBattleBio22Geometry(Source(0, 0)).Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Nonpositive_source_count_preserves_destination_without_consuming_second_source(int count)
    {
        ImmutableArray<byte> current = Cold().Bytes;
        byte[] first = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(first.AsSpan(4), count);
        ImmutableArray<byte> next = NativeBattleEnhancementMorph.Morph(current, first.ToImmutableArray(), default, 4096);
        Assert.True(current.Equals(next));
        Assert.True(NativeBattleEnhancementMorph.Morph(default, first.ToImmutableArray(), default, 4096).IsDefault);
        NativeBattleBio22Geometry unconsumed = new(default);
        Assert.Same(unconsumed, unconsumed.Morph89B0D0(first.ToImmutableArray(), default, 4096));
    }

    [Fact]
    public void Phase_switch_retains_two_independent_physical_meshes_across_warm_load_and_JSON_next()
    {
        NativeBattleBio22Geometry primary = Cold().Morph89B0D0(Source(-100, 3), Source(200, 5), 1024);
        NativeBattleBio22Geometry secondary = Cold(true);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Bio22Geometry = primary, Bio22SecondaryGeometry = secondary };
        NativeBattleBio22Geometry changedSecondary = secondary.Morph89B0D0(Source(700, 11, 42), Source(-200, 17, 42), 2048);
        memory = memory with { Bio22SecondaryGeometry = changedSecondary };
        Assert.Same(primary, memory.Bio22Geometry);
        Assert.Equal(primary.Bytes.ToArray(), memory.Bio22Geometry!.Bytes.ToArray());
        Assert.Equal(secondary.Bytes.AsSpan().Slice(0x158).ToArray(), changedSecondary.Bytes.AsSpan().Slice(0x158).ToArray());
        for (int vertex = 0; vertex < 42; vertex++)
        {
            Assert.Equal(secondary.Bytes.AsSpan().Slice(8 + vertex * 8 + 6, 2).ToArray(), changedSecondary.Bytes.AsSpan().Slice(8 + vertex * 8 + 6, 2).ToArray());
        }
        NativeBattleEffectMemory warm = memory.BeginLoad(22);
        Assert.Same(primary, warm.Bio22Geometry);
        Assert.Same(changedSecondary, warm.Bio22SecondaryGeometry);
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(warm))!;
        NativeBattleBio22Geometry nextPrimary = warm.Bio22Geometry!.Morph89B0D0(Source(19, 21), Source(43, 45), 3072);
        Assert.Equal(nextPrimary.Bytes.ToArray(), restored.Bio22Geometry!.Morph89B0D0(Source(19, 21), Source(43, 45), 3072).Bytes.ToArray());
        Assert.Equal(changedSecondary.Bytes.ToArray(), restored.Bio22SecondaryGeometry!.Bytes.ToArray());
        Assert.Equal(warm.Bio22SecondaryGeometry!.Morph89B0D0(Source(20, 31, 42), Source(40, 41, 42), 4096).Bytes.ToArray(), restored.Bio22SecondaryGeometry.Morph89B0D0(Source(20, 31, 42), Source(40, 41, 42), 4096).Bytes.ToArray());
    }

    [Fact]
    public void Primary_only_import_does_not_hydrate_secondary_and_cold_profiles_cannot_be_crossed()
    {
        NativeBattleEffectMemory primaryOnly = NativeBattleEffectMemory.FromColdProcess() with { Bio22Geometry = Cold() };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(primaryOnly))!;
        Assert.Null(restored.Bio22SecondaryGeometry);
        Assert.Null(restored.BeginLoad(22).Bio22SecondaryGeometry);
        Assert.DoesNotContain("Bio22SecondaryGeometry", NativeBattleTestSnapshots.Json(restored));
        Assert.Throws<InvalidDataException>(() => NativeBattleBio22Geometry.FromColdProcess(new(0, 0x1615CE4, Cold(true).Bytes)));
        Assert.Throws<InvalidDataException>(() => NativeBattleBio22Geometry.FromColdProcess(new(0, 0x1617E3C, Cold().Bytes)));
    }

    [Fact]
    public void Four_physical_owners_remain_distinct_through_second_family_morphs_and_JSON()
    {
        NativeBattleBio22Geometry first = Cold();
        NativeBattleBio22Geometry second = Cold(true);
        NativeBattleBio22Geometry third = ColdProfile(0x1619888, 467, 0x5028);
        NativeBattleBio22Geometry fourth = ColdProfile(0x161F750, 258, 0x4038);
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Bio22Geometry = first, Bio22SecondaryGeometry = second, Bio22ThirdGeometry = third, Bio22FourthGeometry = fourth };
        memory = memory with { Bio22ThirdGeometry = third.Morph89B0D0(Source(-300, 5, 467), Source(700, 7, 467), 2048) };
        Assert.Same(first, memory.Bio22Geometry);
        Assert.Same(second, memory.Bio22SecondaryGeometry);
        Assert.Same(fourth, memory.Bio22FourthGeometry);
        memory = memory with { Bio22FourthGeometry = fourth.Morph89B0D0(Source(200, 11, 258), Source(-800, 13, 258), 3072) };
        NativeBattleEffectMemory restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleEffectMemory>(NativeBattleTestSnapshots.Json(memory.BeginLoad(22)))!;
        Assert.Equal(memory.Bio22ThirdGeometry!.Bytes.ToArray(), restored.Bio22ThirdGeometry!.Bytes.ToArray());
        Assert.Equal(memory.Bio22FourthGeometry!.Morph89B0D0(Source(1, 2, 258), Source(3, 4, 258), 4096).Bytes.ToArray(), restored.Bio22FourthGeometry!.Morph89B0D0(Source(1, 2, 258), Source(3, 4, 258), 4096).Bytes.ToArray());
        foreach ((NativeBattleBio22Geometry original, NativeBattleBio22Geometry changed, int count) in new[] { (third, memory.Bio22ThirdGeometry!, 467), (fourth, memory.Bio22FourthGeometry!, 258) })
        {
            Assert.Equal(original.Bytes.AsSpan().Slice(8 + count * 8).ToArray(), changed.Bytes.AsSpan().Slice(8 + count * 8).ToArray());
            for (int vertex = 0; vertex < count; vertex++)
            {
                Assert.Equal(original.Bytes.AsSpan().Slice(8 + vertex * 8 + 6, 2).ToArray(), changed.Bytes.AsSpan().Slice(8 + vertex * 8 + 6, 2).ToArray());
            }
        }
        Assert.Throws<InvalidDataException>(() => third.ValidateForAddress(0x161F750));
        Assert.Throws<InvalidDataException>(() => fourth.ValidateForAddress(0x1619888));
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().Bio22ThirdGeometry);
        Assert.Null(NativeBattleEffectMemory.FromColdProcess().BeginLoad(22).Bio22FourthGeometry);
    }

    private static NativeBattleBio22Geometry ColdProfile(uint address, int count, int length)
    {
        byte[] bytes = Enumerable.Range(0, length).Select(index => unchecked((byte)(index * 17))).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 8 + count * 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        return NativeBattleBio22Geometry.FromColdProcess(new(0, address, bytes.ToImmutableArray()));
    }

    private static NativeBattleBio22Geometry Cold(bool secondary = false)
    {
        int count = secondary ? 42 : 91;
        byte[] bytes = Enumerable.Range(0, secondary ? 0xA38 : 0x1480).Select(index => unchecked((byte)(index * 13))).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes, 8 + count * 8);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        return NativeBattleBio22Geometry.FromColdProcess(new(0, secondary ? 0x1617E3Cu : 0x1615CE4u, bytes.ToImmutableArray()));
    }

    private static ImmutableArray<byte> Source(short value, short fourth, int count = 91)
    {
        byte[] bytes = new byte[8 + count * 8];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, bytes.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), count);
        for (int vertex = 0; vertex < count; vertex++)
        {
            int offset = 8 + vertex * 8;
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset), unchecked((short)(value + vertex)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 2), unchecked((short)-value));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 4), unchecked((short)(value / 2)));
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(offset + 6), fourth);
        }
        return bytes.ToImmutableArray();
    }
}
