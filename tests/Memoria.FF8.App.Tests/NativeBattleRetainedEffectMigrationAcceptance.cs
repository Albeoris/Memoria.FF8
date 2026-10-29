using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

internal static class NativeBattleRetainedEffectMigrationAcceptance
{
    private sealed record Contract(string Owner, Func<object> Cold, Func<object> Unknown);

    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };

    internal static BattleSnapshot Prepare(BattleSnapshot source, ushort effect, string output)
    {
        if (Environment.GetEnvironmentVariable("MEMORIA_FF8_VERIFY_EFFECT_SOURCE_MIGRATION") != "1")
        {
            return source;
        }
        Contract contract = ForEffect(effect);
        Assert.True(SnapshotRetainedEffectMigration.Owns(contract.Owner));
        BattleSnapshot resolved = BattleSession.Restore(source).Capture();
        BattleSnapshot owned = NativeBattleStorageSnapshots.Restore(resolved with { Definition = null! }) with { Definition = resolved.Definition };
        NativeBattleEffectMemory originalMemory = owned.NativeEffectMemory ?? throw new InvalidDataException("Source acceptance requires its retained effect memory.");
        PropertyInfo property = typeof(NativeBattleEffectMemory).GetProperty(contract.Owner)!;
        object original = property.GetValue(originalMemory) ?? throw new InvalidDataException("Source acceptance requires its selected retained effect owner.");
        JsonNode originalState = Capture(original);
        JsonNode coldState = Capture(contract.Cold());
        bool cold = JsonNode.DeepEquals(originalState, coldState);

        JsonObject carrier = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State, Options)!.AsObject();
        carrier["SnapshotSchemaVersion"] = 3;
        carrier["NativeBattleMemory"]!["EffectMemory"]![contract.Owner] = cold ? Capture(contract.Unknown()) : originalState.DeepClone();
        using MemoryStream input = new(Encoding.UTF8.GetBytes(carrier.ToJsonString(Options)));
        GameState migrated = SnapshotDefaults.Deserialize(input, Options);
        Assert.Equal(GameState.CurrentSnapshotSchemaVersion, migrated.SnapshotSchemaVersion);
        object migratedSnapshot = property.GetValue(migrated.NativeBattleMemory!.EffectMemory!)!;
        Assert.True(JsonNode.DeepEquals(cold ? coldState : originalState, Capture(migratedSnapshot)), contract.Owner);

        BattleSnapshot result = resolved;
        if (cold)
        {
            NativeBattleRetainedMemory migratedMemory = NativeBattleStorageSnapshots.Restore(migrated.NativeBattleMemory!);
            object replacement = property.GetValue(migratedMemory.EffectMemory!)!;
            NativeBattleEffectMemory graft = (NativeBattleEffectMemory)typeof(NativeBattleEffectMemory).GetMethod("<Clone>$", BindingFlags.Public | BindingFlags.Instance)!.Invoke(originalMemory, null)!;
            property.SetValue(graft, replacement);
            BattleSnapshot detached = NativeBattleStorageSnapshots.Capture(owned with { NativeEffectMemory = graft });
            result = BattleSession.Restore(detached).Capture();
        }

        JsonNode before = JsonSerializer.SerializeToNode(resolved, Options)!;
        JsonNode after = JsonSerializer.SerializeToNode(result, Options)!;
        before["NativeEffectMemory"]!.AsObject().Remove(contract.Owner);
        after["NativeEffectMemory"]!.AsObject().Remove(contract.Owner);
        RemoveGraphMetadata(before);
        RemoveGraphMetadata(after);
        Assert.True(JsonNode.DeepEquals(before, after), "Migration acceptance changed state outside the selected retained owner.");
        BattleSnapshot verified = NativeBattleStorageSnapshots.Restore(result with { Definition = null! }) with { Definition = result.Definition };
        Assert.True(JsonNode.DeepEquals(cold ? coldState : originalState, Capture(property.GetValue(verified.NativeEffectMemory!)!)), contract.Owner);

        Directory.CreateDirectory(output);
        using FileStream receipt = new(Path.Combine(output, $"effect-{effect}-source-migration.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(receipt, new { Effect = effect, SourceSchema = 3, TargetSchema = migrated.SnapshotSchemaVersion, Mode = cold ? "cold-placeholder" : "warm-preserved" });
        return result;
    }

    private static JsonNode Capture(object state) => JsonSerializer.SerializeToNode(NativeBattleStorageSnapshots.Capture(state), state.GetType(), Options)!;

    private static void RemoveGraphMetadata(JsonNode node)
    {
        if (node is JsonObject owner)
        {
            owner.Remove("StorageReference");
            owner.Remove("StorageGraph");
            foreach (JsonNode? child in owner.Select(pair => pair.Value))
            {
                if (child is not null)
                {
                    RemoveGraphMetadata(child);
                }
            }
        }
        else if (node is JsonArray array)
        {
            foreach (JsonNode? child in array)
            {
                if (child is not null)
                {
                    RemoveGraphMetadata(child);
                }
            }
        }
    }

    private static Contract Owner<T>(string name, Func<T> cold, Func<T> unknown) where T : class => new(name, () => cold(), () => unknown());

    private static Contract ForEffect(ushort effect) => effect switch
    {
        137 => Owner<NativeBattleOilBlast137State>("OilBlast137", NativeBattleOilBlast137State.FromColdProcess, NativeBattleOilBlast137State.FromUnknown),
        131 => Owner<NativeBattleBadBreath131State>("BadBreath131", NativeBattleBadBreath131State.FromColdProcess, NativeBattleBadBreath131State.FromUnknown),
        138 => Owner<NativeBattleSaliva138State>("Saliva138", NativeBattleSaliva138State.FromColdProcess, NativeBattleSaliva138State.FromUnknown),
        48 => Owner<NativeBattleMagmaBreath48State>("MagmaBreath48", NativeBattleMagmaBreath48State.FromColdProcess, NativeBattleMagmaBreath48State.FromUnknown),
        12 => Owner<NativeBattleBladeShot12State>("BladeShot12", NativeBattleBladeShot12State.FromColdProcess, NativeBattleBladeShot12State.FromUnknown),
        136 => Owner<NativeBattleOilShot136State>("OilShot136", NativeBattleOilShot136State.FromColdProcess, NativeBattleOilShot136State.FromUnknown),
        126 => Owner<NativeBattleElectricDischarge126State>("ElectricDischarge126", NativeBattleElectricDischarge126State.FromColdProcess, NativeBattleElectricDischarge126State.FromUnknown),
        213 => Owner<NativeBattleEffect213State>("Effect213", NativeBattleEffect213State.FromColdProcess, NativeBattleEffect213State.FromUnknown),
        301 => Owner<NativeBattleEffect301State>("Effect301", NativeBattleEffect301State.FromColdProcess, NativeBattleEffect301State.FromUnknown),
        290 => Owner<NativeBattleEffect290State>("Effect290", NativeBattleEffect290State.FromColdProcess, NativeBattleEffect290State.FromUnknown),
        54 => Owner<NativeBattleEffect54State>("Effect54", NativeBattleEffect54State.FromColdProcess, NativeBattleEffect54State.FromUnknown),
        320 => Owner<NativeBattleEffect320State>("Effect320", NativeBattleEffect320State.FromColdProcess, NativeBattleEffect320State.FromUnknown),
        308 => Owner<NativeBattleEffect308State>("Effect308", NativeBattleEffect308State.FromColdProcess, NativeBattleEffect308State.FromUnknown),
        251 => Owner<NativeBattleEffect251State>("Effect251", NativeBattleEffect251State.FromColdProcess, NativeBattleEffect251State.FromUnknown),
        83 => Owner<NativeBattleEffect83State>("Effect83", NativeBattleEffect83State.FromColdProcess, NativeBattleEffect83State.FromUnknown),
        79 => Owner<NativeBattleEffect79State>("Effect79", NativeBattleEffect79State.FromColdProcess, NativeBattleEffect79State.FromUnknown),
        80 => Owner<NativeBattleEffect80State>("Effect80", NativeBattleEffect80State.FromColdProcess, NativeBattleEffect80State.FromUnknown),
        68 => Owner<NativeBattleEffect68State>("Effect68", NativeBattleEffect68State.FromColdProcess, NativeBattleEffect68State.FromUnknown),
        345 => Owner<NativeBattleEffect345State>("Effect345", NativeBattleEffect345State.FromColdProcess, NativeBattleEffect345State.FromUnknown),
        _ => throw new ArgumentOutOfRangeException(nameof(effect), effect, "Source migration acceptance requires a registered retained effect owner.")
    };
}
