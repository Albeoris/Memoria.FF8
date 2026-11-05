using System.Collections.Immutable;
using System.Text.Json;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class GameplaySaveStateRestorationTests
{
    [Fact]
    public void Restores_nondefault_semantic_owners_with_current_installed_definitions()
    {
        GameState source = Source();
        GameplaySaveComponentDocument document = Document(source);
        GameState initialized = Initialized() with
        {
            CharacterVitals = MagicSystemFixture.Create() with { InitOutSha256 = Hash('a'), KernelSha256 = Hash('b') },
            Economy = Economy(Hash('a')),
            CharacterAppearance = Appearance(Hash('a')),
            LimitProgress = Limits(Hash('a')),
            ShopHistory = new(Hash('a'), ImmutableArray.CreateRange(new byte[400])),
            WeaponRecipes = new(Hash('a'), 0),
            ShotAvailability = new(0, [10, 11, 12, 13, 14, 15, 16, 17], Hash('a'), Hash('b'))
        };
        GameState restored = GameplaySaveStateRestoration.Restore(document, initialized);

        Assert.Equal(106, restored.FieldId);
        Assert.Equal(105, restored.PreviousFieldId);
        Assert.Equal(new FieldTransition(106, 123, -456, 7, 192), restored.FieldArrival);
        Assert.Null(restored.FieldLoadContext);
        Assert.Equal(source.CharacterVitals!.Records, restored.CharacterVitals!.Records);
        Assert.Same(initialized.CharacterVitals!.Definitions, restored.CharacterVitals.Definitions);
        Assert.Same(initialized.CharacterVitals.MenuDefinitions, restored.CharacterVitals.MenuDefinitions);
        Assert.Equal(Hash('a'), restored.CharacterVitals.InitOutSha256);
        Assert.Equal(Hash('b'), restored.CharacterVitals.KernelSha256);
        Assert.Equal(source.Economy!.Items, restored.Economy!.Items);
        Assert.Equal(source.Economy.Gil, restored.Economy.Gil);
        Assert.Equal(source.Economy.EventBankGil72, restored.Economy.EventBankGil72);
        Assert.Equal(Hash('a'), restored.Economy.InitOutSha256);
        Assert.Equal(source.CharacterAppearance!.Records, restored.CharacterAppearance!.Records);
        Assert.Equal(Hash('a'), restored.CharacterAppearance.InitOutSha256);
        Assert.Equal(source.LimitProgress!.TrainingCounters, restored.LimitProgress!.TrainingCounters);
        Assert.Equal(source.LimitProgress.AngeloFlags, restored.LimitProgress.AngeloFlags);
        Assert.Equal(Hash('a'), restored.LimitProgress.InitOutSha256);
        Assert.Equal(source.ShopHistory!.Bytes, restored.ShopHistory!.Bytes);
        Assert.Equal(Hash('a'), restored.ShopHistory.InitOutSha256);
        Assert.Equal(7u, restored.WeaponRecipes!.Bits);
        Assert.Equal(2u, restored.WeaponRecipes.ObtainedWeaponsBits);
        Assert.Equal(1u, restored.WeaponRecipes.MagazineRecipeBits);
        Assert.Equal(source.ShotAvailability!.Flags770, restored.ShotAvailability!.Flags770);
        Assert.Equal(initialized.ShotAvailability!.AmmoItemIds, restored.ShotAvailability.AmmoItemIds);
        Assert.Equal(Hash('b'), restored.ShotAvailability.KernelSha256);
        Assert.Equal(source.CardInventory!.Cards, restored.CardInventory!.Cards);
        Assert.Equal(source.KnownMagic!.Words, restored.KnownMagic!.Words);
        Assert.Equal(source.ScannedModels!.Words, restored.ScannedModels!.Words);
        Assert.Equal(source.PocketStation!.Raw, restored.PocketStation!.Raw);
        Assert.Equal(source.PocketStation.ChocoboName, restored.PocketStation.ChocoboName);
        Assert.Equal(source.RefinedCardIds, restored.RefinedCardIds);
        Assert.Equal(source.SeeD!.RankPoints, restored.SeeD!.RankPoints);
        Assert.Equal(source.SeeD.SalaryMovementCounter, restored.SeeD.SalaryMovementCounter);
        Assert.Same(initialized.SeeD!.SalaryTable, restored.SeeD.SalaryTable);
        Assert.Equal(source.ExamTimer!.Alternatives, restored.ExamTimer!.Alternatives);
        Assert.Equal(source.ExamTimer.ClockCalls, restored.ExamTimer.ClockCalls);
        Assert.Equal(source.Playtime, restored.Playtime);
        Assert.Equal(source.EventRandom, restored.EventRandom);
        Assert.Equal(source.CrtRandom, restored.CrtRandom);
        Assert.Equal(source.MenuRandom, restored.MenuRandom);
        Assert.Equal(source.GlobalRandom, restored.GlobalRandom);
        Assert.Equal(source.PendingEntityNames, restored.PendingEntityNames);
        Assert.Equal(source.PendingGameplayPreferences, restored.PendingGameplayPreferences);
        Assert.True(restored.PendingMagicModeReview);
        Assert.Equal(source.DialogueDraft, restored.DialogueDraft);
        Assert.Equal(source.InputConfiguration!.Options, restored.InputConfiguration!.Options);
        Assert.Equal(source.InputConfiguration.ButtonMap, restored.InputConfiguration.ButtonMap);
        Assert.Equal(source.AppliedBattleResultIds, restored.AppliedBattleResultIds);
        Assert.Equal(source.BattleStatistics, restored.BattleStatistics);
        Assert.Equal(source.BattleVariables!.Values, restored.BattleVariables!.Values);
        Assert.Equal(source.PendingAchievementFacts, restored.PendingAchievementFacts);
        Assert.Equal(source.PendingAchievementObservations, restored.PendingAchievementObservations);
        Assert.Equal(source.PendingOriginalAchievementActions, restored.PendingOriginalAchievementActions);
        Assert.Equal(source.PendingOriginalAchievementObservations, restored.PendingOriginalAchievementObservations);
        Assert.Equal(source.OriginalAchievementMutationSequence, restored.OriginalAchievementMutationSequence);
        Assert.Equal("Current", restored.OriginalSave!.Names[0]);
        Assert.Equal((byte)231, restored.OriginalSave.Block[8000]);
        Assert.Same(initialized.NativeBattleMemory, restored.NativeBattleMemory);
        Assert.Null(restored.EventState);
        Assert.Null(restored.WorldEventState);
        Assert.Null(restored.DepartingFieldState);
        Assert.Null(restored.NativeFieldLocation);
        Assert.Null(restored.World);
        Assert.Null(restored.Battle);
        Assert.Null(restored.PendingBattleEncounter);
        Assert.Null(restored.FieldRequestPacket);
        Assert.Equal(NativeSavedWorldOriginKind.GameplaySave, restored.SavedWorld!.Origin.Kind);
        _ = new GameSession(restored);
    }

    [Fact]
    public void Reconstructs_exact_cell_widths_and_rejects_generic_aliases_of_canonical_owners()
    {
        GameplaySaveComponentDocument document = Document(Source());
        GameState restored = GameplaySaveStateRestoration.Restore(document, Initialized());
        Assert.Equal(1, restored.EventMemory.Cells[300].Width);
        Assert.Equal(2, restored.EventMemory.Cells[301].Width);
        Assert.Equal(4, restored.EventMemory.Cells[303].Width);
        Assert.Equal(0x7F, restored.EventMemory.Read(300, 1).RequireExact());
        Assert.Equal(0x1234, restored.EventMemory.Read(301, 2).RequireExact());
        Assert.Equal(unchecked((int)0x89ABCDEFu), restored.EventMemory.Read(303, 4).RequireExact());
        GameplaySaveProgression progress = document.Progression!;
        Assert.Throws<InvalidDataException>(() => GameplaySaveStateRestoration.Restore(document with { Progression = progress with { Story = progress.Story with { Cells = [new(72, 4, 0)] } } }, Initialized()));
        Assert.Throws<InvalidDataException>(() => GameplaySaveStateRestoration.Restore(document with { Progression = progress with { Story = progress.Story with { Cells = [new(1392, 4, 0)] } } }, Initialized()));
    }

    [Fact]
    public void Explicit_zero_empty_and_null_progress_is_not_replaced_from_the_template()
    {
        GameState source = Source();
        source.CharacterVitals!.Records[0] = source.CharacterVitals.Records[0] with { HitPoints = new(0, 0), Experience = 0 };
        source = source with { Economy = source.Economy! with { Gil = 0, ItemOrder = null }, LimitProgress = source.LimitProgress! with { AngeloFlags = 0, TrainingCounters = ImmutableArray.CreateRange(new byte[8]), PetPalsFlags = null }, PendingEntityNames = null, PendingGameplayPreferences = null, DialogueDraft = null, RefinedCardIds = [], KnownMagic = null };
        GameState restored = GameplaySaveStateRestoration.Restore(Document(source), Initialized());
        Assert.Equal((ushort)0, restored.CharacterVitals!.Records[0].HitPoints.Minimum);
        Assert.Equal(0u, restored.CharacterVitals.Records[0].Experience);
        Assert.Equal(0u, restored.Economy!.Gil);
        Assert.Null(restored.Economy.ItemOrder);
        Assert.Equal((byte)0, restored.LimitProgress!.AngeloFlags);
        Assert.All(restored.LimitProgress.TrainingCounters, value => Assert.Equal((byte)0, value));
        Assert.Null(restored.LimitProgress.PetPalsFlags);
        Assert.Null(restored.PendingEntityNames);
        Assert.Null(restored.PendingGameplayPreferences);
        Assert.Null(restored.DialogueDraft);
        Assert.Empty(restored.RefinedCardIds);
        Assert.Null(restored.KnownMagic);
    }

    [Theory]
    [InlineData("characters")]
    [InlineData("economy")]
    [InlineData("limits")]
    [InlineData("appearance")]
    [InlineData("ammo")]
    public void Missing_current_initialization_fails_without_mutating_input_or_document(string missing)
    {
        GameplaySaveComponentDocument document = Document(Source());
        GameState initial = Initialized();
        GameState incomplete = missing switch
        {
            "characters" => initial with { CharacterVitals = null },
            "economy" => initial with { Economy = null },
            "limits" => initial with { LimitProgress = null },
            "appearance" => initial with { CharacterAppearance = null },
            _ => initial with { ShotAvailability = null }
        };
        string before = JsonSerializer.Serialize(document);
        Assert.Throws<NotSupportedException>(() => GameplaySaveStateRestoration.Restore(document, incomplete));
        Assert.Equal(before, JsonSerializer.Serialize(document));
        Assert.Equal((byte)0, initial.CharacterVitals!.Records[0].StatBonuses[0]);
        Assert.Equal(0u, initial.Economy!.Gil);
    }

    [Fact]
    public void Dirty_process_component_only_documents_and_live_battle_cursors_are_not_accepted()
    {
        GameState source = Source();
        GameplaySaveComponentDocument complete = Document(source);
        GameplaySaveComponentDocument partial = GameplaySaveComponentDocument.Capture(FieldResume(), source.CharacterVitals!, source.Economy!, source.LimitProgress!);
        Assert.Throws<NotSupportedException>(() => GameplaySaveStateRestoration.Restore(partial, Initialized()));
        Assert.Throws<InvalidOperationException>(() => GameplaySaveStateRestoration.Restore(complete, Initialized() with { EventState = JsonSerializer.SerializeToElement(new { }) }));
        Assert.Throws<InvalidOperationException>(() => GameplaySaveStateRestoration.Restore(complete, Source()));
        GameplaySaveProgression progress = complete.Progression!;
        Assert.Throws<NotSupportedException>(() => GameplaySaveStateRestoration.Restore(complete with { Progression = progress with { Transactions = progress.Transactions with { DrawCursor = new("battle-1", 3) } } }, Initialized()));
        Assert.Throws<InvalidDataException>(() => GameplaySaveStateRestoration.Restore(complete with { Progression = progress with { Clocks = progress.Clocks with { Playtime = progress.Clocks.Playtime! with { ClockCalls = 0 } } } }, Initialized()));
    }

    [Fact]
    public void Output_mutable_values_do_not_alias_document_or_initialization()
    {
        GameplaySaveComponentDocument document = Document(Source());
        GameState initial = Initialized();
        GameState restored = GameplaySaveStateRestoration.Restore(document, initial);
        restored.CharacterVitals!.Records[0].StatBonuses[0] = 91;
        restored.Economy!.Items[0] = new(1, 1);
        restored.CardInventory!.Cards[0] = 92;
        restored.OriginalSave!.Block[8000] = 93;
        Assert.Equal((byte)7, document.Characters.Records[0].StatBonuses[0]);
        Assert.Equal(new NativeItemStock(17, 43), document.Economy.Items[0]);
        Assert.Equal((byte)4, document.Progression!.Collections.Cards!.Cards[0]);
        Assert.Equal((byte)231, document.Progression.OriginalResidue!.Block[8000]);
        Assert.Equal((byte)0, initial.CharacterVitals!.Records[0].StatBonuses[0]);
        Assert.Equal(new NativeItemStock(0, 0), initial.Economy!.Items[0]);
    }

    [Theory]
    [InlineData(0, 0, false, false, false)]
    [InlineData(0x104, 4, true, true, true)]
    [InlineData(0x100, 0, false, true, false)]
    public void Derives_only_missing_timer_masks_without_advancing_saved_time(ushort persistent, ushort active, bool expected182, bool expected183, bool expectedGlobal)
    {
        GameplaySaveComponentDocument document = Document(Source());
        GameplaySaveProgression progress = document.Progression!;
        NativeExamTimerState timer = progress.Clocks.ExamTimer! with { Event182Mask4 = null, Event183Mask1 = null, SaveGlobalMask4 = null };
        document = document with { Progression = progress with { SceneFacts = progress.SceneFacts! with { PersistentBattleMode = persistent, ActiveBattleMode = active }, Clocks = progress.Clocks with { ExamTimer = timer } } };
        string before = JsonSerializer.Serialize(document);

        GameState restored = GameplaySaveStateRestoration.Restore(document, Initialized());

        Assert.Equal(timer with { Event182Mask4 = expected182, Event183Mask1 = expected183, SaveGlobalMask4 = expectedGlobal }, restored.ExamTimer! with { Alternatives = timer.Alternatives });
        Assert.Equal(timer.Alternatives, restored.ExamTimer!.Alternatives);
        Assert.Equal(progress.Clocks.Playtime, restored.Playtime);
        Assert.Equal(before, JsonSerializer.Serialize(document));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Rejects_contradictory_timer_masks_before_installation(int mask)
    {
        GameplaySaveComponentDocument document = Document(Source());
        GameplaySaveProgression progress = document.Progression!;
        NativeExamTimerState timer = progress.Clocks.ExamTimer! with { Event182Mask4 = mask == 0, Event183Mask1 = mask == 1, SaveGlobalMask4 = mask == 2 };
        document = document with { Progression = progress with { Clocks = progress.Clocks with { ExamTimer = timer } } };
        string before = JsonSerializer.Serialize(document);

        Assert.Throws<InvalidDataException>(() => GameplaySaveStateRestoration.Restore(document, Initialized()));
        Assert.Equal(before, JsonSerializer.Serialize(document));
    }

    [Fact]
    public void Incomplete_scene_words_remain_readable_components_but_cannot_restore_a_game()
    {
        GameplaySaveComponentDocument document = Document(Source());
        GameplaySaveProgression progress = document.Progression!;
        GameplaySaveComponentDocument incomplete = document with { Progression = progress with { SceneFacts = progress.SceneFacts! with { PersistentBattleMode = null, ActiveBattleMode = null } } };
        incomplete.Validate();
        Assert.Throws<NotSupportedException>(() => GameplaySaveStateRestoration.Restore(incomplete, Initialized()));
        Assert.Throws<NotSupportedException>(() => GameplaySaveStateRestoration.Restore(document with { Progression = progress with { SceneFacts = null } }, Initialized()));
        GameState noTimer = GameplaySaveStateRestoration.Restore(document with { Progression = progress with { Clocks = progress.Clocks with { ExamTimer = null, Playtime = null } } }, Initialized());
        Assert.Null(noTimer.ExamTimer);
        Assert.Null(noTimer.Playtime);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(34)]
    [InlineData(35)]
    [InlineData(36)]
    [InlineData(37)]
    [InlineData(38)]
    [InlineData(39)]
    [InlineData(40)]
    [InlineData(48)]
    [InlineData(50)]
    public void World_completion_uses_current_pose_and_metadata_for_supported_normal_reload(int entity)
    {
        GameState field = Source();
        GameState source = field with { Scene = SceneKind.WorldMap, EventState = null, WorldEventState = field.EventState };
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.World, 106, 105, null, new(54321, -12345, 123, 2048, (byte)entity));
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(source, resume);
        GameState pending = GameplaySaveStateRestoration.Restore(document, Initialized());
        Assert.Null(pending.WorldArrival);
        GameplaySaveWorldResumeMetadata metadata = new(3072, 1, 2, 47);
        GameState restored = GameplaySaveWorldResume.Complete(document, pending, metadata);
        NativeSavedWorldState world = restored.SavedWorld!;
        NativeSavedWorldPose pose = world.FootReturn?.Pose0 ?? world.CarReturn?.Pose ?? world.GardenReturn?.Pose3 ?? world.RagnarokReturn!.Pose2;
        Assert.Equal(new NativeSavedWorldPose(54321, -12345, 123, 2048), pose);
        Assert.Equal(new WorldTransition(62, (byte)entity, -1), restored.WorldArrival);
        Assert.Equal(NativeSavedWorldOriginKind.GameplaySave, world.Origin.Kind);
        Assert.Equal(NativeWorldConfiguration.GameplaySaveRule, restored.WorldConfiguration!.Rule);
        Assert.Null(restored.WorldConfiguration.OriginalSaveSha256);
        Assert.Null(restored.World);
        Assert.Null(pending.WorldArrival);
        if (world.CarReturn is { } car)
        {
            Assert.Equal((short)47, car.MapLocationId);
            Assert.Equal((ushort)3072, car.CameraYaw96);
            Assert.Equal(NativeCarWorldRuntimeState.ShapeForEntity((byte)entity), world.Slot4.ShapeId);
            Assert.False(car.RetireSelectedVehicle);
            Assert.Equal(NativeSavedWorldCarReturn.GameplaySaveRule, car.Rule);
        }
        if (world.FootReturn is { } foot)
        {
            Assert.Equal((short)47, foot.MapLocationId);
            Assert.Equal((ushort)3072, foot.CameraYaw96);
            Assert.Equal(NativeSavedWorldFootReturn.GameplaySaveRule, foot.Rule);
            NativeFootWorldRuntimeState runtime = NativeFootWorldRuntimeState.CreateSavedReload(foot);
            Assert.Equal(NativeFootWorldRuntimeState.SavedReloadRule, runtime.Rule);
            Assert.Equal(foot.EntityType105, runtime.EntityType);
        }
        _ = new GameSession(restored);
    }

    [Fact]
    public void Unsupported_world_kind_and_invalid_metadata_fail_without_publishing_or_mutation()
    {
        GameState field = Source();
        GameState source = field with { Scene = SceneKind.WorldMap, EventState = null, WorldEventState = field.EventState };
        GameplaySaveResumeDescriptor resume = new(1, GameplaySaveResumeKind.World, 106, 105, null, new(54321, -12345, 123, 2048, 0));
        GameplaySaveComponentDocument document = GameplaySaveComponentDocument.CaptureProgression(source, resume);
        GameState pending = GameplaySaveStateRestoration.Restore(document, Initialized());
        Assert.Throws<InvalidDataException>(() => GameplaySaveWorldResume.Complete(document, pending, new(4096, 1, 2, 47)));
        Assert.Throws<NotSupportedException>(() => GameplaySaveWorldResume.Complete(document with { Resume = resume with { World = resume.World! with { EntityType = 16 } } }, pending, new(0, 1, 2, 47)));
        Assert.Null(pending.WorldArrival);
        Assert.Null(pending.World);
        Assert.Equal(0, document.Resume.World!.EntityType);
    }

    [Fact]
    public void Stored_field_return_keeps_last_world_position_under_explicit_new_load_provenance()
    {
        GameState source = Source();
        WorldState previous = new() { Frame = 87, X = 1001, Z = -2002, NativeHeight = 123, BodyYaw = 2048, CameraYaw = 3072 };
        source = source with { SavedWorld = source.SavedWorld!.WithFootReturn(NativeSavedWorldFootReturn.Create(previous, 105, 1, 17, 1, 2)) };
        GameplaySaveComponentDocument document = Document(source);
        GameState restored = GameplaySaveStateRestoration.Restore(document, Initialized());
        NativeSavedWorldFootReturn saved = restored.SavedWorld!.FootReturn!;
        Assert.Equal(new NativeSavedWorldPose(1001, -2002, 123, 2048), saved.Pose0);
        Assert.Equal(0, saved.SourceWorldFrame);
        Assert.Equal(NativeSavedWorldFootReturn.GameplaySaveRule, saved.Rule);
        Assert.Equal((ushort)4321, saved.StoryWord);
        Assert.Equal((ushort)105, saved.LastFieldId);
        Assert.Null(restored.WorldArrival);
        Assert.Equal(87, source.SavedWorld.FootReturn!.SourceWorldFrame);
        _ = new GameSession(restored);
    }

    [Fact]
    public void Invalid_achievement_context_fails_and_dormant_scripted_return_restores_without_native_history()
    {
        GameplaySaveComponentDocument document = Document(Source());
        string original = JsonSerializer.Serialize(document);
        GameplaySaveProgression progress = document.Progression!;
        Assert.Throws<InvalidDataException>(() => GameplaySaveStateRestoration.Restore(document with { Progression = progress with { Transactions = progress.Transactions with { MutationContext = "invalid" } } }, Initialized()));
        GameplaySaveWorldReturn scripted = new(GameplaySaveWorldReturnKind.Scripted, 106, 1, new(1, 2, 3, 4), 5, 16, 255, 255, 0, 0, 1) { ScriptedPacket = new(1, 16, 2), ScriptedFieldId = 106, ScriptedFieldPosition = new(1, 2, 3, 4) };
        GameplaySaveComponentDocument stored = document with { Progression = progress with { World = progress.World! with { Return = scripted } } };
        string storedOriginal = JsonSerializer.Serialize(stored);
        GameState restored = GameplaySaveStateRestoration.Restore(stored, Initialized());
        Assert.Equal(scripted, restored.SavedWorld!.DormantScriptedReturn);
        Assert.Equal(scripted.ScriptedPacket, restored.SavedWorld.DormantScriptedReturn!.ScriptedPacket);
        Assert.Null(restored.SavedWorld.ScriptedReturn);
        Assert.Null(restored.WorldArrival);
        Assert.Equal(original, JsonSerializer.Serialize(document));
        Assert.Equal(storedOriginal, JsonSerializer.Serialize(stored));
        Assert.Null(document.Progression!.World!.Return);
        _ = new GameSession(restored);
    }

    [Fact]
    public void Semantic_world_rules_are_distinct_and_do_not_relax_original_import_provenance()
    {
        NativeSavedWorldState world = NativeSavedWorldState.CreateNewGame();
        Assert.Throws<InvalidDataException>(() => (world with { Origin = new(1, NativeSavedWorldOriginKind.GameplaySave, NativeSavedWorldState.OriginalSaveInitializationRule) }).Validate());
        NativeSavedWorldFootReturn original = new() { ProofVersion = 1, Rule = NativeSavedWorldFootReturn.OriginalSaveRule, SourceWorldFrame = 17, LastFieldId = 106, StoryWord = 1, WorldMode = 0, Pose0 = new(1, 2, 3, 4), CameraYaw96 = 5, EntityType105 = 0, Channel106 = 255, Channel107 = 255, AnimationVariant108 = 0, MapDisplayMode110 = 0, MapLocationId = 0 };
        original.Validate();
        Assert.Throws<InvalidDataException>(() => (original with { Rule = NativeSavedWorldFootReturn.GameplaySaveRule }).Validate());
        (original with { Rule = NativeSavedWorldFootReturn.GameplaySaveRule, SourceWorldFrame = 0 }).Validate();
        Assert.Throws<InvalidDataException>(() => new NativeWorldConfiguration(1, NativeWorldConfiguration.OriginalSaveRule, 0, 106).Validate());
        NativeWorldConfiguration.FromGameplaySave(0, 106).Validate();
    }

    private static string Hash(char value) => new(value, 64);
    private static GameplaySaveResumeDescriptor FieldResume() => new(1, GameplaySaveResumeKind.Field, 106, 105, new(123, -456, 7, 192), null);
    private static GameplaySaveComponentDocument Document(GameState source) => GameplaySaveComponentDocument.CaptureProgression(source, FieldResume());
    private static NativeLimitProgress Limits(string hash) => new(hash, 0, 0, 0, 0, ImmutableArray.CreateRange(new byte[8]));
    private static NativeCharacterAppearance Appearance(string hash) => new() { Version = 1, Rule = NativeCharacterAppearance.InitializationRule, InitOutSha256 = hash, Records = Enumerable.Repeat(new NativeCharacterDress(0), 8).ToArray() };
    private static NativeEconomy Economy(string hash) => new() { Version = 1, Rule = NativeEconomy.InitializationRule, InitOutSha256 = hash, Items = Enumerable.Repeat(new NativeItemStock(0, 0), NativeEconomy.ItemSlotCount).ToArray(), ItemOrder = Enumerable.Range(0, 32).Select(value => (byte)value).ToArray(), Gil = 0, PastGil = 0, EventBankGil72 = 0, EventBankPastGil76 = 0 };

    private static GameState Initialized()
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        return session.State with
        {
            CharacterVitals = MagicSystemFixture.Create(),
            Economy = Economy(Hash('0')),
            LimitProgress = Limits(Hash('0')),
            CharacterAppearance = Appearance(Hash('0')),
            ShopHistory = new(Hash('0'), ImmutableArray.CreateRange(new byte[400])),
            WeaponRecipes = new(Hash('0'), 0),
            ShotAvailability = new(0, [1, 2, 3, 4, 5, 6, 7, 8], Hash('0'), Hash('0')),
            PocketStation = new(NativeInputConfiguration.OriginalInitHash, ImmutableArray.CreateRange(new byte[64])),
            KnownMagic = new(NativeInputConfiguration.OriginalInitHash, [0, 0]),
            ScannedModels = new(NativeInputConfiguration.OriginalInitHash, [0, 0, 0, 0, 0]),
            InputConfiguration = new(Hash('0'), 0x00020001, Enumerable.Range(1, 12).Select(value => (byte)value).ToImmutableArray()),
            SavedBattleFlags = new(NativeInputConfiguration.OriginalInitHash, 0),
            SeeD = session.State.SeeD! with { SalaryTable = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, Enumerable.Range(0, 32).Select(value => (ushort)(value * 100)).ToImmutableArray()) }
        };
    }

    private static GameState Source()
    {
        GameState source = Initialized();
        NativeCharacterVitals characters = source.CharacterVitals!.DeepCopy();
        characters.Records[0] = characters.Records[0] with { HitPoints = new(37, 37), Experience = 123456, StatBonuses = [7, 6, 5, 4, 3, 2] };
        NativeItemStock[] items = source.Economy!.Items.ToArray();
        items[0] = new(17, 43);
        NativeCardInventory cards = source.CardInventory!.DeepCopy();
        cards.Cards[0] = 4;
        byte[] block = new byte[8192];
        block[0] = (byte)'S';
        block[1] = (byte)'C';
        block[386] = 0xFF;
        block[387] = 8;
        block[8000] = 231;
        NativeExamTimerState timer = source.ExamTimer!.SetRemaining(321).AdvanceCallbacks(NativeTimerCallbackKind.NormalField, 5);
        AchievementOwner owner = new("test", "ff8", "test-user", "test-profile");
        return source with
        {
            FieldId = 106,
            FieldLoadContext = null,
            CharacterVitals = characters,
            Economy = source.Economy with { Items = items, Gil = 123456, PastGil = 234567, EventBankGil72 = 345678, EventBankPastGil76 = 456789 },
            CharacterAppearance = source.CharacterAppearance! with { Records = Enumerable.Repeat(new NativeCharacterDress(5), 8).ToArray() },
            LimitProgress = source.LimitProgress! with { BlueMagicFlags = 123, DuelFlags = 234, AngeloFlags = 0x91, TrainingSelection = 3, TrainingCounters = [1, 2, 3, 4, 5, 6, 7, 8], PetPalsFlags = 0x17 },
            EventMemory = new(1, new Dictionary<int, NativeMemoryValue> { [300] = new(1, NativeScalarValue.Exact(0x7F)), [301] = new(2, NativeScalarValue.Exact(0x1234)), [303] = new(4, NativeScalarValue.Exact(unchecked((int)0x89ABCDEFu))) }),
            WorldEntryMemory = source.WorldEntryMemory!.WriteBytes(256, 2, 4321),
            CardInventory = cards,
            ShopHistory = new(Hash('0'), ImmutableArray.CreateRange(Enumerable.Repeat((byte)7, 400))),
            WeaponRecipes = new(Hash('0'), 7) { ObtainedWeaponsBits = 2, MagazineRecipeBits = 1 },
            ShotAvailability = source.ShotAvailability! with { Flags770 = 0x91 },
            KnownMagic = source.KnownMagic! with { Words = [0x12345678, 0x87654321] },
            ScannedModels = source.ScannedModels! with { Words = [1, 2, 3, 4, 5] },
            PocketStation = source.PocketStation! with { Raw = ImmutableArray.CreateRange(Enumerable.Repeat((byte)3, 64)), ChocoboName = ImmutableArray.CreateRange(new byte[12]) },
            RefinedCardIds = [2, 9],
            SeeD = source.SeeD! with { RankPoints = 2100, SalaryMovementCounter = 231, KillBaseline18 = 19, SalaryDisplayCountdown184 = 13, PreviousRank186 = 20, TestLevel = 15 },
            ExamTimer = timer,
            Playtime = NativePlaytimeState.FromColdProcess().AdvanceTo(timer.ClockCalls) with { Seconds = 1234 },
            EventRandom = new(12345, 7, new(ImmutableArray.CreateRange(Enumerable.Repeat((byte)3, 64)), 25)),
            CrtRandom = new(0x12345678),
            MenuRandom = new(0x789A),
            GlobalRandom = new(0x87654321),
            PendingEntityNames = [new(16, "GF", "battle:battle-1") { Draft = "REN" }],
            PendingGameplayPreferences = new() { WeaponMagazineRequired = true },
            PendingMagicModeReview = true,
            DialogueDraft = new(1, 2, 3, ""),
            BattleStatistics = new(123, 4, 5),
            BattleVariables = new([1, 2, 3, 4, 5, 6, 7, 8]),
            AppliedBattleResultIds = ["battle-1"],
            PendingAchievementFacts = [new(new("enemy-1"), new("battle-1"), new("enemy-1", BattleDefeatCredit.Character, 0, null, 0, false, BattleRewardPolicy.NativeCombat)) { Owner = owner }],
            PendingAchievementObservations = [new(new("kill-1"), new("battle-1"), 123, BattleOutcome.Victory, BattleRewardPolicy.NativeCombat, 0, owner)],
            PendingOriginalAchievementObservations = [OriginalAchievementObservedEvent.Create(new() { AllCards = false, MagazineCollection = true }, owner)],
            PendingOriginalAchievementActions = [new(new("salary-1"), new("field-1"), OriginalAchievementActionKind.FirstSalary, Owner: owner)],
            OriginalAchievementMutationContext = "00000000000000000000000000000001",
            OriginalAchievementMutationSequence = 47,
            OriginalSave = new(block) { Names = new Dictionary<int, string> { [0] = "Historical" } },
            EventState = JsonSerializer.SerializeToElement(new { FieldId = 106, Scene = new { Names = new Dictionary<int, string> { [0] = "Current" }, UnlockedTutorials = new[] { 2, 9 }, Party = new[] { 0, 4, 255 }, PartyPower = 3, MenuEnabled = true, RunEnabled = true, MapJumpDisabled = false, PlaceId = 321, NativeSalaryMovementUnits = 3, LoadedMusic = 17, MusicPlaying = true, MusicVolume = 91, NativeBattleFlagWrites = new[] { new { PersistentFlags = 0, ActiveFlags = 0 } } } })
        };
    }
}
