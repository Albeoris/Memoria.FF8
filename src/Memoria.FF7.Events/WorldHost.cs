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
    Animation = 0x30E,
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
    FastSpeed = 0x336,
    Unhide = 0x338,
    Hide = 0x339,
    VerticalVelocity = 0x33A,
    FadeOut = 0x33B,
    FieldTransitionSelector = 0x33D,
    Music = 0x33E,
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
    Countdown = 0x355
}

public sealed record WorldCommand(WorldOperation Operation, int[] Arguments);
public sealed record WorldHostRequest(long Id, int ExecutingModel, int ActiveModel, int ProgramCounter, WorldCommand Command);
public sealed record WorldHostReply(bool Completed, int Value = 0);

public interface IWorldHost
{
    WorldHostReply Execute(WorldHostRequest request);
    int ReadSpecial(int selector, int activeModel);
}

public sealed record WorldStackValue(ushort Opcode, int Value);
public sealed record WorldCallSnapshot(int ProgramCounter, byte MoveCounter, byte Priority);
public sealed record WorldEntitySnapshot(int Model, int ProgramCounter, byte MoveCounter, byte Priority, bool ScriptDisabled, int CalledModel, WorldCallSnapshot[] Calls);
public sealed record WorldSnapshot(int Version, long Tick, long NextRequestId, bool Unloading, byte[] Local, WorldStackValue[] Stack, WorldEntitySnapshot System, WorldEntitySnapshot[] Models);


