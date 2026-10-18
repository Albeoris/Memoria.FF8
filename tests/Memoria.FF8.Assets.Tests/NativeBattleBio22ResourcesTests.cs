using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleBio22ResourcesTests
{
    [Fact]
    public void Original_profile_keeps_all_seven_sources_and_the_complete_pointer_directory()
    {
        using GameAssets game = Installed();
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        Assert.Same(resources, game.ReadNativeBio22Resources());
        resources.Validate();
        Assert.Equal("FF8_EN:1626014", resources.Animation.Identity);
        Assert.Equal(0x918, resources.Animation.Data.Length);
        Assert.Equal((ushort)0x918, BinaryPrimitives.ReadUInt16LittleEndian(resources.Animation.Data.AsSpan().Slice(6)));
        Assert.Equal("FF8_EN:16189CC", resources.MorphPointers.Identity);
        Assert.Equal(0x1C, resources.MorphPointers.Data.Length);
        Assert.Equal(7, resources.MorphSources.Length);
        uint[] addresses = [0x1615A04, 0x1617164, 0x1617444, 0x1617724, 0x1617A04, 0x1617CE4, 0x1618874];
        for (int index = 0; index < addresses.Length; index++)
        {
            NativeBattleEffectHeapAllocation source = resources.MorphSources[index];
            int count = index < 5 ? 91 : 42;
            int length = 8 + count * 8;
            Assert.Equal($"FF8_EN:{addresses[index]:X}", source.Identity);
            Assert.Equal(length, source.Data.Length);
            Assert.Equal((uint)length, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()));
            Assert.Equal(count, BinaryPrimitives.ReadInt32LittleEndian(source.Data.AsSpan().Slice(4)));
            Assert.Equal(addresses[index], BinaryPrimitives.ReadUInt32LittleEndian(resources.MorphPointers.Data.AsSpan().Slice(4 * index)));
            NativeBattleEnhancementMorph.Validate(source.Data);
        }
        Assert.Same(resources.MorphSources[0], resources.MorphFirst);
        Assert.Same(resources.MorphSources[1], resources.MorphSecond);
        Assert.Equal(resources.MorphPointers.Data.Take(8), resources.FirstMorphPointers.Data);
        Assert.Equal("FF8_EN:16146E4", resources.SoundDescriptor.Identity);
        Assert.Equal(BinaryPrimitives.ReadUInt32LittleEndian(resources.SoundDescriptor.Data.AsSpan()), resources.Sound);
    }

    [Fact]
    public void Overlay_and_timed_count_tables_keep_their_distinct_original_extents_and_JSON_bytes()
    {
        using GameAssets game = Installed();
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        NativeBattleBio22Resources restored = JsonSerializer.Deserialize<NativeBattleBio22Resources>(JsonSerializer.Serialize(resources))!;
        Assert.Equal("FF8_EN:162693C", resources.OverlayProgram.Identity);
        Assert.Equal(16, resources.OverlayProgram.Data.Length);
        Assert.Equal(0xFF, resources.OverlayProgram.Data[15]);
        Assert.Equal("FF8_EN:162694C", resources.PlanarCounts.Identity);
        Assert.Equal("FF8_EN:162696C", resources.BillboardCounts.Identity);
        Assert.Equal(21, resources.PlanarCounts.Data.Length);
        Assert.Equal(21, resources.BillboardCounts.Data.Length);
        Assert.Contains(resources.PlanarCounts.Data, value => value > 0);
        Assert.Contains(resources.BillboardCounts.Data, value => value > 0);
        Assert.Equal(resources.OverlayProgram.Data.ToArray(), restored.OverlayProgram.Data.ToArray());
        Assert.Equal(resources.PlanarCounts.Data.ToArray(), restored.PlanarCounts.Data.ToArray());
        Assert.Equal(resources.BillboardCounts.Data.ToArray(), restored.BillboardCounts.Data.ToArray());
        Assert.Throws<InvalidDataException>(() => (resources with { PlanarCounts = resources.BillboardCounts }).Validate());
    }

    [Fact]
    public void Four_meshes_use_existing_eight_group_schema_and_generic_TIM_initializer()
    {
        using GameAssets game = Installed();
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        (NativeBattleStaticMeshResource Mesh, uint Address, int Length, int Vertices, int Faces)[] expected = [(resources.MeshBaseline, 0x1615CE4, 0x1480, 91, 160), (resources.SecondMeshBaseline, 0x1617E3C, 0xA38, 42, 80), (resources.ThirdMeshBaseline, 0x1619888, 0x5028, 467, 598), (resources.FourthMeshBaseline, 0x161F750, 0x4038, 258, 512)];
        foreach ((NativeBattleStaticMeshResource mesh, uint address, int length, int vertices, int faces) in expected)
        {
            Assert.Equal(address, mesh.Address);
            Assert.Equal(length, mesh.Bytes.Length);
            Assert.Equal((uint)(8 + vertices * 8), BinaryPrimitives.ReadUInt32LittleEndian(mesh.Bytes.AsSpan()));
            Assert.Equal(vertices, BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4)));
            int cursor = 8 + vertices * 8;
            int[] strides = [12, 12, 20, 24, 20, 24, 28, 36];
            List<int> counts = [];
            for (int group = 0; group < 8; group++)
            {
                int count = BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(cursor));
                counts.Add(count);
                cursor += 4 + count * strides[group];
            }
            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, faces, 0 }, counts);
            Assert.Equal(mesh.Bytes.Length, cursor);
        }
        Assert.Equal(0x1618874u, resources.SecondMeshBaseline.Address + (uint)resources.SecondMeshBaseline.Bytes.Length);
        Assert.Equal(0x161E8B0u, resources.ThirdMeshBaseline.Address + (uint)resources.ThirdMeshBaseline.Bytes.Length);
        Assert.Equal(0x1623788u, resources.FourthMeshBaseline.Address + (uint)resources.FourthMeshBaseline.Bytes.Length);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([22])[21];
        Assert.Equal(0x89A6B0u, binding.Entry);
        Assert.Equal(0x89A690u, binding.Initializer);
        NativeBattleEffectInitializerResource initializer = Assert.IsType<NativeBattleEffectInitializerResource>(binding.InitializerResource);
        Assert.Equal("mag021.tim", initializer.Name);
        Assert.Equal(0x2704568u, initializer.GlobalAddress);
    }

    [Fact]
    public void Second_geometry_family_preserves_initial_sources_and_the_authored_nonaddress_directory_order()
    {
        using GameAssets game = Installed();
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        Assert.Equal(2, resources.SecondFamilyInitialSources.Length);
        Assert.Equal(new[] { "FF8_EN:16189E8", "FF8_EN:161E8B0" }, resources.SecondFamilyInitialSources.Select(source => source.Identity));
        foreach (NativeBattleEffectHeapAllocation source in resources.SecondFamilyInitialSources)
        {
            Assert.Equal(0xEA0, source.Data.Length);
            Assert.Equal(0xEA0u, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()));
            Assert.Equal(467u, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(4)));
        }
        uint[] addresses = [0x1623FA0, 0x16247B8, 0x1624FD0, 0x16257E8, 0x1623788];
        Assert.True(addresses[4] < addresses[0]);
        Assert.Equal("FF8_EN:1626000", resources.SecondFamilyMorphPointers.Identity);
        Assert.Equal(0x14, resources.SecondFamilyMorphPointers.Data.Length);
        Assert.Equal(5, resources.SecondFamilyMorphSources.Length);
        for (int index = 0; index < addresses.Length; index++)
        {
            NativeBattleEffectHeapAllocation source = resources.SecondFamilyMorphSources[index];
            Assert.Equal($"FF8_EN:{addresses[index]:X}", source.Identity);
            Assert.Equal(addresses[index], BinaryPrimitives.ReadUInt32LittleEndian(resources.SecondFamilyMorphPointers.Data.AsSpan().Slice(4 * index)));
            Assert.Equal(0x818, source.Data.Length);
            Assert.Equal(0x818u, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan()));
            Assert.Equal(258u, BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(4)));
        }
    }

    [Fact]
    public void Five_original_kind_zero_sprites_keep_all_seventy_one_frames_and_complete_extents()
    {
        using GameAssets game = Installed();
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        uint[] addresses = [0x1614F74, 0x1615120, 0x16152CC, 0x1615440, 0x16156F8];
        int[] lengths = [0x1AC, 0x1AC, 0x174, 0x174, 0x160];
        int[] counts = [16, 16, 13, 13, 13];
        Assert.Equal(addresses, resources.Sprites.Select(sprite => sprite.Address));
        int frames = 0;
        for (int index = 0; index < resources.Sprites.Length; index++)
        {
            NativeBattleModuleResource sprite = resources.Sprites[index];
            Assert.Same(sprite, resources.Sprite(addresses[index]));
            Assert.Equal((byte)0, sprite.Kind);
            Assert.Equal(lengths[index], sprite.Bytes.Length);
            Assert.Equal(counts[index], BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8)));
            int end = 10 + 2 * (counts[index] + 1);
            for (int frame = 0; frame < counts[index]; frame++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(10 + 2 * frame));
                uint quads = BinaryPrimitives.ReadUInt32LittleEndian(sprite.Bytes.AsSpan().Slice(offset)) & 0x7FFFFFFF;
                end = Math.Max(end, checked(offset + 4 + (int)quads * 20));
                NativeBattleModuleSpriteFrame selected = sprite.SpriteFrame(frame);
                Assert.Equal(checked((int)quads * 20), selected.Quads.Length);
                Assert.Equal(sprite.Bytes.AsSpan().Slice(offset + 4, selected.Quads.Length).ToArray(), selected.Quads.ToArray());
                Assert.Equal(BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan().Slice(12 + 2 * frame)), selected.NextOffset);
                frames++;
            }
            Assert.Equal(sprite.Bytes.Length, end);
            Assert.Throws<InvalidDataException>(() => sprite.SpriteFrame(-1));
            Assert.Throws<InvalidDataException>(() => sprite.SpriteFrame(counts[index]));
        }
        Assert.Equal(71, frames);
        Assert.Throws<NotSupportedException>(() => resources.Sprite(0));
    }

    [Fact]
    public void Immutable_profile_JSON_has_one_source_collection_and_no_duplicated_compatibility_views()
    {
        using GameAssets game = Installed();
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        string serialized = JsonSerializer.Serialize(resources);
        NativeBattleBio22Resources restored = JsonSerializer.Deserialize<NativeBattleBio22Resources>(serialized)!;
        restored.Validate();
        Assert.Equal(serialized, JsonSerializer.Serialize(restored));
        for (int index = 0; index < 7; index++)
        {
            Assert.Equal(resources.MorphSources[index].Data.ToArray(), restored.MorphSources[index].Data.ToArray());
        }
        for (int index = 0; index < 2; index++)
        {
            Assert.Equal(resources.SecondFamilyInitialSources[index].Data.ToArray(), restored.SecondFamilyInitialSources[index].Data.ToArray());
        }
        for (int index = 0; index < 5; index++)
        {
            Assert.Equal(resources.SecondFamilyMorphSources[index].Data.ToArray(), restored.SecondFamilyMorphSources[index].Data.ToArray());
        }
        Assert.Same(restored.MorphSources[0], restored.MorphFirst);
        Assert.Same(restored.MorphSources[1], restored.MorphSecond);
        JsonObject node = JsonNode.Parse(serialized)!.AsObject();
        Assert.Equal(new[] { "Animation", "BillboardCounts", "FourthMeshBaseline", "MeshBaseline", "MorphPointers", "MorphSources", "OverlayProgram", "PlanarCounts", "SecondFamilyInitialSources", "SecondFamilyMorphPointers", "SecondFamilyMorphSources", "SecondMeshBaseline", "SoundDescriptor", "Sprites", "ThirdMeshBaseline" }, node.Select(property => property.Key).Order().ToArray());
        node.Remove("MorphSources");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeBattleBio22Resources>(node.ToJsonString()));
    }

    [Fact]
    public void Profile_rejects_wrong_source_counts_last_pointer_group_and_second_mesh_identity()
    {
        using GameAssets game = Installed();
        NativeBattleBio22Resources resources = game.ReadNativeBio22Resources();
        byte[] source = resources.MorphSources[5].Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(4), 41);
        Assert.Throws<InvalidDataException>(() => (resources with { MorphSources = resources.MorphSources.SetItem(5, resources.MorphSources[5] with { Data = source.ToImmutableArray() }) }).Validate());
        byte[] pointers = resources.MorphPointers.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(pointers.AsSpan(24), 0x1617CE4);
        Assert.Throws<InvalidDataException>(() => (resources with { MorphPointers = resources.MorphPointers with { Data = pointers.ToImmutableArray() } }).Validate());
        byte[] mesh = resources.SecondMeshBaseline.Bytes.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(mesh.AsSpan(0x158), 1);
        Assert.Throws<InvalidDataException>(() => (resources with { SecondMeshBaseline = resources.SecondMeshBaseline with { Bytes = mesh.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MorphSources = resources.MorphSources.RemoveAt(6) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { MorphSources = resources.MorphSources.SetItem(6, resources.MorphSources[6] with { Data = resources.MorphSources[6].Data.RemoveAt(0) }) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SecondMeshBaseline = resources.SecondMeshBaseline with { Address = resources.MeshBaseline.Address } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { ThirdMeshBaseline = resources.ThirdMeshBaseline with { Address = resources.FourthMeshBaseline.Address } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { SecondFamilyInitialSources = resources.SecondFamilyInitialSources.RemoveAt(1) }).Validate());
        byte[] secondPointers = resources.SecondFamilyMorphPointers.Data.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(secondPointers.AsSpan(), 0x1623788);
        Assert.Throws<InvalidDataException>(() => (resources with { SecondFamilyMorphPointers = resources.SecondFamilyMorphPointers with { Data = secondPointers.ToImmutableArray() } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprites = resources.Sprites.RemoveAt(4) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sprites = resources.Sprites.SetItem(0, resources.Sprites[0] with { Address = resources.Sprites[1].Address }) }).Validate());
    }

    private static GameAssets Installed()
    {
        string directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources are optional.");
        return new(directory);
    }
}
