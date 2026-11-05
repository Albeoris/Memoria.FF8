using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum GameplaySaveScriptWait
{
    None,
    Frames,
    RequestStart,
    RequestEnd,
    Halt,
    Animation,
    NativeAnimationBlocking,
    NativeAnimationSync,
    BackgroundShade,
    BackgroundScroll,
    ScriptMenu,
    FaceDirection,
    NativeFaceSync,
    BackgroundAnimation,
    Rotation,
    NativeTurnCompletion,
    NativeScriptMovement,
    NativeMovementFlush,
    NativeJump,
    NativeLadderMode3,
    NativeLadder,
    PartyJoin,
    PartySplit,
    NativeDoorRequest = 23
}

public sealed record GameplaySaveScriptFiber([property: JsonRequired] int ObjectId, [property: JsonRequired] int ScriptId, [property: JsonRequired] int Priority, [property: JsonRequired] long Sequence, [property: JsonRequired] bool Initialization, [property: JsonRequired] int Position, [property: JsonRequired] bool Complete, [property: JsonRequired] bool Started, [property: JsonRequired] bool ResultFrameEntered, [property: JsonRequired] int[] Stack, [property: JsonRequired] Dictionary<int, int> Results, [property: JsonRequired] GameplaySaveScriptWait Wait, [property: JsonRequired] long WaitUntil, [property: JsonRequired] long? RequestSequence, [property: JsonRequired] byte? DoorSlot, [property: JsonRequired] byte? ActorSlot, [property: JsonRequired] bool DefaultResultFrame, [property: JsonRequired] int[]? SavedResults)
{
    /// <summary>Retained BGSHADE instruction whose operands were already consumed; completion must poll without issuing it again.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? ShadeOperation { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveMovementIssue? MovementIssue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveGlobalCall[]? GlobalCalls { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PartyRequestSelector { get; init; }
}

/// <summary>Physical script operands remain observable after pop through native RET result frames; they are not resource memory.</summary>
public sealed record GameplaySaveScriptOperands([property: JsonRequired] int ObjectId, [property: JsonRequired] int?[] Words, [property: JsonRequired] byte?[] SavedDepths, [property: JsonRequired] int? LiveDepth, [property: JsonRequired] ushort? ActorWord398);
public sealed record GameplaySaveDoorSchedule([property: JsonRequired] int ObjectId, [property: JsonRequired] byte CurrentSlot, [property: JsonRequired] long CurrentSequence, [property: JsonRequired] long DefaultSequence, [property: JsonRequired] long InitializationSequence, [property: JsonRequired] int? RetiredOperation);
public sealed record GameplaySaveFieldClock([property: JsonRequired] long Ordinary, [property: JsonRequired] long Movie, [property: JsonRequired] long Menu, [property: JsonRequired] long Loader, [property: JsonRequired] long? Battle, [property: JsonRequired] long Initialization, [property: JsonRequired] long RandomBattle, [property: JsonRequired] long MenuBattle);
public sealed record GameplaySaveFieldInputAxis([property: JsonRequired] int Start, [property: JsonRequired] int Target, [property: JsonRequired] int Duration, [property: JsonRequired] int Elapsed, [property: JsonRequired] int Direction);

/// <summary>Logical cooperative script state only; scene/actor reconstruction must complete independently before input resumes.</summary>
public sealed record GameplaySaveFieldScripts([property: JsonRequired] int Version, [property: JsonRequired] int FieldId, [property: JsonRequired] string ProgramSha256, [property: JsonRequired] long Frame, [property: JsonRequired] double FractionalSeconds, [property: JsonRequired] long Sequence, [property: JsonRequired] bool UserControl, [property: JsonRequired] bool SequenceControlDisabled, [property: JsonRequired] int BackgroundFps, [property: JsonRequired] int NameEntity, [property: JsonRequired] GameplaySaveFieldClock Clock, [property: JsonRequired] GameplaySaveScriptFiber[] Fibers, [property: JsonRequired] GameplaySaveScriptOperands[] Operands, [property: JsonRequired] GameplaySaveDoorSchedule[] Doors, [property: JsonRequired] GameplaySaveEventCell[] Locals)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? PreparedMovie { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public GameplaySaveFieldInputAxis? InputAxis { get; init; }

    public void Validate()
    {
        if (Version != 1 || FieldId is < 0 or > ushort.MaxValue || ProgramSha256 is not { Length: 64 } || !ProgramSha256.All(Uri.IsHexDigit) || Frame < 1 || !double.IsFinite(FractionalSeconds) || FractionalSeconds is < 0 or > 10 || Sequence < 0 || BackgroundFps is < 1 or > 60 || PreparedMovie is < 0 or > byte.MaxValue || Clock is null || Fibers is null || Operands is null || Doors is null || Locals is null)
        {
            throw new InvalidDataException("Gameplay field scripts have an unsupported identity or timebase.");
        }
        if (Clock.Initialization != 1 || Clock.Ordinary < 0 || Clock.Movie < 0 || Clock.Menu < 0 || Clock.Loader < 0 || Clock.Battle < 0 || Clock.RandomBattle < 0 || Clock.MenuBattle < 0 || Clock.Ordinary > Frame - 1 || Clock.Movie != Frame - 1 - Clock.Ordinary)
        {
            throw new InvalidDataException("Gameplay field script callback counts disagree with their saved timebase.");
        }
        if (InputAxis is { } axis && (axis.Duration < 0 || axis.Elapsed < 0 || axis.Elapsed > axis.Duration))
        {
            throw new InvalidDataException("Gameplay field input-axis interpolation has an invalid logical phase.");
        }
        HashSet<long> sequences = [];
        foreach (GameplaySaveScriptFiber fiber in Fibers)
        {
            if (fiber is null || fiber.ObjectId < 0 || fiber.ScriptId < 0 || fiber.Sequence <= 0 || fiber.Sequence > Sequence || !sequences.Add(fiber.Sequence) || fiber.Position < 0 || fiber.Stack is null || fiber.Stack.Length > 80 || fiber.Results is null || fiber.Results.Keys.Any(key => key is < 0 or > 7) || !Enum.IsDefined(fiber.Wait) || fiber.DoorSlot > 7 || fiber.ActorSlot > 7 || fiber.SavedResults is { Length: not 8 } || fiber.Initialization && !fiber.Complete)
            {
                throw new InvalidDataException("Gameplay field script fiber is malformed or incompletely initialized.");
            }
        }
        foreach (GameplaySaveScriptFiber fiber in Fibers)
        {
            if (fiber.PartyRequestSelector is not null && (fiber.Initialization || fiber.ActorSlot is not byte slot || slot >= 8 || slot != fiber.Priority))
            {
                throw new InvalidDataException("Gameplay indexed-party request requires its receiving native actor-slot owner.");
            }
            if (fiber.GlobalCalls is { } calls && (calls.Length > 8 || fiber.Complete && calls.Length != 0 || calls.Any(call => call is null || call.CallOperation < 0 || call.OperandBase is < 0 or > 79)))
            {
                throw new InvalidDataException("Gameplay global calls exceed their live physical frame domain.");
            }
            if (fiber.Wait == GameplaySaveScriptWait.BackgroundShade ? fiber.Complete || fiber.ShadeOperation != fiber.Position : fiber.ShadeOperation is not null)
            {
                throw new InvalidDataException("Gameplay background shade wait has no matching issued instruction.");
            }
            if (fiber.RequestSequence is long request && !sequences.Contains(request) || fiber.Wait is GameplaySaveScriptWait.RequestStart or GameplaySaveScriptWait.RequestEnd or GameplaySaveScriptWait.NativeDoorRequest && fiber.RequestSequence is null)
            {
                throw new InvalidDataException("Gameplay field script request wait has no retained target fiber.");
            }
        }
        if (Operands.Any(item => item is null) || Doors.Any(item => item is null) || Locals.Any(item => item is null) || Locals.Select(cell => cell.Address).Distinct().Count() != Locals.Length || Operands.Select(item => item.ObjectId).Distinct().Count() != Operands.Length || Doors.Select(item => item.ObjectId).Distinct().Count() != Doors.Length)
        {
            throw new InvalidDataException("Gameplay script actor frames are duplicated.");
        }
        foreach (GameplaySaveScriptOperands operands in Operands)
        {
            if (operands.ObjectId < 0 || operands.Words is not { Length: 80 } || operands.SavedDepths is not { Length: 8 } || operands.LiveDepth is < -1 or > 79 || operands.SavedDepths.Any(depth => depth is > 79 and not 255))
            {
                throw new InvalidDataException("Gameplay script operands require the bounded physical result-frame domain.");
            }
        }
        foreach (GameplaySaveDoorSchedule door in Doors)
        {
            if (door.ObjectId < 0 || door.CurrentSlot > 7 || !sequences.Contains(door.CurrentSequence) || !sequences.Contains(door.DefaultSequence) || !sequences.Contains(door.InitializationSequence) || door.RetiredOperation < 0)
            {
                throw new InvalidDataException("Gameplay door scheduling has an invalid retained fiber identity.");
            }
        }
        NativeEventMemory local = new(NativeEventMemory.CurrentVersion, Locals.ToDictionary(cell => cell.Address, cell => new NativeMemoryValue(cell.Width, NativeScalarValue.Exact(cell.Value))));
        local.Validate();
        if (local.CellRanges().Any(cell => cell.Address < 1024 || (long)cell.Address + cell.Width > 2304))
        {
            throw new InvalidDataException("Gameplay field locals extend outside the field-local event bank.");
        }
    }
}
