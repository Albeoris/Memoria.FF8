using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class GameplaySaveSessionLoaderTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Installed_semantic_document_loads_an_independent_process_preserving_progress_and_continuing(bool world)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = Document(assets, world);
        Assert.Null(document.Progression!.OriginalResidue);
        Assert.DoesNotContain("OriginalResidue", JsonSerializer.Serialize(document));
        string before = JsonSerializer.Serialize(document);
        string path = Path.Combine(Path.GetTempPath(), "Memoria.FF8-gameplay-" + Guid.NewGuid().ToString("N") + ".sav");
        try
        {
            using (FileStream destination = File.Create(path))
            {
                GameplaySaveCodec.Write(destination, document);
            }
            byte[] original = File.ReadAllBytes(path);
            GameState restored = GameplaySaveSessionLoader.Load(path, directory, sharedAssets: assets);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(before, JsonSerializer.Serialize(document));
            AssertProgress(document, restored);
            Assert.Equal(world ? SceneKind.WorldMap : SceneKind.Field, restored.Scene);
            Assert.NotNull(world ? restored.WorldEventState : restored.EventState);
            if (world)
            {
                Assert.NotNull(restored.World!.NativeFootRuntime);
                Assert.Equal(document.Resume.World!.X, restored.World.X);
                Assert.Equal(document.Resume.World.Z, restored.World.Z);
                Assert.Equal(new WorldTransition(62, 0, -1), restored.WorldArrival);
                Assert.Equal("Semantic", restored.WorldEventState!.Value.GetProperty("Scene").GetProperty("Names").GetProperty("0").GetString());
            }
            else
            {
                Assert.Equal(NativeFieldLoadOrigin.GameplaySave, restored.FieldLoadContext!.Origin);
                Assert.Null(restored.NativeFieldLocation);
                Assert.Equal("Semantic", restored.EventState!.Value.GetProperty("Scene").GetProperty("Names").GetProperty("0").GetString());
            }
            GameSession continued = new(restored);
            using GameWorld resumed = new(continued, directory, sharedAssets: assets);
            resumed.EnsureField();
            Assert.True(resumed.Apply(new(PlayerActionKind.Wait, Frames: 1)).Accepted);
            GameState next = continued.PrepareSnapshot();
            AssertProgress(document, next, ordinaryFieldCallback: !world);
            Assert.True((world ? next.World!.Frame : resumed.Events.Frame) > (world ? restored.World!.Frame : restored.EventState!.Value.GetProperty("Frame").GetInt64()));
            _ = new GameSession(next);
            Assert.NotNull(assets.LoadField(165));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Unbound_current_follower_pose_fails_without_changing_source_or_caller_resources()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = Document(assets, false);
        GameplaySaveProgression progress = document.Progression!;
        document = document with { Progression = progress with { FieldLocation = new(165, 164, [null, new(1, 2, 3, 4), null]) } };
        string before = JsonSerializer.Serialize(document);
        Assert.Throws<NotSupportedException>(() => GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets));
        Assert.Equal(before, JsonSerializer.Serialize(document));
        Assert.NotNull(assets.LoadField(165));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Failure_before_or_during_scene_construction_does_not_dispose_caller_assets_or_mutate_the_document(bool duringSceneConstruction)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = Document(assets, true);
        document = duringSceneConstruction ? document with { Progression = document.Progression! with { SceneFacts = document.Progression.SceneFacts! with { FieldParty = [0, 0, 255] } } } : document with { Resume = document.Resume with { World = document.Resume.World! with { EntityType = 16 } } };
        string before = JsonSerializer.Serialize(document);
        if (duringSceneConstruction)
        {
            Assert.Throws<InvalidDataException>(() => GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets));
        }
        else
        {
            Assert.Throws<NotSupportedException>(() => GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets));
        }
        Assert.Equal(before, JsonSerializer.Serialize(document));
        Assert.NotNull(assets.LoadField(165));
    }

    [Fact]
    public void Historical_other_field_menu_positions_do_not_override_the_current_normal_load_arrival()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = Document(assets, false);
        document = document with { Progression = document.Progression! with { FieldLocation = new(164, 163, [new(1, 2, 3, 4), new(5, 6, 7, 8), null]) } };
        GameState restored = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets);
        Assert.Null(restored.NativeFieldLocation);
        Assert.Equal(document.Resume.FieldId, restored.FieldArrival!.FieldId);
        Assert.Equal(document.Resume.Field!.X, restored.FieldArrival.X);
        Assert.Equal(document.Resume.Field.Y, restored.FieldArrival.Y);
        AssertProgress(document, restored);
    }

    [Fact]
    public void Historical_residue_and_compact_owners_allocate_the_same_semantic_field_process()
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameplaySaveComponentDocument document = GameplaySaveFieldPartyResumeTests.ThreePartyDocument(assets);
        GameState current = GameplaySaveSessionLoader.Restore(document, directory, sharedAssets: assets);
        NativePcSaveData original = NativePcSaveCodec.Decode(NativePcSaveConverter.Export(current, assets.ReadNewGameCharacterVitals(), originalPlaceId: 0));
        GameplaySaveComponentDocument historical = document with { Progression = document.Progression! with { NativeInteroperability = null, OriginalResidue = new(original.Block, document.Progression.SceneFacts!.Names.ToDictionary()) } };
        GameState legacy = GameplaySaveSessionLoader.Restore(historical, directory, sharedAssets: assets);
        Assert.Null(current.OriginalSave);
        Assert.NotNull(legacy.OriginalSave);
        Assert.True(JsonElement.DeepEquals(current.EventState!.Value, legacy.EventState!.Value));
        GameSession session = new(legacy);
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();
        GameplaySaveComponentDocument recaptured = world.CaptureGameplaySave();
        Assert.Null(recaptured.Progression!.OriginalResidue);
        Assert.NotNull(recaptured.Progression.NativeInteroperability);
    }

    internal static GameplaySaveComponentDocument Document(GameAssets assets, bool world)
    {
        GameSession initializer = GameSession.CreateColdProcess(assets.ReadColdProcessNativeBattleMemory());
        initializer.StartNewGame();
        using (MovieClock clock = new(initializer, assets.GameDirectory))
        using (GameWorld initialized = new(initializer, assets.GameDirectory, clock, sharedAssets: assets))
        {
        }
        GameState state = initializer.State;
        NativeCharacterVitals characters = state.CharacterVitals!.DeepCopy();
        characters.Records[0] = characters.Records[0] with { Experience = 123456, HitPoints = new(37, 37) };
        NativeItemStock[] inventory = state.Economy!.Items.ToArray();
        inventory[0] = new(1, 7);
        byte[] original = new byte[8192];
        original[0] = (byte)'S';
        original[1] = (byte)'C';
        original[386] = 0xFF;
        original[387] = 8;
        original[8000] = 231;
        NativeExamTimerState timer = state.ExamTimer!.SetRemaining(321).AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 5) with { Event182Mask4 = false, Event183Mask1 = false, SaveGlobalMask4 = false };
        JsonElement facts = JsonSerializer.SerializeToElement(new { FieldId = 165, Scene = new { Names = new Dictionary<int, string> { [0] = "Semantic" }, UnlockedTutorials = new[] { 2, 9 }, Party = new[] { 0, 255, 255 }, PartyPower = 3, MenuEnabled = true, RunEnabled = true, MapJumpDisabled = false, PlaceId = 321, NativeSalaryMovementUnits = 3, LoadedMusic = 17, MusicPlaying = true, MusicVolume = 91, NativeBattleFlagWrites = new[] { new { PersistentFlags = 0, ActiveFlags = 0 } } } });
        GameState source = state with
        {
            Scene = world ? SceneKind.WorldMap : SceneKind.Field,
            FieldId = 165,
            PreviousFieldId = 164,
            FieldLoadContext = null,
            CharacterVitals = characters,
            Economy = state.Economy with { Items = inventory, Gil = 123456, PastGil = 234567, EventBankGil72 = 345678, EventBankPastGil76 = 456789 },
            LimitProgress = state.LimitProgress! with { AngeloFlags = 0x91, TrainingSelection = 3, TrainingCounters = [1, 2, 3, 4, 5, 6, 7, 8] },
            EventMemory = state.EventMemory.Write(500, 4, NativeScalarValue.Exact(12345)),
            ExamTimer = timer,
            Playtime = state.Playtime!.AdvanceTo(timer.ClockCalls) with { Seconds = 1234 },
            AppliedBattleResultIds = ["committed-result"],
            BattleStatistics = new(123, 4, 5),
            PendingOriginalAchievementActions = [new(new("salary-1"), new("field-1"), OriginalAchievementActionKind.FirstSalary)],
            OriginalSave = new(original) { Names = new Dictionary<int, string> { [0] = "Historical" } },
            EventState = world ? null : facts,
            WorldEventState = world ? facts : null
        };
        int triangle = world ? 0 : FieldPartyTraceGeometry.Resolve(assets.LoadField(165).Walkmesh, -700, -8593, 567) ?? throw new InvalidDataException("The selected field has no save-point arrival triangle.");
        GameplaySaveResumeDescriptor resume = new(1, world ? GameplaySaveResumeKind.World : GameplaySaveResumeKind.Field, 165, 164, world ? null : new(-700, -8593, triangle, 128), world ? new(17151, -28610, 0, 1024, 0) { AnimationVariant = 0, MapDisplayMode = 2 } : null);
        return GameplaySaveComponentDocument.CaptureProgression(source, resume);
    }

    private static void AssertProgress(GameplaySaveComponentDocument document, GameState restored, bool ordinaryFieldCallback = false)
    {
        Assert.Equal(123456u, restored.CharacterVitals!.Records[0].Experience);
        Assert.Equal((ushort)37, restored.CharacterVitals.Records[0].HitPoints.Minimum);
        Assert.Equal(123456u, restored.Economy!.Gil);
        Assert.Equal(234567u, restored.Economy.PastGil);
        Assert.Equal(ordinaryFieldCallback ? restored.Economy.Gil : 345678u, restored.Economy.EventBankGil72);
        Assert.Equal(ordinaryFieldCallback ? restored.Economy.PastGil : 456789u, restored.Economy.EventBankPastGil76);
        Assert.Equal(new NativeItemStock(1, 7), restored.Economy.Items[0]);
        Assert.Equal((byte)0x91, restored.LimitProgress!.AngeloFlags);
        Assert.Equal(document.Limits.TrainingCounters, restored.LimitProgress.TrainingCounters);
        Assert.Equal(12345, restored.EventMemory.Read(500, 4).RequireExact());
        Assert.Equal(document.Progression!.Transactions.AppliedBattleResults, restored.AppliedBattleResultIds);
        Assert.Equal(document.Progression.Transactions.BattleStatistics, restored.BattleStatistics);
        Assert.Equal(document.Progression.Transactions.PendingOriginalActions, restored.PendingOriginalAchievementActions);
        Assert.Null(restored.OriginalSave);
        Assert.Equal((byte)231, restored.NativeSaveInteroperability!.Tail![8000 - 5410]);
        Assert.Contains(restored.ExamTimer!.Alternatives, alternative => alternative.Remaining == 321);
        Assert.True(restored.Playtime!.Seconds >= 1234);
    }
}
