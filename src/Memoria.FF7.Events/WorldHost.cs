namespace Memoria.FF7.Events;

public enum WorldOperation
{
    CreateModel = 0x300,
    SetPlayer = 0x302,
    Speed = 0x303,
    Direction = 0x304,
    Move = 0x306,
    EnableControl = 0x307,
    ChunkPosition = 0x308,
    LocalPosition = 0x309,
    VerticalSpeed = 0x30A,
    Elevation = 0x30B,
    BoardTransport = 0x30C,
    RestoreModelPosition = 0x30D,
    Animation = 0x30E,
    DeleteActiveModel = 0x31A,
    SelectLight = 0x310,
    LightChunkPosition = 0x311,
    LightLocalPosition = 0x312,
    AmbientColor = 0x313,
    LightRadii = 0x314,
    LightColor = 0x315,
    LightColor2 = 0x316,
    Battle = 0x317,
    FieldTransition = 0x318,
    ViewMode = 0x319,
    CameraMode = 0x31C,
    Sound = 0x31D,
    CameraRotation = 0x31E,
    CameraRotationSpeed = 0x31F,
    ResetMarsh = 0x320,
    FaceZone = 0x321,
    Window = 0x324,
    Text = 0x325,
    Choice = 0x326,
    TargetDirection = 0x328,
    CameraZoom = 0x329,
    CameraZoomSpeed = 0x32A,
    EncounterRate = 0x32B,
    WindowAttributes = 0x32C,
    CloseWindow = 0x32D,
    WaitWindow = 0x32E,
    CameraTilt = 0x32F,
    StartLeaveVehicle = 0x331,
    ReleaseChocobo = 0x332,
    FaceModel = 0x333,
    RestoreAttachedRider = 0x335,
    FastSpeed = 0x336,
    EnableTinyBroncoFlight = 0x337,
    Unhide = 0x338,
    Hide = 0x339,
    VerticalVelocity = 0x33A,
    FadeOut = 0x33B,
    Surface = 0x33C,
    FieldTransitionSelector = 0x33D,
    Music = 0x33E,
    BeginChocoboPath = 0x33F,
    RunChocoboPath = 0x340,
    WaitChocoboPath = 0x341,
    WaitChocoboRuns = 0x342,
    AttachRider = 0x343,
    SavePosition = 0x344,
    FaceSavedPosition = 0x345,
    ChangeModelIdentity = 0x346,
    CopyModelPosition = 0x347,
    FadeIn = 0x348,
    WorldState = 0x349,
    Effect = 0x34A,
    ChocoboColor = 0x34B,
    SubmarineColor = 0x34C,
    EffectStart = 0x34D,
    EffectStop = 0x34E,
    Height = 0x34F,
    Meteor = 0x350,
    MusicVolume = 0x351,
    Earthquake = 0x352,
    PlaceDismountedRider = 0x353,
    Countdown = 0x355
}

public sealed record WorldCommand(WorldOperation Operation, int[] Arguments);
public sealed record WorldHostRequest(long Id, int ExecutingModel, int ActiveModel, int ProgramCounter, WorldCommand Command)
{
    [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? FreeModelIndex { get; init; }
}
public sealed record WorldHostReply(bool Completed, int Value = 0, int? ActiveModel = null);
/// <summary>An admitted native script request. SourceProgramCounter is the executing caller's next PC captured before target admission.</summary>
public sealed record WorldScriptDispatchObservation(int SourceModel, int SourceProgramCounter, int TargetModel, int Header, int Entry, int CallDepth, int Priority, int SourcePriority = 0, int SourceCallDepth = 0);

public interface IWorldHost
{
    WorldHostReply Execute(WorldHostRequest request);
    int ReadSpecial(int selector, int activeModel);
    void ModelScriptCompleted(int model, int? freeModelIndex = null)
    {
    }

    int ReadModelDistance(int activeModel, int targetModel) => throw new NotSupportedException("The FFVII world host does not support model distances.");
    int ReadStoredPositionDistance(int activeModel, int slot) => throw new NotSupportedException("The FFVII world host does not support stored-position distances.");
    int ReadZoneDistance(int activeModel, int zone) => throw new NotSupportedException("The FFVII world host does not support zone distances.");
    int ReadZoneDirection(int activeModel, int zone) => throw new NotSupportedException("The FFVII world host does not support zone directions.");
    int ReadSpecial(int selector, int activeModel, int? freeModelIndex) => ReadSpecial(selector, activeModel);
    int ReadModelDistance(int activeModel, int targetModel, int? freeModelIndex) => ReadModelDistance(activeModel, targetModel);
    int ReadStoredPositionDistance(int activeModel, int slot, int? freeModelIndex) => ReadStoredPositionDistance(activeModel, slot);
    int ReadZoneDistance(int activeModel, int zone, int? freeModelIndex) => ReadZoneDistance(activeModel, zone);
    int ReadZoneDirection(int activeModel, int zone, int? freeModelIndex) => ReadZoneDirection(activeModel, zone);
}

public sealed record WorldStackValue(ushort Opcode, int Value);
public sealed record WorldCallSnapshot(int ProgramCounter, byte MoveCounter, byte Priority);
public sealed record WorldEntitySnapshot(int Model, int ProgramCounter, byte MoveCounter, byte Priority, bool ScriptDisabled, int CalledModel, WorldCallSnapshot[] Calls)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingDefault)]
    public bool Detached { get; init; }
}
public sealed record WorldSnapshot(int Version, string ProgramIdentity, long Tick, long NextRequestId, bool Unloading, byte[] Local, WorldStackValue[] Stack, WorldEntitySnapshot System, WorldEntitySnapshot[] Models)
{
    [global::System.Text.Json.Serialization.JsonIgnore(Condition = global::System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WorldEntitySnapshot[]? FreeModels { get; init; }
}
