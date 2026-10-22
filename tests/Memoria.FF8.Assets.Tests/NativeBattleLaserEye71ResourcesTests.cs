using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleLaserEye71ResourcesTests
{
    [Fact]
    public void Original_addressed_sources_have_complete_geometry_sprite_camera_and_binding_bounds()
    {
        using GameAssets game = Open();
        NativeBattleLaserEye71Resources resources = game.ReadNativeLaserEye71Resources();
        Assert.Same(resources, game.ReadNativeLaserEye71Resources());
        NativeBattleLaserEye71ResourcesReader.ValidatePayloads(resources);
        Assert.Equal(0x60B0, resources.Executable.Data.Length);
        Assert.Equal(0x1789344u, resources.Executable.ResourceAddress);
        Assert.Equal(new[] { 1816, 168, 280 }, resources.Meshes.Select(mesh => mesh.Bytes.Length));
        Assert.Equal(196, resources.ParticleSprite.Bytes.Length);
        Assert.Equal(340, resources.Camera.Data.Length);
        Assert.Equal(0x1B74, resources.CasterDat.Data.Length);
        Assert.Equal(19, resources.EmissionRows.Length);
        Assert.Equal(0x14BB0, NativeBattleLaserEye71Profile.WorkingBytes);
    }

    [Fact]
    public void Typed_resources_repeat_JSON_and_resolve_only_the_actual_mesh_addresses()
    {
        using GameAssets game = Open();
        NativeBattleLaserEye71Resources resources = game.ReadNativeLaserEye71Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleLaserEye71Resources restored = JsonSerializer.Deserialize<NativeBattleLaserEye71Resources>(json)!;
        NativeBattleLaserEye71ResourcesReader.ValidatePayloads(restored);
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Equal(resources.Meshes[0].Bytes.ToArray(), restored.ReadMesh(0x158F688).Bytes.ToArray());
        Assert.Throws<NotSupportedException>(() => restored.ReadMesh(0x158FF60));
    }

    [Fact]
    public void Cold_source_seed_runs_once_and_never_recreates_unknown_or_warm_program_bytes()
    {
        using GameAssets game = Open();
        NativeBattleLaserEye71Resources resources = game.ReadNativeLaserEye71Resources();
        NativeBattleLaserEye71State seeded = NativeBattleLaserEye71State.FromColdProcess().SeedColdSource(resources);
        Assert.False(seeded.ColdSourcePending);
        Assert.Equal(resources.Executable.Data.ToArray(), seeded.ExecutableBytes!.Value.ToArray());
        byte[] bytes = seeded.ExecutableBytes!.Value.ToArray();
        bytes[0] = 1;
        bytes[100] ^= 0x80;
        NativeBattleLaserEye71State warm = seeded with { ExecutableBytes = bytes.ToImmutableArray() };
        Assert.Same(warm, warm.SeedColdSource(resources));
        Assert.Equal(bytes, warm.ExecutableBytes!.Value.ToArray());
        NativeBattleLaserEye71State unknown = NativeBattleLaserEye71State.FromUnknown();
        Assert.Same(unknown, unknown.SeedColdSource(resources));
        Assert.Throws<NotSupportedException>(() => unknown.ReadSourceFlag());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Malformed_channel_pointer_vertex_selection_and_emission_extent_fail(int kind)
    {
        using GameAssets game = Open();
        NativeBattleLaserEye71Resources resources = game.ReadNativeLaserEye71Resources();
        NativeBattleLaserEye71Resources changed;
        if (kind == 0)
        {
            byte[] source = resources.Executable.Data.ToArray();
            int node = BinaryPrimitives.ReadInt32LittleEndian(source.AsSpan(4, 4));
            BinaryPrimitives.WriteInt32LittleEndian(source.AsSpan(node + 0xC8, 4), source.Length - 1);
            changed = resources with { Executable = resources.Executable with { Data = source.ToImmutableArray() } };
        }
        else if (kind == 1)
        {
            byte[] mesh = resources.Meshes[0].Bytes.ToArray();
            int first = BinaryPrimitives.ReadInt32LittleEndian(mesh.AsSpan(0, 4));
            BinaryPrimitives.WriteUInt16LittleEndian(mesh.AsSpan(first + 8, 2), 132);
            changed = resources with { Meshes = resources.Meshes.SetItem(0, resources.Meshes[0] with { Bytes = mesh.ToImmutableArray() }) };
        }
        else
        {
            changed = resources with { EmissionRows = resources.EmissionRows.RemoveAt(18) };
        }
        Assert.Throws<InvalidDataException>(() => NativeBattleLaserEye71ResourcesReader.ValidatePayloads(changed));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select original resources to validate Laser Eye ingress.");
        return new(directory!);
    }
}
