using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect287Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect287Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect287ResourceTests
{
    [Fact]
    public void Original_selected_inputs_and_raw_meshes_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect287Resources resources = game.ReadNativeEffect287Resources();
        resources.Validate();
        Assert.Equal(168480, resources.MainTim.Data.Length);
        Assert.Equal(16928, resources.SecondaryTim.Data.Length);
        Assert.Equal(668, resources.Camera.Data.Length);
        Assert.Equal(504, resources.ReadMesh(RawMeshAddress).Bytes.Length);
        Assert.Equal(416, resources.Sprite(FirstSpriteAddress).Bytes.Length);
        Assert.Equal(324, resources.Sprite(SecondSpriteAddress).Bytes.Length);
        Assert.Equal(312, resources.ReadStatic(TriangleDescriptorsAddress, 312).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(TriangleDescriptorsAddress, 313));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(MeshQueueAddress, 4));
        Assert.Equal(33824, resources.ReadFile(360).Data.Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(361));
        Assert.NotEqual((ushort)0, resources.ReadNormalizationWord(64));
        Assert.NotEqual((ushort)0, resources.ReadNormalizationWord(255));
        Assert.Throws<NotSupportedException>(() => resources.ReadNormalizationWord(63));
        Assert.Throws<NotSupportedException>(() => resources.ReadNormalizationWord(256));
    }

    [Fact]
    public void Original_all_frame_outputs_bound_actual_morph_to_the_1408_byte_COMMON_interval()
    {
        using GameAssets game = Open();
        NativeBattleEffect287Resources resources = game.ReadNativeEffect287Resources();
        int[] frames = [75, 75, 18];
        int[] nodes = [27, 2, 5];
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
        Assert.Equal(2265, total);
        Assert.Equal(1408, maximumMorph);
        Assert.Equal(TaperOffset - MorphOffset, maximumMorph);
    }

    [Fact]
    public void Resource_JSON_and_offset32_camera_tracks_preserve_real_sprite_and_programme_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect287Resources resources = game.ReadNativeEffect287Resources();
        NativeBattleEffect287Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect287Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        int[] frames = [14, 12];
        for (int sprite = 0; sprite < SpriteAddresses.Length; sprite++)
        {
            NativeBattleModuleResource module = restored.Sprite(SpriteAddresses[sprite]);
            Assert.Equal(frames[sprite], (int)BinaryPrimitives.ReadUInt16LittleEndian(module.Bytes.AsSpan(8, 2)));
            for (int frame = 0; frame < frames[sprite]; frame++)
            {
                _ = module.ProjectedFrame571C80(frame);
            }
            Assert.True(BinaryPrimitives.ReadInt16LittleEndian(module.Bytes.AsSpan(10 + frames[sprite] * 2, 2)) < 0);
        }
        NativeBattleCameraBank bank = new(restored.Camera.Data.AsSpan().Slice(32).ToArray().ToImmutableArray());
        for (byte selector = 0; selector < 8; selector++)
        {
            int? offset = bank.TrackOffset(selector);
            Assert.NotNull(offset);
            Assert.InRange(offset.Value, 0, restored.Camera.Data.Length - 33);
        }
    }

    [Fact]
    public void Selected_catalog_hydrates_same_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect287Resources resources = game.ReadNativeEffect287Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([287])[286];
        Assert.Same(resources, binding.Effect287Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect287 resources.");
        return new(directory!);
    }
}
