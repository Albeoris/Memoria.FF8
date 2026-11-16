using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticDuelCycleDeclaration([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] byte? BasicAction = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? FinisherAction { get; init; }

    [JsonIgnore]
    public byte MoveAction => FinisherAction ?? BasicAction ?? throw new InvalidDataException("Duel has no declared move.");

    [JsonIgnore]
    public ushort Effect => FinisherAction switch { null => 274, 6 => 212, 7 => 112, 8 => 216, 9 => 215, _ => 0 };

    [JsonIgnore]
    public byte Command => FinisherAction.HasValue ? (byte)239 : (byte)241;

    public void Validate(BattleInput input, ushort effect, int? nativeAction, byte? nativeCommand)
    {
        if (this is not ({ BasicAction: <= 5, FinisherAction: null } or { BasicAction: null, FinisherAction: >= 6 and <= 9 }) || effect != Effect || nativeCommand != Command || nativeAction != MoveAction || input.Kind != BattleInputKind.Action || input.Ability != BattleAbility.LimitBreak || input.ActionId != 11 << 16 || string.IsNullOrWhiteSpace(input.ActorId) || string.IsNullOrWhiteSpace(input.TargetId))
        {
            throw new InvalidDataException("Duel diagnostics require a genuine command11 invocation and its declared original move.");
        }
    }
}

public sealed record DiagnosticDuelObservation(long Callback, BattleDiagnosticDuelState? Duel);
public sealed record DiagnosticDuelInputReceipt(BattleNativeDuelChoice Choice, DiagnosticDuelObservation Before, DiagnosticDuelObservation After);
public sealed record DiagnosticDuelDispatchPackets(long Serial, long Callback, int HeaderIndex, long HeaderGeneration, NativeBattleRetainedActionPackets Packets)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool AttachedPresentation { get; init; }
    public byte? Parameter80 { get; init; }
    public byte? Parameter82 { get; init; }
    public ushort? HeaderEffect { get; init; }
    public ushort? RetainedEffect { get; init; }
    public BattleDiagnosticDuelState? Duel { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleDiagnosticHostedDuelTargets? HostedTargets { get; init; }
}
public sealed record DiagnosticDuelCycleReceipt(int Execution, string ActorId, string TargetId, byte ActorIndex, byte TargetIndex, DiagnosticDuelCycleDeclaration Declaration, BattleDiagnosticLimitSelectionReceipt Selection, DiagnosticDuelObservation BeforeOpening)
{
    public DiagnosticDuelObservation? Opening { get; init; }
    public DiagnosticDuelInputReceipt[] Inputs { get; init; } = [];
    public DiagnosticDuelObservation? ReadyAfterBasic { get; init; }
    public DiagnosticDuelObservation? TimedOut { get; init; }
    public DiagnosticDuelObservation? Completed { get; init; }
    public BattleDiagnosticEffectDispatch[] Dispatches { get; init; } = [];
    public DiagnosticDuelDispatchPackets[] DispatchPackets { get; init; } = [];
    public long? FocalSerial { get; init; }
}
