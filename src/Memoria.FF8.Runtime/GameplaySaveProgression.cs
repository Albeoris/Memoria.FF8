using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>Durable root owners, independently of scene reconstruction. Capturing this component does not certify a complete playable save.</summary>
public sealed record GameplaySaveProgression
{
    public const int CurrentRuntimeOwnershipVersion = 1;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RuntimeOwnershipVersion { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveMusicPlayback? MusicPlayback { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveSalaryPresentation? SalaryPresentation { get; init; }

    [JsonRequired]
    public required GameplaySaveStory Story { get; init; }
    [JsonRequired]
    public required GameplaySaveWorldData? World { get; init; }
    [JsonRequired]
    public required GameplaySaveWorldConfiguration? WorldConfiguration { get; init; }
    [JsonRequired]
    public required GameplaySaveFieldLocation? FieldLocation { get; init; }
    [JsonRequired]
    public required GameplaySaveCollections Collections { get; init; }
    [JsonRequired]
    public required GameplaySaveRules Rules { get; init; }
    [JsonRequired]
    public required GameplaySaveClocks Clocks { get; init; }
    [JsonRequired]
    public required GameplaySaveTransactions Transactions { get; init; }
    [JsonRequired]
    public required GameplaySaveSceneFacts? SceneFacts { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveOriginalResidue? OriginalResidue { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSaveInteroperability? NativeInteroperability { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveFieldContinuation? FieldContinuation { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveWorldUi? WorldUi { get; init; }

    /// <summary>Moves current field-local authority out of durable globals into the logical continuation.</summary>
    public GameplaySaveProgression WithFieldContinuation(GameplaySaveFieldContinuation continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        return (this with { FieldContinuation = continuation.Detach() }).NormalizeFieldContinuation();
    }

    /// <summary>Accepts equivalent transitional aliases on reads and returns a separate canonical owner projection.</summary>
    public GameplaySaveProgression NormalizeFieldContinuation()
    {
        if (FieldContinuation is not { } continuation)
        {
            return this;
        }
        continuation.Validate();
        GameplaySaveEventCell[] locals = Story.Cells.Where(cell => cell.Address < 2304 && (long)cell.Address + cell.Width > 1024).OrderBy(cell => cell.Address).ToArray();
        if (locals.Length != 0 && !locals.SequenceEqual(continuation.Scripts.Locals.OrderBy(cell => cell.Address)))
        {
            throw new InvalidDataException("Gameplay global/local aliases disagree with the authoritative field continuation.");
        }
        return this with { Story = Story with { Cells = Story.Cells.Where(cell => cell.Address >= 2304 || (long)cell.Address + cell.Width <= 1024).ToArray() }, FieldLocation = null };
    }

    public static GameplaySaveProgression Capture(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.RequiresLegacyLimitInitialization)
        {
            throw new InvalidOperationException("Historical Limit features must be initialized with the selected game's definitions before gameplay save capture.");
        }
        if (state.Scene is not (SceneKind.Field or SceneKind.WorldMap) || state.Battle is not null || state.PendingBattleEncounter is not null || state.TripleTriadPlay is not null || state.RandomEncounters?.Pending is not null || state.NativeGameOver is not null)
        {
            throw new NotSupportedException("Gameplay progression projection requires a field or world with no active battle, card match, pending encounter or game-over request.");
        }
        if (state.Scene == SceneKind.WorldMap && state.EventMemory.Cells.Any(pair => pair.Key < 1024 && (long)pair.Key + pair.Value.Width > 1024 || pair.Key < 2304 && (long)pair.Key + pair.Value.Width > 2304))
        {
            throw new NotSupportedException("Gameplay world projection cannot split a cross-owner field-local event cell.");
        }
        GameplaySaveProgression result = new()
        {
            RuntimeOwnershipVersion = CurrentRuntimeOwnershipVersion,
            MusicPlayback = GameplaySaveMusicPlayback.Capture(state.GlobalSoundProcess?.MusicPlayback),
            SalaryPresentation = GameplaySaveSalaryPresentation.Capture(state.SalaryPresentation),
            Story = GameplaySaveStory.Capture(state),
            World = state.SavedWorld is { } world ? GameplaySaveWorldData.Capture(world) : null,
            WorldUi = GameplaySaveWorldUi.Capture(state),
            WorldConfiguration = state.WorldConfiguration is { } configuration ? new(configuration.EntityByte, configuration.SourceField) : null,
            FieldLocation = state.NativeFieldLocation is { } location ? new(location.FieldId, location.PreviousFieldId, location.Slots.Select(slot => slot.Pose).ToArray()) : state.GameplayFieldLocation,
            Collections = GameplaySaveCollections.Capture(state),
            Rules = GameplaySaveRules.Capture(state),
            Clocks = GameplaySaveClocks.Capture(state),
            Transactions = GameplaySaveTransactions.Capture(state),
            SceneFacts = GameplaySaveSceneFacts.Capture(state),
            OriginalResidue = null,
            NativeInteroperability = state.NativeSaveInteroperability ?? (state.OriginalSave is { } original ? NativeSaveInteroperability.Capture(original) : null)
        };
        if (state.Scene == SceneKind.WorldMap)
        {
            // No field VM is resumed by a new world normal-load document.
            result = result with { Story = result.Story with { Cells = result.Story.Cells.Where(cell => cell.Address >= 2304 || (long)cell.Address + cell.Width <= 1024).ToArray() } };
        }
        result.Validate();
        return GameplaySaveCodec.Detach(result);
    }

    public void Validate()
    {
        if (RuntimeOwnershipVersion is not (null or CurrentRuntimeOwnershipVersion))
        {
            throw new InvalidDataException("Unsupported gameplay runtime-ownership capability version.");
        }
        if (Story is null || Collections is null || Rules is null || Clocks is null || Transactions is null)
        {
            throw new InvalidDataException("Gameplay progression is missing a required owner group.");
        }
        Story.Validate();
        MusicPlayback?.Validate();
        SalaryPresentation?.Validate();
        World?.Validate();
        FieldLocation?.Validate();
        Collections.Validate();
        Rules.Validate();
        Clocks.Validate();
        Transactions.Validate();
        SceneFacts?.Validate();
        OriginalResidue?.Validate();
        NativeInteroperability?.Validate();
        FieldContinuation?.Validate();
        WorldUi?.Validate();
        if (WorldUi is not null && (World is null || FieldContinuation is not null || SceneFacts is null || SceneFacts.Origin == GameplaySaveSceneFactsOrigin.Field))
        {
            throw new InvalidDataException("Gameplay world UI requires its ordinary world scene facts.");
        }
        if (FieldContinuation is { } continuation)
        {
            if (SceneFacts is not { Origin: GameplaySaveSceneFactsOrigin.Field } facts || facts.FieldId != continuation.Scripts.FieldId)
            {
                throw new InvalidDataException("A gameplay field continuation requires its current field scene facts.");
            }
            _ = NormalizeFieldContinuation();
        }
    }

    public void RequireRuntimeOwnership()
    {
        if (RuntimeOwnershipVersion != CurrentRuntimeOwnershipVersion)
        {
            throw new NotSupportedException("This gameplay save uses an older continuation format and requires migration from a full standalone snapshot. It does not contain the bar, shake, music and salary state needed to resume reliably.");
        }
    }
}

public sealed record GameplaySaveEventCell([property: JsonRequired] int Address, [property: JsonRequired] int Width, [property: JsonRequired] int Value);
public sealed record GameplaySaveWorldConfiguration([property: JsonRequired] byte EntityType, [property: JsonRequired] ushort SourceField);
public sealed record GameplaySaveFieldLocation([property: JsonRequired] ushort FieldId, [property: JsonRequired] ushort? PreviousFieldId, [property: JsonRequired] NativeFieldLocationPose?[] PartyPositions)
{
    /// <summary>Native slot storage includes dormant empty-party poses and does not establish an actor binding.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsNativeImport { get; init; }

    public void Validate()
    {
        if (FieldId < 72 || PartyPositions is not { Length: 3 })
        {
            throw new InvalidDataException("Gameplay field location requires three logical party positions.");
        }
    }
}
public sealed record GameplaySaveWorldControls([property: JsonRequired] byte Flags, [property: JsonRequired] uint EventFlags, [property: JsonRequired] byte Menu, [property: JsonRequired] byte MusicKeep);
public sealed record GameplaySaveBattleFlags([property: JsonRequired] byte Value, [property: JsonRequired] byte? KnownMask, [property: JsonRequired] NativeSavedBattleFlagFieldWrite? FieldWrite, [property: JsonRequired] NativeSavedBattleFlagFieldWrite? WitchWrite);

public sealed record GameplaySaveStory([property: JsonRequired] GameplaySaveEventCell[] Cells, [property: JsonRequired] ImmutableArray<byte>? WorldEntry, [property: JsonRequired] GameplaySaveWorldControls? Controls, [property: JsonRequired] byte? Disc, [property: JsonRequired] GameplaySaveBattleFlags? BattleFlags, [property: JsonRequired] NativeFadeWords? SavedFade, [property: JsonRequired] byte? MapFadeDisabled, [property: JsonRequired] NativeMessageVariables? MessageVariables)
{
    internal static GameplaySaveStory Capture(GameState state)
    {
        state.EventMemory.Validate();
        NativeEventMemoryOwnership.Validate(state.EventMemory, NativeEventMemoryOwnership.RootRegions(state));
        state.WorldEntryMemory?.Validate();
        state.WorldControls?.Validate();
        state.DiscProcess?.Validate();
        state.SavedBattleFlags?.Validate();
        state.FieldFade?.Validate();
        List<GameplaySaveEventCell> cells = [];
        foreach ((int address, NativeMemoryValue cell) in state.EventMemory.Cells.OrderBy(pair => pair.Key))
        {
            if (!cell.Value.IsExact)
            {
                throw new NotSupportedException("Gameplay event variables require exact values; uncertain executed reads need a semantic event adapter.");
            }
            cells.Add(new(address, cell.Width, cell.Value.Values[0]));
        }
        return new(cells.ToArray(), state.WorldEntryMemory?.Bytes, state.WorldControls is { } controls ? new(controls.EventFlags103, controls.EventFlags104, controls.Menu209, controls.MusicKeep207) : null, state.DiscProcess?.SavedDisc204, state.SavedBattleFlags is { } flags ? new(flags.Value, flags.KnownMask, flags.FieldWrite, flags.WitchFieldWrite) : null, state.FieldFade?.Saved, state.FieldFade?.MapFadeDisabled, state.MessageVariables);
    }

    public NativeEventMemory RestoreEventMemory()
    {
        if (Cells is null || Cells.Any(cell => cell is null) || Cells.Select(cell => cell.Address).Distinct().Count() != Cells.Length)
        {
            throw new InvalidDataException("Gameplay event cells are missing or duplicated.");
        }
        return new(NativeEventMemory.CurrentVersion, Cells.ToDictionary(cell => cell.Address, cell => new NativeMemoryValue(cell.Width, NativeScalarValue.Exact(cell.Value))));
    }

    public void Validate()
    {
        RestoreEventMemory().Validate();
        if (WorldEntry is { } bytes && (bytes.IsDefault || bytes.Length != NativeWorldEntryMemory.ByteCount))
        {
            throw new InvalidDataException("Gameplay story has malformed world-entry or fade flags.");
        }
        if (BattleFlags is { } flags)
        {
            new NativeSavedBattleFlags(NativeInputConfiguration.OriginalInitHash, flags.Value, flags.KnownMask, flags.FieldWrite, flags.WitchWrite).Validate();
        }
    }
}

/// <summary>Bounded original-format compatibility residue for saved fields that do not yet have a typed owner.</summary>
public sealed record GameplaySaveOriginalResidue([property: JsonRequired] byte[] Block, [property: JsonRequired] Dictionary<int, string> Names)
{
    internal static GameplaySaveOriginalResidue Capture(NativePcSaveData source)
    {
        source.Validate();
        return new(source.Block.ToArray(), source.Names.ToDictionary());
    }

    public void Validate()
    {
        new NativePcSaveData(Block).Validate();
        if (Names is null || Names.Any(pair => !GameNames.IsValidSubject(pair.Key) || !GameNames.IsValidDraft(pair.Value)))
        {
            throw new InvalidDataException("Original compatibility names are malformed.");
        }
    }
}
