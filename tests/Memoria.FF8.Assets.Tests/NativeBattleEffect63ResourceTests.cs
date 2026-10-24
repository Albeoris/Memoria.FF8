using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect63Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect63Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect63ResourceTests
{
    [Fact]
    public void Original_selected_inputs_and_raw_meshes_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect63Resources resources = game.ReadNativeEffect63Resources();
        resources.Validate();
        Assert.Equal(168480, resources.MainTim.Data.Length);
        Assert.Equal(536, resources.Camera.Data.Length);
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(0, 2)));
        Assert.Equal((ushort)8, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(2, 2)));
        Assert.Equal((ushort)52, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(4, 2)));
        Assert.Equal((ushort)536, BinaryPrimitives.ReadUInt16LittleEndian(resources.Camera.Data.AsSpan(6, 2)));
        Assert.Equal(7768, resources.ReadMesh(BeamAddress).Bytes.Length);
        Assert.Equal(324, resources.Sprite(SpriteAddress).Bytes.Length);
        Assert.Equal(4, resources.ReadStatic(SoundAddress, 4).Length);
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(FirstPointAddress + 6, 2)));
        Assert.Equal((short)1, BinaryPrimitives.ReadInt16LittleEndian(resources.ReadStatic(SecondPointAddress + 6, 2)));
        Assert.Equal(8, resources.ReadStatic(FirstPointAddress, 8).Length);
        Assert.Equal(8, resources.ReadStatic(SecondPointAddress, 8).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(FirstPointAddress, 9));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SoundAddress, 5));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(WorkGlobal, 4));
    }

    [Fact]
    public void Original_all_frame_outputs_bound_actual_morph_to_the_finite_1512_byte_COMMON_output()
    {
        using GameAssets game = Open();
        NativeBattleEffect63Resources resources = game.ReadNativeEffect63Resources();
        int[] frames = [48, 45, 28];
        int[] nodes = [4, 18, 17];
        int maximumMorph = 0;
        bool alternateEuler = false;
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
                    alternateEuler |= (node.Header & 0x40000) != 0 && NativeBattleActMeshRendering.Visible(node);
                    uint selectors = index == 2 ? SecondSelectorsAddress : FirstSelectorsAddress;
                    Assert.Equal(index == 2 && node.GeometryIndex == 0 ? (byte)0x31 : (byte)0x30, resources.ReadStatic(selectors + node.GeometryIndex, 1)[0]);
                    total++;
                    return current;
                });
                Assert.Equal(nodes[index], step.EmittedNodes);
                workspace = step.Workspace;
            }
            workspace = JsonSerializer.Deserialize<NativeBattleGfAnimationWorkspace>(JsonSerializer.Serialize(workspace))!;
            Assert.Equal(0, workspace.Advance701970(source, 0, (_, _) => throw new InvalidDataException("A completed source emitted another node.")).EmittedNodes);
        }
        Assert.Equal(1478, total);
        Assert.Equal(1512, maximumMorph);
        Assert.True(alternateEuler);
        Assert.Equal(MaximumMorphBytes, maximumMorph);
        Assert.Equal(1670, total + nodes[0] * frames[0]);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(FirstSelectorsAddress, 3));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(SecondSelectorsAddress, 7));
    }

    [Fact]
    public void Resource_JSON_and_offset52_camera_tracks_preserve_real_sprite_and_programme_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect63Resources resources = game.ReadNativeEffect63Resources();
        NativeBattleEffect63Resources restored = JsonSerializer.Deserialize<NativeBattleEffect63Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
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
            int? offset = bank.TrackOffset(selector);
            Assert.NotNull(offset);
            Assert.InRange(offset.Value, 0, restored.Camera.Data.Length - 53);
        }
    }

    [Fact]
    public void Selected_catalog_hydrates_same_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect63Resources resources = game.ReadNativeEffect63Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([63])[62];
        Assert.Same(resources, binding.Effect63Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag062.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect63 resources.");
        return new(directory!);
    }
}
