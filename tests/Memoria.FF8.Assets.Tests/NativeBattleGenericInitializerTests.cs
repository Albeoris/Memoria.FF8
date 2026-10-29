using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleGenericInitializerTests
{
    [Fact]
    public void AuthenticatedCatalogProvesEveryStraightLineLoaderWithoutMaterializingItsAssets()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        byte[] executable = File.ReadAllBytes(Path.Combine(assets.GameDirectory, "FF8_EN.exe"));
        using PEReader reader = new(new MemoryStream(executable));
        var catalog = assets.ReadNativeEffectCatalog();
        Assert.Equal(264, catalog.Count(binding => binding.InitializerResource is not null));
        Assert.Equal(57, catalog.Count(binding => binding.Initializer == 0));
        Assert.Equal(17, catalog.Count(binding => binding.InitializerIsRet == true));
        Assert.Equal(62, catalog.Count(binding => binding.Initializer != 0 && binding.InitializerIsRet != true && binding.InitializerResource is null));
        Assert.Equal((ushort)2, Assert.Single(catalog.Where(binding => binding.InitializerAsset is not null)).Effect);
        foreach (NativeBattleEffectBinding binding in catalog.Where(binding => binding.InitializerResource is not null))
        {
            NativeBattleEffectInitializerResource descriptor = binding.InitializerResource!;
            if (binding.Initializer == NativeBattleEffect140Profile.Initializer)
            {
                Assert.Equal(NativeBattleEffect140Profile.Effect, binding.Effect);
                Assert.Equal(NativeBattleEffect140Profile.InitializerGlobal, descriptor.GlobalAddress);
                byte[] original = new NativeExecutableMemory(executable).Read(binding.Initializer, 29);
                Assert.Equal(new byte[] { 0x56, 0x68 }, original.AsSpan(0, 2).ToArray());
                Assert.Equal(new byte[] { 0xC7, 5 }, original.AsSpan(6, 2).ToArray());
                Assert.Equal(0x1DCD6E8u, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(8)));
                Assert.Equal(NativeBattleEffect140Profile.LoaderCallback, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(12)));
                Assert.Equal((byte)0xE8, original[16]);
                Assert.Equal(0x571B80L, binding.Initializer + 21L + BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(17)));
                Assert.Equal(new byte[] { 0x83, 0xC4, 4, 0xA3 }, original.AsSpan(21, 4).ToArray());
                Assert.Equal(descriptor.GlobalAddress, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(25)));
                continue;
            }
            if (binding.Initializer == 0x6897F0)
            {
                Assert.Equal((ushort)274, binding.Effect);
                Assert.Equal(new NativeBattleEffectInitializerResource("mag273.tim", 0x2508410), descriptor);
                byte[] original = new NativeExecutableMemory(executable).Read(binding.Initializer, 29);
                Assert.Equal((byte)0x68, original[0]);
                Assert.Equal(new byte[] { 0xC7, 5 }, original.AsSpan(5, 2).ToArray());
                Assert.Equal(0x1DCD6E8u, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(7)));
                Assert.Equal(0x689810u, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(11)));
                Assert.Equal((byte)0xE8, original[15]);
                Assert.Equal(0x571B80L, binding.Initializer + 20L + BinaryPrimitives.ReadInt32LittleEndian(original.AsSpan(16)));
                Assert.Equal(new byte[] { 0x83, 0xC4, 4, 0xA3 }, original.AsSpan(20, 4).ToArray());
                Assert.Equal(descriptor.GlobalAddress, BinaryPrimitives.ReadUInt32LittleEndian(original.AsSpan(24)));
                Assert.Equal((byte)0xC3, original[28]);
                continue;
            }
            long rva = binding.Initializer - (long)reader.PEHeaders.PEHeader!.ImageBase;
            SectionHeader section = reader.PEHeaders.SectionHeaders.Single(section => rva >= section.VirtualAddress && rva + 19 <= section.VirtualAddress + section.SizeOfRawData);
            byte[] code = executable.AsSpan(checked((int)(rva - section.VirtualAddress + section.PointerToRawData)), 19).ToArray();
            Assert.Equal((byte)0x68, code[0]);
            Assert.Equal((byte)0xE8, code[5]);
            Assert.Equal(0x571B80L, binding.Initializer + 10L + BinaryPrimitives.ReadInt32LittleEndian(code.AsSpan().Slice(6)));
            Assert.Equal(new byte[] { 0x83, 0xC4, 4, 0xA3 }, code.AsSpan().Slice(10, 4).ToArray());
            Assert.Equal(descriptor.GlobalAddress, BinaryPrimitives.ReadUInt32LittleEndian(code.AsSpan().Slice(14)));
            Assert.Equal((byte)0xC3, code[18]);
            Assert.False(string.IsNullOrEmpty(descriptor.Name));
        }
    }

    [Fact]
    public void OnlyExplicitlyRequestedInitializerAssetsMaterializeAndShareTheOriginalArenaLoader()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        var catalog = assets.ReadNativeEffectCatalogForEffects([1, 2, 4, 4]);
        Assert.Equal(new ushort[] { 1, 2, 4 }, catalog.Where(binding => binding.InitializerAsset is not null).Select(binding => binding.Effect));
        Assert.Equal("mag000-049.tim", catalog[0].InitializerAsset!.Name);
        Assert.Equal("mag003.tim", catalog[3].InitializerAsset!.Name);
        foreach (ushort id in new ushort[] { 1, 2, 4 })
        {
            NativeBattleOriginalEffectState selected = NativeBattleOriginalEffects.Select(id, catalog);
            var initialized = NativeBattleOriginalEffects.Initialize(assets.ReadColdProcessNativeBattleMemory().EffectMemory!, selected);
            Assert.Equal(selected.Binding.InitializerAsset!.Data.Length, initialized.Memory.Cursor);
            Assert.Equal(selected.Binding.InitializerAsset.Data.ToArray(), initialized.Memory.Arena!.Read(0, initialized.Memory.Cursor));
            Assert.Equal(new NativeBattleEffectResourcePointer(selected.Binding.InitializerResource!.GlobalAddress, 0), Assert.Single(initialized.Effect.ResourcePointers));
            Assert.Null(initialized.Effect.ReturnedDescriptor);
            NativeBattleOriginalEffectState restored = JsonSerializer.Deserialize<NativeBattleOriginalEffectState>(JsonSerializer.Serialize(initialized.Effect))!;
            Assert.Equal(selected.Binding.InitializerResource, restored.Binding.InitializerResource);
        }
        Assert.Single(assets.ReadNativeEffectCatalog().Where(binding => binding.InitializerAsset is not null));
        NativeBattleEffectBinding ordered = assets.ReadNativeEffectCatalogForEffects([6])[5];
        Assert.Equal(2, NativeBattleEffectInitializerPolicy.Resources(ordered).Length);
        Assert.Equal(2, NativeBattleEffectInitializerPolicy.Assets(ordered).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => assets.ReadNativeEffectCatalogForEffects([0]));
        NativeBattleOriginalEffectState unsupported = NativeBattleOriginalEffects.Select(6, catalog);
        Assert.Throws<NotSupportedException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), unsupported));
        NativeBattleOriginalEffectState missingAsset = NativeBattleOriginalEffects.Select(1, assets.ReadNativeEffectCatalog());
        Assert.Throws<NotSupportedException>(() => NativeBattleOriginalEffects.Initialize(NativeBattleEffectMemory.FromColdProcess(), missingAsset));
    }
}
