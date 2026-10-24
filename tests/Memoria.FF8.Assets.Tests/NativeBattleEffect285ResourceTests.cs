using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect285Profile;
using static Memoria.FF8.Runtime.NativeBattleEffect285Resources;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect285ResourceTests
{
    [Fact]
    public void Original_selected_inputs_and_completion_file_are_physically_bounded()
    {
        using GameAssets game = Open();
        NativeBattleEffect285Resources resources = game.ReadNativeEffect285Resources();
        resources.Validate();
        Assert.Equal(185888, resources.MainTim.Data.Length);
        Assert.Equal(196, resources.Sprite(0x1084AF8).Bytes.Length);
        NativeBattleEffectFileAsset file = resources.ReadFile(360);
        Assert.Equal("MA8DEF_0.TIM", file.Name);
        Assert.Equal(33824, file.Data.Length);
        Assert.Equal(8u, BinaryPrimitives.ReadUInt32LittleEndian(file.Data.AsSpan(4, 4)));
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(361));
        foreach (uint address in new uint[] { 0x108FAB0, 0x108FAB8 })
        {
            Assert.Equal(8, resources.ReadStatic(address, 8).Length);
            Assert.Throws<NotSupportedException>(() => resources.ReadStatic(address, 9));
        }
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaGlobal, 4));
    }

    [Fact]
    public void Original_all_frame_outputs_bound_actual_morph_to_the_896_byte_COMMON_interval()
    {
        using GameAssets game = Open();
        NativeBattleEffect285Resources resources = game.ReadNativeEffect285Resources();
        int[] frames = [39, 23];
        int[] nodes = [25, 38];
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
        Assert.Equal(1849, total);
        Assert.Equal(896, maximumMorph);
    }

    [Fact]
    public void Resource_JSON_preserves_actual_file_programme_and_terminal_sprite_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect285Resources resources = game.ReadNativeEffect285Resources();
        NativeBattleEffect285Resources restored = JsonSerializer.Deserialize<NativeBattleEffect285Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.ReadFile(360).Data.ToArray(), restored.ReadFile(360).Data.ToArray());
        NativeBattleModuleResource sprite = restored.Sprite(0x1084AF8);
        Assert.Equal((ushort)7, BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan(8, 2)));
        for (int frame = 0; frame < 7; frame++)
        {
            _ = sprite.ProjectedFrame571C80(frame);
        }
        Assert.Equal((short)-1, BinaryPrimitives.ReadInt16LittleEndian(sprite.Bytes.AsSpan(24, 2)));
    }

    [Fact]
    public void Selected_catalog_hydrates_same_resource_instance()
    {
        using GameAssets game = Open();
        NativeBattleEffect285Resources resources = game.ReadNativeEffect285Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([285])[284];
        Assert.Same(resources, binding.Effect285Resources);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag284.tim", InitializerGlobal), binding.InitializerResource);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed effect285 resources.");
        return new(directory!);
    }
}
