using System.Security.Cryptography;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Remastered.Tests;

public sealed class InstalledWorldNativeResourceTests
{
    [Fact]
    public void WorldTablesUseTheAuthenticatedGuestDataAndPreserveItsIdentity()
    {
        string directory = InstalledDirectory();
        NativeWorldForestTables installed = new(directory);
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, installed.SourceHash);
        Assert.Equal("FA4AB9025D0922FDAFA6993E1559F96CEB82C5C7DC59C53CAFFD501B56A13B00", Convert.ToHexString(SHA256.HashData(installed.Random)));
        Assert.Equal(11, installed.Effects.Count);
        Assert.Equal((short)-8, installed.Effects[6].VelocityY);
        Assert.Equal((short)-16, installed.Effects[10].VelocityY);
        using WorldAssets assets = new(directory);
        Assert.Equal(installed.Effects.ToArray(), assets.ForestTables.Effects.ToArray());
        NativeGardenCameraDefaults garden = assets.ReadGardenCameraDefaults();
        Assert.Equal(new NativeGardenCameraDefaults(NativeFieldRuntimeProfile.RemasteredEnImageHash, -112, 0, -341, -7168), garden);
        NativeRagnarokCameraDefaults ragnarok = assets.ReadRagnarokCameraDefaults();
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, ragnarok.ExecutableHash);
        ragnarok.Validate();
        Assert.Equal(new short[] { 7168, 6144, 5120, 4096, 3072 }, Enumerable.Range(0, 5).Select(index => assets.ReadWorldMessageSpeed((byte)index)));
        for (byte window = 0; window <= 12; window++)
        {
            assets.ReadNativeWorldMessage(window, 0).Validate();
        }
        Assert.Equal(new NativeWorldContactShape(740, 1024, 520, 3), assets.ReadNativeContactShape(1));
        Assert.Equal(new NativeWorldContactShape(1704, 567, 212, 1), assets.ReadNativeContactShape(88));
        foreach (NativeRagnarokTransferKind kind in new[] { NativeRagnarokTransferKind.Landing, NativeRagnarokTransferKind.Takeoff })
        {
            assets.ReadRagnarokTransferCameraTargets(kind, 0).Validate();
        }
    }

    [Fact]
    public void ScriptedRailRoundTripRetainsDatIdentity()
    {
        using WorldAssets assets = new(InstalledDirectory());
        NativeWorldScriptedPathDefinition definition = assets.ReadScriptedPath(12);
        Assert.Equal(NativeFieldRuntimeProfile.RemasteredEnImageHash, definition.ExecutableHash);
        Assert.Equal("189C03D03F48DA9467F629A0CC4B895FBA3CE6E6D5C2B80E76CC75812EB5A501", definition.ChunkHash);
        Assert.Equal("50820772483EBAC6FF5B7675E529085BE10DEEEAFE2161B5858AC9BFACC997C0", definition.CameraHash);
        NativeWorldScriptedPathDefinition restored = JsonSerializer.Deserialize<NativeWorldScriptedPathDefinition>(JsonSerializer.Serialize(definition))!;
        restored.Validate();
        Assert.Equal(definition.ExecutableHash, restored.ExecutableHash);
        Assert.Throws<InvalidDataException>(() => (restored with { ExecutableHash = new string('0', 64) }).Validate());
    }

    [Fact]
    public void WorldTableReadersRejectUnknownIdentityBeforeReadingBytes()
    {
        Assert.Throws<NotSupportedException>(() => new NativeWorldForestTables(new UnsupportedMemory()));
        Assert.Throws<NotSupportedException>(() => NativeGardenWorldAllocationReader.ReadCameraDefaults(new UnsupportedMemory()));
        Assert.Throws<NotSupportedException>(() => NativeGardenWorldAllocationReader.ReadCactuarLoadPosition(new UnsupportedMemory()));
        Assert.Throws<NotSupportedException>(() => NativeWorldScriptedPathReader.Read([], [], new UnsupportedMemory(), 0));
    }

    private static string InstalledDirectory()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_REMASTERED_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory), "Set MEMORIA_FF8_REMASTERED_DIR to run optional installed Remastered checks.");
        return directory!;
    }

    private sealed class UnsupportedMemory : IGameNativeMemorySource
    {
        public string SourceHash => new string('0', 64);
        public byte[] Read(uint address, int count) => throw new InvalidOperationException("Unsupported identities must be rejected before reading memory.");
        public byte[] ReadLoader(uint address, int count) => Read(address, count);
    }
}
