using System.Text.Json.Serialization;

namespace Memoria.FF7.Events;

public interface IFieldHost
{
    /// <summary>Idempotently starts or polls this request. Restore reattaches using the same ID.</summary>
    FieldHostReply Execute(FieldHostRequest request);
    /// <summary>Stops a scripted move interrupted by request preemption. Repeated cancellation is harmless.</summary>
    void Cancel(FieldHostRequest request)
    {
        throw new NotSupportedException("The host cannot cancel this FFVII scripted move.");
    }
}

public sealed record FieldHostRequest(long Id, int Entity, int Priority, int ProgramCounter, FieldCommand Command);
public sealed record FieldHostReply(bool Completed, int Value = 0, FieldActorPosition? Position = null);
public sealed record FieldActorPosition(short X, short Y, short Z, short Triangle);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "command")]
[JsonDerivedType(typeof(FieldMovieCommand), "movie")]
[JsonDerivedType(typeof(FieldActorBind), "bind")]
[JsonDerivedType(typeof(FieldActorPlacement), "place")]
[JsonDerivedType(typeof(FieldActorMove), "move")]
[JsonDerivedType(typeof(FieldPartyRecovery), "partyRecovery")]
[JsonDerivedType(typeof(FieldCameraShake), "cameraShake")]
[JsonDerivedType(typeof(FieldAnimation), "animation")]
[JsonDerivedType(typeof(FieldActorProperty), "actor-property")]
[JsonDerivedType(typeof(FieldWindowCommand), "window")]
[JsonDerivedType(typeof(FieldTimerCommand), "timer")]
[JsonDerivedType(typeof(FieldDialogueCommand), "dialogue")]
[JsonDerivedType(typeof(FieldTransitionCommand), "transition")]
[JsonDerivedType(typeof(FieldBattleCommand), "battle")]
[JsonDerivedType(typeof(FieldControlCommand), "control")]
[JsonDerivedType(typeof(FieldBackgroundCommand), "background")]
[JsonDerivedType(typeof(FieldPaletteCommand), "palette")]
[JsonDerivedType(typeof(FieldMenuCommand), "menu")]
[JsonDerivedType(typeof(FieldPreloadCommand), "preload")]
[JsonDerivedType(typeof(FieldRandomCommand), "random")]
[JsonDerivedType(typeof(FieldMateriaCommand), "materia")]
[JsonDerivedType(typeof(FieldItemCommand), "item")]
[JsonDerivedType(typeof(FieldGilCommand), "gil")]
[JsonDerivedType(typeof(FieldTriangleCommand), "triangle")]
[JsonDerivedType(typeof(FieldLineCommand), "line")]
[JsonDerivedType(typeof(FieldAudioCommand), "audio")]
[JsonDerivedType(typeof(FieldCameraCommand), "camera")]
[JsonDerivedType(typeof(FieldPartyCommand), "party")]
[JsonDerivedType(typeof(FieldFadeCommand), "fade")]
[JsonDerivedType(typeof(FieldMusicCommand), "music")]
[JsonDerivedType(typeof(FieldActorTurn), "turn")]
[JsonDerivedType(typeof(FieldActorFace), "face-actor")]
[JsonDerivedType(typeof(FieldActorLadder), "ladder")]
[JsonDerivedType(typeof(FieldActorJump), "jump")]
[JsonDerivedType(typeof(FieldActorOffset), "offset")]
[JsonDerivedType(typeof(FieldModelEffectCommand), "model-effect")]
[JsonDerivedType(typeof(FieldPartyJoin), "party-join")]
[JsonDerivedType(typeof(FieldPartySplit), "party-split")]
[JsonDerivedType(typeof(FieldActorQuery), "actor-query")]
[JsonDerivedType(typeof(FieldPartyProjection), "party-projection")]
[JsonDerivedType(typeof(FieldPartyCharacter), "party-character")]
public abstract record FieldCommand;

public enum FieldMovieOperation
{
    Prepare,
    Play,
    ReadFrame,
    SetBackground
}
public sealed record FieldMovieCommand(FieldMovieOperation Operation, int MovieId = 0) : FieldCommand;
public sealed record FieldActorBind(int ModelIndex) : FieldCommand;
public sealed record FieldActorPlacement(short X, short Y, short? Z, short? Triangle) : FieldCommand;
public sealed record FieldActorMove(short X, short Y, bool Turn = true, bool Animated = true, int? TargetEntity = null) : FieldCommand;
public sealed record FieldTimerCommand(uint Seconds) : FieldCommand;
public sealed record FieldPartyRecovery(bool AllCharacters = false) : FieldCommand;
public sealed record FieldGilCommand(uint Amount) : FieldCommand;
public sealed record FieldCameraShake(byte Axes, byte XAmplitude, byte XDuration, byte YAmplitude, byte YDuration) : FieldCommand;
public sealed record FieldAnimation(int Animation, int Speed, bool Blocking, bool HoldLast, bool Default = false, int? FirstFrame = null, int? LastFrame = null, bool DivideFirstFrameBySpeed = false) : FieldCommand;
public enum FieldActorPropertyKind
{
    Visible,
    Direction,
    MoveSpeed,
    AnimationSpeed,
    TalkEnabled,
    CollisionEnabled,
    TalkRadius,
    CollisionRadius,
    PlayerCharacter,
    AnimationWait,
    StopAnimation,
    PartyCharacter,
    OffsetWait,
    TurnWait
}
public sealed record FieldActorProperty(FieldActorPropertyKind Property, int Value) : FieldCommand;
public enum FieldWindowOperation
{
    Size,
    Style,
    Close,
    CloseAndWait,
    Move,
    Rows,
    Reset,
    Special,
    SizeWhenFree
}
public sealed record FieldWindowCommand(FieldWindowOperation Operation, int Window, short X = 0, short Y = 0, short Width = 0, short Height = 0, int Style = 0, int Flags = 0) : FieldCommand;
public sealed record FieldDialogueCommand(int Window, int Text, int? FirstChoice = null, int? LastChoice = null, int Selected = 0) : FieldCommand;
public sealed record FieldTransitionCommand(ushort Map, short X, short Y, ushort Triangle, byte Direction) : FieldCommand;
public sealed record FieldBattleCommand(ushort Formation) : FieldCommand;
public enum FieldControlKind
{
    PlayerLocked,
    BattlesDisabled,
    MapJumpDisabled,
    MenuDisabled,
    BattleMode,
    BattleMode2,
    BattleTable,
    BattleResult,
    CurrentMapName,
    CursorDisabled,
    MovieCameraDisabled,
    MapDisplay
}
public sealed record FieldControlCommand(FieldControlKind Property, int Value, int Value2 = 0) : FieldCommand;
public enum FieldBackgroundOperation
{
    On,
    Off,
    Clear,
    Roll,
    RollBack
}
public sealed record FieldBackgroundCommand(FieldBackgroundOperation Operation, int Parameter, int State) : FieldCommand;
public sealed record FieldAudioCommand(int Operation, int Parameter0 = 0, int Parameter1 = 0, int Parameter2 = 0, int Parameter3 = 0, int Parameter4 = 0) : FieldCommand;



public enum FieldCameraOperation
{
    Coordinates,
    Entity,
    Player,
    ResetToPlayer,
    Wait,
    Lock,
    ViewOffset
}
public sealed record FieldCameraCommand(FieldCameraOperation Operation, int Mode = 0, short X = 0, short Y = 0, short Steps = 0, int Entity = -1) : FieldCommand;
public enum FieldPartyOperation
{
    Add,
    Remove,
    Replace,
    Contains,
    Available,
    SetAvailable,
    SetLocked
}
public sealed record FieldPartyCommand(FieldPartyOperation Operation, int Member0, int Member1 = 255, int Member2 = 255) : FieldCommand;
public sealed record FieldFadeCommand(int Type, int Red = 0, int Green = 0, int Blue = 0, int Speed = 0, int Adjustment = 0, bool NewFade = false, bool Wait = false) : FieldCommand;
public enum FieldMusicOperation
{
    Play,
    NextField,
    NextBattle,
    SaveCurrentAndPlay,
    SwapSaved,
    Lock
}
public sealed record FieldMusicCommand(FieldMusicOperation Operation, int Track) : FieldCommand;
public sealed record FieldActorTurn(short Direction, byte DirectionPolicy, byte Steps, byte Mode, bool SignedTarget = false) : FieldCommand;
public enum FieldActorQueryKind
{
    Triangle,
    Direction,
    Coordinates,
    Position
}
public sealed record FieldActorQuery(FieldActorQueryKind Query, int Entity) : FieldCommand;
public sealed record FieldPartyProjection(int Member0, int Member1, int Member2, ushort VisibilityOrMask = 0) : FieldCommand;
public sealed record FieldPartyCharacter(int CharacterId, int PartySlot) : FieldCommand;

public enum FieldPaletteOperation
{
    Store,
    Load,
    Copy,
    Add,
    Multiply
}
public sealed record FieldPaletteCommand(FieldPaletteOperation Operation, int Source, int Destination, int Count, int Red = 0, int Green = 0, int Blue = 0, int First = 0) : FieldCommand;

public sealed record FieldMenuCommand(int Mode, int Parameter) : FieldCommand;

public sealed record FieldActorOffset(short X, short Y, short Z, byte Mode, short Steps) : FieldCommand;

public sealed record FieldPreloadCommand(ushort Map = 0, bool Wait = false) : FieldCommand;

public sealed record FieldRandomCommand : FieldCommand;

public sealed record FieldTriangleCommand(ushort Triangle, bool Blocked) : FieldCommand;

public enum FieldLineOperation
{
    Define,
    Replace,
    Enable,
    Slip
}
public sealed record FieldLineCommand(FieldLineOperation Operation, short X1 = 0, short Y1 = 0, short Z1 = 0, short X2 = 0, short Y2 = 0, short Z2 = 0, byte Value = 0) : FieldCommand;

public sealed record FieldModelEffectCommand(byte Operation, byte Mode = 0, bool Wait = false) : FieldCommand
{
    public byte[]? Payload { get; init; }

    public bool Equals(FieldModelEffectCommand? other) => other is not null && Operation == other.Operation && Mode == other.Mode && Wait == other.Wait && (Payload is null ? other.Payload is null : other.Payload is not null && Payload.AsSpan().SequenceEqual(other.Payload));

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Operation);
        hash.Add(Mode);
        hash.Add(Wait);
        hash.Add(Payload is null);
        foreach (byte value in Payload ?? [])
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }
}

public sealed record FieldPartyJoin(byte Duration, int Leader, int Follower1, int Follower2) : FieldCommand;

public sealed record FieldPartySplit(byte Duration, int Leader, int Follower1, int Follower2, short X1, short Y1, byte Direction1, short X2, short Y2, byte Direction2, FieldByteOperand? Direction1Source = null, FieldByteOperand? Direction2Source = null) : FieldCommand;

public sealed record FieldByteOperand(FieldBank Bank, byte Value);

public sealed record FieldActorFace(int TargetEntity, byte Steps, byte DirectionPolicy, bool Immediate = false) : FieldCommand;

public sealed record FieldActorLadder(short X, short Y, short Z, short Triangle, byte Mode, byte Animation, byte Direction, byte AnimationDivisor) : FieldCommand;

public sealed record FieldActorJump(short X, short Y, short Triangle, short Duration) : FieldCommand;

public sealed record FieldMateriaCommand(uint Encoded) : FieldCommand;
public sealed record FieldItemCommand(ushort Encoded) : FieldCommand;
