using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect207Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect207ResourceTests
{
    [Fact]
    public void Original_selected_geometry_and_shaping_tables_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect207Resources resources = game.ReadNativeEffect207Resources();
        resources.Validate();
        Assert.Equal(MutableMeshLength, resources.OriginalCopiedMeshData.Length);
        Assert.Equal(ProgrammeLength, resources.Programme.Data.Length);
        Assert.Equal(8, resources.Geometry.Count);
        Assert.Equal(8, resources.Selectors.Length);
        Assert.Equal(8192, resources.ReadStatic(ShapingTableAddress, 8192).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ShapingTableAddress, 8193));
        _ = resources.PaletteWord(31, 127);
        Assert.Throws<InvalidDataException>(() => resources.PaletteWord(32, 0));
        Assert.Throws<InvalidDataException>(() => resources.PaletteWord(0, -1));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MutableMeshAddress, 4));
        NativeBattleGfAnimationWorkspace workspace = new(new byte[ProgrammeWorkspaceLength].ToImmutableArray());
        Assert.Equal(ProgrammeWorkspaceLength, NativeBattleStorageSnapshots.Capture(workspace.Initialize7016B0(resources.Programme, ProgrammeWorkspaceLength)).Data.Length);
    }

    [Fact]
    public void Original_camera_tracks_retire_and_JSON_preserves_imported_tables()
    {
        using GameAssets game = Open();
        NativeBattleEffect207Resources original = game.ReadNativeEffect207Resources();
        NativeBattleEffect207Resources resources = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect207Resources>(NativeBattleSnapshotJson.Serialize(original))!;
        resources.Validate();
        Assert.Equal(original.ReadStatic(ShapingTableAddress, ShapingTableLength), resources.ReadStatic(ShapingTableAddress, ShapingTableLength));
        NativeBattleCameraBank bank = new(resources.Camera.Data.AsSpan().Slice(CameraModelOffset).ToArray().ToImmutableArray());
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
        NativeBattleEffect207Resources resources = game.ReadNativeEffect207Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([207])[206];
        Assert.Same(resources, binding.Effect207Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag206.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect207 resources.");
        return new(directory!);
    }
}
