using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect70Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect70ResourceTests
{
    [Fact]
    public void Original_five_ACT_sources_advance_the_complete_selected_node_frame_directories()
    {
        using GameAssets game = Open();
        NativeBattleEffect70Resources resources = game.ReadNativeEffect70Resources();
        resources.Validate();
        int[] nodes = [2, 3, 3, 55, 5];
        int[] frames = [22, 200, 30, 182, 200];
        int[] geometries = [1, 2, 2, 15, 3];
        for (int index = 0; index < 5; index++)
        {
            NativeBattleGfAnimationSource source = resources.Act(ActAddresses[index]);
            int table = checked((int)Dword(source.Data, 4));
            Assert.Equal(nodes[index], checked((int)Dword(source.Data, table)));
            Assert.Equal(frames[index], checked((int)Dword(source.Data, table + 4)));
            int physical = index >= 3 ? MainWorkspaceLength : SecondaryWorkspaceLength;
            NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, physical).ToImmutableArray());
            workspace = workspace.Initialize7016B0(source, WorkspaceClearLengths[index]);
            Assert.All(workspace.Data.AsSpan().Slice(WorkspaceClearLengths[index]).ToArray(), value => Assert.Equal((byte)0xA5, value));
            int callbacks = 0;
            for (int frame = 0; frame < frames[index]; frame++)
            {
                NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
                {
                    Assert.InRange(node.GeometryIndex, (ushort)0, checked((ushort)(geometries[index] - 1)));
                    uint address = checked(source.Address + Dword(source.Data, 8 + node.GeometryIndex * 4));
                    Assert.Equal(address, resources.ReadGeometry(address).Address);
                    callbacks++;
                    return current;
                });
                Assert.Equal(frames[index] - frame - 1, next.Remaining);
                workspace = next.Workspace;
            }
            Assert.True(callbacks > 0);
            Assert.Equal(physical, workspace.Data.Length);
            Assert.All(workspace.Data.AsSpan().Slice(WorkspaceClearLengths[index]).ToArray(), value => Assert.Equal((byte)0xA5, value));
        }
    }

    [Fact]
    public void Selected_TIMs_sprites_dispatch_and_morph_fronts_keep_their_original_layouts()
    {
        using GameAssets game = Open();
        NativeBattleEffect70Resources resources = game.ReadNativeEffect70Resources();
        Assert.Equal(234016, resources.MainTim.Data.Length);
        Assert.Equal(9u, Dword(resources.MainTim.Data, 4));
        Assert.Equal(new[] { 33824, 33824 }, new[] { resources.ReadFile(360).Data.Length, resources.ReadFile(361).Data.Length });
        Assert.Equal(8u, Dword(resources.ReadFile(360).Data, 4));
        Assert.Equal("0000000000000000", Encoding.ASCII.GetString(resources.DefaultDispatch.AsSpan()));
        Assert.Equal("000000100111000", Encoding.ASCII.GetString(resources.MainDispatch.AsSpan()));
        Assert.Equal(new[] { 428, 532 }, PuffAddresses.Select(address => resources.Puff(address).Bytes.Length).ToArray());
        Assert.Equal(12u, Dword(resources.ParticleMesh.Bytes, 4));
        int maximumMorph = 0;
        int maximumStatic = 0;
        foreach (NativeBattleGfAnimationSource source in resources.Acts)
        {
            for (int index = 0; index < Dword(source.Data, 0); index++)
            {
                NativeBattleStaticMeshResource mesh = resources.ReadGeometry(source.Address + Dword(source.Data, 8 + index * 4));
                int vertices = checked((int)Dword(mesh.Bytes, 4));
                maximumStatic = Math.Max(maximumStatic, vertices);
                if (Dword(mesh.Bytes, 8) > 1)
                {
                    maximumMorph = Math.Max(maximumMorph, vertices * 8);
                }
            }
        }
        Assert.Equal(114, maximumStatic);
        Assert.Equal(MorphLength, maximumMorph);
        NativeBattleGfAnimationSource main = resources.Act(MainActAddress);
        NativeBattleStaticMeshResource flat = resources.ReadGeometry(MainActAddress + Dword(main.Data, 8 + 11 * 4));
        int polygon = checked((int)Dword(flat.Bytes, 0));
        Assert.Equal(16u, Dword(flat.Bytes, polygon + 4));
        Assert.Equal((byte)0x2A, flat.Bytes[polygon + 8 + 3]);
    }

    [Fact]
    public void Resource_JSON_preserves_addressed_fronts_and_rejects_unselected_or_changed_inputs()
    {
        using GameAssets game = Open();
        NativeBattleEffect70Resources resources = game.ReadNativeEffect70Resources();
        string json = JsonSerializer.Serialize(resources);
        NativeBattleEffect70Resources restored = JsonSerializer.Deserialize<NativeBattleEffect70Resources>(json)!;
        restored.Validate();
        Assert.Equal(json, JsonSerializer.Serialize(restored));
        Assert.Throws<NotSupportedException>(() => resources.Act(MainActAddress + 1));
        Assert.Throws<NotSupportedException>(() => resources.Puff(PuffAddresses[0] + 1));
        Assert.Throws<NotSupportedException>(() => resources.ReadFile(362));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(ArenaAddress + MorphOffset, MorphLength));
        Assert.Throws<InvalidDataException>(() => (resources with { MainDispatch = resources.MainDispatch.SetItem(6, (byte)'0') }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Files = resources.Files.Remove(361) }).Validate());
        Assert.Throws<InvalidDataException>(() => (resources with { Acts = resources.Acts.SetItem(3, resources.Acts[3] with { Data = resources.Acts[3].Data.RemoveAt(0) }) }).Validate());
    }

    private static uint Dword(ImmutableArray<byte> data, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan().Slice(offset, 4));
    [Fact]
    public void Selected_catalog_retains_exact_resources_and_initializer_publication()
    {
        using GameAssets game = Open();
        NativeBattleEffect70Resources resources = game.ReadNativeEffect70Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([70])[69];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag069-259.tim", InitializerGlobal), binding.InitializerResource);
        Assert.Same(resources, binding.Effect70Resources);
        Assert.Equal(resources.MainTim.Data, binding.InitializerAsset!.Data);
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect70 resources.");
        return new(directory!);
    }
}
