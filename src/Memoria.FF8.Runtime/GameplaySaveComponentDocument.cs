using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>A versioned component document for the opt-in gameplay coordinator, not a complete replacement for an execution snapshot.</summary>
public sealed record GameplaySaveComponentDocument
{
    public const string CurrentFormat = "memoria-ff8-gameplay-components";
    public const int CurrentVersion = 3;
    public const string InitialCoverage = "characters,economy,limits";
    public const string CurrentCoverage = "characters,economy,limits,durable-progression";

    [JsonRequired]
    public required string Format { get; init; }
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Coverage { get; init; }
    [JsonRequired]
    public required GameplaySaveResumeDescriptor Resume { get; init; }
    [JsonRequired]
    public required GameplaySaveCharacters Characters { get; init; }
    [JsonRequired]
    public required GameplaySaveEconomy Economy { get; init; }
    [JsonRequired]
    public required GameplaySaveLimitProgress Limits { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveProgression? Progression { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? SavedLocalAt { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveAudioPresentation? AudioPresentation { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CompletedGameData? CompletedGame { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NewGamePlusDefinition? NewGamePlus { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NewGamePlusRewardRules? NewGamePlusRewards { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleLoadoutOverrides? BattleLoadouts { get; init; }

    public NativeCharacterVitals RestoreCharacterVitals(NativeCharacterVitals installed)
    {
        if (NewGamePlus is { } run)
        {
            installed = NewGamePlusGuardianForceDefinitions.BindEmbedded(installed, run.InitialGuardianForceTemplates, run.GuardianForceExcludedDocuments);
        }
        return Characters.Restore(installed) with { LevelBasedStatBonuses = Progression?.Rules.Preferences.Tuning.LevelBasedStatBonuses ?? false };
    }

    /// <summary>Captures only the named component owners. The coordinator must separately preserve all remaining gameplay owners.</summary>
    public static GameplaySaveComponentDocument Capture(GameplaySaveResumeDescriptor resume, NativeCharacterVitals characters, NativeEconomy economy, NativeLimitProgress limits)
    {
        ArgumentNullException.ThrowIfNull(resume);
        resume.Validate();
        GameplaySaveComponentDocument result = new()
        {
            Format = CurrentFormat,
            Version = 1,
            Coverage = InitialCoverage,
            Resume = resume,
            Characters = GameplaySaveCharacters.Capture(characters),
            Economy = GameplaySaveEconomy.Capture(economy),
            Limits = GameplaySaveLimitProgress.Capture(limits)
        };
        result.Validate();
        return result;
    }

    /// <summary>Projects the declared durable owners; the caller still needs a proved semantic scene reconstruction handler.</summary>
    public static GameplaySaveComponentDocument CaptureProgression(GameState state, GameplaySaveResumeDescriptor resume)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.CharacterVitals is null || state.Economy is null || state.LimitProgress is null || resume.FieldId != state.FieldId || (resume.Kind == GameplaySaveResumeKind.World) != (state.Scene == SceneKind.WorldMap))
        {
            throw new NotSupportedException("Gameplay progression document requires its character, economy, Limit and matching scene owners.");
        }
        GameplaySaveComponentDocument result = Capture(resume, state.CharacterVitals, state.Economy, state.LimitProgress) with { Version = CurrentVersion, Coverage = CurrentCoverage, Progression = GameplaySaveProgression.Capture(state), AudioPresentation = state.AudioPresentation, NewGamePlus = state.NewGamePlus, NewGamePlusRewards = state.NewGamePlusRewards, BattleLoadouts = state.BattleLoadouts?.DeepCopy() };
        result.Validate();
        return result;
    }

    public void Validate()
    {
        if (Format != CurrentFormat || (Version == 1 ? Coverage != InitialCoverage || Progression is not null : Version is not (2 or CurrentVersion) || Coverage != CurrentCoverage || Progression is null && CompletedGame is null) || Version < 3 && (CompletedGame is not null || NewGamePlus is not null || NewGamePlusRewards is not null))
        {
            throw new InvalidDataException("Unsupported gameplay component document format, version or coverage.");
        }
        if (Resume is null || Characters is null || Economy is null || Limits is null)
        {
            throw new InvalidDataException("Gameplay component document is missing a required owner.");
        }
        Resume.Validate();
        Characters.Validate();
        Economy.Validate();
        Limits.Validate();
        BattleLoadouts?.Validate();
        Progression?.Validate();
        CompletedGame?.Validate();
        NewGamePlus?.Validate();
        NewGamePlusRewards?.Validate();
        if ((Resume.Kind == GameplaySaveResumeKind.Completed) != (CompletedGame is not null) || CompletedGame is not null && Progression is not null)
        {
            throw new InvalidDataException("Completed games require a dedicated non-resumable transfer purpose.");
        }
    }
}
