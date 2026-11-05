using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum GameplaySaveSceneFactsOrigin
{
    Field,
    WorldEventContext,
    SuspendedField,
    PreparedWorldField
}

public sealed record GameplaySaveMusicChannel([property: JsonRequired] int ChannelId, [property: JsonRequired] byte? MusicId, [property: JsonRequired] uint? ChoiceMask);
public sealed record GameplaySaveSceneMusic([property: JsonRequired] int LoadedMusic, [property: JsonRequired] bool Playing, [property: JsonRequired] int Volume, [property: JsonRequired] byte? BattleMusic, [property: JsonRequired] byte? MusicVolume, [property: JsonRequired] byte? SoundVolume, [property: JsonRequired] GameplaySaveMusicChannel? PrimaryChannel, [property: JsonRequired] GameplaySaveMusicChannel? SecondaryChannel, [property: JsonRequired] byte? WorldSelection);

/// <summary>Persistent scene facts only. A suspended field's facts never replace current root global owners.</summary>
public sealed record GameplaySaveSceneFacts([property: JsonRequired] GameplaySaveSceneFactsOrigin Origin, [property: JsonRequired] int FieldId, [property: JsonRequired] Dictionary<int, string> Names, [property: JsonRequired] int[] UnlockedTutorials, [property: JsonRequired] byte[] FieldParty, [property: JsonRequired] int PartyPower, [property: JsonRequired] bool MenuEnabled, [property: JsonRequired] bool RunEnabled, [property: JsonRequired] bool MapJumpDisabled, [property: JsonRequired] int PlaceId, [property: JsonRequired] byte? PreviousSalaryMovement, [property: JsonRequired] GameplaySaveSceneMusic? Music, [property: JsonRequired] GameplaySaveSceneMusic? SuspendedFieldMusic)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? PersistentBattleMode { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? ActiveBattleMode { get; init; }

    /// <summary>Projects the current field process after the host has published its fresh snapshot, including world callbacks.</summary>
    public static GameplaySaveSceneFacts? FromPreparedState(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return Capture(state, true);
    }

    internal static GameplaySaveSceneFacts? Capture(GameState state) => Capture(state, false);

    private static GameplaySaveSceneFacts? Capture(GameState state, bool prepared)
    {
        JsonElement? captured;
        GameplaySaveSceneFactsOrigin origin;
        if (state.Scene == SceneKind.WorldMap)
        {
            if (state.WorldEventState is { } world)
            {
                captured = world;
                origin = GameplaySaveSceneFactsOrigin.WorldEventContext;
            }
            else
            {
                captured = state.EventState ?? state.DepartingFieldState;
                origin = prepared && state.EventState is not null ? GameplaySaveSceneFactsOrigin.PreparedWorldField : GameplaySaveSceneFactsOrigin.SuspendedField;
            }
        }
        else
        {
            captured = state.EventState;
            origin = GameplaySaveSceneFactsOrigin.Field;
        }
        if (captured is not { } context)
        {
            return null;
        }
        if (context.ValueKind != JsonValueKind.Object || !context.TryGetProperty("Scene", out JsonElement scene) || scene.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Gameplay scene facts require an explicit captured Scene object.");
        }
        Dictionary<int, string> names = state.OriginalSave?.Names.ToDictionary() ?? [];
        foreach (JsonProperty property in Required(scene, "Names").EnumerateObject())
        {
            if (!int.TryParse(property.Name, NumberStyles.None, CultureInfo.InvariantCulture, out int subject))
            {
                throw new InvalidDataException("Gameplay scene name has an invalid subject.");
            }
            names[subject] = property.Value.GetString() ?? throw new InvalidDataException("Gameplay scene name cannot be null.");
        }
        GameplaySaveSceneMusic music = new(Required(scene, "LoadedMusic").GetInt32(), Required(scene, "MusicPlaying").GetBoolean(), Required(scene, "MusicVolume").GetInt32(), Optional<byte>(scene, "NativeBattleMusic203"), Optional<byte>(scene, "SoundMusicVolume197"), Optional<byte>(scene, "SoundMusicVolume198"), Channel(scene, "MusicChannel108"), Channel(scene, "MusicChannel112"), scene.TryGetProperty("WorldMusicSelection", out JsonElement selection) && selection.ValueKind != JsonValueKind.Null ? Required(selection, "Music").GetByte() : null);
        GameplaySaveSceneFacts result = new(origin, Required(context, "FieldId").GetInt32(), names, Required(scene, "UnlockedTutorials").EnumerateArray().Select(value => value.GetInt32()).ToArray(), Required(scene, "Party").EnumerateArray().Select(value => value.GetByte()).ToArray(), Required(scene, "PartyPower").GetInt32(), Required(scene, "MenuEnabled").GetBoolean(), Required(scene, "RunEnabled").GetBoolean(), Required(scene, "MapJumpDisabled").GetBoolean(), Required(scene, "PlaceId").GetInt32(), Optional<byte>(scene, "NativeSalaryMovementUnits"), origin == GameplaySaveSceneFactsOrigin.SuspendedField ? null : music, origin == GameplaySaveSceneFactsOrigin.SuspendedField ? music : null);
        if (origin != GameplaySaveSceneFactsOrigin.SuspendedField && scene.TryGetProperty("NativeBattleFlagWrites", out JsonElement writes) && writes.ValueKind == JsonValueKind.Array && writes.GetArrayLength() > 0)
        {
            JsonElement last = writes[writes.GetArrayLength() - 1];
            result = result with { PersistentBattleMode = Required(last, "PersistentFlags").GetUInt16(), ActiveBattleMode = Required(last, "ActiveFlags").GetUInt16() };
        }
        return result;
    }

    public void Validate()
    {
        if (!Enum.IsDefined(Origin) || FieldId is < 72 or > ushort.MaxValue || Names is null || Names.Any(pair => !GameNames.IsValidSubject(pair.Key) || !GameNames.IsValidDraft(pair.Value)) || UnlockedTutorials is null || UnlockedTutorials.Any(value => value is < 0 or >= 128) || UnlockedTutorials.Distinct().Count() != UnlockedTutorials.Length || FieldParty is not { Length: 3 } || FieldParty.Any(value => value != 255 && value > 10) || PreviousSalaryMovement is not (null or 0 or 3 or 5) || (PersistentBattleMode is null) != (ActiveBattleMode is null) || Origin == GameplaySaveSceneFactsOrigin.SuspendedField && PersistentBattleMode is not null || (Origin == GameplaySaveSceneFactsOrigin.SuspendedField ? Music is not null : SuspendedFieldMusic is not null))
        {
            throw new InvalidDataException("Gameplay persistent scene facts are malformed or claim current music from a suspended field.");
        }
    }

    private static JsonElement Required(JsonElement source, string property)
    {
        if (!source.TryGetProperty(property, out JsonElement value))
        {
            throw new InvalidDataException($"Gameplay scene projection is missing {property}; no default is inferred.");
        }
        return value;
    }

    private static T? Optional<T>(JsonElement source, string property) where T : struct
    {
        return source.TryGetProperty(property, out JsonElement value) && value.ValueKind != JsonValueKind.Null ? value.Deserialize<T>() : null;
    }

    private static GameplaySaveMusicChannel? Channel(JsonElement scene, string property)
    {
        if (!scene.TryGetProperty(property, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }
        return new(Required(value, "ChannelId").GetInt32(), Optional<byte>(value, "MusicId"), Optional<uint>(value, "ChoiceMask"));
    }
}
