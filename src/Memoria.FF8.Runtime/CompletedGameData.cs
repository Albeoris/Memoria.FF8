using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

/// <summary>Self-contained FFVIII clear data, independent of any ending VM or source path.</summary>
public sealed record CompletedGameData
{
    public int SchemaVersion { get; init; } = 1;
    public string Campaign { get; init; } = "ff8";
    public string RunIdentity { get; init; } = "";
    public string? ParentCompletionIdentity { get; init; }
    public required Ff8CompletionState Completion { get; init; }
    public required GameplayPreferences Settings { get; init; }
    public ImmutableArray<ImmutableArray<GuardianForceAbilityTemplate>> GuardianForceTemplates { get; init; } = [];
    public ImmutableArray<string> EmbeddedGuardianForceDocuments { get; init; } = [];
    public NativeCardInventory? Cards { get; init; }
    public bool? FullBattlePlaythrough { get; init; }
    public bool? RandomEncountersEnabled { get; init; }
    public NativePlaytimeState? Playtime { get; init; }
    public int[] Portraits { get; init; } = [];
    public required string InitializationIdentity { get; init; }
    public required string ContentIdentity { get; init; }

    public void Validate()
    {
        if (SchemaVersion != 1 || Campaign != "ff8" || Completion is null || Settings is null || Cards is null || InitializationIdentity is not { Length: 64 } || ContentIdentity is not { Length: 64 } || Portraits is null || Portraits.Length > 11 || Portraits.Any(id => id is < 0 or > 10) || GuardianForceTemplates.IsDefault || GuardianForceTemplates.Length != 16 || GuardianForceTemplates.Any(row => row.IsDefault || row.Any(entry => entry is null)) || EmbeddedGuardianForceDocuments.IsDefault)
        {
            throw new InvalidDataException("Unsupported or incomplete FFVIII completed game data.");
        }
        Completion.Validate();
        Settings.Validate();
        Cards.Validate();
        if (EmbeddedGuardianForceDocuments.Distinct(StringComparer.Ordinal).Count() != EmbeddedGuardianForceDocuments.Length)
        {
            throw new InvalidDataException("Completed Game GF document identities must be distinct.");
        }
        foreach (string identity in EmbeddedGuardianForceDocuments)
        {
            GameplaySaveGuardianForceIntent.ValidateIdentity(identity);
        }
        new GuardianForceAbilityProfile(GuardianForceTemplates, "completed-game-effective-definitions-v1", [], [], EmbeddedGuardianForceDocuments).Validate();
    }

    public static GameplaySaveComponentDocument Capture(GameState state)
    {
        if (state.Ff8Completion is not { } completion || state.Battle is not null || state.PendingBattleEncounter is not null || state.CharacterVitals is not { } characters || state.Economy is null || state.LimitProgress is null || state.CardInventory is null)
        {
            throw new InvalidOperationException("A completed game can only be saved after the ending and credits have finished.");
        }
        GameplaySaveComponentDocument result = new()
        {
            Format = GameplaySaveComponentDocument.CurrentFormat,
            Version = GameplaySaveComponentDocument.CurrentVersion,
            Coverage = GameplaySaveComponentDocument.CurrentCoverage,
            Resume = new(1, GameplaySaveResumeKind.Completed, completion.FieldId, null, null, null),
            Characters = GameplaySaveCharacters.Capture(characters),
            Economy = GameplaySaveEconomy.Capture(state.Economy),
            Limits = GameplaySaveLimitProgress.Capture(state.LimitProgress),
            SavedLocalAt = DateTimeOffset.Now,
            CompletedGame = new()
            {
                Completion = completion,
                RunIdentity = string.IsNullOrEmpty(state.NewGamePlus?.RunIdentity) ? completion.Identity : state.NewGamePlus.RunIdentity,
                ParentCompletionIdentity = state.NewGamePlus?.SourceCompletionIdentity,
                Settings = state.GameplayPreferences,
                Cards = state.CardInventory,
                Playtime = state.Playtime,
                FullBattlePlaythrough = state.FullBattlePlaythrough,
                RandomEncountersEnabled = state.RandomEncounters?.Enabled,
                Portraits = SaveSummary.CapturePartyPortraits(state),
                InitializationIdentity = characters.InitOutSha256,
                ContentIdentity = characters.KernelSha256,
                GuardianForceTemplates = characters.ResolveGuardianForceTemplates(),
                EmbeddedGuardianForceDocuments = characters.GuardianForceProfile is { } profile ? profile.AppliedLayers.Select(layer => layer.Document.DocumentId).Concat(profile.LocalDocuments.Select(document => document.DocumentId)).Concat(profile.ExcludedDocuments).Distinct(StringComparer.Ordinal).ToImmutableArray() : []
            }
        };
        result.Validate();
        return GameplaySaveCodec.Detach(result);
    }

    public static GameState ToTransferState(GameplaySaveComponentDocument document, GameState initialized)
    {
        document.Validate();
        CompletedGameData clear = document.CompletedGame ?? throw new InvalidDataException("Select a Completed Game save to configure New Game+.");
        NativeCharacterVitals characters = initialized.CharacterVitals ?? throw new NotSupportedException("New Game+ requires initialized character definitions.");
        NativeEconomy economy = initialized.Economy ?? throw new NotSupportedException("New Game+ requires initialized inventory definitions.");
        characters = NewGamePlusGuardianForceDefinitions.BindEmbedded(characters, clear.GuardianForceTemplates, clear.EmbeddedGuardianForceDocuments);
        GameplaySaveCharacters transfer = document.Characters with { GuardianForceIntent = null };
        return initialized with
        {
            Scene = SceneKind.Title,
            CharacterVitals = transfer.Restore(characters),
            Economy = document.Economy.Restore(economy),
            CardInventory = clear.Cards,
            GameplayPreferences = clear.Settings,
            Playtime = clear.Playtime,
            FullBattlePlaythrough = clear.FullBattlePlaythrough,
            RandomEncounters = clear.RandomEncountersEnabled is bool enabled ? new(enabled, NativeRandomEncounterState.FromColdProcess(), null) : null,
            CompletedGame = clear,
            NewGamePlus = null,
            NewGamePlusRewards = null,
            Ff8Completion = null,
            EventState = null
        };
    }
}
