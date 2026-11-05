using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveDurableProgressionTests
{
    [Fact]
    public void Every_root_member_has_an_explicit_persistence_authority()
    {
        Assert.Equal(typeof(GameState).GetProperties().Select(property => property.Name).Order(), GameplaySaveRootOwnership.Members.Keys.Order());
        Assert.Equal(GameplaySavePersistenceRequirement.Runtime, GameplaySaveRootOwnership.Members[nameof(GameState.NativeBattleMemory)].Requirement);
        Assert.Equal(GameplaySavePersistenceRequirement.ResumeHandler, GameplaySaveRootOwnership.Members[nameof(GameState.World)].Requirement);
        Assert.Equal(GameplaySavePersistenceRequirement.Durable, GameplaySaveRootOwnership.Members[nameof(GameState.PendingAchievementFacts)].Requirement);
    }

    [Fact]
    public void Durable_projection_round_trips_nondefault_root_progression_without_native_memory()
    {
        GameState source = State();
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(source, Resume());
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, document);
        stream.Position = 0;
        GameplaySaveComponentDocument loaded = GameplaySaveCodec.Read(stream);
        GameplaySaveProgression restored = loaded.Progression!;

        Assert.Equal(2, loaded.Version);
        Assert.Equal(0x12345678, restored.Story.RestoreEventMemory().Read(300, 4).RequireExact());
        Assert.Equal(source.WorldEntryMemory!.Bytes, restored.Story.WorldEntry!.Value);
        Assert.Equal(1234u, restored.Clocks.Playtime!.Seconds);
        Assert.Equal(231u, restored.Clocks.SeeD!.SalaryMovement);
        Assert.Equal((ushort)2100, restored.Clocks.SeeD.RankPoints);
        Assert.Equal(5, restored.Clocks.ExamTimer!.Alternatives[0].Remaining);
        Assert.Equal(source.EventRandom!.DrawPoints!.Depletion, restored.Clocks.EventRandom!.DrawPoints!.Depletion);
        Assert.Equal(source.CardInventory!.Cards, restored.Collections.Cards!.Cards);
        Assert.Equal(source.ShopHistory!.Bytes, restored.Collections.ShopHistory!.Value);
        Assert.Equal(7u, restored.Collections.Recipes!.Discovered);
        Assert.Equal(2u, restored.Collections.Recipes.ObtainedWeapons);
        Assert.Equal(1u, restored.Collections.Recipes.MagazineRecipes);
        Assert.Equal(source.KnownMagic!.Words, restored.Collections.KnownMagic!.Value);
        Assert.Equal(source.ScannedModels!.Words, restored.Collections.ScannedModels!.Value);
        Assert.Equal(source.RefinedCardIds, restored.Collections.RefinedCardIds);
        Assert.Equal(source.PocketStation!.Raw, restored.Collections.PocketStation!.Values);
        Assert.True(restored.Rules.PendingMagicModeReview);
        Assert.Equal("Draft", Assert.Single(restored.Rules.PendingNames!).Draft);
        Assert.True(restored.Rules.PendingPreferences!.WeaponMagazineRequired);
        Assert.Equal(new NativeBattleStatistics(123, 4, 5), restored.Transactions.BattleStatistics);
        Assert.Equal(source.AppliedBattleResultIds, restored.Transactions.AppliedBattleResults);
        Assert.Equal(source.PendingOriginalAchievementActions, restored.Transactions.PendingOriginalActions);
        Assert.Equal(source.PendingOriginalAchievementObservations, restored.Transactions.PendingOriginalObservations);
        Assert.Equal(source.PendingAchievementObservations, restored.Transactions.PendingKillObservations);
        Assert.Equal(source.PendingAchievementFacts, restored.Transactions.PendingDefeats);
        Assert.Equal(47, restored.Transactions.MutationSequence);
        Assert.Equal(231, restored.OriginalResidue!.Block[8000]);
        Assert.Equal("Current", restored.SceneFacts!.Names[0]);
        Assert.Equal(new[] { 2, 9 }, restored.SceneFacts.UnlockedTutorials);
        Assert.Equal(new byte[] { 0, 4, 255 }, restored.SceneFacts.FieldParty);
        Assert.Equal(17, restored.SceneFacts.Music!.LoadedMusic);
        Assert.True(stream.Length < 100_000);
    }

    [Fact]
    public void Current_world_context_wins_over_frozen_field_and_root_owners_win_over_VM_mirrors()
    {
        GameState source = State() with { Scene = SceneKind.WorldMap, EventState = Scene("Stale", 1), DepartingFieldState = Scene("Older", 2), WorldEventState = Scene("World", 7) };
        GameplaySaveProgression saved = GameplaySaveProgression.Capture(source);
        Assert.Equal(GameplaySaveSceneFactsOrigin.WorldEventContext, saved.SceneFacts!.Origin);
        Assert.Equal("World", saved.SceneFacts.Names[0]);
        Assert.Equal(7, saved.SceneFacts.PartyPower);
        Assert.Equal(1234u, saved.Clocks.Playtime!.Seconds);
        Assert.Equal(231u, saved.Clocks.SeeD!.SalaryMovement);
        Assert.Equal(0x12345678, saved.Story.Cells.Single(cell => cell.Address == 300).Value);
    }

    [Fact]
    public void Dormant_world_departure_preserves_persistent_names_party_and_tutorials_but_not_current_music()
    {
        GameState source = State() with { Scene = SceneKind.WorldMap, EventState = null, DepartingFieldState = Scene("Dormant", 9), WorldEventState = null };
        GameplaySaveProgression saved = GameplaySaveProgression.Capture(source);
        Assert.Equal(GameplaySaveSceneFactsOrigin.SuspendedField, saved.SceneFacts!.Origin);
        Assert.Equal("Dormant", saved.SceneFacts.Names[0]);
        Assert.Equal(new[] { 2, 9 }, saved.SceneFacts.UnlockedTutorials);
        Assert.Equal(new byte[] { 0, 4, 255 }, saved.SceneFacts.FieldParty);
        Assert.Null(saved.SceneFacts.Music);
        Assert.Equal(17, saved.SceneFacts.SuspendedFieldMusic!.LoadedMusic);
        Assert.Equal(1234u, saved.Clocks.Playtime!.Seconds);
    }

    [Fact]
    public void Field_projection_never_uses_departing_field_facts_as_the_active_scene()
    {
        GameplaySaveProgression saved = GameplaySaveProgression.Capture(State() with { EventState = null, DepartingFieldState = Scene("Dormant", 9) });
        Assert.Null(saved.SceneFacts);
        Assert.Equal("Original", saved.OriginalResidue!.Names[0]);
    }

    [Fact]
    public void Captured_collections_names_event_cells_and_original_bytes_are_detached()
    {
        GameState source = State();
        GameplaySaveProgression saved = GameplaySaveProgression.Capture(source);
        source.CardInventory!.Cards[0] = 91;
        source.OriginalSave!.Block[8000] = 0;
        source.CharacterAppearance!.Records[0] = new(99);
        Assert.Equal((byte)7, saved.Collections.Cards!.Cards[0]);
        Assert.Equal((byte)231, saved.OriginalResidue!.Block[8000]);
        Assert.Equal((byte)4, saved.Collections.Dresses![0]);
        saved.SceneFacts!.Names[0] = "Changed";
        Assert.Equal("Current", source.EventState!.Value.GetProperty("Scene").GetProperty("Names").GetProperty("0").GetString());
    }

    [Fact]
    public void Saved_world_transport_values_exclude_loader_receipts_and_source_frame()
    {
        GameState source = State();
        WorldState position = new() { Frame = 87, X = 12345, Z = -23456, NativeHeight = 123, BodyYaw = 2048, CameraYaw = 1024 };
        NativeSavedWorldFootReturn receipt = NativeSavedWorldFootReturn.Create(position, 106, 99, 11, 1, 2);
        source = source with { SavedWorld = source.SavedWorld!.WithFootReturn(receipt) };
        GameplaySaveWorldData saved = GameplaySaveProgression.Capture(source).World!;
        Assert.Equal(GameplaySaveWorldReturnKind.Foot, saved.Return!.Kind);
        Assert.Equal(new NativeSavedWorldPose(12345, -23456, 123, 2048), saved.Return.Pose);
        Assert.Equal((ushort)1024, saved.Return.CameraYaw);
        Assert.Equal((short)11, saved.Return.MapLocation);
        Assert.Equal((byte)1, saved.Return.AnimationVariant);
        Assert.Equal((byte)2, saved.Return.MapDisplayMode);
        string json = JsonSerializer.Serialize(saved);
        Assert.DoesNotContain("SourceWorldFrame", json);
        Assert.DoesNotContain("Load", json);
        Assert.DoesNotContain("Rule", json);
        NativeSavedWorldState parking = saved.RestoreParkingAndFlags(NativeSavedWorldState.CreateNewGame());
        Assert.Equal(saved.Slot4, parking.Slot4);
        Assert.Equal(saved.Slot5, parking.Slot5);
        Assert.Null(parking.FootReturn);
        Assert.Null(parking.CarReturn);
        Assert.Null(parking.ScriptedReturn);
    }

    [Fact]
    public void Unavailable_scene_facts_or_unknown_event_values_do_not_create_defaults()
    {
        GameState source = State() with { EventState = JsonSerializer.SerializeToElement(new { FieldId = 106, Scene = new { Names = new Dictionary<int, string>() } }) };
        Assert.Throws<InvalidDataException>(() => GameplaySaveProgression.Capture(source));
        GameplaySaveProgression saved = GameplaySaveProgression.Capture(State());
        Assert.Throws<InvalidDataException>(() => (saved.Story with { Cells = [new(300, 4, 1), new(300, 4, 2)] }).Validate());
        Assert.Throws<InvalidDataException>(() => (saved.Collections with { Dresses = [] }).Validate());
        Assert.Throws<NotSupportedException>(() => GameplaySaveProgression.Capture(State() with { Scene = SceneKind.Movie }));
    }

    [Fact]
    public void Historical_v1_component_payload_without_progression_remains_readable()
    {
        GameState state = State();
        GameplaySaveComponentDocument source = GameplaySaveComponentDocument.Capture(Resume(), state.CharacterVitals!, state.Economy!, state.LimitProgress!);
        using MemoryStream stream = new();
        GameplaySaveCodec.Write(stream, source);
        using (JsonDocument payload = JsonDocument.Parse(stream.ToArray().AsMemory(48)))
        {
            Assert.False(payload.RootElement.TryGetProperty("Progression", out _));
        }
        stream.Position = 0;
        GameplaySaveComponentDocument loaded = GameplaySaveCodec.Read(stream);
        Assert.Equal(1, loaded.Version);
        Assert.Equal(GameplaySaveComponentDocument.InitialCoverage, loaded.Coverage);
        Assert.Null(loaded.Progression);
    }

    [Fact]
    public void Version_two_cannot_claim_durable_coverage_without_its_progression_component()
    {
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(State(), Resume());
        Assert.Throws<InvalidDataException>(() => (document with { Progression = null }).Validate());
        Assert.Throws<InvalidDataException>(() => (document with { Coverage = GameplaySaveComponentDocument.InitialCoverage }).Validate());
    }

    [Fact]
    public void Durable_type_graph_contains_only_reviewed_values_and_no_VM_or_native_resources()
    {
        HashSet<Type> visited = [];
        Queue<Type> pending = new([typeof(GameplaySaveProgression)]);
        string[] approved = ["NativeCardInventory", "TripleTriadStatistics", "ChocoboRewardLedger", "ChocoboImportReceipt", "GameplayPreferences", "GameplayTuning", "RareItemProbabilities", "TripleTriadSettings", "PendingEntityName", "DialogueDraft", "NativeRandomEncounterState", "NativeExamTimerState", "NativeTimerAlternative", "NativeTimerDisplayBlink", "NativePlaytimeState", "NativeEventRandomState", "NativeDrawPointMemory", "NativeCrtRandomState", "NativeMenuRandomState", "NativeGlobalRandomState", "NativeBattleStatistics", "NativeBattleVariableState", "EnemyDefeatedEvent", "BattleKillTotalObservedEvent", "OriginalAchievementObservedEvent", "OriginalAchievementActionEvent", "OriginalAchievementDrawCursor", "BattleAchievementCursor", "AchievementEventId", "AchievementContextId", "EnemyDefeatFacts", "AchievementOwner", "OriginalAchievementProgress", "BattleNativeDrawContext", "NativeSavedWorldVehicleSlot", "NativeSavedWorldPose", "NativeWorldEventFlags", "NativeSavedWorldAngleWords", "NativeWorldTerrainFlags109", "NativeSavedWorldUnsupportedEntry", "WorldTransition", "NativeSavedBattleFlagFieldWrite", "NativeFadeWords", "NativeMessageVariables", "NativeFieldLocationPose"];
        while (pending.TryDequeue(out Type? type))
        {
            if (!visited.Add(type))
            {
                continue;
            }
            Assert.NotEqual(typeof(JsonElement), type);
            if (type.IsArray)
            {
                pending.Enqueue(type.GetElementType()!);
            }
            foreach (Type argument in type.GenericTypeArguments)
            {
                pending.Enqueue(argument);
            }
            if (type.IsArray || type.Namespace != typeof(GameState).Namespace)
            {
                continue;
            }
            Assert.True(type.IsEnum || type.Name.StartsWith("GameplaySave", StringComparison.Ordinal) || approved.Contains(type.Name), $"Unreviewed durable member type: {type.FullName}");
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).Where(property => property.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition != JsonIgnoreCondition.Always))
            {
                pending.Enqueue(property.PropertyType);
            }
        }
    }

    private static GameplaySaveResumeDescriptor Resume() => new(1, GameplaySaveResumeKind.Field, 106, 105, new(10, 20, 7, 192), null);

    private static JsonElement Scene(string name, int power)
    {
        return JsonSerializer.SerializeToElement(new { FieldId = 106, Scene = new { Names = new Dictionary<int, string> { [0] = name }, UnlockedTutorials = new[] { 2, 9 }, Party = new[] { 0, 4, 255 }, PartyPower = power, MenuEnabled = true, RunEnabled = true, MapJumpDisabled = false, PlaceId = 321, NativeSalaryMovementUnits = 3, LoadedMusic = 17, MusicPlaying = true, MusicVolume = 91, NativeBattleMusic203 = 37, SoundMusicVolume197 = 88, SoundMusicVolume198 = 77, MusicChannel108 = new { ChannelId = 1, MusicId = 17, ChoiceMask = (uint?)null } }, Playtime = new { Seconds = 1 }, SeeD = new { SalaryMovementCounter = 2 }, EventMemory = new { } });
    }

    private static GameState State()
    {
        string hash = new('0', 64);
        NativeItemStock[] items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray();
        items[0] = new(17, 43);
        NativeCardInventory cards = NativeCardInventory.CreateNewGame();
        cards.Cards[0] = 7;
        byte[] original = new byte[NativePcSaveData.BlockSize];
        original[0] = (byte)'S';
        original[1] = (byte)'C';
        original[386] = 0xFF;
        original[387] = 8;
        original[8000] = 231;
        AchievementOwner owner = new("test", "ff8", "test-user", "test-profile");
        return GameSession.CreateColdProcess().State with
        {
            Scene = SceneKind.Field,
            FieldId = 106,
            PreviousFieldId = 105,
            CharacterVitals = MagicSystemFixture.Create(),
            CharacterAppearance = new() { Version = 1, Rule = NativeCharacterAppearance.InitializationRule, InitOutSha256 = hash, Records = Enumerable.Repeat(new NativeCharacterDress(4), 8).ToArray() },
            Economy = new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = hash, Items = items, ItemOrder = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(), Gil = 12345, PastGil = 6789, EventBankGil72 = 3456, EventBankPastGil76 = 9876 },
            LimitProgress = new(hash, 0x1234, 0x5678, 0x91, 3, [1, 2, 3, 4, 5, 6, 7, 8]),
            EventMemory = NativeEventMemory.FromDWords(new Dictionary<int, int> { [300] = 0x12345678 }),
            WorldEntryMemory = NativeWorldEntryMemory.CreateNewGame().WriteBytes(256, 2, 1234),
            WorldControls = NativeWorldControlState.CreateNewGame(),
            SavedWorld = NativeSavedWorldState.CreateNewGame(),
            SeeD = NativeSeeDState.CreateNewGame() with { SalaryMovementCounter = 231, RankPoints = 2100 },
            ExamTimer = NativeExamTimerState.FromColdProcess().SetRemaining(5),
            Playtime = new(1234, 17, 0),
            EventRandom = new(12345, 7, new(ImmutableArray.CreateRange(Enumerable.Repeat((byte)3, 64)), 25)),
            CardInventory = cards,
            ShopHistory = new(hash, ImmutableArray.CreateRange(Enumerable.Repeat((byte)7, 400))),
            WeaponRecipes = new(hash, 7) { ObtainedWeaponsBits = 2, MagazineRecipeBits = 1 },
            ShotAvailability = new(7, [1, 2, 3, 4, 5, 6, 7, 8], hash, hash),
            KnownMagic = new(NativeInputConfiguration.OriginalInitHash, [0x12345678, 0x87654321]),
            ScannedModels = new(NativeInputConfiguration.OriginalInitHash, [1, 2, 3, 4, 5]),
            PocketStation = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.CreateRange(Enumerable.Repeat((byte)3, 64))),
            RefinedCardIds = [2, 9],
            InputConfiguration = new(NativeInputConfiguration.OriginalInitHash, 0x00020001, Enumerable.Range(1, 12).Select(value => (byte)value).ToImmutableArray()),
            PendingEntityNames = [new(16, "GF", "item:24") { Draft = "Draft" }],
            PendingGameplayPreferences = new() { WeaponMagazineRequired = true },
            PendingMagicModeReview = true,
            BattleStatistics = new(123, 4, 5),
            AppliedBattleResultIds = ["battle-7"],
            PendingAchievementFacts = [new(new("defeat-1"), new("battle-7"), new("enemy-1", BattleDefeatCredit.Character, 0, null, 0, false, BattleRewardPolicy.NativeCombat)) { Owner = owner }],
            PendingAchievementObservations = [new(new("kill-1"), new("battle-7"), 123, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 0, owner)],
            PendingOriginalAchievementObservations = [OriginalAchievementObservedEvent.Create(new() { AllCards = false, MagazineCollection = true }, owner)],
            PendingOriginalAchievementActions = [new(new("action-1"), new("field-1"), OriginalAchievementActionKind.FirstSalary, Owner: owner)],
            OriginalAchievementMutationContext = "field-1",
            OriginalAchievementMutationSequence = 47,
            OriginalSave = new(original) { Names = new Dictionary<int, string> { [0] = "Original" } },
            EventState = Scene("Current", 3)
        };
    }
}
