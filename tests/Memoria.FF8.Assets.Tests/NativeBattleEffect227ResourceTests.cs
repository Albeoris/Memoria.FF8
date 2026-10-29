using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect227Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect227Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect227ResourceTests
{
    [Fact]
    public void Original_selected_inputs_and_bone_point_tables_have_exact_physical_extents()
    {
        using GameAssets game = Open();
        NativeBattleEffect227Resources resources = game.ReadNativeEffect227Resources();
        resources.Validate();
        Assert.Equal(167968, resources.MainTim.Data.Length);
        Assert.Equal(1504, resources.Camera.Data.Length);
        Assert.Equal(278, resources.Vibration.Data.Length);
        Assert.Equal(592, resources.ReadMesh(0x112DAA8).Bytes.Length);
        foreach ((uint address, int count) in new (uint, int)[] { (0x1131660, 96), (0x1131960, 96), (0x1131C60, 119) })
        {
            byte[] points = resources.ReadStatic(address, count * 8);
            for (int index = 0; index < count; index++)
            {
                Assert.InRange(BinaryPrimitives.ReadInt16LittleEndian(points.AsSpan(index * 8 + 6, 2)), (short)0, (short)68);
            }
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, count * 8 + 1));
        }
        Assert.Equal(8, resources.ReadStatic(AnchorPointAddress, 8).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(AnchorPointAddress, 9));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(AnchorAddress, 8));
        NativeBattleModuleResource sprite = resources.Sprite(0x112D2E0);
        Assert.Equal(18, BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan(8, 2)));
        for (int life = 18; life > 0; life--)
        {
            int index = 18 - ((life - 1) % 18);
            int offset = BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan(8 + index * 2, 2));
            uint prefix = BinaryPrimitives.ReadUInt32LittleEndian(sprite.Bytes.AsSpan(offset, 4));
            Assert.Equal(0u, prefix & 0x80000000);
            Assert.Equal(1u, prefix & 0x7FFFFFFF);
        }
    }

    [Fact]
    public void Original_all_frame_outputs_select_only_shape_zero_without_any_morph_scratch()
    {
        using GameAssets game = Open();
        NativeBattleEffect227Resources resources = game.ReadNativeEffect227Resources();
        int[] frames = [140, 18, 16];
        int[] nodes = [11, 2, 2];
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
                    Assert.Equal((short)0, node.Word40);
                    Assert.Equal((short)0, node.Word42);
                    total++;
                    return current;
                });
                Assert.Equal(nodes[index], step.EmittedNodes);
                workspace = step.Workspace;
            }
            workspace = NativeBattleSnapshotJson.Deserialize<NativeBattleGfAnimationWorkspace>(NativeBattleSnapshotJson.Serialize(workspace))!;
            Assert.Equal(0, workspace.Advance701970(source, 0, (_, _) => throw new InvalidDataException("A completed resource emitted another node.")).EmittedNodes);
        }
        Assert.Equal(1608, total);
    }

    [Fact]
    public void Resource_JSON_and_camera_tracks_preserve_imported_current_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect227Resources resources = game.ReadNativeEffect227Resources();
        NativeBattleEffect227Resources restored = NativeBattleSnapshotJson.Deserialize<NativeBattleEffect227Resources>(NativeBattleSnapshotJson.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.ReadStatic(0x1131C60, 952), restored.ReadStatic(0x1131C60, 952));
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
    public void Selected_catalog_hydrates_same_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect227Resources resources = game.ReadNativeEffect227Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([227])[226];
        Assert.Same(resources, binding.Effect227Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag226.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect227 resources.");
        return new(directory!);
    }
}
