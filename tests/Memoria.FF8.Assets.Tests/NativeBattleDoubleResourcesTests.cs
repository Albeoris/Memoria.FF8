using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleDoubleResourcesTests
{
    [Fact]
    public void Original_Double_kernel_catalog_and_resources_keep_their_native_addresses_and_bounds()
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleActionDefinition action = Assert.Single(assets.ActionDefinitions, action => action.Ability == BattleAbility.Magic && action.NativeAction?.MagicId == 4);
        Assert.Equal("Double", action.Name);
        Assert.Equal(33, action.Id);
        NativeBattleEffectBinding binding = game.ReadNativeEffectCatalog()[3];
        Assert.Equal(0x8D5080u, binding.Entry);
        Assert.Equal(0x8D5060u, binding.Initializer);
        NativeBattleEffectInitializerResource initializer = binding.InitializerResource ?? throw new InvalidDataException("Original Double initializer is missing.");
        Assert.Equal("mag003.tim", initializer.Name);
        Assert.Equal(0x276FB00u, initializer.GlobalAddress);
        NativeBattleDoubleResources resources = game.ReadNativeDoubleResources();
        Assert.Same(resources, game.ReadNativeDoubleResources());
        Assert.Equal(300027u, resources.Sound);
        Assert.Equal(0x630, resources.Animation.Data.Length);
        Assert.Equal(new uint[] { 0x163E930, 0x163EA90, 0x163EB6C }, resources.Sprites.Select(sprite => sprite.Address));
        Assert.Equal(new[] { 13, 8, 1 }, resources.Sprites.Select(sprite => (int)BinaryPrimitives.ReadUInt16LittleEndian(sprite.Bytes.AsSpan().Slice(8))));
        Assert.Equal(new uint[] { 0x163EB94, 0x163EDF4, 0x163F014, 0x163F224, 0x163FFD0 }, resources.Meshes.Select(mesh => mesh.Address));
        Assert.Equal(new[] { 24, 24, 17, 76, 76 }, resources.Meshes.Select(mesh => BinaryPrimitives.ReadInt32LittleEndian(mesh.Bytes.AsSpan().Slice(4))));
        Assert.Equal(new[] { 0x260, 0x220, 0x210, 0x8DC, 0x8DC }, resources.Meshes.Select(mesh => mesh.Bytes.Length));
        foreach (NativeBattleModuleResource sprite in resources.Sprites)
        {
            sprite.Validate();
        }
        Assert.Equal(0x268, resources.MorphFirst.Length);
        Assert.Equal(0x268, resources.MorphSecond.Length);
        Assert.Equal(76, BinaryPrimitives.ReadInt32LittleEndian(resources.MorphFirst.AsSpan().Slice(4)));
        Assert.Equal(76, BinaryPrimitives.ReadInt32LittleEndian(resources.MorphSecond.AsSpan().Slice(4)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2048)]
    [InlineData(4096)]
    public void Actual_Double_morph_sources_write_only_XYZ_into_the_current_destination(int ratio)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleDoubleResources resources = game.ReadNativeDoubleResources();
        NativeBattleStaticMeshResource destination = Assert.Single(resources.Meshes, mesh => mesh.Address == 0x163FFD0);
        var morphed = NativeBattleGfMorphGeometry.Morph45E9D0(resources.MorphFirst, 8, resources.MorphSecond, 8, 76, ratio, destination.Bytes, 8);
        Assert.Equal(destination.Bytes.AsSpan().Slice(0, 8).ToArray(), morphed.AsSpan().Slice(0, 8).ToArray());
        Assert.Equal(destination.Bytes.AsSpan().Slice(0x268).ToArray(), morphed.AsSpan().Slice(0x268).ToArray());
        for (int vertex = 0; vertex < 76; vertex++)
        {
            int offset = 8 + vertex * 8;
            Assert.Equal(destination.Bytes.AsSpan().Slice(offset + 6, 2).ToArray(), morphed.AsSpan().Slice(offset + 6, 2).ToArray());
            if (ratio is 0 or 4096)
            {
                var source = ratio == 0 ? resources.MorphFirst : resources.MorphSecond;
                Assert.Equal(source.AsSpan().Slice(offset, 6).ToArray(), morphed.AsSpan().Slice(offset, 6).ToArray());
            }
        }
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void Original_Double_entry_registers_and_uploads_only_when_parameter_flag_allows_it(byte flags)
    {
        using GameAssets game = new(LaunchOptions.DefaultGameDirectory);
        NativeBattleDoubleResources resources = game.ReadNativeDoubleResources();
        NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(4, game.ReadNativeEffectCatalogForEffects([4]));
        var initialized = NativeBattleOriginalEffects.Initialize(game.ReadColdProcessNativeBattleMemory().EffectMemory!, selected);
        NativeBattleOriginalEffectState effect = initialized.Effect with { Parameters = new(0, flags, 8, 0, 0, 0) };
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        //Controlled physical header tests the original entry independently of formula or menu admission.
        (packets, _) = packets.Prepare(new(0, 2, 11, 0, 33, 4, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleDoubleFrame frame = NativeBattleDoubleController.Initialize8D5080(initialized.Memory, effect, packets, resources);
        Assert.Equal(0x2775058u, frame.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 2, 4, 40, 200, 200, 4 }, frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Equal(0x8D5180u, frame.Effect.Pools[0].Slots[0].Callback);
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(0x2D));
        NativeBattleEffectTextureState textures = frame.Memory.Textures!;
        Assert.Equal(flags == 0 ? 1 : 0, textures.Count);
        if (flags == 0)
        {
            Assert.Equal(8, textures.Animation!.StreamOffset);
            Assert.Equal(0x144, textures.Animation.ModelTableOffset);
            Assert.Equal(2, textures.Drain505D20(frame.Memory.Arena).Uploads.Length);
        }
        else
        {
            Assert.Equal(initialized.Memory.Textures!.Animation, textures.Animation);
            NativeBattleDoubleFrame skipped = NativeBattleDoubleController.Initialize8D5080(initialized.Memory with { Textures = null, Arena = null }, effect with { ResourcePointers = [] }, packets, resources);
            Assert.Null(skipped.Memory.Textures);
        }
    }
}
