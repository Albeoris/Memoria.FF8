using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattlePcPaletteShiftMigrationTests
{
    [Fact]
    public void Explicitly_migrate_only_the_three_observed_palette_shift_owners_in_the_named_callback92_checkpoint()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SHIFT_MIGRATION_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SHIFT_MIGRATION_OUTPUT");
        Assert.SkipUnless(input is not null && output is not null, "Explicit original and separately named palette-shift migration paths are required.");
        Assert.NotEqual(Path.GetFullPath(input!), Path.GetFullPath(output!));
        Assert.False(File.Exists(output!), "Preserve existing standalone snapshots; choose a distinct output path.");
        byte[] source = File.ReadAllBytes(input!);
        const string sourceHash = "83AE4D5B43082F2C1740E7268106D4F3C02C1335CFD196F6A31864CD2A72D973";
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(source)));
        JsonObject document = JsonNode.Parse(source)!.AsObject();
        Assert.Equal(12, document["Frame"]!.GetValue<int>());
        Assert.Equal(92, document["NativeGraphics"]!["Callback"]!.GetValue<int>());
        Assert.Equal(94, document["Definition"]!["EncounterId"]!.GetValue<int>());
        Assert.Equal(31, document["Definition"]!["StageId"]!.GetValue<int>());
        JsonObject original = document.DeepClone().AsObject();
        foreach (JsonObject color in Colors(document))
        {
            Assert.False(color["SubtractUsesNormalizedColor"]!.GetValue<bool>());
            Assert.False(color.ContainsKey("AlternatePaletteShift"));
            //This named fresh diagnostic starts from authenticated PE zero1CA89E8 and never executes45B540's field285 transition.
            color["AlternatePaletteShift"] = 0;
        }
        JsonObject comparison = document.DeepClone().AsObject();
        foreach (JsonObject color in Colors(comparison))
        {
            color.Remove("AlternatePaletteShift");
        }
        Assert.True(JsonNode.DeepEquals(original, comparison), "Migration changed more than the three proved retained palette-shift owners.");
        BattleSnapshot migrated = JsonSerializer.Deserialize<BattleSnapshot>(document)!;
        Assert.Equal((byte?)0, migrated.Definition.NativeRetainedMemory!.EffectMemory!.PrimitiveProcess!.Color!.AlternatePaletteShift);
        Assert.Equal((byte?)0, migrated.NativeEffectMemory!.PrimitiveProcess!.Color!.AlternatePaletteShift);
        Assert.Equal((byte?)0, migrated.NativeGraphics!.PrimitiveFrame!.LastColorState!.AlternatePaletteShift);
        BattleSession restored = BattleSession.Restore(migrated);
        Assert.Equal(JsonSerializer.Serialize(migrated), JsonSerializer.Serialize(restored.Capture()));
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, document);
        }
        Assert.Equal(sourceHash, Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input!))));
        Assert.Equal(12, restored.Frame);
        Assert.Equal(92, restored.NativeGraphics!.Callback);
        Console.WriteLine($"Migrated standalone snapshot: {Path.GetFullPath(output!)}; SHA256={Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(output!)))}; exact changes: three AlternatePaletteShift absent->0; frame12/callback92 unchanged; typed load/capture passed.");
    }

    private static JsonObject[] Colors(JsonObject document) =>
    [
        document["Definition"]!["NativeRetainedMemory"]!["EffectMemory"]!["PrimitiveProcess"]!["Color"]!.AsObject(),
        document["NativeEffectMemory"]!["PrimitiveProcess"]!["Color"]!.AsObject(),
        document["NativeGraphics"]!["PrimitiveFrame"]!["LastColorState"]!.AsObject()
    ];

    [Fact]
    public void Continue_the_named_palette_shift_checkpoint_one_native_callback_on_independent_clones()
    {
        string? input = Environment.GetEnvironmentVariable("MEMORIA_FF8_PC_SHIFT_VALIDATION_SNAPSHOT");
        Assert.SkipUnless(input is not null, "A separately published palette-shift migration snapshot is required.");
        byte[] source = File.ReadAllBytes(input!);
        BattleSnapshot snapshot = JsonSerializer.Deserialize<BattleSnapshot>(source)!;
        Assert.Equal(12, snapshot.Frame);
        Assert.Equal(92, snapshot.NativeGraphics!.Callback);
        Assert.Equal((byte?)0, snapshot.NativeGraphics.PrimitiveFrame!.LastColorState!.AlternatePaletteShift);
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession independent = BattleSession.Restore(snapshot);
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback);
        independent.AdvanceNativeGraphicsPresentation(callback);
        Assert.Equal(13, first.Frame);
        Assert.Equal(93, first.NativeGraphics!.Callback);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(independent.Capture()));
        Assert.Equal(source, File.ReadAllBytes(input!));
        Console.WriteLine("Independent cloned battle frame12->13/native callback92->93 continuation passed; published standalone frame12/callback92 bytes remained unchanged.");
    }
}
