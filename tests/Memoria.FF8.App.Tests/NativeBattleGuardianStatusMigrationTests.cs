using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleGuardianStatusMigrationTests
{
    [Fact]
    public void Add_original_enemy_resistances_to_the_named_fixture_and_validate_contact_continuation()
    {
        string? source = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUARDIAN_STATUS_SOURCE");
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_GUARDIAN_STATUS_OUTPUT");
        Assert.SkipUnless(source is not null && output is not null, "Select a standalone guardian fixture and a distinct migrated output.");
        Assert.NotEqual(Path.GetFullPath(source!), Path.GetFullPath(output!));
        byte[] originalBytes = File.ReadAllBytes(source!);
        JsonNode original = JsonNode.Parse(originalBytes)!;
        JsonNode migrated = original.DeepClone();
        JsonObject definition = migrated["Definition"]!["Combatants"]!.AsArray().Single(actor => actor!["Id"]!.GetValue<string>() == "enemy")!.AsObject();
        JsonObject status = migrated["Combatants"]!.AsArray().Single(actor => actor!["Id"]!.GetValue<string>() == "enemy")!["NativeStatus"]!.AsObject();
        Assert.Empty(definition["NativeStatusResistances"]!.GetValue<string>());
        Assert.Empty(status["Resistances"]!.GetValue<string>());
        using BattleAssets assets = new(Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR") ?? TestResources.GameDirectory);
        byte[] resistance = assets.ReadEnemyNativeStatusData(definition["ModelId"]!.GetValue<int>()).Resistances.ToArray();
        Assert.Equal(40, resistance.Length);
        definition["NativeStatusResistances"] = Convert.ToBase64String(resistance);
        status["Resistances"] = Convert.ToBase64String(resistance);
        JsonNode reverted = migrated.DeepClone();
        reverted["Definition"]!["Combatants"]!.AsArray().Single(actor => actor!["Id"]!.GetValue<string>() == "enemy")!["NativeStatusResistances"] = "";
        reverted["Combatants"]!.AsArray().Single(actor => actor!["Id"]!.GetValue<string>() == "enemy")!["NativeStatus"]!["Resistances"] = "";
        Assert.True(JsonNode.DeepEquals(original, reverted));
        BattleSnapshot snapshot = migrated.Deserialize<BattleSnapshot>()!;
        BattleSession first = BattleSession.Restore(snapshot);
        BattleSession second = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(snapshot))!);
        Assert.Equal(JsonSerializer.Serialize(snapshot), JsonSerializer.Serialize(first.Capture()));
        TimeSpan callback = TimeSpan.FromTicks((TimeSpan.TicksPerSecond + 29) / 30);
        first.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        second.AdvanceNativeGraphicsPresentation(callback, TimeSpan.Zero);
        Assert.Equal(snapshot.Frame + 1, first.Frame);
        Assert.Equal(JsonSerializer.Serialize(first.Capture()), JsonSerializer.Serialize(second.Capture()));
        BattleScenePresentation scene = new(assets, snapshot.Definition, true);
        byte[] pixels = scene.Render(first, 320, 224).ToArray();
        Assert.Equal(pixels, new BattleScenePresentation(assets, snapshot.Definition, true).Render(second, 320, 224).ToArray());
        string directory = Path.GetDirectoryName(Path.GetFullPath(output!))!;
        Directory.CreateDirectory(directory);
        using (FileStream stream = new(output!, FileMode.CreateNew, FileAccess.Write))
        {
            JsonSerializer.Serialize(stream, migrated);
        }
        NativeBattleRestorationPresentationTests.Publish(directory, "after-status-contact", first.Capture(), pixels);
        Assert.Equal(SHA256.HashData(originalBytes), SHA256.HashData(File.ReadAllBytes(source!)));
    }
}
