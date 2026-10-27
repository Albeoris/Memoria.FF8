using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class NativeInitialDataResourceTests
{
    [Theory]
    [InlineData(2804)]
    [InlineData(2806)]
    [InlineData(2812)]
    [InlineData(3200)]
    public void SelectedTemplatePreservesCopiedItemsAndUncopiedClear(int length)
    {
        byte[] initial = Template(length);
        if (length > NativeInitialData.InventoryOffset)
        {
            initial[^2] = 9;
            initial[^1] = 37;
        }
        string fingerprint = Convert.ToHexString(SHA256.HashData(initial));
        NativeEconomy economy = NativeEconomyInitialization.Read(initial);
        Assert.Equal(fingerprint, economy.InitOutSha256);
        int copiedPairs = (length - NativeInitialData.InventoryOffset) / 2;
        for (int index = 0; index < NativeEconomy.ItemSlotCount; index++)
        {
            Assert.Equal(index == copiedPairs - 1 ? new NativeItemStock(9, 37) : new NativeItemStock(0, 0), economy.Items[index]);
        }
        Assert.Equal(fingerprint, NativeInputInitialization.Read(initial).InitOutSha256);
        NativePocketStationState pocket = NativePocketStationInitialization.Read(initial);
        pocket.Validate();
        Assert.Equal(NativeInputConfiguration.OriginalInitHash, pocket.InitOutSha256);
        Assert.All(pocket.Raw, value => Assert.Equal((byte)0, value));
        Assert.Equal(fingerprint, NativeLimitProgressInitialization.Read(initial).InitOutSha256);
        Assert.Equal(fingerprint, NativeAppearanceInitialization.Read(initial).InitOutSha256);
        Assert.Equal(fingerprint, NativeShopDefinitionReader.ReadNewGameHistory(initial).InitOutSha256);
        Assert.Equal(fingerprint, NativeWeaponShopDefinitionReader.ReadNewGameRecipes(initial).InitOutSha256);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2802)]
    [InlineData(2805)]
    [InlineData(3199)]
    [InlineData(3202)]
    public void UnsupportedExtentFailsBeforeAnyOwnerReads(int length)
    {
        byte[] initial = new byte[length];
        Assert.Throws<InvalidDataException>(() => NativeInitialData.Validate(initial));
        Assert.Throws<InvalidDataException>(() => NativeEconomyInitialization.Read(initial));
        Assert.Throws<InvalidDataException>(() => NativeInputInitialization.Read(initial));
        Assert.Throws<InvalidDataException>(() => NativePocketStationInitialization.Read(initial));
        Assert.Throws<InvalidDataException>(() => NativeCharacterInitialization.Read(initial, []));
        Assert.Throws<InvalidDataException>(() => NativeShotAvailabilityInitialization.Read(initial, []));
    }

    [Fact]
    public void SelectedInputStillRequiresSupportedOptionsAndMapping()
    {
        byte[] initial = Template(NativeInitialData.MaximumLength);
        initial[2712] = 0;
        Assert.Throws<InvalidDataException>(() => NativeInputInitialization.Read(initial));
        initial = Template(NativeInitialData.MaximumLength);
        initial[2708] ^= 0x20;
        Assert.Throws<InvalidDataException>(() => NativeInputInitialization.Read(initial));
    }

    [Fact]
    public void InstalledTemplateCanExtendItsInventoryWithoutChangingEarlierOwners()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native initialization resources are required.");
        using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
        ResourceService resources = new(source);
        byte[] original = resources.Read(new ResourceKey("main/init.out"));
        byte[] selected = new byte[NativeInitialData.MaximumLength];
        original.CopyTo(selected, 0);
        selected[^2] = 7;
        selected[^1] = 21;
        byte[] kernel = resources.Read(new ResourceKey("main/kernel.bin"));
        string fingerprint = Convert.ToHexString(SHA256.HashData(selected));
        NativeCharacterVitals vitals = NativeCharacterInitialization.Read(selected, kernel);
        vitals.Validate();
        Assert.Equal(fingerprint, vitals.InitOutSha256);
        Assert.Equal(NativeCharacterInitialization.Read(original, kernel).Records, vitals.Records);
        Assert.Equal(fingerprint, NativeShotAvailabilityInitialization.Read(selected, kernel).InitOutSha256);
        Assert.Equal(new NativeItemStock(7, 21), NativeEconomyInitialization.Read(selected).Items[^1]);
        Assert.NotEqual(Convert.ToHexString(SHA256.HashData(original)), fingerprint);
    }

    [Fact]
    public void ExternalTemplatesReachEveryNativeInitializerWithoutTruncation()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_INITIAL_DATA_FIXTURES");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Configure optional native initialization fixtures.");
        string[] fixtures = Directory.GetDirectories(directory!).Where(path => File.Exists(Path.Combine(path, "init.out")) && File.Exists(Path.Combine(path, "kernel.bin"))).ToArray();
        Assert.NotEmpty(fixtures);
        foreach (string fixture in fixtures)
        {
            byte[] initial = File.ReadAllBytes(Path.Combine(fixture, "init.out"));
            byte[] kernel = File.ReadAllBytes(Path.Combine(fixture, "kernel.bin"));
            string fingerprint = Convert.ToHexString(SHA256.HashData(initial));
            NativeCharacterVitals vitals = NativeCharacterInitialization.Read(initial, kernel);
            vitals.Validate();
            Assert.Equal(fingerprint, vitals.InitOutSha256);
            NativeEconomy economy = NativeEconomyInitialization.Read(initial);
            for (int slot = 0; slot < NativeEconomy.ItemSlotCount; slot++)
            {
                int offset = NativeInitialData.InventoryOffset + 2 * slot;
                Assert.Equal(offset < initial.Length ? new NativeItemStock(initial[offset], initial[offset + 1]) : new NativeItemStock(0, 0), economy.Items[slot]);
            }
            Assert.Equal(fingerprint, economy.InitOutSha256);
            Assert.Equal(fingerprint, NativeInputInitialization.Read(initial).InitOutSha256);
            NativePocketStationState pocket = NativePocketStationInitialization.Read(initial);
            pocket.Validate();
            Assert.Equal(NativeInputConfiguration.OriginalInitHash, pocket.InitOutSha256);
            Assert.All(pocket.Raw, value => Assert.Equal((byte)0, value));
            Assert.Equal(fingerprint, NativeShotAvailabilityInitialization.Read(initial, kernel).InitOutSha256);
            Assert.Equal(fingerprint, NativeLimitProgressInitialization.Read(initial).InitOutSha256);
            Assert.Equal(fingerprint, NativeAppearanceInitialization.Read(initial).InitOutSha256);
            Assert.Equal(fingerprint, NativeShopDefinitionReader.ReadNewGameHistory(initial).InitOutSha256);
            Assert.Equal(fingerprint, NativeWeaponShopDefinitionReader.ReadNewGameRecipes(initial).InitOutSha256);
        }
    }

    [Fact]
    public void SelectedInitializationBundleKeepsCopiedAndResetOwnersIndependent()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed native initialization resources are required.");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_INITIAL_DATA_FIXTURES");
        Assert.SkipUnless(directory is not null && Directory.Exists(directory), "Configure optional native initialization fixtures.");
        string[] fixtures = Directory.GetDirectories(directory!).Where(path => File.Exists(Path.Combine(path, "init.out")) && File.Exists(Path.Combine(path, "kernel.bin"))).ToArray();
        Assert.NotEmpty(fixtures);
        foreach (string fixture in fixtures)
        {
            byte[] initial = File.ReadAllBytes(Path.Combine(fixture, "init.out"));
            byte[] kernel = File.ReadAllBytes(Path.Combine(fixture, "kernel.bin"));
            using IResourceSource source = GameDataSourceFactory.Open(TestResources.GameDirectory);
            ResourceService resources = new(source);
            resources.Prepare(null, encodedResources: [new(new ResourceKey("main/init.out"), "out", 1, "fixture", initial), new(new ResourceKey("main/kernel.bin"), "bin", 1, "fixture", kernel)]).Commit();
            using GameAssets assets = new(TestResources.GameDirectory, resources: resources);
            GameSession session = GameSession.CreateColdProcess();
            session.StartNewGame();
            session.InitializeNewGameKnownMagic(assets.ReadNewGameKnownMagic());
            session.InitializeNewGameScannedModels(assets.ReadNewGameScannedModels());
            session.InitializeNewGameSavedBattleFlags(assets.ReadNewGameSavedBattleFlags());
            session.InitializeNewGameLimitProgress(assets.ReadNewGameLimitProgress());
            session.InitializeNewGamePocketStation(assets.ReadNewGamePocketStation());
            session.InitializeNewGameGlobalRandom(assets.ReadNewGameGlobalRandom());
            session.InitializeNewGameWeaponRecipes(assets.ReadNewGameWeaponRecipes());
            session.InitializeNewGameShopHistory(assets.ReadNewGameShopHistory());
            session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
            session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
            session.InitializeNewGameSalaryTable(assets.ReadNativeSalaryTable());
            session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
            session.InitializeNewGameCharacterVitals(assets.ReadNewGameCharacterVitals());
            session.InitializeNewGameCharacterAppearance(assets.ReadNewGameCharacterAppearance());
            GameState state = new GameSession(session.State).State;
            string fingerprint = Convert.ToHexString(SHA256.HashData(initial));
            Assert.Equal(fingerprint, state.CharacterVitals!.InitOutSha256);
            Assert.Equal(fingerprint, state.Economy!.InitOutSha256);
            Assert.Equal(fingerprint, state.InputConfiguration!.InitOutSha256);
            Assert.Equal(fingerprint, state.LimitProgress!.InitOutSha256);
            Assert.Equal(fingerprint, state.ShotAvailability!.InitOutSha256);
            Assert.Equal(fingerprint, state.CharacterAppearance!.InitOutSha256);
            Assert.Equal(NativeInputConfiguration.OriginalInitHash, state.SavedBattleFlags!.InitOutSha256);
            Assert.Equal(NativeInputConfiguration.OriginalInitHash, state.KnownMagic!.InitOutSha256);
            Assert.Equal(NativeInputConfiguration.OriginalInitHash, state.ScannedModels!.InitOutSha256);
            Assert.Equal(NativeInputConfiguration.OriginalInitHash, state.PocketStation!.InitOutSha256);
            Assert.Equal((byte)0, state.SavedBattleFlags.Value);
            Assert.All(state.KnownMagic.Words, word => Assert.Equal(0u, word));
            Assert.All(state.ScannedModels.Words, word => Assert.Equal(0u, word));
            Assert.All(state.PocketStation.Raw, value => Assert.Equal((byte)0, value));
            Assert.Null(state.EventState);
        }
    }

    private static byte[] Template(int length)
    {
        byte[] initial = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(initial.AsSpan(2708), 0x00020001);
        for (int index = 0; index < 12; index++)
        {
            initial[2712 + index] = (byte)(index + 1);
        }
        for (int index = 0; index < 32; index++)
        {
            initial[2772 + index] = (byte)index;
        }
        return initial;
    }
}
