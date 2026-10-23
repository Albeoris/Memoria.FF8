using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect183Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect183ResourcesTests
{
    [Fact]
    public void Original_distinct_meshes_and_color_sound_fronts_are_complete_and_cached()
    {
        using GameAssets game = Open();
        NativeBattleEffect183Resources resources = game.ReadNativeEffect183Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect183Resources());
        Assert.Equal(0x420, resources.MeshA.Bytes.Length);
        Assert.Equal(0xAB0, resources.MeshB.Bytes.Length);
        Assert.Equal(31, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshA.Bytes.AsSpan().Slice(4)));
        Assert.Equal(81, BinaryPrimitives.ReadInt32LittleEndian(resources.MeshB.Bytes.AsSpan().Slice(4)));
        Assert.Equal(10, resources.Colors.Length);
        Assert.Equal(0x49490u, resources.Sound);
        Assert.Same(resources.MeshA, resources.Mesh(MeshAAddress));
        Assert.Same(resources.MeshB, resources.Mesh(MeshBAddress));
        Assert.Throws<NotSupportedException>(() => resources.Mesh(ColorsAddress));
    }

    [Fact]
    public void Resource_JSON_roundtrip_preserves_both_original_addressed_meshes()
    {
        using GameAssets game = Open();
        NativeBattleEffect183Resources resources = game.ReadNativeEffect183Resources();
        NativeBattleEffect183Resources restored = JsonSerializer.Deserialize<NativeBattleEffect183Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(resources), JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void Changed_extents_groups_face_indices_colors_and_sound_fail_fast()
    {
        using GameAssets game = Open();
        NativeBattleEffect183Resources resources = game.ReadNativeEffect183Resources();
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = resources.MeshA.Bytes.RemoveAt(0) } }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Colors = resources.Colors.RemoveAt(0) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Sound = 0 }).Validate());
        byte[] mesh = resources.MeshA.Bytes.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(mesh.AsSpan(8 + 31 * 8 + 8), 5);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = mesh.ToImmutableArray() } }).Validate());
        mesh = resources.MeshA.Bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(8 + 31 * 8 + 12 + 4), 62);
        Assert.Throws<InvalidDataException>(() => (resources with { MeshA = resources.MeshA with { Bytes = mesh.ToImmutableArray() } }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect183 resources.");
        return new(directory!);
    }
}
