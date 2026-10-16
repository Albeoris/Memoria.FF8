using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public readonly record struct NativeGardenTransferInput(uint CurrentButtons, uint PreviousButtons, int EntityType, byte ScriptMode, int TransitionPhase, bool Activity, int ActiveWorldScript, int Cooldown, byte ScriptRequest, bool ViewToggleBlocked);
public readonly record struct NativeGardenTransferAdmission(bool Action, bool ConsumeScriptRequest, bool ToggleView, bool RisingAction);
public readonly record struct NativeGardenTransferStep(NativeGardenTransferState State, NativeWorldPathPosition GardenPosition, bool Completed);

/// <summary>54A7F0's retained destination and54B460 case8's physical callback counter; current object poses remain separately owned.</summary>
public sealed record NativeGardenTransferState
{
    public const string CurrentRule = "ff8-en-garden-transfer-54a7f0-54b460-ng004-v1";

    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string Rule { get; init; }
    [JsonRequired]
    public required long SourceWorldFrame { get; init; }
    [JsonRequired]
    public required int GardenObjectIndex { get; init; }
    [JsonRequired]
    public required int DestinationEntity { get; init; }
    [JsonRequired]
    public required NativeWorldPathPosition TargetPosition { get; init; }
    [JsonRequired]
    public required int? DestinationObjectIndex { get; init; }
    [JsonRequired]
    public required int Counter { get; init; }
    [JsonRequired]
    public required short? LoweringStep { get; init; }
    [JsonRequired]
    public required NativeWorldPathPosition? InitialGardenPosition { get; init; }
    [JsonRequired]
    public required int? InitialGroundHeight { get; init; }
    [JsonRequired]
    public required NativeWorldPathPosition? LastGardenPosition { get; init; }
    [JsonRequired]
    public required long? LastLoweringFrame { get; init; }

    public static NativeGardenTransferState Create(long sourceWorldFrame, int gardenObjectIndex, int destinationEntity, NativeWorldPathPosition targetPosition, int? destinationObjectIndex = null)
    {
        NativeGardenTransferState state = new() { Version = 1, Rule = CurrentRule, SourceWorldFrame = sourceWorldFrame, GardenObjectIndex = gardenObjectIndex, DestinationEntity = destinationEntity, TargetPosition = targetPosition, DestinationObjectIndex = destinationObjectIndex, Counter = 0, LoweringStep = null, InitialGardenPosition = null, InitialGroundHeight = null, LastGardenPosition = null, LastLoweringFrame = null };
        state.Validate();
        return state;
    }

    public void Validate()
    {
        if (Version != 1 || Rule != CurrentRule || SourceWorldFrame < 0 || GardenObjectIndex < 0 || DestinationObjectIndex < 0 || DestinationEntity is not (>= 0 and < 10 or 128 or >= 32 and <= 40 or 132 or 48 or 49 or 50) || Counter is < 0 or > 31 || (Counter == 0) != (LoweringStep is null) || (Counter == 0) != (InitialGardenPosition is null) || (Counter == 0) != (InitialGroundHeight is null) || (Counter == 0) != (LastGardenPosition is null) || (Counter == 0) != (LastLoweringFrame is null) || LastLoweringFrame < SourceWorldFrame || LastLoweringFrame - SourceWorldFrame < Counter - 1)
        {
            throw new InvalidDataException("Garden transfer requires its retained native destination, physical object owner and exact lowering callback phase.");
        }
        if (InitialGardenPosition is NativeWorldPathPosition initial && InitialGroundHeight is int ground && LoweringStep != unchecked((short)(unchecked(ground - initial.Y) / 30)))
        {
            throw new InvalidDataException("Garden lowering step differs from its first executed native ground/body observations.");
        }
    }

    public NativeGardenTransferStep StepLowering(long worldFrame, NativeWorldPathPosition gardenPosition, int currentGroundHeight)
    {
        Validate();
        if (Counter == 31)
        {
            throw new InvalidOperationException("Completed Garden transfer must commit its destination before another lowering callback.");
        }
        if (worldFrame < SourceWorldFrame || LastLoweringFrame is long previous && worldFrame <= previous)
        {
            throw new InvalidOperationException("Garden lowering requires a distinct ordinary callback at or after its accepted input.");
        }
        short step = LoweringStep ?? unchecked((short)(unchecked(currentGroundHeight - gardenPosition.Y) / 30));
        NativeWorldPathPosition lowered = gardenPosition with { Y = unchecked(gardenPosition.Y + step) };
        NativeGardenTransferState next = this with { Counter = Counter + 1, LoweringStep = step, InitialGardenPosition = InitialGardenPosition ?? gardenPosition, InitialGroundHeight = InitialGroundHeight ?? currentGroundHeight, LastGardenPosition = lowered, LastLoweringFrame = worldFrame };
        return new NativeGardenTransferStep(next, lowered, next.Counter > 30);
    }

    public static NativeGardenTransferAdmission EvaluateInput(NativeGardenTransferInput input)
    {
        uint rising = input.CurrentButtons & (input.CurrentButtons ^ input.PreviousButtons);
        bool edge = (rising & 0x40) != 0;
        if (input.Cooldown > 0)
        {
            return new NativeGardenTransferAdmission(false, false, false, edge);
        }
        bool viewEntity = input.EntityType is >= 0 and < 10 or 128 or >= 16 and <= 22 or >= 64 and <= 66 or 49 or >= 32 and <= 40 or 132 or 50 or 48;
        bool toggle = input.ScriptMode == 0 && (rising & 2) != 0 && viewEntity && !input.ViewToggleBlocked;
        bool viewChangesPhase = toggle && input.EntityType is >= 0 and < 10 or 128 or 49;
        bool ordinary = edge && input.ScriptMode == 0 && input.TransitionPhase == 0 && !viewChangesPhase && !input.Activity && input.ActiveWorldScript == -1;
        bool scripted = input.EntityType is 48 or 50 && (input.ScriptRequest & 0x40) != 0;
        bool action = ordinary || scripted;
        return new NativeGardenTransferAdmission(action, action, toggle, edge);
    }

    public static bool PassesBoardingGate(int sourceEntity, int destinationEntity, uint currentPolygonFlags, int currentGroundHeight, int objectHeight, bool collisionEnabled)
    {
        return NativeAbsoluteDifference(objectHeight, currentGroundHeight) < 200 && NativeWorldTerrain.CanDisembark(currentPolygonFlags, destinationEntity, collisionEnabled) && NativeWorldTerrain.CanDisembark(currentPolygonFlags, sourceEntity, collisionEnabled);
    }

    public static bool PassesPlacementGate(int sourceEntity, int destinationEntity, uint targetFlags, int currentGroundHeight, int targetGroundHeight, bool collisionEnabled)
    {
        return NativeWorldTerrain.CanTraverse(targetFlags, destinationEntity, collisionEnabled) && NativeWorldTerrain.CanDisembark(targetFlags, sourceEntity, collisionEnabled) && NativeAbsoluteDifference(targetGroundHeight, currentGroundHeight) < 200;
    }

    private static int NativeAbsoluteDifference(int first, int second)
    {
        int difference = unchecked(first - second);
        return difference <= 0 ? unchecked(second - first) : difference;
    }
}
