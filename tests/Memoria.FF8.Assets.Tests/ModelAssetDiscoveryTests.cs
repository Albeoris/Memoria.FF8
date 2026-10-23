using System.Buffers.Binary;
using Memoria.FF8.Assets;

namespace Memoria.FF8.Assets.Tests;

public sealed class ModelAssetDiscoveryTests
{
    [Fact]
    public void PartyFileResolutionUsesKernelTierSelectorAndNativeFilenameTable()
    {
        FakeMemory memory = new();
        memory.Put32(0xB8B940 + 4 * 4, 0x1000);
        memory.Put16(0x1000 + 5 * 2, 72);
        memory.Put32(0xB84CCC + 72 * 4, 0x2000);
        memory.Put(0x2000, System.Text.Encoding.ASCII.GetBytes("d4w123.dat\0").Concat(new byte[117]).ToArray());
        Assert.Equal("battle/d4w123.dat", ModelAssetDiscoveryBindings.ResolvePartyFile(memory, 4, true, 5));
        Assert.DoesNotContain(memory.Reads, read => read.Address == 0x1000 + 123 * 2);
    }

    [Fact]
    public void AbsentAndSelfContainedBindingsFailWithoutGuessingNames()
    {
        FakeMemory memory = new();
        Assert.Throws<NotSupportedException>(() => ModelAssetDiscoveryBindings.ResolvePartyFile(memory, 7, true, 0));
        memory.Put32(0xB8B940, 0);
        Assert.Throws<NotSupportedException>(() => ModelAssetDiscoveryBindings.ResolvePartyFile(memory, 0, true, 0));
    }

    [Fact]
    public void ExternalBankRejectsUnboundedResourceBeforeReadingPayload()
    {
        FakeMemory memory = new();
        byte[] header = new byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(header, 3);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(16), int.MaxValue);
        memory.Put(0x1000, header);
        Assert.Throws<InvalidDataException>(() => ModelAssetDiscoveryBindings.ReadExternalBank(memory, 0x1000));
        Assert.Single(memory.Reads);
    }

    [Fact]
    public void BankSlotsRetainDistinctReplacementAddresses()
    {
        Assert.NotEqual(ModelAssetDiscoveryBindings.ClipTarget("battle/d0c000.dat", "embedded", 0), ModelAssetDiscoveryBindings.ClipTarget("battle/d0w000.dat", "embedded", 0));
        Assert.NotEqual(ModelAssetDiscoveryBindings.ClipTarget("battle/d0c000.dat", "embedded", 0), ModelAssetDiscoveryBindings.ClipTarget("battle/d0c000.dat", "finisher-165", 0));
        Assert.Equal(9, ModelAssetDiscoveryBindings.SquallExternalBanks.Count);
        Assert.Equal(9, ModelAssetDiscoveryBindings.SquallExternalBanks.Select(bank => bank.Bank).Distinct().Count());
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(9, true)]
    [InlineData(7, false)]
    public void InstalledPartyDiscoveryPreservesRigRolesAndExplicitBanks(int character, bool reduced)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipUnless(Directory.Exists(game), "Optional installed game resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        ModelCatalogEntry selected = assets.EnumerateModelCatalog().First(entry => entry.ResourcePath.StartsWith($"battle/d{character:x}c", StringComparison.OrdinalIgnoreCase));
        ModelAssetDiscoveryItem[] items = assets.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).ToArray();
        Assert.Contains(items, item => item.Kind == ModelAssetDiscoveryKind.Coverage);
        ModelAssetDiscoveryItem[] weapons = items.Where(item => item.Kind == ModelAssetDiscoveryKind.Object && item.AlternativeGroup is not null).ToArray();
        if (character == 7)
        {
            Assert.Empty(weapons);
            return;
        }
        Assert.NotEmpty(weapons);
        Assert.All(weapons, weapon => Assert.Equal(reduced, weapon.UsesOwnerSkeleton));
        if (reduced)
        {
            Assert.All(weapons, weapon => Assert.Equal("mesh/1", weapon.ReplacementPart));
            Assert.DoesNotContain(items, item => item.Kind == ModelAssetDiscoveryKind.Clip && weapons.Any(weapon => weapon.Id == item.OwnerId));
        }
        if (character == 9)
        {
            Assert.True(items.Any(item => item.Kind == ModelAssetDiscoveryKind.Clip && item.Bank.StartsWith("victory-", StringComparison.Ordinal)), string.Join("; ", items.Where(item => item.Error is not null).Select(item => item.Id + ": " + item.Error)));
        }
        if (character == 0)
        {
            Assert.Contains(items, item => item.Kind == ModelAssetDiscoveryKind.Clip && item.Bank == "finisher-165");

            ModelAssetDiscoveryItem clip = items.First(item => item.Kind == ModelAssetDiscoveryKind.Clip && item.Bank == "finisher-165");
            Assert.NotNull(clip.Load);
            Assert.True(clip.NativeFrameCount > 0);
            Assert.True(clip.Load!().Animations.Count > clip.ClipIndex);
        }
    }

    [Fact]
    public void InstalledFieldDiscoveryRetainsSeparateReferringEntryTargets()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipUnless(Directory.Exists(game), "Optional installed game resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        ModelCatalogEntry selected = assets.EnumerateModelCatalog().First(entry => entry.Category == "Field" && entry.Name.StartsWith("d000", StringComparison.OrdinalIgnoreCase));
        ModelAssetDiscoveryItem[] clips = assets.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).Where(item => item.Kind == ModelAssetDiscoveryKind.Clip).ToArray();
        Assert.True(clips.Select(clip => clip.ResourcePath).Distinct().Count() > 1);
        Assert.Equal(clips.Length, clips.Select(clip => clip.Id).Distinct().Count());
        ModelAssetDiscoveryItem referred = clips.First(clip => clip.ResourcePath != selected.ResourcePath);
        Assert.Contains("field main_chr reference", referred.Provenance);
        Assert.Equal(selected.Id, referred.RigOwnerId);
        Assert.True(referred.Load!().Animations.Count > referred.ClipIndex);
    }
    [Theory]
    [InlineData("World character")]
    [InlineData("World static")]
    [InlineData("Battle enemy")]
    public void InstalledOtherDomainsDoNotAcquireCharacterWeaponLinks(string category)
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipUnless(Directory.Exists(game), "Optional installed game resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        ModelCatalogEntry selected = assets.EnumerateModelCatalog().First(entry => entry.Category == category && entry.LoadError is null);
        ModelAssetDiscoveryItem[] items = assets.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).ToArray();
        Assert.Single(items, item => item.Kind == ModelAssetDiscoveryKind.Object);
        Assert.DoesNotContain(items, item => item.AlternativeGroup is not null || item.Kind == ModelAssetDiscoveryKind.Clip && item.OwnerId != selected.Id);
        if (category == "World character")
        {
            ModelAssetDiscoveryItem clip = items.First(item => item.Kind == ModelAssetDiscoveryKind.Clip && item.NativeFrameCount > 1);
            Assert.Equal(1f / ModelExchange.WorldFramesPerSecond, clip.SecondsPerFrame);
            EditableAnimation animation = clip.Load!().Animations[clip.ClipIndex];
            Assert.Equal((clip.NativeFrameCount - 1) / ModelExchange.WorldFramesPerSecond, animation.Duration, 5);
        }
        if (category.StartsWith("World", StringComparison.Ordinal))
        {
            Assert.Contains(items, item => item.Kind == ModelAssetDiscoveryKind.Diagnostic && item.Provenance == "world loader scope");
        }
    }

    [Fact]
    public void UnsupportedGfResourceHasExplicitDiagnosticWithoutCrossDomainFallback()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipUnless(Directory.Exists(game), "Optional installed game resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        ModelCatalogEntry selected = new("gf/unsupported", "Unresolved GF bank", "GF", "gf/unsupported", "GF");
        ModelAssetDiscoveryItem[] items = assets.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).ToArray();
        Assert.DoesNotContain(items, item => item.Kind is ModelAssetDiscoveryKind.Object or ModelAssetDiscoveryKind.Clip);
        Assert.Contains(items, item => item.Kind == ModelAssetDiscoveryKind.Diagnostic && item.Error is not null);
        Assert.Contains(items, item => item.Kind == ModelAssetDiscoveryKind.Coverage && item.Error is not null);
    }
    [Fact]
    public void InstalledNpcTextureAliasDoesNotShareGeometryOrClips()
    {
        string? game = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? Environment.GetEnvironmentVariable("FF8_GAME_DIRECTORY");
        Assert.SkipUnless(Directory.Exists(game), "Optional installed game resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        ModelCatalogEntry selected = new("field/bgpaty_1/6", "Texture alias NPC", "Field", "field/bgpaty_1/chara.one", "", ModelIndex: 6);
        ModelAssetDiscoveryItem[] items = assets.DiscoverModelAssets(selected, TestContext.Current.CancellationToken).ToArray();
        Assert.Single(items, item => item.Kind == ModelAssetDiscoveryKind.Object);
        Assert.Contains(items, item => item.Kind == ModelAssetDiscoveryKind.Clip);
        Assert.All(items.Where(item => item.Kind is ModelAssetDiscoveryKind.Clip or ModelAssetDiscoveryKind.Object), item => Assert.Equal(selected.Id, item.OwnerId));
        Assert.DoesNotContain(items, item => item.Provenance.Contains("main_chr", StringComparison.Ordinal));
    }
    private sealed class FakeMemory : IGameNativeMemorySource
    {
        private readonly Dictionary<uint, byte[]> blocks = [];
        public List<(uint Address, int Length)> Reads { get; } = [];
        public string SourceHash => "synthetic";
        public void Put(uint address, byte[] value) => blocks[address] = value;
        public void Put32(uint address, uint value)
        {
            byte[] bytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
            Put(address, bytes);
        }
        public void Put16(uint address, short value)
        {
            byte[] bytes = new byte[2];
            BinaryPrimitives.WriteInt16LittleEndian(bytes, value);
            Put(address, bytes);
        }
        public byte[] Read(uint address, int count)
        {
            Reads.Add((address, count));
            return blocks[address].AsSpan(0, count).ToArray();
        }
        public byte[] ReadLoader(uint address, int count) => Read(address, count);
    }
}
