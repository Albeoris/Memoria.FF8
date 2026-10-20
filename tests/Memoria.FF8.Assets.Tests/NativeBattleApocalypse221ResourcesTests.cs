using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleApocalypse221ResourcesTests
{
    [Fact]
    public void Original_directories_preserve_the_extended_primitive_metadata_and_selected_handlers()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeExecutableMemory native = new(File.ReadAllBytes(Path.Combine(TestResources.GameDirectory, "FF8_EN.exe")));
        NativeBattleCinematicProfile profile = NativeBattleCinematicProfile.ForEffect(221);
        NativeBattleEffectBinding binding = assets.ReadNativeEffectCatalogForEffects([221])[220];
        Assert.Equal(0xAA5560u, binding.Entry);
        Assert.Equal(0xAA5530u, binding.Initializer);
        Assert.Equal("mag220_b.00", binding.InitializerAssets!.Value[0].Name);
        NativeBattleCinematicResources resources = assets.ReadNativeCinematicResources(221);
        resources.Validate();
        Assert.Equal(8, resources.TextureProfile.Count);
        Assert.Equal(0x186D3A4u, profile.BoneTable);
        Assert.Equal(0x186D4DCu, profile.PrimitiveSizes);
        Assert.Equal(0x186D570u, profile.SpriteTable);
        Assert.Equal(0x186D610u, profile.DrawTable);
        Assert.Equal(0x186D7B4u, profile.InstructionTable);
        Assert.Equal(native.Read(profile.PrimitiveSizes, 20 + 128), resources.ReadStatic(profile.PrimitiveSizes, 20 + 128).ToArray());
        Assert.Equal(native.Read(profile.InstructionTable, 0x147 * 4), resources.ReadStatic(profile.InstructionTable, 0x147 * 4).ToArray());
        foreach ((string table, uint basis, int index, uint handler, uint canonical) in new[]
        {
            ("vm_table", profile.InstructionTable, 0x5C, 0xAB2460u, 0xAF3660u),
            ("prim_table", profile.PrimitiveTable, 16, 0xAAA2C0u, 0xAEC030u),
            ("draw_table", profile.DrawTable, 37, 0xAA7890u, 0xAE8E20u),
            ("draw_table", profile.DrawTable, 61, 0xAA6910u, 0xAE71D0u),
            ("bone_table", profile.BoneTable, 11, 0xAA6270u, 0xAA6270u)
        })
        {
            uint selected = BinaryPrimitives.ReadUInt32LittleEndian(resources.ReadStatic(basis + (uint)(4 * index), 4));
            Assert.Equal(handler, selected);
            Assert.Equal(canonical, NativeBattleCinematicDispatch.RequireSlot(221, table, index, selected));
            Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.RequireSlot(221, table, index, handler + 1));
        }
        Assert.Throws<NotSupportedException>(() => NativeBattleCinematicDispatch.Canonicalize(221, 0));
    }
}
