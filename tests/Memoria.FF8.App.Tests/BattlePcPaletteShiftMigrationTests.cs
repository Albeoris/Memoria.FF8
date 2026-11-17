using System.Security.Cryptography;
using System.Text.Json;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePcPaletteShiftMigrationTests
{
    [Fact]
    public void Archived_callback92_records_the_three_palette_shift_owners()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SHIFT_MIGRATION_SOURCE");
        Assert.SkipUnless(input is not null, "Select the original palette-shift fixture.");
        byte[] source = File.ReadAllBytes(input!);
        const string sourceHash = "83AE4D5B43082F2C1740E7268106D4F3C02C1335CFD196F6A31864CD2A72D973";
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(source)));
        using JsonDocument archive = JsonDocument.Parse(source);
        JsonElement logical = archive.RootElement;
        JsonElement graphics = NativeBattleFixtureState.ReadArchived<JsonElement>(logical, nameof(NativeBattleFixtureState.NativeGraphics));
        JsonElement effects = NativeBattleFixtureState.ReadArchived<JsonElement>(logical, nameof(NativeBattleFixtureState.NativeEffectMemory));
        Assert.Equal(12, logical.GetProperty("Frame").GetInt32());
        Assert.Equal(92, graphics.GetProperty("Callback").GetInt32());
        Assert.Equal(94, logical.GetProperty("Definition").GetProperty("EncounterId").GetInt32());
        Assert.Equal(31, logical.GetProperty("Definition").GetProperty("StageId").GetInt32());
        JsonElement retainedColor = logical.GetProperty("Definition").GetProperty("NativeRetainedMemory").GetProperty("EffectMemory").GetProperty("PrimitiveProcess").GetProperty("Color");
        JsonElement liveColor = effects.GetProperty("PrimitiveProcess").GetProperty("Color");
        JsonElement drawnColor = graphics.GetProperty("PrimitiveFrame").GetProperty("LastColorState");
        Assert.All(new[] { retainedColor, liveColor, drawnColor }, color =>
        {
            Assert.False(color.GetProperty("SubtractUsesNormalizedColor").GetBoolean());
            Assert.False(color.TryGetProperty("AlternatePaletteShift", out _));
        });
        Assert.Equal(source, File.ReadAllBytes(input!));
    }
}
