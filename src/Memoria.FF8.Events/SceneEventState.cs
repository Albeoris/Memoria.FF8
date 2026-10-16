using System.Numerics;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record DialogueState(int Channel, int MessageId, string Text, int X, int Y, int? FirstChoice = null, int? LastChoice = null, int? InitialChoice = null, int? CancelChoice = null, bool IsNamePrompt = false, int ObjectId = -1, int ScriptId = -1, int Operation = -1, bool CanConfirm = true);
public sealed record ScriptCommand(int FieldId, int ObjectId, int ScriptId, int Operation, string Name, IReadOnlyList<int> Arguments, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<NativeScalarValue>? ScalarArguments = null);
public enum SoundResourceStatus
{
    Pending,
    Ready,
    Muted
}

public sealed record SoundResourceRequest(int Index, string? NativeResourceName, SoundResourceStatus Status);
public sealed record SoundRequest(int EffectIndex, int Pan, int Volume, int Channel);
public sealed record PartyTraceSample(int X, int Y, int Z, int Triangle, int Direction, int Animation = 0, int FirstFlag = 1, int SecondFlag = 1);
public sealed record PartyTraceState(int Cursor, int FirstOffset, int SecondOffset, PartyTraceSample?[] FirstTrail, PartyTraceSample?[] SecondTrail);
public sealed record FieldFixedPosition(int X, int Y, int Z);
/// <summary>Executed normal-entry lookup stack history; it does not supply residue to a later loader or a historical save.</summary>
public sealed record NativeFieldEntryStackReceipt(int Version, string WalkmeshHash, FieldTransition Arrival, int FirstWinnerRecord, int FirstCarriedRecord, int CarriedTriangle);
public sealed record BackgroundShadeLoop(int ObjectId, int ModuleIndex, int FadeFirst, int FadeSecond, Vector3 FirstColor, Vector3 SecondColor, int HoldFirst, int HoldSecond, bool FirstToSecond, int Elapsed, int Remaining, int HoldRemaining, Vector3 CurrentColor);
public sealed record BackgroundShade(int FadeFrames, Vector3 FirstColor, Vector3 SecondColor, long StartFrame, int ModuleIndex = 0);
public sealed record DialogueWindow(int Channel, int X, int Y, int Width, int Height);

[JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
public sealed class SceneEventState
{
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTripleTriadRequest? TripleTriadRequest { get; internal set; }

    [JsonRequired]
    [JsonInclude]
    public NativeMovieMaskState? NativeMovieMask { get; internal set; }

    [JsonRequired]
    [JsonInclude]
    public NativeFieldModelPartsState? ModelParts { get; internal set; }

    [System.Text.Json.Serialization.JsonRequired]
    [System.Text.Json.Serialization.JsonInclude]
    public long AudioLoadEpoch { get; internal set; }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMovieActorLifecycle? MovieActors { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicProcessState? MusicProcess { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicLoadRequest? MusicLoadRequest { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicPlayRequest? MusicPlayRequest { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicReplayRequest? MusicReplayRequest { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldMusicSelection? WorldMusicSelection { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeWorldMusicLoad? WorldMusicLoad { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? PreviousWorldEntityByte86 { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldCallbackClock? FieldCallbackClock { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeActorBackgroundScroll? ActorBackgroundScroll { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeNormalBackgroundScrollCenter? NormalBackgroundScrollCenter { get; internal set; }
    [JsonRequired]
    [JsonInclude]
    public NativeLayerZeroScrollCurrent? NativeLayerZeroScrollCurrent { get; internal set; }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, NativeBackgroundScrollCurrent>? NativeBackgroundScrollCurrents { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoundVolumeTransition? SoundVolumeTransition { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoundVolume? SoundVolume { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoundPosition? SoundPosition { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFootstepOffAll? FootstepOffAll { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeVibrationRequest? VibrationRequest { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeAllSoundVolumeTransition? AllSoundVolumeTransition { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeAllSoundVolume? AllSoundVolume { get; internal set; }
    [JsonInclude]
    public bool RandomBattlesEnabled { get; internal set; } = true;
    [JsonInclude]
    public bool MapJumpDisabled { get; internal set; }
    [JsonInclude]
    public NativeWorldTransitionRequest? WorldTransitionRequest { get; internal set; }
    public Dictionary<long, NativeWorldJumpWait> WorldJumpWaits { get; } = [];
    [JsonInclude]
    public bool MenuEnabled { get; internal set; } = true;
    [JsonInclude]
    public bool RunEnabled { get; internal set; } = true;
    [JsonInclude]
    [JsonRequired]
    public byte? NativeSalaryMovementUnits { get; internal set; }
    [JsonInclude]
    public int PlaceId { get; internal set; }
    [JsonInclude]
    public int CameraIndex { get; internal set; }
    [JsonInclude]
    public PartyTraceState? PartyTrace { get; internal set; }
    [JsonInclude]
    [JsonRequired]
    public byte? NativeLadderSpeed490E { get; internal set; }
    [JsonInclude]
    public int NativePartyTraceVersion { get; internal set; }
    [JsonInclude]
    public FieldFixedPosition? NativePlayerPosition { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? NativePlayerEarlyRetryState { get; internal set; }
    [JsonInclude]
    public int NativeFieldLoadVersion { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldInitializationProof? NativeFieldInitialization { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeFieldEntryStackReceipt? NativeFieldEntryStack { get; internal set; }
    [JsonInclude]
    public PartyTraceSample? FieldLoadFirstRecordZero { get; internal set; }
    [JsonInclude]
    public PartyTraceSample? FieldLoadSecondRecordZero { get; internal set; }
    [JsonInclude]
    public int LoadedMusic { get; internal set; } = -1;
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ushort? PreviousFieldWord84 { get; internal set; }
    [JsonInclude]
    public bool MusicPlaying { get; internal set; }
    [JsonInclude]
    public int MusicVolume { get; internal set; } = 127;
    [JsonInclude]
    public int MusicCrossfadeFrames { get; internal set; }
    [JsonInclude]
    public int BattleMusic { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoundInitialization? SoundInitialization { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeSoundStop? SoundStop { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? SoundMusicVolume197 { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? SoundMusicVolume198 { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicChannelState? MusicChannel108 { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicChannelState? MusicChannel112 { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicStop? MusicStop { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMusicVolume? MusicVolumeRequest { get; internal set; }
    public SortedSet<int> LockedTriangles { get; } = [];
    [JsonRequired]
    [JsonInclude]
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Replace)]
    public List<int> Party { get; internal set; } = [255, 255, 255];
    [JsonIgnore]
    public IEnumerable<int> ActiveParty => Party.Where(character => character != 255);
    public List<int> FieldPartyNativeActorIds { get; } = [];
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativePartyBindingMemory? PartyBindingMemory { get; internal set; }
    [JsonInclude]
    public int NativeActorLayoutVersion { get; internal set; }
    [JsonInclude]
    public int NativePlayerMovementVersion { get; internal set; }
    public List<int> NativeActorObjectIds { get; } = [];
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeTutorialRequest? NativeTutorial { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMenuTutorialRequest? NativeMenuTutorial { get; internal set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public System.Text.Json.JsonElement? NativeTutorialPlayback { get; set; }
    public SortedSet<int> UnlockedTutorials { get; } = [];
    public List<MinimalMenuCompletion> MinimalMenuCompletions { get; } = [];
    public Dictionary<long, int> MinimalMenuWaits { get; } = [];
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NativeBattleCompletion>? NativeBattleCompletions { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<long, int>? NativeBattleWaits { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? NativeBattleResult { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<NativeBattleFlagWrite>? NativeBattleFlagWrites { get; internal set; }
    public Dictionary<int, string> Names { get; } = [];
    [JsonInclude]
    public int PartyPower { get; internal set; }
    [JsonInclude]
    public int PreparedField { get; internal set; } = -1;
    [JsonInclude]
    public int MessageMode { get; internal set; }
    public Dictionary<int, NativeMessageChannelTiming> MessageChannelTiming { get; } = [];
    public Dictionary<int, SoundResourceRequest> LoadedSoundEffects { get; } = [];
    public List<SoundRequest> SoundRequests { get; } = [];
    public List<GlobalSoundRequest> GlobalSoundRequests { get; } = [];
    public List<NativeFieldSoundRequest> NativeFieldSoundRequests { get; } = [];
    /// <summary>Only executed ON/OFF/SET bytes are known, including SET's dense-instance attachment. Missing slots do not imply disabled emitters or an absent-PMD reset.</summary>
    public Dictionary<int, byte> NativeParticleSlots { get; } = [];
    public List<NativeParticleRequest> NativeParticleRequests { get; } = [];
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeMediaHistoryBoundary? NativeMediaHistoryBoundary { get; internal set; }
    /// <summary>Derived historical attempts only; current physical effect IDs belong to the persistent GlobalSoundProcess.</summary>
    [JsonIgnore]
    public IReadOnlyDictionary<int, GlobalSoundRequest> GlobalSoundLatestAttempts => GlobalSoundRequests.GroupBy(request => request.Voice).ToDictionary(group => group.Key, group => group.Last());
    /// <summary>Native byte1CE490A; only value1 requests a color-buffer clear. Fresh normal field loads reset it to0.</summary>
    [JsonInclude]
    public byte BackgroundClearValue { get; internal set; }
    [JsonInclude]
    public int DrawPointFieldStateVersion { get; internal set; }
    [JsonInclude]
    public byte DrawPointEnabled { get; internal set; }
    [JsonInclude]
    public byte DrawPointDisplayParameter { get; internal set; }
    /// <summary>Current physical WORD1CDBFEA, supplied only by executed4747A0; setup receipts are historical samples.</summary>
    [JsonInclude]
    [JsonRequired]
    public ushort? NativeDrawPointDisplayMask { get; internal set; }
    /// <summary>Known native B242 only; an absent legacy value does not mean index0.</summary>
    [JsonInclude]
    public byte? DrawPointIndex { get; internal set; }
    [JsonInclude]
    [JsonRequired]
    public NativeDrawPointInteraction? DrawPointInteraction { get; internal set; }
    [JsonInclude]
    public NativeDrawPointReset? DrawPointReset { get; internal set; }

    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeDrawPointParticles? NativeDrawPointParticles { get; internal set; }
    public Dictionary<int, BackgroundShade> BackgroundShades { get; } = [];
    public Dictionary<int, BackgroundShadeLoop> BackgroundShadeLoops { get; } = [];
    public Dictionary<int, DialogueWindow> DialogueWindows { get; } = [];
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? NativeDoorDriverVersion { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? NativeDoorInitializationFrame { get; internal set; }
    [JsonInclude]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<int, byte>? NativeDoorPendingEvents { get; internal set; }
    public Dictionary<int, BackgroundScrollSettings> BackgroundScrollLayers { get; private set; } = [];
    public Dictionary<int, BackgroundScrollPosition> BackgroundScrollPositions { get; private set; } = [];

    /// <summary>Detached scroll words for read-only renderer admission; other owners are shared and must not be mutated.</summary>
    public SceneEventState CreateBackgroundScrollProjection(int layer, BackgroundScrollSettings settings)
    {
        SceneEventState projection = (SceneEventState)MemberwiseClone();
        projection.BackgroundScrollLayers = new(BackgroundScrollLayers) { [layer] = settings };
        projection.BackgroundScrollPositions = BackgroundScrollPositions.ToDictionary(pair => pair.Key, pair => pair.Key is >= 0 and < 8 && pair.Value is { Mode: 3, Status: 0 } pending ? NativeDirectScrollPhase.Commit(pending) : pair.Value);
        return projection;
    }
}
