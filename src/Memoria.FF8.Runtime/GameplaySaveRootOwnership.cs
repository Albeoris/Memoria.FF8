namespace Memoria.FF8.Runtime;

internal enum GameplaySavePersistenceRequirement
{
    Durable,
    Split,
    ResumeHandler,
    Runtime,
    UnsupportedActiveOperation,
    Derived
}

internal sealed record GameplaySaveRootMemberOwnership(string Component, GameplaySavePersistenceRequirement Requirement);

/// <summary>Explicit review inventory. A new GameState member must be classified before gameplay persistence can omit it.</summary>
internal static class GameplaySaveRootOwnership
{
    internal static IReadOnlyDictionary<string, GameplaySaveRootMemberOwnership> Members { get; } = Create();

    private static IReadOnlyDictionary<string, GameplaySaveRootMemberOwnership> Create()
    {
        Dictionary<string, GameplaySaveRootMemberOwnership> members = new(StringComparer.Ordinal);
        void Add(string component, GameplaySavePersistenceRequirement requirement, params string[] names)
        {
            foreach (string name in names)
            {
                members.Add(name, new(component, requirement));
            }
        }
        Add("AudioPresentation", GameplaySavePersistenceRequirement.Split, nameof(GameState.AudioPresentation));
        Add("Characters", GameplaySavePersistenceRequirement.Durable, nameof(GameState.CharacterVitals));
        Add("Economy", GameplaySavePersistenceRequirement.Durable, nameof(GameState.Economy));
        Add("Limits", GameplaySavePersistenceRequirement.Durable, nameof(GameState.LimitProgress));
        Add("Progression.OriginalResidue", GameplaySavePersistenceRequirement.Durable, nameof(GameState.OriginalSave));
        Add("Progression.Transactions", GameplaySavePersistenceRequirement.Durable, nameof(GameState.PendingAchievementFacts), nameof(GameState.PendingAchievementObservations), nameof(GameState.PendingOriginalAchievementObservations), nameof(GameState.PendingOriginalAchievementActions), nameof(GameState.OriginalAchievementDrawCursor), nameof(GameState.OriginalAchievementMutationContext), nameof(GameState.OriginalAchievementMutationSequence), nameof(GameState.LastOriginalAchievementObservation), nameof(GameState.AchievementCursor), nameof(GameState.AppliedBattleResultIds), nameof(GameState.BattleVariables), nameof(GameState.BattleStatistics));
        Add("Progression.Transactions", GameplaySavePersistenceRequirement.Split, nameof(GameState.AbandonedNativeBattles));
        Add("Progression.Story", GameplaySavePersistenceRequirement.Durable, nameof(GameState.EventMemory), nameof(GameState.WorldEntryMemory), nameof(GameState.WorldControls), nameof(GameState.DiscProcess), nameof(GameState.SavedBattleFlags), nameof(GameState.MessageVariables));
        Add("Progression.Story", GameplaySavePersistenceRequirement.Split, nameof(GameState.FieldFade));
        Add("Progression.Collections", GameplaySavePersistenceRequirement.Durable, nameof(GameState.RefinedCardIds), nameof(GameState.KnownMagic), nameof(GameState.ScannedModels), nameof(GameState.ShopHistory), nameof(GameState.WeaponRecipes), nameof(GameState.CharacterAppearance), nameof(GameState.CardInventory), nameof(GameState.ShotAvailability), nameof(GameState.PocketStation), nameof(GameState.ChocoboRewards));
        Add("Progression.Rules", GameplaySavePersistenceRequirement.Durable, nameof(GameState.GameplayPreferences), nameof(GameState.ConfiguredGameplayTuning), nameof(GameState.PendingGameplayPreferences), nameof(GameState.PendingMagicModeReview), nameof(GameState.PendingEntityNames), nameof(GameState.InputConfiguration), nameof(GameState.FullBattlePlaythrough), nameof(GameState.DialogueDraft));
        Add("Progression.Rules", GameplaySavePersistenceRequirement.Split, nameof(GameState.RandomEncounters));
        Add("Progression.Clocks", GameplaySavePersistenceRequirement.Durable, nameof(GameState.CrtRandom), nameof(GameState.ExamTimer), nameof(GameState.Playtime), nameof(GameState.EventRandom), nameof(GameState.MenuRandom), nameof(GameState.GlobalRandom), nameof(GameState.SeeD));
        Add("Progression.World", GameplaySavePersistenceRequirement.Split, nameof(GameState.SavedWorld), nameof(GameState.WorldConfiguration), nameof(GameState.NativeFieldLocation));
        Add("Progression.SceneFacts and semantic resume", GameplaySavePersistenceRequirement.Split, nameof(GameState.EventState), nameof(GameState.WorldEventState), nameof(GameState.DepartingFieldState));
        Add("Resume and scene reconstruction", GameplaySavePersistenceRequirement.ResumeHandler, nameof(GameState.Scene), nameof(GameState.FieldId), nameof(GameState.PreviousFieldId), nameof(GameState.EntryPoint), nameof(GameState.X), nameof(GameState.Y), nameof(GameState.FieldArrival), nameof(GameState.FieldRequestPacket), nameof(GameState.FieldLoadContext), nameof(GameState.WorldArrival), nameof(GameState.World), nameof(GameState.DepartingWorldState));
        Add("Active operation not yet supported", GameplaySavePersistenceRequirement.UnsupportedActiveOperation, nameof(GameState.TripleTriadPlay), nameof(GameState.PendingBattleEncounter), nameof(GameState.NativeGameOver), nameof(GameState.Battle));
        Add("Fresh normal-load process", GameplaySavePersistenceRequirement.Runtime, nameof(GameState.NativeFieldDebugRequested), nameof(GameState.FieldBars), nameof(GameState.NativeBattleMemory), nameof(GameState.MovieIndex), nameof(GameState.MovieSeconds), nameof(GameState.MoviePlayback), nameof(GameState.MovieSource), nameof(GameState.VibrationProcess), nameof(GameState.FieldButtonInput), nameof(GameState.GlobalSoundProcess), nameof(GameState.SalaryPresentation), nameof(GameState.FieldShake), nameof(GameState.WorldProcess), nameof(GameState.DebugView));
        Add("Schema, resource identity or canonical alias", GameplaySavePersistenceRequirement.Derived, nameof(GameState.SnapshotSchemaVersion), nameof(GameState.Version), nameof(GameState.FieldName), nameof(GameState.MagicSystem), nameof(GameState.Variables), nameof(GameState.NativeVariableValues));
        return members.AsReadOnly();
    }
}
