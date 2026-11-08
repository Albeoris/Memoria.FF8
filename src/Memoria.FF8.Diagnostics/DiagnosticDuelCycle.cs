using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticDuelCycleDeclaration([property: JsonRequired] byte BasicAction)
{
    public void Validate(BattleInput input, ushort effect, int? nativeAction, byte? nativeCommand)
    {
        if (BasicAction > 5 || effect != 274 || nativeCommand != 241 || nativeAction != BasicAction || input.Kind != BattleInputKind.Action || input.Ability != BattleAbility.LimitBreak || input.ActionId != 11 << 16 || string.IsNullOrWhiteSpace(input.ActorId) || string.IsNullOrWhiteSpace(input.TargetId))
        {
            throw new InvalidDataException("Duel diagnostics require a genuine command11 invocation and one declared native basic0..5, not a raw239 or241 substitute.");
        }
    }
}

public sealed record DiagnosticDuelObservation(long Callback, BattleNativeDuelState? Duel);
public sealed record DiagnosticDuelInputReceipt(BattleNativeDuelChoice Choice, DiagnosticDuelObservation Before, DiagnosticDuelObservation After);
public sealed record DiagnosticDuelDispatchPackets(long Serial, long Callback, int HeaderIndex, long HeaderGeneration, NativeBattleActionPacketsState Packets)
{
    public byte? Parameter80 { get; init; }
    public byte? Parameter82 { get; init; }
    public ushort? HeaderEffect { get; init; }
    public ushort? RetainedEffect { get; init; }
    public BattleNativeDuelState? Duel { get; init; }
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
