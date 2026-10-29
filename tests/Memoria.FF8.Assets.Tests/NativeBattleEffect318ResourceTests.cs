using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect318Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect318Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect318ResourceTests
{
    [Fact]
    public void Original_selected_inputs_and_completion_file_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect318Resources resources = game.ReadNativeEffect318Resources();
        resources.Validate();
        Assert.Equal(202784, resources.MainTim.Data.Length);
        Assert.Equal(596, resources.Camera.Data.Length);
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(0, 2)));
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(2, 2)));
        Assert.Equal((ushort)52, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(4, 2)));
        Assert.Equal((ushort)596, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(6, 2)));
        Assert.Equal(4360, resources.ReadMesh(0xFF7CB0).Bytes.Length);
        Assert.Equal(364, resources.Sprite(0xFF7A00).Bytes.Length);
        NativeBattleEffectFileAsset file = resources.ReadFile(360);
        Assert.Equal("MA8DEF_0.TIM", file.Name);
        Assert.Equal(33824, file.Data.Length);
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(file.Data.AsSpan(4, 4)));
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(361));
        Assert.Equal(4, resources.ReadStatic(0xFF77A8, 4).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xFF77A8, 5));
        foreach (uint address in new uint[] { 0xFFE688, 0xFFE690, 0xFFE698 })
        {
            Assert.Equal(8, resources.ReadStatic(address, 8).Length);
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, 9));
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaGlobal, 4));
    }

    [Fact]
    public void Original_all_frame_outputs_bound_actual_morph_to_the_1032_byte_COMMON_interval()
    {
        using GameAssets game = Open();
        NativeBattleEffect318Resources resources = game.ReadNativeEffect318Resources();
        int[] frames = [22, 41, 32];
        int[] nodes = [2, 13, 9];
        int maximumMorph = 0;
        int scheduledOutputs = 0;
        int scheduledMaximumMorph = 0;
        int[] scheduledFrames = [22, 16, 32];
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
                        if (frame < scheduledFrames[index])
                        {
                            scheduledMaximumMorph = Math.Max(scheduledMaximumMorph, vertices * 8);
                        }
                    }
                    total++;
                    if (frame < scheduledFrames[index])
                    {
                        scheduledOutputs++;
                    }
                    return current;
                });
                Assert.Equal(nodes[index], step.EmittedNodes);
                workspace = step.Workspace;
            }
            workspace = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace))!;
            Assert.Equal(0, workspace.Advance701970(source, 0, (_, _) => throw new InvalidDataException("A completed source emitted another node.")).EmittedNodes);
        }
        Assert.Equal(865, total);
        Assert.Equal(1032, maximumMorph);
        Assert.Equal(540, scheduledOutputs);
        Assert.Equal(1032, scheduledMaximumMorph);
        Assert.Equal(FirstPacketOffset - MorphOffset, maximumMorph);
    }

    [Fact]
    public void Resource_JSON_and_offset52_camera_tracks_preserve_real_file_and_programme_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect318Resources resources = game.ReadNativeEffect318Resources();
        NativeBattleEffect318Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect318Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.ReadFile(360).Data.ToArray(), restored.ReadFile(360).Data.ToArray());
        int[] frames = [12];
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
    public void Custom_projected_quad_directory_uses_actual_remaining_life_and_separate_resource_API()
    {
        using GameAssets game = Open();
        NativeBattleEffect318Resources resources = game.ReadNativeEffect318Resources();
        NativeBattleModuleResource source = resources.ReadProjectedQuads(ProjectedQuadsAddress);
        Assert.Equal(ProjectedQuadsLength, source.Bytes.Length);
        int count = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan(8, 2));
        Assert.Equal(12, count);
        List<int> selected = [];
        for (int life = 11; life > 0; life--)
        {
            int frame = count - life;
            selected.Add(frame);
            int offset = BinaryPrimitives.ReadUInt16LittleEndian(source.Bytes.AsSpan(8 + frame * 2, 2));
            Assert.Equal(1u, BinaryPrimitives.ReadUInt32LittleEndian(source.Bytes.AsSpan(offset, 4)));
            Assert.Equal((byte)0x20, source.Bytes[offset + 4]);
            Assert.True(offset + 4 + 20 <= source.Bytes.Length);
        }
        Assert.Equal(Enumerable.Range(1, 11), selected);
        Assert.Equal(source.Bytes.ToArray(), resources.ReadStatic(ProjectedQuadsAddress, ProjectedQuadsLength));
        Assert.Throws<NotSupportedException>(() => resources.Sprite(ProjectedQuadsAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadMesh(ProjectedQuadsAddress));
        Assert.Throws<NotSupportedException>(() => resources.ReadProjectedQuads(SpriteAddresses[0]));
    }

    [Fact]
    public void Selected_catalog_hydrates_same_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect318Resources resources = game.ReadNativeEffect318Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([318])[317];
        Assert.Same(resources, binding.Effect318Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag317.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect318 resources.");
        return new(directory!);
    }
}
