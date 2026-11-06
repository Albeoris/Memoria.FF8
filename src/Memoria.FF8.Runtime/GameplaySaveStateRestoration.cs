namespace Memoria.FF8.Runtime;

/// <summary>Installs semantic owners into a fresh resource-initialized process without executing scripts or publishing a session.</summary>
public static class GameplaySaveStateRestoration
{
    public static GameState Restore(GameplaySaveComponentDocument document, GameState initialized)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(initialized);
        document.Validate();
        if (document.Version != GameplaySaveComponentDocument.CurrentVersion || document.Progression is null)
        {
            throw new NotSupportedException("Component-only gameplay documents cannot restore a complete game.");
        }
        RequireFreshProcess(initialized);
        document.Progression.RequireRuntimeOwnership();
        GameplaySaveComponentDocument saved = GameplaySaveCodec.Detach(document);
        GameplaySaveProgression progress = saved.Progression!;
        GameplaySaveStory story = progress.Story;
        GameplaySaveCollections collections = progress.Collections;
        GameplaySaveRules rules = progress.Rules;
        GameplaySaveClocks clocks = progress.Clocks;
        NativeExamTimerState? timer = RestoreTimer(clocks.ExamTimer, progress.SceneFacts);
        GameplaySaveTransactions transactions = progress.Transactions;
        if (transactions.BattleCursor is not null || transactions.DrawCursor is not null)
        {
            throw new NotSupportedException("A live battle achievement cursor requires its encounter continuation; it cannot be resumed as exploration.");
        }
        NativeCharacterVitals characters = saved.Characters.Restore(Require(initialized.CharacterVitals, "character definitions"));
        NativeEconomy economy = saved.Economy.Restore(Require(initialized.Economy, "inventory initialization"));
        NativeLimitProgress limits = saved.Limits.Restore(Require(initialized.LimitProgress, "Limit initialization"));
        NativeWorldControlState? controls = story.Controls is { } control ? Require(initialized.WorldControls, "world controls") with { EventFlags103 = control.Flags, EventFlags104 = control.EventFlags, Menu209 = control.Menu, MusicKeep207 = control.MusicKeep } : null;
        NativeWorldEntryMemory? worldEntry = story.WorldEntry is { } bytes ? Require(initialized.WorldEntryMemory, "world-entry initialization") with { Bytes = bytes } : null;
        NativeSavedWorldState? world = progress.World is { } parked ? GameplaySaveWorldResume.RestoreSavedWorld(parked, Require(initialized.SavedWorld, "saved-world initialization"), worldEntry?.StoryWord) : null;
        NativeFieldFadeState? fade = RestoreFade(story, initialized.FieldFade);
        NativeCharacterAppearance? appearance = collections.Dresses is { } dresses ? Require(initialized.CharacterAppearance, "character appearance initialization") with { Records = dresses.Select(dress => new NativeCharacterDress(dress)).ToArray() } : null;
        NativeShopHistory? shops = collections.ShopHistory is { } shopValues ? Require(initialized.ShopHistory, "shop initialization") with { Bytes = shopValues } : null;
        NativeWeaponRecipes? recipes = collections.Recipes is { } recipeValues ? Require(initialized.WeaponRecipes, "weapon recipe initialization") with { Bits = recipeValues.Discovered, ObtainedWeaponsBits = recipeValues.ObtainedWeapons, MagazineRecipeBits = recipeValues.MagazineRecipes } : null;
        NativeShotAvailabilityState? shots = collections.ShotAvailability is byte shotFlags ? Require(initialized.ShotAvailability, "installed ammo definitions") with { Flags770 = shotFlags } : null;
        NativePocketStationState? pocket = collections.PocketStation is { } pocketValues ? Require(initialized.PocketStation, "PocketStation initialization") with { Raw = pocketValues.Values, ChocoboName = pocketValues.ChocoboName } : null;
        NativeKnownMagic? known = collections.KnownMagic is { } knownWords ? Require(initialized.KnownMagic, "known-magic initialization") with { Words = knownWords } : null;
        NativeScannedModels? scanned = collections.ScannedModels is { } scanWords ? Require(initialized.ScannedModels, "Scan initialization") with { Words = scanWords } : null;
        NativeInputConfiguration? input = rules.Input is { } inputValues ? Require(initialized.InputConfiguration, "input initialization") with { Options = inputValues.Options, ButtonMap = inputValues.ButtonMap, BattleMessageSpeed = inputValues.BattleMessageSpeed, NativeBattleSpeedByte = inputValues.BattleSpeed, FieldMessageSpeedByte = inputValues.FieldMessageSpeed, SoundVolume = inputValues.SoundVolume } : null;
        NativeSeeDState? seed = clocks.SeeD is { } seedValues ? Require(initialized.SeeD, "installed salary definitions") with { RankPoints = seedValues.RankPoints, SalaryUiEnabled = seedValues.SalaryUiEnabled, SalaryMovementCounter = seedValues.SalaryMovement, KillBaseline18 = seedValues.KillBaseline, SalaryDisplayCountdown184 = seedValues.SalaryDisplayCountdown, PreviousRank186 = seedValues.PreviousRank, TestLevel = seedValues.TestLevel } : null;
        NativeSavedBattleFlags? flags = story.BattleFlags is { } flagValues ? Require(initialized.SavedBattleFlags, "saved battle flag initialization") with { Value = flagValues.Value, KnownMask = flagValues.KnownMask, FieldWrite = flagValues.FieldWrite, WitchFieldWrite = flagValues.WitchWrite } : null;
        NativePcSaveData? original = RestoreOriginalResidue(progress);
        GameplaySaveResumeDescriptor resume = saved.Resume;
        GameplaySaveFieldPosition? field = resume.Field;
        GameState result = initialized with
        {
            SnapshotSchemaVersion = GameState.CurrentSnapshotSchemaVersion,
            Version = 1,
            Scene = resume.Kind == GameplaySaveResumeKind.Field ? SceneKind.Field : SceneKind.WorldMap,
            FieldId = resume.FieldId,
            PreviousFieldId = resume.PreviousFieldId,
            FieldName = $"field_{resume.FieldId}",
            EntryPoint = 0,
            X = field?.X ?? 0,
            Y = field?.Y ?? 0,
            FieldArrival = field is null ? null : new(resume.FieldId, field.X, field.Y, field.Triangle, field.Direction),
            FieldLoadContext = null,
            FieldRequestPacket = null,
            WorldArrival = null,
            World = null,
            DepartingWorldState = null,
            SavedWorld = world,
            WorldConfiguration = progress.WorldConfiguration is { } configuration ? NativeWorldConfiguration.FromGameplaySave(configuration.EntityType, configuration.SourceField) : null,
            NativeFieldLocation = null,
            EventMemory = story.RestoreEventMemory(),
            WorldEntryMemory = worldEntry,
            WorldControls = controls,
            DiscProcess = story.Disc is byte disc ? Require(initialized.DiscProcess, "disc initialization").Assign(disc) : null,
            FieldFade = fade,
            MessageVariables = story.MessageVariables,
            CharacterVitals = characters,
            CharacterAppearance = appearance,
            Economy = economy,
            LimitProgress = limits,
            CardInventory = collections.Cards,
            ShopHistory = shops,
            WeaponRecipes = recipes,
            ShotAvailability = shots,
            PocketStation = pocket,
            ChocoboRewards = collections.ChocoboRewards,
            KnownMagic = known,
            ScannedModels = scanned,
            RefinedCardIds = collections.RefinedCardIds,
            GameplayPreferences = rules.Preferences,
            ConfiguredGameplayTuning = rules.ConfiguredTuning,
            PendingGameplayPreferences = rules.PendingPreferences,
            PendingMagicModeReview = rules.PendingMagicModeReview,
            PendingEntityNames = rules.PendingNames,
            InputConfiguration = input,
            FullBattlePlaythrough = rules.FullBattlePlaythrough,
            RandomEncounters = rules.Encounters is { } encounters ? new(encounters.Enabled, encounters.Progress, null) : null,
            DialogueDraft = rules.DialogueChoice,
            ExamTimer = timer,
            Playtime = clocks.Playtime,
            EventRandom = clocks.EventRandom,
            CrtRandom = clocks.CrtRandom,
            MenuRandom = clocks.MenuRandom,
            GlobalRandom = clocks.GlobalRandom,
            SeeD = seed,
            GlobalSoundProcess = progress.MusicPlayback is { } playback ? Require(initialized.GlobalSoundProcess, "initialized sound process") with { MusicPlayback = playback.Restore(Require(initialized.GlobalSoundProcess?.MusicPlayback, "initialized music playback")) } : initialized.GlobalSoundProcess,
            SalaryPresentation = progress.SalaryPresentation is { } salary ? salary.Restore(Require(initialized.SalaryPresentation, "initialized salary presentation")) : initialized.SalaryPresentation,
            SavedBattleFlags = flags,
            AppliedBattleResultIds = transactions.AppliedBattleResults,
            BattleStatistics = transactions.BattleStatistics,
            BattleVariables = transactions.BattleVariables,
            PendingAchievementFacts = transactions.PendingDefeats,
            PendingAchievementObservations = transactions.PendingKillObservations,
            PendingOriginalAchievementObservations = transactions.PendingOriginalObservations,
            PendingOriginalAchievementActions = transactions.PendingOriginalActions,
            OriginalAchievementDrawCursor = null,
            OriginalAchievementMutationContext = transactions.MutationContext,
            OriginalAchievementMutationSequence = transactions.MutationSequence,
            LastOriginalAchievementObservation = transactions.LastOriginalObservation,
            AchievementCursor = null,
            OriginalSave = original,
            NativeSaveInteroperability = progress.NativeInteroperability ?? (original is not null ? NativeSaveInteroperability.Capture(original) : null),
            GameplaySceneFacts = progress.SceneFacts,
            GameplayFieldLocation = RestoreFieldLocation(progress, original, resume),
            // Committed outcome IDs remain authoritative; former native abandonment waits are retired by normal load.
            AbandonedNativeBattles = null,
            PendingBattleEncounter = null,
            Battle = null,
            TripleTriadPlay = null,
            EventState = null,
            WorldEventState = null,
            DepartingFieldState = null,
            NativeGameOver = null,
            NativeFieldDebugRequested = false,
            DebugView = null,
            MovieSeconds = 0,
            MoviePlayback = null,
            MovieSource = null
        };
        ValidateOwners(result);
        return result;
    }

    private static void RequireFreshProcess(GameState state)
    {
        if (state.Scene != SceneKind.Field || state.FieldId != 74 || state.FieldLoadContext is not { Origin: NativeFieldLoadOrigin.NewGame } || state.SavedWorld?.Origin.Kind != NativeSavedWorldOriginKind.NativeNewGame || state.EventState is not null || state.WorldEventState is not null || state.DepartingFieldState is not null || state.World is not null || state.DepartingWorldState is not null || state.Battle is not null || state.PendingBattleEncounter is not null || state.TripleTriadPlay is not null || state.OriginalSave is not null || state.NativeGameOver is not null || state.NativeBattleMemory is null)
        {
            throw new InvalidOperationException("Gameplay restoration requires a fresh initialized process before any field VM, world or battle has run.");
        }
    }

    private static T Require<T>(T? value, string owner) where T : class
    {
        return value ?? throw new NotSupportedException($"Gameplay restoration requires current {owner} from installed resources.");
    }

    private static NativeFieldFadeState? RestoreFade(GameplaySaveStory story, NativeFieldFadeState? initialized)
    {
        if ((story.SavedFade is null) != (story.MapFadeDisabled is null))
        {
            throw new InvalidDataException("Gameplay fade state must preserve both the saved words and map flag, or explicitly omit both.");
        }
        return story.SavedFade is { } words ? Require(initialized, "fade initialization") with { Saved = words, MapFadeDisabled = story.MapFadeDisabled!.Value } : null;
    }

    private static NativeExamTimerState? RestoreTimer(NativeExamTimerState? timer, GameplaySaveSceneFacts? scene)
    {
        if (scene?.PersistentBattleMode is not ushort persistent || scene.ActiveBattleMode is not ushort active)
        {
            throw new NotSupportedException("Gameplay restoration requires current complete persistent and active battle-mode words.");
        }
        if (timer is null)
        {
            return null;
        }
        bool event182Mask4 = (persistent & 4) != 0;
        bool event183Mask1 = (persistent & 0x100) != 0;
        bool saveGlobalMask4 = (active & 4) != 0;
        if (timer.Event182Mask4 is bool saved182 && saved182 != event182Mask4 || timer.Event183Mask1 is bool saved183 && saved183 != event183Mask1 || timer.SaveGlobalMask4 is bool savedGlobal && savedGlobal != saveGlobalMask4)
        {
            throw new InvalidDataException("Gameplay quest timer masks contradict the saved complete battle-mode words.");
        }
        return timer with { Event182Mask4 = event182Mask4, Event183Mask1 = event183Mask1, SaveGlobalMask4 = saveGlobalMask4 };
    }

    private static GameplaySaveFieldLocation? RestoreFieldLocation(GameplaySaveProgression progress, NativePcSaveData? original, GameplaySaveResumeDescriptor resume)
    {
        GameplaySaveFieldLocation? location = progress.FieldLocation;
        if (original is null || progress.FieldContinuation is not null || resume.Kind != GameplaySaveResumeKind.Field || System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(original.Block.AsSpan(NativePcSaveData.MainOffset + 3312)) == 2 || System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(original.Block.AsSpan(NativePcSaveData.MainOffset + 3314)) != resume.FieldId)
        {
            return location;
        }
        NativeFieldLocationPose?[] poses = Enumerable.Range(0, 3).Select(slot => location?.PartyPositions[slot] ?? original.ReadFieldPartyPosition(slot)).ToArray();
        return new(checked((ushort)resume.FieldId), location?.PreviousFieldId, poses) { IsNativeImport = true };
    }

    private static NativePcSaveData? RestoreOriginalResidue(GameplaySaveProgression progress)
    {
        if (progress.OriginalResidue is not { } residue)
        {
            return null;
        }
        Dictionary<int, string> names = residue.Names.ToDictionary();
        foreach ((int subject, string name) in progress.SceneFacts?.Names ?? [])
        {
            names[subject] = name;
        }
        return new(residue.Block) { Names = names };
    }

    private static void ValidateOwners(GameState state)
    {
        state.CharacterVitals!.ValidateMagicSystemConsistency();
        if (state.CharacterVitals.MagicSystem.SharedMagicStock != state.GameplayPreferences.SharedMagicStock || state.CharacterVitals.MagicSystem.InfiniteMagic != state.GameplayPreferences.InfiniteMagic || state.CharacterVitals.MagicSystem.UnlimitedMagic != state.GameplayPreferences.Tuning.UnlimitedMagic || state.Economy!.InitOutSha256 != state.CharacterVitals.InitOutSha256 || state.CharacterAppearance is { } appearance && appearance.InitOutSha256 != state.CharacterVitals.InitOutSha256 || state.SavedBattleFlags is { } flags && ((flags.Value & 1) != 0) != state.CharacterVitals.DreamEnabled)
        {
            throw new InvalidDataException("Restored gameplay owners disagree about their installed definitions, magic rules or dream alias.");
        }
        NativeEventMemoryOwnership.Validate(state.EventMemory, NativeEventMemoryOwnership.RootRegions(state));
        if (state.SavedWorld is not null && (state.WorldEntryMemory is null || state.EventMemory.CellRanges().Any(cell => cell.Address < 1396 && (long)cell.Address + cell.Width > 1392)))
        {
            throw new InvalidDataException("Restored saved-world state requires canonical story/fuel owners without generic aliases.");
        }
        if (state.Playtime is { } playtime && (state.ExamTimer is null || playtime.ClockCalls != state.ExamTimer.ClockCalls))
        {
            throw new InvalidDataException("Restored playtime and quest timer disagree about their admitted callback count.");
        }
        if (state.OriginalAchievementMutationSequence < 0 || state.OriginalAchievementMutationContext is null && state.OriginalAchievementMutationSequence != 0 || state.OriginalAchievementMutationContext is { } context && !Guid.TryParseExact(context, "N", out _))
        {
            throw new InvalidDataException("Restored achievement mutations require their saved context and nonnegative sequence.");
        }
        foreach (OriginalAchievementObservedEvent observation in state.PendingOriginalAchievementObservations)
        {
            observation.Validate();
        }
        state.LastOriginalAchievementObservation?.Validate();
        state.SavedWorld?.Validate();
        state.WorldConfiguration?.Validate();
        state.CharacterAppearance?.Validate();
        state.ShopHistory?.Validate();
        state.WeaponRecipes?.Validate();
        state.ShotAvailability?.Validate();
        state.PocketStation?.Validate();
        state.KnownMagic?.Validate();
        state.ScannedModels?.Validate();
        state.InputConfiguration?.Validate();
        state.SeeD?.Validate();
        state.SavedBattleFlags?.Validate();
        state.FieldFade?.Validate();
    }
}
