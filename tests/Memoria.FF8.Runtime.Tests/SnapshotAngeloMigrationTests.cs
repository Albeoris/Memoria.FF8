using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotAngeloMigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };
    private static readonly string[] AngeloProperties = ["AngeloFlags", "TrainingSelection", "TrainingCounters", "PetPalsFlags"];

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    public void HistoricalAngeloOmissionsUseOneCoherentTemplateAndPreserveOtherLimitProgress(int schema)
    {
        JsonObject saved = Historical();
        if (schema == 0)
        {
            saved.Remove("SnapshotSchemaVersion");
        }
        else
        {
            saved["SnapshotSchemaVersion"] = schema;
        }
        JsonObject limits = saved["LimitProgress"]!.AsObject();
        foreach (string name in AngeloProperties)
        {
            limits.Remove(name);
        }
        string original = saved.ToJsonString();
        int initializations = 0;
        GameState restored = Deserialize(saved, () =>
        {
            initializations++;
            return Initial();
        });

        Assert.Equal(1, initializations);
        Assert.Equal(Initial().AngeloFlags, restored.LimitProgress!.AngeloFlags);
        Assert.Equal(Initial().TrainingSelection, restored.LimitProgress.TrainingSelection);
        Assert.Equal(Initial().TrainingCounters.ToArray(), restored.LimitProgress.TrainingCounters.ToArray());
        Assert.Equal(Initial().PetPalsFlags, restored.LimitProgress.PetPalsFlags);
        AssertUnrelatedProgress(restored.LimitProgress);
        Assert.Equal(original, saved.ToJsonString());
        Assert.Equal(GameState.CurrentSnapshotSchemaVersion, restored.SnapshotSchemaVersion);
        Assert.Equal((ushort)0, NativeBattleLimitRules.AngeloActionId(1, (byte)(restored.LimitProgress.AngeloFlags >> 4)));
    }

    [Theory]
    [InlineData("AngeloFlags")]
    [InlineData("TrainingSelection")]
    [InlineData("TrainingCounters")]
    [InlineData("PetPalsFlags")]
    public void SingleOmissionPreservesEveryRepresentedAngeloValue(string missing)
    {
        JsonObject saved = Historical();
        JsonObject expected = saved["LimitProgress"]!.DeepClone().AsObject();
        saved["LimitProgress"]!.AsObject().Remove(missing);
        expected[missing] = JsonSerializer.SerializeToNode(Initial(), Options)![missing]!.DeepClone();

        GameState restored = Deserialize(saved, Initial);

        Assert.True(JsonNode.DeepEquals(expected, JsonSerializer.SerializeToNode(restored.LimitProgress, Options)));
    }

    [Fact]
    public void CurrentFieldCopiesRecoverComplementarySavedValuesWithoutReadingTheTemplate()
    {
        JsonObject saved = Historical();
        saved["Scene"] = "Field";
        JsonObject current = saved["LimitProgress"]!.DeepClone().AsObject();
        saved["EventState"] = new JsonObject { ["LimitProgress"] = current };
        saved["LimitProgress"]!.AsObject().Remove("AngeloFlags");
        current.Remove("TrainingCounters");

        GameState restored = Deserialize(saved, () => throw new InvalidOperationException("Saved counterpart already contains the value."));

        Assert.Equal((byte)0xA5, restored.LimitProgress!.AngeloFlags);
        JsonElement currentJson = restored.EventState!.Value.GetProperty("LimitProgress");
        Assert.Equal(JsonValueKind.Array, currentJson.GetProperty("TrainingCounters").ValueKind);
        NativeLimitProgress currentLimits = currentJson.Deserialize<NativeLimitProgress>()!;
        Assert.Equal(restored.LimitProgress.TrainingCounters.ToArray(), currentLimits.TrainingCounters.ToArray());
        Assert.Equal(JsonSerializer.Serialize(restored.LimitProgress, Options), JsonSerializer.Serialize(currentLimits, Options));
    }

    [Fact]
    public void FrozenWorldDepartureDoesNotReplaceCurrentProgress()
    {
        JsonObject saved = Historical();
        saved["Scene"] = "WorldMap";
        JsonObject departure = saved["LimitProgress"]!.DeepClone().AsObject();
        departure["AngeloFlags"] = 255;
        departure.Remove("TrainingCounters");
        saved["DepartingFieldState"] = new JsonObject { ["LimitProgress"] = departure };
        saved["LimitProgress"]!.AsObject().Remove("AngeloFlags");

        SnapshotLimitProgressMigration.Apply(saved, Options, 8, Initial);

        Assert.Equal(Initial().AngeloFlags, saved["LimitProgress"]!["AngeloFlags"]!.GetValue<byte>());
        Assert.Equal(255, departure["AngeloFlags"]!.GetValue<int>());
        Assert.IsType<JsonArray>(departure["TrainingCounters"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CurrentOrBattleOmissionsDoNotReadOrApplyInitialDefaults(bool battle)
    {
        JsonObject saved = Historical();
        saved["LimitProgress"]!.AsObject().Remove("AngeloFlags");
        if (battle)
        {
            saved["Battle"] = new JsonObject();
        }
        else
        {
            saved["SnapshotSchemaVersion"] = GameState.CurrentSnapshotSchemaVersion;
        }
        Assert.Throws<JsonException>(() => Deserialize(saved, () => throw new InvalidOperationException("Initialization is not authorized for this input.")));
    }

    [Fact]
    public void ExplicitNullZeroAndPartialArrayAreNeverReplaced()
    {
        JsonObject saved = Historical();
        saved["LimitProgress"]!["AngeloFlags"] = 0;
        saved["LimitProgress"]!["TrainingSelection"] = 0;
        saved["LimitProgress"]!["TrainingCounters"] = new JsonArray(0);
        saved["LimitProgress"]!["PetPalsFlags"] = null;

        GameState restored = Deserialize(saved, () => throw new InvalidOperationException("No property is absent."));

        Assert.Equal((byte)0, restored.LimitProgress!.AngeloFlags);
        Assert.Equal((byte)0, restored.LimitProgress.TrainingSelection);
        Assert.Equal(new byte[] { 0 }, restored.LimitProgress.TrainingCounters.ToArray());
        Assert.Null(restored.LimitProgress.PetPalsFlags);
        Assert.Throws<InvalidDataException>(restored.LimitProgress.Validate);
        saved["LimitProgress"] = null;
        Assert.Null(Deserialize(saved, () => throw new InvalidOperationException("An explicit null owner is not an omitted Angelo feature.")).LimitProgress);
    }

    [Theory]
    [InlineData("AngeloFlags")]
    [InlineData("TrainingSelection")]
    [InlineData("TrainingCounters")]
    public void ExplicitNullRequiredFieldsStillFail(string property)
    {
        JsonObject saved = Historical();
        saved["LimitProgress"]![property] = null;
        Assert.Throws<JsonException>(() => Deserialize(saved, () => throw new InvalidOperationException("An explicit null is not absent.")));
    }

    [Fact]
    public void ContradictorySavedOwnersRefuseWithoutPartiallyChangingOwners()
    {
        JsonObject saved = Historical();
        saved["Scene"] = nameof(SceneKind.Field);
        saved["LimitProgress"]!.AsObject().Remove("AngeloFlags");
        JsonObject departure = saved["LimitProgress"]!.DeepClone().AsObject();
        departure["InitOutSha256"] = new string('1', 64);
        saved["EventState"] = new JsonObject { ["LimitProgress"] = departure };
        string original = saved.ToJsonString();

        Assert.Throws<InvalidDataException>(() => SnapshotLimitProgressMigration.Apply(saved, Options, 8, Initial));

        Assert.Equal(original, saved.ToJsonString());
    }

    [Fact]
    public void ResourceAwareReadPreservesSourceAndWritesCurrentIdempotentState()
    {
        JsonObject saved = Historical();
        saved["LimitProgress"]!.AsObject().Remove("TrainingCounters");
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-angelo-source-" + Guid.NewGuid().ToString("N"));
        string current = path + ".current";
        try
        {
            File.WriteAllText(path, saved.ToJsonString(Options));
            byte[] original = File.ReadAllBytes(path);
            Assert.Throws<JsonException>(() => GameSession.Load(path));
            GameState restored = GameSession.Load(path, Initial);
            AssertUnrelatedProgress(restored.LimitProgress!);
            NativeMovementProgressResult movement = NativeMovementProgress.Sample(NativeEventMemory.FromDWords(new Dictionary<int, int> { [244] = 592 }), MagicSystemFixture.Create(), restored.LimitProgress, [4, 255, 255], new(NativeInputConfiguration.OriginalInitHash, 0), 0);
            Assert.Equal(Initial().TrainingCounters[restored.LimitProgress!.TrainingSelection] - 1, movement.Limits!.TrainingCounters[restored.LimitProgress.TrainingSelection]);
            Assert.Equal(restored.LimitProgress.AngeloFlags, movement.Limits.AngeloFlags);
            new GameSession(restored with { LimitProgress = movement.Limits }).Save(current, compact: true);
            GameState first = GameSession.Load(current, () => throw new InvalidOperationException("Current saves need no initialization."));
            new GameSession(first).Save(current, compact: true);
            GameState second = GameSession.Load(current);
            Assert.Equal(JsonSerializer.Serialize(first, Options), JsonSerializer.Serialize(second, Options));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(current);
            File.Delete(path);
        }
    }

    private static NativeLimitProgress Initial() => new(new string('0', 64), 1, 2, 0x11, 3, ImmutableArray.CreateRange(new byte[] { 0, 5, 6, 7, 0, 9, 10, 11 })) { SpecialMagicFlags = 1, PetPalsFlags = 0x19, Options978 = 0 };

    private static JsonObject Historical()
    {
        NativeLimitProgress progress = Initial() with { BlueMagicFlags = 0x8181, DuelFlags = 0x4242, SpecialMagicFlags = 0x35, Options978 = 0x1234, AngeloFlags = 0xA5, TrainingSelection = 7, TrainingCounters = ImmutableArray.CreateRange(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }), PetPalsFlags = 0xFE };
        return JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State with { SnapshotSchemaVersion = 8, LimitProgress = progress }, Options)!.AsObject();
    }

    private static void AssertUnrelatedProgress(NativeLimitProgress progress)
    {
        Assert.Equal((ushort)0x8181, progress.BlueMagicFlags);
        Assert.Equal((ushort)0x4242, progress.DuelFlags);
        Assert.Equal((byte?)0x35, progress.SpecialMagicFlags);
        Assert.Equal((ushort?)0x1234, progress.Options978);
    }

    private static GameState Deserialize(JsonObject saved, Func<NativeLimitProgress> initialize)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));
        return SnapshotDefaults.Deserialize(stream, Options, initialize);
    }
}
