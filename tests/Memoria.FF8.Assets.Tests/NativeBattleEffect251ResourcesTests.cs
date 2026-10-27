using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect251Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect251ResourcesTests
{
    [Fact]
    public void Original_sources_keep_embedded_TIM_command_tables_and_stream_separate()
    {
        using GameAssets game = Open();
        NativeBattleEffect251Resources resources = game.ReadNativeEffect251Resources();
        resources.Validate();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([251])[250];
        Assert.Same(resources, binding.Effect251Resources);
        Assert.True(binding.InitializerIsRet);
        Assert.Null(binding.InitializerResource);
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(132640, resources.EmbeddedTim.Data.Length);
        Assert.Equal(0x10u, BinaryPrimitives.ReadUInt32LittleEndian(resources.EmbeddedTim.Data.AsSpan()));
        Assert.Equal(396, resources.Camera.Data.Length);
        Assert.Equal(new byte[] { 0x30, 0x30, 0x30, 0x30 }, resources.ReadStatic(0x1438778, 4));
        Assert.Equal(new byte[] { 0x31, 0x30, 0, 0 }, resources.ReadStatic(0x145D2B0, 4));
        Assert.Equal(new byte[] { 0x41, 0x42, 0x31, 0x30 }, resources.ReadStatic(0x145D2B4, 4));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x1438778, 5));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x145D2B0, 5));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(EmbeddedTimAddress, 4));
        Assert.Throws<NotSupportedException>(() => resources.Act(0));
        Assert.Throws<NotSupportedException>(() => resources.Sprite(0));
        Assert.Equal(StreamFileName, resources.Files[713].Name);
        Assert.Equal(168480, resources.Files[713].Data.Length);
        Assert.Equal(7, resources.Programmes.Count);
        Assert.Equal(18, resources.Geometry.Count);
    }

    [Fact]
    public void Original_ACT_nodes_use_only_bounded_shapes_and_command_table_indices()
    {
        using GameAssets game = Open();
        NativeBattleEffect251Resources resources = game.ReadNativeEffect251Resources();
        int[] nodes = [5, 2, 6, 5, 7, 4, 10];
        int largestMorph = 0;
        for (int index = 0; index < ProgrammeAddresses.Length; index++)
        {
            NativeBattleGfAnimationSource source = resources.Act(ProgrammeAddresses[index]);
            NativeBattleGfAnimationWorkspace workspace = new NativeBattleGfAnimationWorkspace(new byte[ProgrammeWorkspaceLengths[index]].ToImmutableArray()).Initialize7016B0(source, ProgrammeWorkspaceLengths[index]);
            for (int frame = 0; frame < ProgrammeDurations[index]; frame++)
            {
                NativeBattleGfAnimationAdvance advance = workspace.Advance701970(source, 0, (current, node) =>
                {
                    Assert.InRange(node.GeometryIndex, 0, 3);
                    uint relative = BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan(8 + node.GeometryIndex * 4, 4));
                    NativeBattleStaticMeshResource geometry = resources.ReadGeometry(source.Address + relative);
                    int variants = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan(8, 4)));
                    Assert.InRange((int)node.Word40, 0, variants - 1);
                    Assert.InRange((int)node.Word42, 0, variants - 1);
                    if (node.Word40 != node.Word42 && node.Scalar38 is not 0 and not 4096)
                    {
                        int vertices = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(geometry.Bytes.AsSpan(4, 4)));
                        largestMorph = Math.Max(largestMorph, vertices * 8);
                    }
                    return current;
                });
                Assert.Equal(nodes[index], advance.EmittedNodes);
                workspace = advance.Workspace;
            }
            Assert.Equal(0, workspace.Advance701970(source, 0, (current, _) => current).EmittedNodes);
        }
        Assert.InRange(largestMorph, 0, 336 * 8);
    }

    [Fact]
    public void JSON_preserves_mutable_ACT_seeds_and_selected_extended_sprite_records()
    {
        using GameAssets game = Open();
        NativeBattleEffect251Resources original = game.ReadNativeEffect251Resources();
        NativeBattleEffect251Resources resources = JsonSerializer.Deserialize<NativeBattleEffect251Resources>(JsonSerializer.Serialize(original))!;
        resources.Validate();
        Assert.True(original.Act(0x1434430).Data.AsSpan().SequenceEqual(resources.Act(0x1434430).Data.AsSpan()));
        Assert.Equal(336u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadGeometry(0x1434448).Bytes.AsSpan(4, 4)));
        Assert.Equal(80u, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadGeometry(0x14370B4).Bytes.AsSpan(4, 4)));
        foreach (uint address in SpriteAddresses)
        {
            NativeBattleModuleResource sprite = resources.Sprite(address);
            int count = BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan(8, 2));
            for (int frame = address == 0xC79220 ? 1 : 0; frame < count; frame++)
            {
                int offset = BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan(10 + frame * 2, 2));
                uint header = BinaryPrimitives.ReadUInt32LittleEndian(sprite.Bytes.AsSpan(offset, 4));
                Assert.Equal(1u, header & 0x7FFFFFFF);
                Assert.Equal(address == 0x1438AE4, (header & 0x80000000) != 0);
                _ = sprite.ProjectedFrame571C80(frame);
            }
        }
    }

    [Fact]
    public void Native_clear_length_does_not_truncate_the_larger_original_ACT_cache()
    {
        using GameAssets game = Open();
        NativeBattleEffect251Resources resources = game.ReadNativeEffect251Resources();
        uint main = NativeBattleEffect251Controller.CommonAddress + 0xA294;
        uint workspace = main + 0x50;
        NativeBattleEffectTaskPool pool = NativeBattleEffectTaskPool.Initialize(MainDescriptor, main, 0x5EC, 1);
        (pool, _) = pool.Append(MainCallback);
        pool = NativeBattleEffectTaskMemory.WriteBankSpan(pool, 12, NativeBattleEffectArena.Clear().Write(0, Enumerable.Repeat((byte)0x6B, 0x5E0).ToArray()), 0x5E0);
        NativeBattleEffect251State state = NativeBattleEffect251State.FromColdProcess().WriteDword(ArenaGlobal, NativeBattleEffect251Controller.CommonAddress).DetachCommonQueue(MainDescriptor);
        NativeBattleOriginalEffectState effect = new(251, 250, new(251, Entry, Initializer), new(3, 0, 1, 0, 0, 0), RootDescriptor) { Pools = [pool] };
        NativeBattleEffectMemory memory = NativeBattleEffectTaskMemory.BindArenaBankOwnership(NativeBattleEffectMemory.FromColdProcess() with { Arena = NativeBattleEffectArena.Clear() }, [pool]);
        NativeBattleEffect251Frame frame = new(memory, effect, new(1), state);
        frame = NativeBattleEffect251Controller.BindProgramme(frame, workspace, 0x1432448, 0x144, resources);
        byte[] cache = NativeBattleEffect251Controller.ReadBytes(frame, workspace, 392).Read(0, 392);
        Assert.Equal(0x1432448u, BinaryPrimitives.ReadUInt32LittleEndian(cache));
        Assert.Contains(Enumerable.Range(0, (392 - 0x144) / 4), index => BinaryPrimitives.ReadUInt32LittleEndian(cache.AsSpan(0x144 + index * 4, 4)) == 0x10000000);
        Assert.Equal(Enumerable.Repeat((byte)0x6B, 8).ToArray(), NativeBattleEffect251Controller.ReadBytes(frame, workspace + 392, 8).Read(0, 8));
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect251 resources.");
        return new(directory!);
    }
}
