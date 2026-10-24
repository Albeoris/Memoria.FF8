using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect167Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect167ResourceTests
{
    [Fact]
    public void Original_fronts_preserve_physical_extents_and_addressed_plane_mapping()
    {
        using GameAssets game = Open();
        NativeBattleEffect167Resources resources = game.ReadNativeEffect167Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(SpriteLength, resources.Sprite(SpriteAddress).Bytes.Length);
        Assert.Equal(56, resources.Mapping.Length);
        Assert.All(resources.Mapping, value => Assert.InRange(value, 0u, 5u));
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        Assert.Equal(224, resources.ReadStatic(MappingAddress, 224).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MappingAddress, 225));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(EndpointsAddress, 16));
    }

    [Fact]
    public void Original_camera_all_tracks_retire_and_resource_JSON_retains_mapping()
    {
        using GameAssets game = Open();
        NativeBattleEffect167Resources resources = game.ReadNativeEffect167Resources();
        NativeBattleEffect167Resources restored = JsonSerializer.Deserialize<NativeBattleEffect167Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.Mapping.ToArray(), restored.Mapping.ToArray());
        NativeBattleCameraBank bank = new(restored.Camera.Data.AsSpan().Slice(24).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, selector);
            for (int callback = 0; callback < 2000 && !camera.Order.IsEmpty; callback++)
            {
                camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
            }
            Assert.Empty(camera.Order);
        }
    }

    [Fact]
    public void Selected_catalog_hydrates_same_resource_instance_and_initializer()
    {
        using GameAssets game = Open();
        NativeBattleEffect167Resources resources = game.ReadNativeEffect167Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([167])[166];
        Assert.Same(resources, binding.Effect167Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag166.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect167 resources.");
        return new(directory!);
    }
}
