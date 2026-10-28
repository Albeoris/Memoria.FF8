using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;
using static Memoria.FF8.Runtime.NativeBattleEffect80Profile;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect80ResourceTests
{
    [Fact]
    public void Original_resource_fronts_keep_initialized_mutable_inputs_separate_from_static_reads()
    {
        using GameAssets game = Open();
        NativeBattleEffect80Resources resources = game.ReadNativeEffect80Resources();
        resources.Validate();
        Assert.Equal(TextureLength, resources.MainTim.Data.Length);
        Assert.Equal(3, resources.OriginalNodeBanks.Count);
        Assert.Equal(14310, resources.ReadOriginalInitializedSource(TrailAddress).Count(value => value != 0));
        Assert.Equal(TrailAddress, BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadOriginalInitializedSource(TrailPointerAddress).AsSpan()));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(TrailPointerAddress, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(TrailAddress, 2));
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(0x1749C64, 4));
        Assert.Equal(BirthTableLength, resources.ReadStatic(BirthTableAddress, BirthTableLength).Length);
        Assert.Throws<NotSupportedException>(() => resources.ReadStatic(BirthTableAddress - 4, 4));
        NativeBattleEffect80State state = NativeBattleEffect80State.FromColdProcess().BindInitializedSource(resources, TrailPointerAddress).BindNodeSource(resources, 0x1750660);
        Assert.Equal((ushort)0xAC20, state.ReadWord(TrailPointerAddress));
        Assert.Equal((ushort)0x175, state.ReadWord(TrailPointerAddress + 2));
        Assert.Equal((ushort)5892, state.ReadWord(0x17531BE));
        state = state.WriteWord(0x17531BE, 6000).BindNodeSource(resources, 0x1750660);
        Assert.Equal((ushort)6000, state.ReadWord(0x17531BE));
        Assert.Equal(DistanceLength, resources.ReadStatic(DistanceAddress, DistanceLength).Length);
        Assert.Equal(ShapeTableLength, resources.ReadStatic(ShapeTableAddress, ShapeTableLength).Length);
    }

    [Fact]
    public void Both_original_ACTs_advance_complete_node_timelines_with_exact_cache_and_shape_bounds()
    {
        using GameAssets game = Open();
        NativeBattleEffect80Resources resources = game.ReadNativeEffect80Resources();
        for (int index = 0; index < 2; index++)
        {
            NativeBattleGfAnimationSource source = resources.Act(ActAddresses[index]);
            int clear = index == 0 ? 196 : 216;
            int frames = index == 0 ? 128 : 24;
            NativeBattleGfAnimationWorkspace workspace = new(Enumerable.Repeat((byte)0xA5, 236).ToImmutableArray());
            workspace = workspace.Initialize7016B0(source, clear);
            Assert.All(NativeBattleStorageSnapshots.Capture(workspace).Data.AsSpan().Slice(clear).ToArray(), value => Assert.Equal((byte)0xA5, value));
            int observations = 0;
            for (int frame = 0; frame < frames; frame++)
            {
                NativeBattleGfAnimationAdvance next = workspace.Advance701970(source, 0, (current, node) =>
                {
                    Assert.InRange(node.GeometryIndex, (ushort)0, (ushort)2);
                    uint address = source.Address + BinaryPrimitives.ReadUInt32LittleEndian(source.Data.AsSpan().Slice(8 + node.GeometryIndex * 4));
                    NativeBattleStaticMeshResource geometry = resources.ReadGeometry(address);
                    Assert.Equal(address, geometry.Address);
                    int vertices = BinaryPrimitives.ReadInt32LittleEndian(geometry.Bytes.AsSpan().Slice(4));
                    int shapes = BinaryPrimitives.ReadInt32LittleEndian(geometry.Bytes.AsSpan().Slice(8));
                    Assert.True(shapes == 1 || vertices * 8 == MorphLength);
                    observations++;
                    return current;
                });
                Assert.Equal(frames - frame - 1, next.Remaining);
                workspace = next.Workspace;
            }
            Assert.True(observations > 0);
        }
    }

    [Fact]
    public void Selected_catalog_and_json_retain_exact_resource_binding_without_import_rehydration()
    {
        using GameAssets game = Open();
        NativeBattleEffect80Resources resources = game.ReadNativeEffect80Resources();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([80])[79];
        Assert.Equal(Entry, binding.Entry);
        Assert.Equal(Initializer, binding.Initializer);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag079.tim", InitializerGlobal), binding.InitializerResource);
        Assert.Same(resources, binding.Effect80Resources);
        Assert.Equal(resources.MainTim.Data, binding.InitializerAsset!.Data);
        NativeBattleEffect80Resources restored = JsonSerializer.Deserialize<NativeBattleEffect80Resources>(JsonSerializer.Serialize(resources))!;
        restored.Validate();
        Assert.Equal(resources.ReadOriginalInitializedSource(TrailAddress), restored.ReadOriginalInitializedSource(TrailAddress));
        Assert.Throws<NotSupportedException>(() => NativeBattleEffect80State.FromUnknown().BindInitializedSource(restored, TrailAddress));
        Assert.Throws<InvalidDataException>(() => (restored with { StaticTables = restored.StaticTables.SetItem(BirthTableAddress, new byte[142].ToImmutableArray()) }).Validate());
    }

    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original effect80 resources.");
        return new(directory!);
    }
}
