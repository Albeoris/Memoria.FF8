using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffect101ResourcesTests
{
    [Fact]
    public void Original_source_retains_large_direct_shapes_and_exact_interpolated_union()
    {
        using GameAssets game = Open();
        NativeBattleEffect101Resources resources = game.ReadNativeEffect101Resources();
        resources.Validate();
        Assert.Same(resources, game.ReadNativeEffect101Resources());
        Assert.Equal(0x1321BECu, resources.MainAct.Address);
        Assert.Equal(0x2CD4, resources.MainAct.Data.Length);
        Assert.Equal("FF8_EN:13248C0", resources.Camera.Identity);
        Assert.Equal(0x11C, resources.Camera.Data.Length);
        Assert.Equal(0x64218u, resources.Sound);
        ReadOnlySpan<byte> bytes = resources.MainAct.Data.AsSpan();
        Assert.Equal(5, BinaryPrimitives.ReadInt32LittleEndian(bytes));
        int[] vertices = [114, 61, 25, 48, 25];
        int[] variants = [1, 1, 1, 2, 1];
        for (int index = 0; index < 5; index++)
        {
            int offset = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(8 + index * 4));
            Assert.Equal(vertices[index], BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 4)));
            Assert.Equal(variants[index], BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(offset + 8)));
        }
        Assert.Equal(48 * 8, NativeBattleEffect101Profile.MorphBytes);
        Assert.Equal(NativeBattleEffect101Profile.FirstPacketOffset, NativeBattleEffect101Profile.MorphOffset + NativeBattleEffect101Profile.MorphBytes);
        NativeBattleGfAnimationWorkspace workspace = new(new byte[0x2B0].ToImmutableArray());
        workspace = workspace.Initialize7016B0(resources.MainAct, 0x2B0);
        for (int frame = 0; frame < 38; frame++)
        {
            NativeBattleGfAnimationAdvance step = workspace.Advance701970(resources.MainAct, 0, (current, node) => current);
            Assert.Equal(9, step.EmittedNodes);
            workspace = step.Workspace;
        }
        Assert.Equal(38, workspace.Frame);
        Assert.Equal(0, workspace.Advance701970(resources.MainAct, 0, (current, node) => current).EmittedNodes);
    }
    [Fact]
    public void Original_resource_JSON_and_strict_lookup_preserve_the_only_addressed_ACT()
    {
        using GameAssets game = Open();
        NativeBattleEffect101Resources source = game.ReadNativeEffect101Resources();
        NativeBattleEffect101Resources restored = JsonSerializer.Deserialize<NativeBattleEffect101Resources>(JsonSerializer.Serialize(source))!;
        restored.Validate();
        Assert.Equal(JsonSerializer.Serialize(source), JsonSerializer.Serialize(restored));
        Assert.Same(restored.MainAct, restored.Act(0x1321BEC));
        Assert.Throws<NotSupportedException>(() => restored.Act(0x11355D4));
        Assert.Throws<InvalidDataException>(() => (restored with { MainAct = restored.MainAct with { Data = restored.MainAct.Data.Take(restored.MainAct.Data.Length - 4).ToImmutableArray() } }).Validate());
    }
    [Fact]
    public void Original_catalog_binds_the_exact_unnamed_entry_and_initializer_without_source_alias()
    {
        using GameAssets game = Open();
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalogForEffects([101])[100];
        Assert.Equal((ushort)101, binding.Effect);
        Assert.Equal(NativeBattleEffect101Profile.Entry, binding.Entry);
        Assert.Equal(NativeBattleEffect101Profile.Initializer, binding.Initializer);
        Assert.False(binding.InitializerIsRet);
        Assert.Equal(new NativeBattleEffectInitializerResource("mag100.tim", NativeBattleEffect101Profile.InitializerGlobal), binding.InitializerResource);
        Assert.Null(binding.InitializerResources);
        Assert.Equal("mag100.tim", binding.InitializerAsset!.Name);
        Assert.Equal(133664, binding.InitializerAsset.Data.Length);
        Assert.Same(game.ReadNativeEffect101Resources(), binding.Effect101Resources);
        NativeBattleEffectBinding restored = JsonSerializer.Deserialize<NativeBattleEffectBinding>(JsonSerializer.Serialize(binding))!;
        restored.Effect101Resources!.Validate();
        Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(restored));
        Assert.Equal(binding.InitializerAsset.Data.ToArray(), restored.InitializerAsset!.Data.ToArray());
    }
    private static GameAssets Open()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(directory is not null, "Select installed original Effect101 resources.");
        return new(directory!);
    }
}
