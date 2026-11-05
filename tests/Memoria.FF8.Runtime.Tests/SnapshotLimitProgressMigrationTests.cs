using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime.Tests;

public sealed class SnapshotLimitProgressMigrationTests
{
    private static readonly JsonSerializerOptions Options = new() { Converters = { new JsonStringEnumConverter(), new ImmutableBytesJsonConverter() } };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resource_free_parse_cannot_promote_unresolved_historical_features_or_replace_a_save(bool wholeOwner)
    {
        JsonObject saved = Historical(8);
        if (wholeOwner)
        {
            saved.Remove("LimitProgress");
        }
        else
        {
            saved["LimitProgress"]!.AsObject().Remove("PetPalsFlags");
        }
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));
        GameState parsed = SnapshotDefaults.Deserialize(stream, Options);
        Assert.True(parsed.RequiresLegacyLimitInitialization);
        Assert.Equal(GameState.CurrentSnapshotSchemaVersion, parsed.SnapshotSchemaVersion);
        Assert.Throws<InvalidOperationException>(() => GameplaySaveProgression.Capture(parsed));
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".save");
        byte[] original = [1, 2, 3, 4];
        File.WriteAllBytes(path, original);
        try
        {
            GameSession session = new(parsed);
            Assert.Throws<InvalidOperationException>(() => session.Save(path, compact: false));
            Assert.Throws<InvalidOperationException>(() => session.Save(path, compact: true));
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.False(File.Exists(path + ".tmp"));
            GameState prepared = Deserialize(saved, Initial);
            Assert.False(prepared.RequiresLegacyLimitInitialization);
            Assert.Equal(Initial().PetPalsFlags, prepared.LimitProgress!.PetPalsFlags);
            Assert.False(Deserialize(JsonSerializer.SerializeToNode(prepared, Options)!.AsObject(), UnexpectedInitialization).RequiresLegacyLimitInitialization);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    public void WhollyUntrackedHistoricalOwnerInitializesEveryFeatureWithoutChangingRepresentedState(int version)
    {
        JsonObject saved = Historical(version);
        saved.Remove("LimitProgress");
        string original = saved.ToJsonString();
        int initializations = 0;

        GameState restored = Deserialize(saved, () =>
        {
            initializations++;
            return Initial();
        });

        Assert.Equal(1, initializations);
        Assert.Equal(JsonSerializer.Serialize(Initial(), Options), JsonSerializer.Serialize(restored.LimitProgress, Options));
        Assert.Equal(original, saved.ToJsonString());
        JsonObject withoutAddition = JsonSerializer.SerializeToNode(restored, Options)!.AsObject();
        withoutAddition.Remove("LimitProgress");
        JsonObject expected = Historical(GameState.CurrentSnapshotSchemaVersion);
        expected.Remove("LimitProgress");
        Assert.True(JsonNode.DeepEquals(expected, withoutAddition));
    }

    [Theory]
    [InlineData("BlueMagicFlags")]
    [InlineData("DuelFlags")]
    [InlineData("SpecialMagicFlags")]
    [InlineData("Options978")]
    public void IndependentlyUntrackedFeatureDefaultsWithoutReplacingOtherProgress(string missing)
    {
        JsonObject saved = Historical(8);
        saved["LimitProgress"] = JsonSerializer.SerializeToNode(Progress(), Options);
        JsonObject expected = saved["LimitProgress"]!.DeepClone().AsObject();
        saved["LimitProgress"]!.AsObject().Remove(missing);
        expected[missing] = JsonSerializer.SerializeToNode(Initial(), Options)![missing]!.DeepClone();

        GameState restored = Deserialize(saved, Initial);

        Assert.True(JsonNode.DeepEquals(expected, JsonSerializer.SerializeToNode(restored.LimitProgress, Options)));
    }

    [Fact]
    public void CurrentFieldOwnerRecoversRootWithoutResourceAccessOrOriginalSaveFallback()
    {
        JsonObject saved = Historical(8);
        saved.Remove("LimitProgress");
        saved["EventState"] = new JsonObject { ["LimitProgress"] = JsonSerializer.SerializeToNode(Progress()) };
        saved["OriginalSave"] = JsonSerializer.SerializeToNode(Original(), Options);

        GameState restored = Deserialize(saved, () => throw new InvalidOperationException("The current field contains every feature."));

        Assert.Equal(JsonSerializer.Serialize(Progress(), Options), JsonSerializer.Serialize(restored.LimitProgress, Options));
    }

    [Fact]
    public void MissingCurrentFieldOwnerCopiesAllRootFeaturesWithEmbeddedArrayEncoding()
    {
        JsonObject saved = Historical(8);
        saved["LimitProgress"] = JsonSerializer.SerializeToNode(Progress(), Options);
        saved["EventState"] = new JsonObject { ["Scene"] = new JsonObject() };

        GameState restored = Deserialize(saved, () => throw new InvalidOperationException("The root contains every feature."));

        NativeLimitProgress current = restored.EventState!.Value.GetProperty("LimitProgress").Deserialize<NativeLimitProgress>()!;
        Assert.Equal(JsonSerializer.Serialize(Progress(), Options), JsonSerializer.Serialize(current, Options));
        Assert.Equal(JsonValueKind.Array, restored.EventState.Value.GetProperty("LimitProgress").GetProperty("TrainingCounters").ValueKind);
    }

    [Fact]
    public void PendingOrdinaryFieldReloadUsesItsCurrentCheckpoint()
    {
        JsonObject saved = Historical(8);
        saved.Remove("LimitProgress");
        saved["DepartingFieldState"] = new JsonObject { ["TransitionRequested"] = true, ["Scene"] = new JsonObject { ["WorldTransitionRequest"] = null }, ["LimitProgress"] = JsonSerializer.SerializeToNode(Progress()) };

        GameState restored = Deserialize(saved, () => throw new InvalidOperationException("The pending field checkpoint is current."));

        Assert.Equal(JsonSerializer.Serialize(Progress(), Options), JsonSerializer.Serialize(restored.LimitProgress, Options));
    }

    [Fact]
    public void RetainedOriginalSuppliesMissingFeaturesWithoutTreatingFrozenWorldCopiesAsCurrent()
    {
        JsonObject saved = Historical(8);
        saved["Scene"] = "WorldMap";
        saved.Remove("LimitProgress");
        saved["OriginalSave"] = JsonSerializer.SerializeToNode(Original(), Options);
        saved["EventState"] = new JsonObject { ["LimitProgress"] = JsonSerializer.SerializeToNode(Progress()) };
        saved["DepartingFieldState"] = saved["EventState"]!.DeepClone();
        string frozen = saved["EventState"]!.ToJsonString();

        GameState restored = Deserialize(saved, () => throw new InvalidOperationException("Original saved bytes contain every feature."));

        NativeLimitProgress limits = restored.LimitProgress!;
        Assert.Equal((ushort)0x1234, limits.BlueMagicFlags);
        Assert.Equal((ushort)0x5678, limits.DuelFlags);
        Assert.Equal((byte?)0x25, limits.SpecialMagicFlags);
        Assert.Equal((byte)0x35, limits.AngeloFlags);
        Assert.Equal((byte?)0x77, limits.PetPalsFlags);
        Assert.Equal((byte)5, limits.TrainingSelection);
        Assert.Equal(new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }, limits.TrainingCounters.ToArray());
        Assert.Equal((ushort?)0x4321, limits.Options978);
        Assert.Equal(Initial().InitOutSha256, limits.InitOutSha256);
        Assert.Equal(frozen, saved["EventState"]!.ToJsonString());
        Assert.True(JsonElement.DeepEquals(restored.EventState!.Value, restored.DepartingFieldState!.Value));
    }

    [Fact]
    public void FrozenWorldPartyHistoryDoesNotSeedMissingCurrentProgress()
    {
        JsonObject saved = Historical(8);
        saved["Scene"] = "WorldMap";
        saved.Remove("LimitProgress");
        saved["DepartingFieldState"] = new JsonObject { ["LimitProgress"] = JsonSerializer.SerializeToNode(Progress()) };

        GameState restored = Deserialize(saved, Initial);

        Assert.Equal(JsonSerializer.Serialize(Initial(), Options), JsonSerializer.Serialize(restored.LimitProgress, Options));
        Assert.Equal(JsonSerializer.Serialize(Progress(), Options), JsonSerializer.Serialize(restored.DepartingFieldState!.Value.GetProperty("LimitProgress").Deserialize<NativeLimitProgress>(Options), Options));
    }

    [Fact]
    public void PresentFeatureValuesTakePrecedenceOverOriginalBytesAndDefaults()
    {
        JsonObject saved = Historical(8);
        JsonObject progress = JsonSerializer.SerializeToNode(Progress(), Options)!.AsObject();
        progress.Remove("DuelFlags");
        progress["SpecialMagicFlags"] = null;
        progress["Options978"] = 0;
        saved["LimitProgress"] = progress;
        saved["OriginalSave"] = JsonSerializer.SerializeToNode(Original(), Options);

        GameState restored = Deserialize(saved, () => throw new InvalidOperationException("Original bytes cover the only omitted feature."));

        Assert.Equal(Progress().BlueMagicFlags, restored.LimitProgress!.BlueMagicFlags);
        Assert.Equal((ushort)0x5678, restored.LimitProgress.DuelFlags);
        Assert.Null(restored.LimitProgress.SpecialMagicFlags);
        Assert.Equal((ushort?)0, restored.LimitProgress.Options978);
        Assert.Equal(Progress().AngeloFlags, restored.LimitProgress.AngeloFlags);
    }

    [Fact]
    public void CurrentUnknownOwnersAndActiveBattlesNeverReset()
    {
        JsonObject nullRoot = Historical(GameState.CurrentSnapshotSchemaVersion);
        nullRoot["LimitProgress"] = null;
        Assert.Null(Deserialize(nullRoot, UnexpectedInitialization).LimitProgress);
        JsonObject nullCurrent = Historical(GameState.CurrentSnapshotSchemaVersion);
        nullCurrent.Remove("LimitProgress");
        nullCurrent["EventState"] = new JsonObject { ["LimitProgress"] = null };
        Assert.Null(Deserialize(nullCurrent, UnexpectedInitialization).LimitProgress);
        JsonObject current = Historical(GameState.CurrentSnapshotSchemaVersion);
        current.Remove("LimitProgress");
        Assert.Null(Deserialize(current, UnexpectedInitialization).LimitProgress);
        JsonObject battle = Historical(8);
        battle.Remove("LimitProgress");
        battle["Battle"] = new JsonObject();
        Assert.Throws<JsonException>(() => Deserialize(battle, UnexpectedInitialization));
    }

    [Fact]
    public void MissingHistoricalOwnerContinuesMovementAndSurvivesTwoCurrentColdSaves()
    {
        JsonObject saved = Historical(8);
        saved.Remove("LimitProgress");
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-limit-owner-" + Guid.NewGuid().ToString("N"));
        string output = path + ".current";
        try
        {
            File.WriteAllText(path, saved.ToJsonString(Options));
            byte[] original = File.ReadAllBytes(path);
            GameState loaded = GameSession.Load(path, Initial);
            NativeMovementProgressResult progress = NativeMovementProgress.Sample(NativeEventMemory.FromDWords(new Dictionary<int, int> { [244] = 592 }), loaded.CharacterVitals, loaded.LimitProgress, [4, 255, 255], new(NativeInputConfiguration.OriginalInitHash, 0), 0);
            Assert.NotNull(progress.Limits);
            new GameSession(loaded with { LimitProgress = progress.Limits }).Save(output, compact: true);
            GameState first = GameSession.Load(output, UnexpectedInitialization);
            new GameSession(first).Save(output, compact: true);
            GameState second = GameSession.Load(output, UnexpectedInitialization);
            Assert.Equal(JsonSerializer.Serialize(first, Options), JsonSerializer.Serialize(second, Options));
            Assert.Equal(original, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(output);
            File.Delete(path);
        }
    }

    [Fact]
    public void NewlyRecoveredInitializationIdentityCannotBeMixedWithADifferentTemplate()
    {
        JsonObject saved = Historical(8);
        saved.Remove("LimitProgress");
        saved["CharacterVitals"] = null;
        JsonObject current = JsonSerializer.SerializeToNode(Progress() with { InitOutSha256 = new string('1', 64) })!.AsObject();
        current.Remove("DuelFlags");
        saved["EventState"] = new JsonObject { ["LimitProgress"] = current };
        string original = saved.ToJsonString();

        Assert.Throws<InvalidDataException>(() => SnapshotLimitProgressMigration.Apply(saved, Options, 8, Initial));

        Assert.Equal(original, saved.ToJsonString());
        Assert.False(saved.ContainsKey("LimitProgress"));
    }

    [Fact]
    public void CurrentUnknownRootDoesNotAddIncidentalCheckpointProperties()
    {
        JsonObject saved = Historical(GameState.CurrentSnapshotSchemaVersion);
        saved["LimitProgress"] = null;
        saved["EventState"] = new JsonObject { ["Scene"] = new JsonObject() };
        string original = saved.ToJsonString();

        SnapshotLimitProgressMigration.Apply(saved, Options, GameState.CurrentSnapshotSchemaVersion, UnexpectedInitialization);

        Assert.Equal(original, saved.ToJsonString());
    }

    [Fact]
    public void HistoricalWholeNullIsUninitializedWhileConstituentNullsRemainRepresented()
    {
        JsonObject saved = Historical(8);
        saved["LimitProgress"] = null;
        GameState initialized = Deserialize(saved, Initial);
        Assert.Equal(JsonSerializer.Serialize(Initial(), Options), JsonSerializer.Serialize(initialized.LimitProgress, Options));
        JsonObject current = JsonSerializer.SerializeToNode(Progress())!.AsObject();
        current["SpecialMagicFlags"] = null;
        current["PetPalsFlags"] = null;
        current["Options978"] = null;
        saved["EventState"] = new JsonObject { ["LimitProgress"] = current };
        GameState recovered = Deserialize(saved, UnexpectedInitialization);
        Assert.Equal(JsonSerializer.Serialize(current.Deserialize<NativeLimitProgress>(), Options), JsonSerializer.Serialize(recovered.LimitProgress, Options));
        saved["EventState"] = new JsonObject { ["LimitProgress"] = null };
        Assert.Equal(JsonSerializer.Serialize(Initial(), Options), JsonSerializer.Serialize(Deserialize(saved, Initial).LimitProgress, Options));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2147483647")]
    [InlineData("null")]
    [InlineData("\"8\"")]
    [InlineData("true")]
    public void InvalidVersionsRefuseBeforeAccessingResources(string version)
    {
        JsonObject saved = Historical(8);
        saved.Remove("LimitProgress");
        saved["SnapshotSchemaVersion"] = JsonNode.Parse(version);

        Assert.Throws<InvalidDataException>(() => Deserialize(saved, UnexpectedInitialization));
    }

    private static NativeLimitProgress Initial() => new(new string('0', 64), 1, 2, 0x11, 3, ImmutableArray.CreateRange(new byte[] { 0, 5, 6, 7, 0, 9, 10, 11 })) { SpecialMagicFlags = 1, PetPalsFlags = 0x19, Options978 = 0 };
    private static NativeLimitProgress Progress() => Initial() with { BlueMagicFlags = 0x8181, DuelFlags = 0x4242, SpecialMagicFlags = 0x35, Options978 = 0x1234, AngeloFlags = 0xA5, TrainingSelection = 7, TrainingCounters = ImmutableArray.CreateRange(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }), PetPalsFlags = 0xFE };
    private static NativeLimitProgress UnexpectedInitialization() => throw new InvalidOperationException("This represented state must not initialize.");

    private static JsonObject Historical(int version)
    {
        JsonObject saved = JsonSerializer.SerializeToNode(GameSession.CreateColdProcess().State with { Scene = SceneKind.Field, SnapshotSchemaVersion = version, CharacterVitals = MagicSystemFixture.Create() with { InitOutSha256 = Initial().InitOutSha256 }, LimitProgress = Initial() }, Options)!.AsObject();
        if (version == 0)
        {
            saved.Remove("SnapshotSchemaVersion");
        }
        return saved;
    }

    private static NativePcSaveData Original()
    {
        byte[] block = new byte[NativePcSaveData.BlockSize];
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(NativePcSaveData.SaveMapOffset + 2), 0x08FF);
        void W(int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(block.AsSpan(NativePcSaveData.MainOffset + offset), value);
        W(2756, 0x1234);
        W(2758, 0x5678);
        W(3280, 0x4321);
        block[NativePcSaveData.MainOffset + 2761] = 0x25;
        block[NativePcSaveData.MainOffset + 2762] = 0x35;
        block[NativePcSaveData.MainOffset + 2763] = 0x77;
        block[NativePcSaveData.MainOffset + 2747] = 5;
        new byte[] { 8, 7, 6, 5, 4, 3, 2, 1 }.CopyTo(block, NativePcSaveData.MainOffset + 2764);
        return new NativePcSaveData(block);
    }

    private static GameState Deserialize(JsonObject saved, Func<NativeLimitProgress> initialize)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(saved.ToJsonString(Options)));
        return SnapshotDefaults.Deserialize(stream, Options, initialize);
    }
}
