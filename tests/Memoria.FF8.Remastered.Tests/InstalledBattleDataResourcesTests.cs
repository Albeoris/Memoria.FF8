using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class InstalledBattleDataResourcesTests
{
    [Fact]
    public void SharedBattleDataReadersPreserveAuthenticatedRemasteredIdentity()
    {
        using GameAssets assets = new(InstalledDirectory());
        NativeBattleActionCameraRules camera = assets.ReadNativeActionCameraRules();
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, camera.ExecutableSha256);
        camera.Validate();
        Assert.Throws<InvalidDataException>(() => (camera with { ExecutableSha256 = new string('0', 64) }).Validate());
        NativeBattleGraphicsRules graphics = assets.ReadNativeBattleGraphicsRules();
        Assert.Equal(camera.ExecutableSha256, graphics.ExecutableSha256);
        graphics.Validate();
        Assert.Throws<InvalidDataException>(() => (graphics with { ExecutableSha256 = new string('0', 64) }).Validate());
        Assert.Equal(5, graphics.StatusIcons!.Entries.Length);
        Assert.Equal(160 * 7, graphics.ActorSounds1120!.Value.Length);
        Assert.Equal(0x27, assets.ReadNativeBattleCommandSequences().Length);
        Assert.Equal(43, assets.ReadNativeBattleEntryTransitionResources().SpecialEncounters.Length);
    }

    [Fact]
    public void BattleArchiveAndDispatchReadersUseRemasteredNativeTables()
    {
        using BattleAssets assets = new(InstalledDirectory());
        NativeBattlePrimitiveTables tables = assets.ReadNativePrimitiveTables();
        tables.Validate();
        Assert.Equal(0x4667C0u, assets.ReadNativeSoftwarePrimitiveTable().Handler(0x20));
        NativeBattleModelDatSource body = assets.ReadNativeModelDatSource(0, false, 0);
        Assert.NotEmpty(body.Bytes);
        Assert.InRange(body.Section(0), 0, body.Bytes.Length - 1);
        NativeBattleModelDatSource weapon = assets.ReadNativeModelDatSource(0, true, 0);
        Assert.NotEmpty(weapon.Bytes);
        Assert.InRange(weapon.Section(0), 0, weapon.Bytes.Length - 1);
        Assert.NotEmpty(assets.ReadNativeModelDatSource(16, false, 0).Bytes);
        Assert.NotEmpty(assets.ReadNativeBattleFile(checked((ushort)body.FileId)).Bytes);
    }

    [Fact]
    public void ClassicByteEntryPointsStillRequireAnAuthenticatedClassicImage()
    {
        Assert.Throws<NotSupportedException>(() => NativeBattleActionCameraRulesReader.Read(Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(() => NativeBattleGraphicsRulesReader.Read(Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(() => NativeBattleCommandSequenceReader.Read(Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(() => NativeBattleEntryTransitionResourcesReader.Read(Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(() => NativeBattlePrimitiveTablesReader.Read(Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(() => NativeSoftwarePrimitiveTableReader.Read(Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(() => NativeBattleShadowSourceReader.Read(Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(() => NativeBattleStatusIconResourcesReader.Read(Array.Empty<byte>()));
        Assert.Throws<NotSupportedException>(() => NativeMusicCatalog.ReadBattleActorSounds(Array.Empty<byte>()));
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        return directory!;
    }
}
