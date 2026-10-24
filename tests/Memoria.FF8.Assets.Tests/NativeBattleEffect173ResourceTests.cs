using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect173Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect173ResourceTests
{
    [Fact]
    public void Original_selected_resources_and_addressed_palette_are_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect173Resources resources = game.ReadNativeEffect173Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(16, resources.Palette.Length);
        Assert.Equal(5, resources.MeshSources.Length);
        for (int index = 0; index < MeshAddresses.Length; index++)
        {
            Assert.Equal(MeshLengths[index], resources.ReadMesh(MeshAddresses[index]).Bytes.Length);
        }
        Assert.Equal(64, resources.ReadStatic(PaletteAddress, 64).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(PaletteAddress, 65));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ProgrammeFlagsAddress, 5));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(FaceRecordAddresses[0], 32));
        Assert.True(resources.Sprite(SpriteAddresses[0]).ProjectedFrame571C80(15).NextOffset < 0);
    }

    [Fact]
    public void Original_programme_emits_six_current_outputs_and_retains_full_workspace_after_JSON()
    {
        using GameAssets game = Open();
        NativeBattleEffect173Resources resources = game.ReadNativeEffect173Resources();
        NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[64].ToImmutableArray()).Initialize7016B0(resources.Programme, 64);
        for (int frame = 0; frame < 6; frame++)
        {
            int emitted = 0;
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(resources.Programme, 0, (current, node) =>
            {
                emitted++;
                Assert.Equal(44, node.Data.Length);
                Assert.Equal((ushort)0, node.GeometryIndex);
                Assert.Equal((short)0, node.Word40);
                Assert.Equal((short)0, node.Word42);
                Assert.Equal((short)0, node.Scalar38);
                Assert.Equal(node.Vector24.X, node.Vector24.Y);
                Assert.Equal(node.Vector24.X, node.Vector24.Z);
                return current;
            });
            Assert.Equal(1, emitted);
            Assert.Equal(5 - frame, step.Remaining);
            workspace = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(step.Workspace))!;
        }
        Assert.Equal(0, workspace.Advance701970(resources.Programme, 0, (_, _) => throw new InvalidDataException("A retired source emitted another node.")).EmittedNodes);
    }

    [Fact]
    public void Original_camera_tracks_and_resource_JSON_preserve_actual_tables()
    {
        using GameAssets game = Open();
        NativeBattleEffect173Resources resources = game.ReadNativeEffect173Resources();
        NativeBattleEffect173Resources restored = JsonSerializer.Deserialize<NativeBattleEffect173Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.Palette.ToArray(), restored.Palette.ToArray());
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
    public void Selected_catalog_hydrates_same_original_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect173Resources resources = game.ReadNativeEffect173Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([173])[172];
        Assert.Same(resources, binding.Effect173Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag172.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect173 resources.");
        return new(directory!);
    }
}
