using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect322Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect322Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect322ResourceTests
{
    [Fact]
    public void Original_selected_inputs_and_completion_file_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect322Resources resources = game.ReadNativeEffect322Resources();
        resources.Validate();
        Assert.Equal(202272, resources.MainTim.Data.Length);
        Assert.Equal(644, resources.Camera.Data.Length);
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(0, 2)));
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(2, 2)));
        Assert.Equal((ushort)52, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(4, 2)));
        Assert.Equal((ushort)644, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(6, 2)));
        Assert.Equal(5512, resources.ReadMesh(0xFE44C8).Bytes.Length);
        Assert.Equal(220, resources.Sprite(0xFE42A8).Bytes.Length);
        NativeBattleEffectFileAsset file = resources.ReadFile(360);
        Assert.Equal("MA8DEF_0.TIM", file.Name);
        Assert.Equal(33824, file.Data.Length);
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(file.Data.AsSpan(4, 4)));
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(361));
        Assert.Equal(624, resources.ReadMesh(0xFE5A50).Bytes.Length);
        Assert.Equal(324, resources.Sprite(0xFE4384).Bytes.Length);
        Assert.Equal(4, resources.ReadStatic(0xFE7000, 4).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xFE7000, 5));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaGlobal, 4));
    }

    [Fact]
    public void Original_all_frame_outputs_bound_actual_morph_to_the_200_byte_COMMON_interval()
    {
        using GameAssets game = Open();
        NativeBattleEffect322Resources resources = game.ReadNativeEffect322Resources();
        int[] frames = [16];
        int[] nodes = [6];
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
            workspace = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
            Assert.Equal(0, workspace.Advance701970(source, 0, (_, _) => throw new InvalidDataException("A completed source emitted another node.")).EmittedNodes);
        }
        Assert.Equal(96, total);
        Assert.Equal(200, maximumMorph);
    }

    [Fact]
    public void Resource_JSON_and_offset52_camera_tracks_preserve_real_file_and_programme_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect322Resources resources = game.ReadNativeEffect322Resources();
        NativeBattleEffect322Resources restored = JsonSerializer.Deserialize<NativeBattleEffect322Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.ReadFile(360).Data.ToArray(), restored.ReadFile(360).Data.ToArray());
        int[] frames = [8, 12];
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
        NativeBattleCameraBank bank = new(restored.Camera.Data.AsSpan().Slice(52).ToArray().ToImmutableArray());
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
        NativeBattleEffect322Resources resources = game.ReadNativeEffect322Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([322])[321];
        Assert.Same(resources, binding.Effect322Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag321.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect322 resources.");
        return new(directory!);
    }
}
