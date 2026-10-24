using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect209Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect209ResourceTests
{
    [Fact]
    public void Original_literal_resources_and_lookup_extents_are_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect209Resources resources = game.ReadNativeEffect209Resources();
        resources.Validate();
        Assert.Equal(TimLength, resources.EmbeddedTim.Data.Length);
        Assert.Equal(CameraLength, resources.Camera.Data.Length);
        Assert.Equal(VibrationLength, resources.Vibration.Data.Length);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        Assert.Equal(18, resources.ScrollColors.Length);
        Assert.Equal(4, resources.StageDepthRows.Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ScrollColorsAddress, 73));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(GridAddress, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x1D99BE8, 2));
    }

    [Fact]
    public void All_ten_original_stage_variants_decode_current_prefix_geometry_and_TIM()
    {
        using GameAssets game = Open();
        NativeBattleEffect209Resources resources = game.ReadNativeEffect209Resources();
        Assert.Equal(10, resources.Files.Count);
        Assert.Equal(10, resources.StageVariants.Length);
        Assert.NotNull(resources.DecodeReloadStage);
        foreach (NativeBattleStageVariant137Entry entry in resources.StageVariants)
        {
            NativeBattleEffectFileAsset file = resources.ReadFile(entry.FileId);
            NativeBattleStageSource source = resources.DecodeReloadStage!(entry.FileId, file.Data.ToArray());
            Assert.Equal(4, source.Groups.Length);
            Assert.NotNull(source.Geometry);
            Assert.NotNull(source.PhysicalSource);
            Assert.Equal(entry.HeaderOffset, source.PhysicalSource!.DirectoryOffset);
            Assert.InRange(source.PhysicalSource.Prefix.Length - entry.HeaderOffset, 32, 0x8000);
        }
        NativeBattleEffect209Resources restored = JsonSerializer.Deserialize<NativeBattleEffect209Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Null(restored.DecodeReloadStage);
        Assert.Equal(resources.StageVariants.ToArray(), restored.StageVariants.ToArray());
        Assert.Equal(resources.ReadFile(resources.StageVariants[0].FileId).Data.ToArray(), restored.ReadFile(restored.StageVariants[0].FileId).Data.ToArray());
    }

    [Fact]
    public void Original_camera_all_groups_and_tracks_retire()
    {
        using GameAssets game = Open();
        NativeBattleEffect209Resources resources = game.ReadNativeEffect209Resources();
        NativeBattleCameraBank bank = new(resources.Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
        for (int group = 0; group < 2; group++)
        {
            for (int track = 0; track < 8; track++)
            {
                NativeBattleCameraState camera = NativeBattleCameraState.FromColdProcess().Start(bank, (byte)(group * 16 + track));
                for (int callback = 0; callback < 4000 && !camera.Order.IsEmpty; callback++)
                {
                    camera = camera.Tick(0, new() { TransformReference = (_, point) => point });
                }
                Assert.Empty(camera.Order);
            }
        }
    }

    [Fact]
    public void Selected_catalog_hydrates_same_original_resource_instance_with_RET_initializer()
    {
        using GameAssets game = Open();
        NativeBattleEffect209Resources resources = game.ReadNativeEffect209Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([209])[208];
        Assert.Same(resources, binding.Effect209Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect209 resources.");
        return new(directory!);
    }
}
