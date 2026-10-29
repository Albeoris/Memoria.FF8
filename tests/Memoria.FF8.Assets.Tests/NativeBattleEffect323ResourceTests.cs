using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect323Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect323Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect323ResourceTests
{
    [Fact]
    public void Original_selected_inputs_and_raw_meshes_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect323Resources resources = game.ReadNativeEffect323Resources();
        resources.Validate();
        Assert.Equal(169504, resources.MainTim.Data.Length);
        Assert.Equal(740, resources.Camera.Data.Length);
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(0, 2)));
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(2, 2)));
        Assert.Equal((ushort)32, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(4, 2)));
        Assert.Equal((ushort)740, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(6, 2)));
        Assert.Equal(5512, resources.ReadMesh(0xFE13EC).Bytes.Length);
        Assert.Equal(312, resources.Sprite(0xFE12B4).Bytes.Length);
        Assert.Equal(624, resources.ReadMesh(0xFE2974).Bytes.Length);
        Assert.Equal(4, resources.ReadStatic(0xFE12B0, 4).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0xFE12B0, 5));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaGlobal, 4));
    }

    [Fact]
    public void Original_all_frame_outputs_bound_actual_morph_to_the_208_byte_COMMON_interval()
    {
        using GameAssets game = Open();
        NativeBattleEffect323Resources resources = game.ReadNativeEffect323Resources();
        int[] frames = [16];
        int[] nodes = [8];
        int maximumMorph = 0;
        bool billboardEuler = false;
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
                    billboardEuler |= (node.Header & 0x10000) != 0 && NativeBattleActMeshRendering.Visible(node);
                    total++;
                    return current;
                });
                Assert.Equal(nodes[index], step.EmittedNodes);
                workspace = step.Workspace;
            }
            workspace = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace))!;
            Assert.Equal(0, workspace.Advance701970(source, 0, (_, _) => throw new InvalidDataException("A completed source emitted another node.")).EmittedNodes);
        }
        Assert.Equal(128, total);
        Assert.Equal(208, maximumMorph);
        Assert.True(billboardEuler);
        Assert.Equal(FirstPacketOffset - MorphOffset, maximumMorph);
    }

    [Fact]
    public void Resource_JSON_and_offset32_camera_tracks_preserve_real_sprite_and_programme_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect323Resources resources = game.ReadNativeEffect323Resources();
        NativeBattleEffect323Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect323Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        int[] frames = [10];
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
        NativeBattleEffect323Resources resources = game.ReadNativeEffect323Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([323])[322];
        Assert.Same(resources, binding.Effect323Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag322.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect323 resources.");
        return new(directory!);
    }
}
