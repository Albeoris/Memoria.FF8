using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record GameplaySaveFieldFixedPosition([property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Z);
public sealed record GameplaySaveFieldTrailSample([property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Z, [property: JsonRequired] int Triangle, [property: JsonRequired] int Direction, [property: JsonRequired] int Animation, [property: JsonRequired] int FirstFlag, [property: JsonRequired] int SecondFlag);
public sealed record GameplaySaveFieldTrail([property: JsonRequired] int Cursor, [property: JsonRequired] int FirstOffset, [property: JsonRequired] int SecondOffset, [property: JsonRequired] GameplaySaveFieldTrailSample?[] First, [property: JsonRequired] GameplaySaveFieldTrailSample?[] Second);
public sealed record GameplaySaveFieldScroll([property: JsonRequired] short OffsetX, [property: JsonRequired] short OffsetY, [property: JsonRequired] short ScaleX, [property: JsonRequired] short ScaleY);
public sealed record GameplaySaveFieldScrollCurrent([property: JsonRequired] short X, [property: JsonRequired] short Y);
public sealed record GameplaySaveFieldBackgroundAnimation([property: JsonRequired] int Id, [property: JsonRequired] int First, [property: JsonRequired] int Last, [property: JsonRequired] int Speed, [property: JsonRequired] bool Complete, [property: JsonRequired] int Counter, [property: JsonRequired] int Position, [property: JsonRequired] bool Active, [property: JsonRequired] bool Loop, [property: JsonRequired] short? Flag404);
public sealed record GameplaySaveFieldMusic([property: JsonRequired] bool? Pending, [property: JsonRequired] byte? Requested, [property: JsonRequired] byte? Complete, [property: JsonRequired] byte? Bank201, [property: JsonRequired] byte? Bank202, [property: JsonRequired] NativeMusicSelectionAuthority? Authority, [property: JsonRequired] NativeMusicBufferIdentity? Buffer0, [property: JsonRequired] NativeMusicBufferIdentity? Buffer1);
public sealed record GameplaySaveFieldSound([property: JsonRequired] int Index, [property: JsonRequired] string? NativeResourceName);
public sealed record GameplaySaveFieldMessageStyle([property: JsonRequired] byte Flags, [property: JsonRequired] short Mode);
public sealed record GameplaySaveFieldMessageTiming([property: JsonRequired] ushort Speed, [property: JsonRequired] ushort Counter);
public sealed record GameplaySaveFieldMessageWindow([property: JsonRequired] int Channel, [property: JsonRequired] int X, [property: JsonRequired] int Y, [property: JsonRequired] int Width, [property: JsonRequired] int Height);

/// <summary>Current logical field owners only. Resource memory, loader receipts and historical opcode attempts are reconstructed separately.</summary>
public sealed record GameplaySaveFieldScene
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMovieSource? PreparedMovieSource { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? MovieMask { get; init; }

    [JsonRequired]
    public required int CameraIndex { get; init; }
    [JsonRequired]
    public required byte BackgroundClear { get; init; }
    [JsonRequired]
    public required Dictionary<int, int> BackgroundStates { get; init; }
    [JsonRequired]
    public required GameplaySaveFieldBackgroundAnimation[] BackgroundAnimations { get; init; }
    [JsonRequired]
    public required int[] LockedTriangles { get; init; }
    [JsonRequired]
    public required Dictionary<int, GameplaySaveFieldScroll> ScrollLayers { get; init; }
    [JsonRequired]
    public required GameplaySaveFieldScrollCurrent? LayerZeroCurrent { get; init; }
    [JsonRequired]
    public required Dictionary<int, GameplaySaveFieldScrollCurrent>? ScrollCurrents { get; init; }
    [JsonRequired]
    public required ushort? PreviousField { get; init; }
    [JsonRequired]
    public required byte? PreviousWorldEntity { get; init; }
    [JsonRequired]
    public required byte? FieldLoadReason { get; init; }
    [JsonRequired]
    public required int[] PartyBindings { get; init; }
    [JsonRequired]
    public required GameplaySaveFieldTrail? Trail { get; init; }
    [JsonRequired]
    public required GameplaySaveFieldFixedPosition? PlayerPosition { get; init; }
    [JsonRequired]
    public required int PartyTraceVersion { get; init; }
    [JsonRequired]
    public required int PlayerMovementVersion { get; init; }
    [JsonRequired]
    public required byte? PlayerEarlyRetry { get; init; }
    [JsonRequired]
    public required int FieldLoadVersion { get; init; }
    [JsonRequired]
    public required int? DoorDriverVersion { get; init; }
    [JsonRequired]
    public required long? DoorInitializationFrame { get; init; }
    [JsonRequired]
    public required Dictionary<int, byte>? DoorPendingEvents { get; init; }
    [JsonRequired]
    public required int DrawPointVersion { get; init; }
    [JsonRequired]
    public required byte DrawPointEnabled { get; init; }
    [JsonRequired]
    public required byte DrawPointDisplayParameter { get; init; }
    [JsonRequired]
    public required ushort? DrawPointDisplayMask { get; init; }
    [JsonRequired]
    public required byte? DrawPointIndex { get; init; }
    [JsonRequired]
    public required byte? ParticleRandomPhase { get; init; }
    [JsonRequired]
    public required uint SoundStopMask { get; init; }
    [JsonRequired]
    public required byte? DialogueFocus { get; init; }
    [JsonRequired]
    public required byte? AskPreviousFocus { get; init; }
    [JsonRequired]
    public required byte? AaskPreviousFocus { get; init; }
    [JsonRequired]
    public required GameplaySaveFieldMusic? Music { get; init; }
    [JsonRequired]
    public required GameplaySaveFieldSound[] LoadedSounds { get; init; }
    [JsonRequired]
    public required byte? LadderSpeed { get; init; }
    [JsonRequired]
    public required int PreparedField { get; init; }
    [JsonRequired]
    public required byte? BattleResult { get; init; }
    [JsonRequired]
    public required Dictionary<int, GameplaySaveFieldMessageStyle> MessageStyles { get; init; }
    [JsonRequired]
    public required Dictionary<int, GameplaySaveFieldMessageTiming> MessageTiming { get; init; }
    [JsonRequired]
    public required Dictionary<int, GameplaySaveFieldMessageWindow> MessageWindows { get; init; }

    public void Validate()
    {
        if (CameraIndex < 0 || BackgroundStates is null || BackgroundAnimations is null || BackgroundAnimations.Any(item => item is null || item.Speed is < short.MinValue or > short.MaxValue || item.Counter is < short.MinValue or > short.MaxValue || item.Position is < short.MinValue or > short.MaxValue) || LockedTriangles is null || LockedTriangles.Any(value => value < 0) || ScrollLayers is null || ScrollLayers.Any(pair => pair.Key is < 0 or >= 8 || pair.Value is null) || ScrollCurrents?.Any(pair => pair.Key is < 1 or >= 8 || pair.Value is null) == true || PartyBindings is not { Length: 3 } || PartyBindings.Any(value => value is < 0 or > 255) || PartyTraceVersion is < 0 or > 1 || PlayerMovementVersion is < 0 or > 1 || FieldLoadVersion != 1 || DoorDriverVersion is not (null or 2) || DoorInitializationFrame < 0 || DoorPendingEvents?.Any(pair => pair.Key < 0 || pair.Value != 0) == true || LoadedSounds is null || LoadedSounds.Any(item => item is null || item.Index < 0) || PreparedField is < -1 or > ushort.MaxValue || MessageStyles is null || MessageStyles.Any(pair => pair.Key is < 0 or > 7 || pair.Value is null) || MessageTiming is null || MessageTiming.Any(pair => pair.Key is < 0 or > 7 || pair.Value is null) || MessageWindows is null || MessageWindows.Any(pair => pair.Key is < 0 or > 7 || pair.Value is null || pair.Value.Channel != pair.Key))
        {
            throw new InvalidDataException("Gameplay field scene contains malformed or unsupported logical owners.");
        }
        if (Trail is { } trail && (trail.Cursor is < 0 or >= 64 || trail.FirstOffset is < 0 or >= 64 || trail.SecondOffset is < 0 or >= 64 || trail.First is not { Length: 64 } || trail.Second is not { Length: 64 }))
        {
            throw new InvalidDataException("Gameplay field party trail has invalid bounded storage.");
        }
        if (Music is { } music)
        {
            new NativeMusicProcessState(1, music.Pending, music.Requested, music.Complete, music.Bank201, music.Bank202) { SelectionAuthority = music.Authority, Buffer0 = music.Buffer0, Buffer1 = music.Buffer1 }.Validate();
            if (music.Pending == true)
            {
                throw new NotSupportedException("Gameplay field continuation does not yet preserve pending music I/O.");
            }
        }
        PreparedMovieSource?.Validate();
    }
}
