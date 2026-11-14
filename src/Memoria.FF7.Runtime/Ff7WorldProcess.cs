using Memoria.FF7.Events;
using System.Text.Json.Serialization;

namespace Memoria.FF7.Runtime;

public sealed record Ff7WorldEntity(int Model, int X = 0, int Y = 0, int Z = 0, int Direction = 0, int TargetDirection = 0, byte Speed = 0, int Flags = 0, int Animation = 0, int AnimationFrame = 0, int Elevation = 0, int VerticalSpeed = 0, int VerticalVelocity = 0, int Terrain = 0, bool TerrainResolved = false, int PreviousX = 0, int PreviousZ = 0, int PoseAnimation = 0, int PoseFrame = 0, int FrameRemainder = 0, int FrameDivisor = 32, int TurnOffset = 0, int GroundHeight = 0)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PreviousY { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ScriptDirection { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? AttachedModel { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldTerrainReference[]? TerrainCache { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TiltX { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TiltY { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int TiltZ { get; init; }
    [JsonIgnore]
    public long PresentationEpoch { get; init; }
    [JsonIgnore]
    public long PresentationAnimationEpoch { get; init; }
}
public sealed record Ff7WorldLight(int Index, int X = 0, int Z = 0, int Enabled = 0, int AmbientRed = 0, int AmbientGreen = 0, int AmbientBlue = 0, int Radius = 0, int Radius2 = 0, int Red = 0, int Green = 0, int Blue = 0, int Red2 = 0, int Green2 = 0, int Blue2 = 0);
public sealed record Ff7WorldMarshPoint(int X, int Z, int Direction = 0, int Height = 0);
public sealed record Ff7WorldMarsh(bool Initialized = false, int X = 0, int Z = 0, int Direction = 0x400)
{
    public Ff7WorldMarshPoint[]? Points { get; init; }
    public Ff7WorldTerrainReference[]? TerrainCache { get; init; }
    public int Head { get; init; }
    public int TurnTimer { get; init; }
    public int TurnDelta { get; init; }
    public int PreviousTerrain { get; init; }
    public int PreviousModel { get; init; }
    public bool InMarsh { get; init; }
}
public sealed record Ff7WorldApproachBubble(int X, int Y, int Z, int Age = 0);
public sealed record Ff7WorldFieldDeparture(long RequestId, int ExecutingModel, int ProgramCounter, ushort Selector, WorldOperation Operation);
public sealed record Ff7WorldProcess(int WorldId, long Tick, int EntrySelector, WorldSnapshot Vm, Ff7WorldEntity[] Entities, int ControlledModel, bool MovementEnabled, int ViewMode, int RadarMode, Ff7WorldCameraState? Camera, Ff7WorldLight[] Lights, int SelectedLight, Ff7WorldMarsh Marsh, int PlayerModel, int EncounterRate, Ff7Window? Window = null, Ff7Dialogue? Dialogue = null, short LastSelection = 0, int ControlLock = 0, int MainState = -15, int FadeAlpha = 255, int FadeRate = 0, int FadeDisplayedAlpha = 255, bool Initialized = false, uint PresentationCounter = 0, Ff7WorldInputState? Input = null, Ff7WorldAudioState? Audio = null, Ff7WorldSkyState? Sky = null, int WorldMapState = 0)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldFieldDeparture? FieldDeparture { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldBattleState? BattleState { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldChocoboState? Chocobo { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldChocoboPathControl? ChocoboPathControl { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldVehicleState? Vehicle { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int ChocoboTint { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldSnowState? Snow { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldPartyChange? PartyChange { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldEntity[]? FreeEntities { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int EntryModule { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? SubmarineTint { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldApproachBubble? ApproachBubble { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int WhiteFadeCallbacks { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool WhiteFadeDisplayed { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldScreenShake? ScreenShake { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldMapTransfer? MapTransfer { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool SubmarineMapEntry { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldScriptCameraState? ScriptCamera { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldStoredPosition[]? StoredPositions { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool MeteorVisible { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldEffectsState? Effects { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Ff7WorldCameraOcclusion? CameraOcclusion { get; init; }
    public IReadOnlyList<Ff7Window> Windows => Window is null ? [] : [Window];
    public IReadOnlyList<Ff7Dialogue> Dialogues => Dialogue is null ? [] : [Dialogue];
}
