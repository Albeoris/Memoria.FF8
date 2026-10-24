using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect174Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect174ResourceTests
{
    [Fact]
    public void Original_selected_fronts_and_color_tables_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect174Resources resources = game.ReadNativeEffect174Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(5, resources.HistoryColors.Length);
        Assert.Equal(12, resources.MaterialCycle12.Length);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        Assert.Equal(10424, resources.ReadMesh(0xD5ADCC).Bytes.Length);
        Assert.Equal(20, resources.ReadStatic(HistoryColorsAddress, 20).Length);
        Assert.Equal(48, resources.ReadStatic(MaterialCycleAddress, 48).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(HistoryColorsAddress, 21));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MaterialCycleAddress, 49));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(HistoryAddress, 4));
    }

    [Fact]
    public void Original_camera_tracks_retire_and_resource_JSON_preserves_actual_imported_tables()
    {
        using GameAssets game = Open();
        NativeBattleEffect174Resources resources = game.ReadNativeEffect174Resources();
        NativeBattleEffect174Resources restored = JsonSerializer.Deserialize<NativeBattleEffect174Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.HistoryColors.ToArray(), restored.HistoryColors.ToArray());
        Assert.Equal(resources.MaterialCycle12.ToArray(), restored.MaterialCycle12.ToArray());
        NativeBattleCameraBank bank = new(restored.Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
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
    public void Selected_catalog_hydrates_same_original_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect174Resources resources = game.ReadNativeEffect174Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([174])[173];
        Assert.Same(resources, binding.Effect174Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag173.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect174 resources.");
        return new(directory!);
    }
}
