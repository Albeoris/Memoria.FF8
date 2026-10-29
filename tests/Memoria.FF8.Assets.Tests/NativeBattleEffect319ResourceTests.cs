using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect319Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect319Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect319ResourceTests
{
    [Fact]
    public void Original_selected_inputs_and_completion_file_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect319Resources resources = game.ReadNativeEffect319Resources();
        resources.Validate();
        Assert.Equal(202784, resources.MainTim.Data.Length);
        Assert.Equal(428, resources.Camera.Data.Length);
        Assert.Equal(4360, resources.ReadMesh(0xFF13F0).Bytes.Length);
        Assert.Equal(272, resources.Sprite(0xFF12E0).Bytes.Length);
        NativeBattleEffectFileAsset file = resources.ReadFile(360);
        Assert.Equal("MA8DEF_0.TIM", file.Name);
        Assert.Equal(33824, file.Data.Length);
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(file.Data.AsSpan(4, 4)));
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(361));
        foreach (uint address in new uint[] { 0xFF7780, 0xFF7788, 0xFF7790 })
        {
            Assert.Equal(8, resources.ReadStatic(address, 8).Length);
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, 9));
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaGlobal, 4));
    }

    [Fact]
    public void Original_all_frame_outputs_bound_actual_morph_to_the_560_byte_COMMON_interval()
    {
        using GameAssets game = Open();
        NativeBattleEffect319Resources resources = game.ReadNativeEffect319Resources();
        int[] frames = [20, 15];
        int[] nodes = [7, 13];
        int maximumMorph = 0;
        int total = 0;
        for (int index = 0; index < ActAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = resources.Act(ActAddresses[index]);
            Assert.Equal(ActLengths[index], source.Data.Length);
            Assert.Equal(ActWorkspaceLengths[index], resources.WorkspaceLength(source.Address));
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[ActWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ActWorkspaceLengths[index]);
            for (int frame = 0; frame < frames[index]; frame++)
            {
                NativeBattleGfAnimationAdvance step = workspace.Advance701970(source, 0, (current, node) =>
                {
                    uint address = checked(source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan(8 + node.GeometryIndex * 4, 4)));
                    NativeBattleStaticMeshResource geometry = resources.ReadGeometry(address);
                    int shapes = BinaryPrimitives.ReadInt32LittleEndian(geometry.Bytes.AsSpan(8, 4));
                    Assert.InRange((int)node.Word40, 0, shapes - 1);
                    Assert.InRange((int)node.Word42, 0, shapes - 1);
                    if (node.Word40 != node.Word42 && node.Scalar38 is not (0 or 4096))
                    {
                        int vertices = BinaryPrimitives.ReadInt32LittleEndian(geometry.Bytes.AsSpan(4, 4));
                        maximumMorph = Math.Max(maximumMorph, vertices * 8);
                    }
                    total++;
                    return current;
                });
                Assert.Equal(nodes[index], step.EmittedNodes);
                workspace = step.Workspace;
            }
            workspace = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace))!;
            Assert.Equal(0, workspace.Advance701970(source, 0, (_, _) => throw new InvalidDataException("A completed source emitted another node.")).EmittedNodes);
        }
        Assert.Equal(335, total);
        Assert.Equal(560, maximumMorph);
    }

    [Fact]
    public void Resource_JSON_and_offset40_camera_tracks_preserve_real_file_and_programme_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect319Resources resources = game.ReadNativeEffect319Resources();
        NativeBattleEffect319Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect319Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.ReadFile(360).Data.ToArray(), restored.ReadFile(360).Data.ToArray());
        NativeBattleCameraBank bank = new(restored.Camera.Data.AsSpan().Slice(40).ToArray().ToImmutableArray());
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
    public void Selected_catalog_hydrates_same_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect319Resources resources = game.ReadNativeEffect319Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([319])[318];
        Assert.Same(resources, binding.Effect319Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag318.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect319 resources.");
        return new(directory!);
    }
}
