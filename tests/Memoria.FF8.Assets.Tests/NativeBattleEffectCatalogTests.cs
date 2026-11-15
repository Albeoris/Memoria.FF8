using System.Buffers.Binary;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeBattleEffectCatalogTests
{
    [Theory]
    [InlineData(NativeBattleEffect210Profile.Entry, NativeBattleEffect210Profile.Initializer)]
    [InlineData(NativeBattleEffect314Profile.Entry, NativeBattleEffect314Profile.Initializer)]
    [InlineData(NativeBattleEffect315Profile.Entry, NativeBattleEffect315Profile.Initializer)]
    [InlineData(NativeBattleEffect326Profile.Entry, NativeBattleEffect326Profile.Initializer)]
    public void Caster_companion_capability_is_registered_by_callback_pair(uint entry, uint initializer)
    {
        var catalog = NativeBattleEffectCatalogReader.Read(new CompanionCatalogImage(entry, initializer));

        NativeBattleEffectBinding declared = Assert.Single(catalog, binding => binding.RequiresOriginalCasterCompanion);
        Assert.Equal((ushort)17, declared.Effect);
        Assert.Equal(entry, declared.Entry);
        Assert.Equal(initializer, declared.Initializer);
        Assert.Equal(entry, catalog[17].Entry);
        Assert.False(catalog[17].RequiresOriginalCasterCompanion);
    }

    [Fact]
    public void OriginalExecutableSuppliesAll400EffectBindingsAndActualAutomaticGuardianModules()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        var catalog = assets.ReadNativeEffectCatalog();
        Assert.Equal(400, catalog.Length);
        Assert.Equal(Enumerable.Range(1, 400).Select(value => (ushort)value), catalog.Select(binding => binding.Effect));
        Assert.Equal(new NativeBattleEffectBinding(187, 0x6472E0, 0x575570) { InitializerIsRet = true }, catalog[186]);
        Assert.Equal(new NativeBattleEffectBinding(327, 0x58D760, 0x58D6E0) { InitializerIsRet = false, InitializerResource = new("mag326-329.tim", 0x2200FB4) }, catalog[326]);
        Assert.Equal(new NativeBattleEffectBinding(328, 0x58D930, 0x58D700) { InitializerIsRet = false, InitializerResource = new("mag326-329.tim", 0x2200FB0) }, catalog[327]);
        Assert.Equal(new NativeBattleEffectBinding(329, 0x58DB10, 0x58D720) { InitializerIsRet = false, InitializerResource = new("mag326-329.tim", 0x2200F9C) }, catalog[328]);
        Assert.Equal(new NativeBattleEffectBinding(330, 0x58DCF0, 0x58D740) { InitializerIsRet = false, InitializerResource = new("mag326-329.tim", 0x2200EC0) }, catalog[329]);
        Assert.Equal(new NativeBattleEffectBinding(89, 0x6F9CF0, 0x5755E0) { InitializerIsRet = true }, catalog[88]);
        Assert.Equal(catalog, assets.ReadNativeEffectCatalog());
        Assert.Throws<NotSupportedException>(() => NativeBattleEffectCatalogReader.Read(new byte[128]));
    }

    private sealed class CompanionCatalogImage(uint entry, uint initializer) : IGameNativeImageSource
    {
        public string SourceHash => NativeFieldRuntimeProfile.FF8EnExecutableHash;

        public byte[] Read(uint address, int count)
        {
            byte[] bytes = new byte[count];
            if (address is 0xC817B4 or 0xC817B8)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, entry);
            }
            else if (address == 0xC81DF8)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(bytes, initializer);
            }
            else if (address == initializer)
            {
                bytes[0] = 0xC3;
            }
            return bytes;
        }

        public byte[] ReadLoader(uint address, int count) => Read(address, count);
        public bool IsExecutableAddress(uint address) => address == entry || address == initializer;
        public bool IsWritableRange(uint address, int count) => false;
    }
}
