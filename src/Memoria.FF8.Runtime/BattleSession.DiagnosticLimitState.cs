using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record BattleDiagnosticShotState(byte ActionId, ushort TargetMask, short InitialTimer, short RemainingTimer, byte RemainingAmmo, BattleNativeShotPhase Phase)
{
    public bool FireRequested { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleShotWidgetState? Widget { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleTimedInputUi? Ui { get; init; }
}

public sealed record BattleDiagnosticDuelState(byte MoveSetId, ushort TargetMask, short InitialTimer, short RemainingTimer, BattleNativeDuelPhase Phase)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public byte? Selection1D28DEF { get; init; }
    public ImmutableArray<ushort> InputRing { get; init; } = [];
    public byte InputCursor { get; init; }
    public short AutomaticCountdown { get; init; }
    public ushort PreviousButtons { get; init; }
    public ushort PendingPulse { get; init; }
    public byte WidgetPhase { get; init; }
    public byte PublicationLatch { get; init; }
    public ushort WidgetRevealQ12 { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BattleTimedInputUi? Ui { get; init; }
}

public sealed partial class BattleSession
{
    public BattleDiagnosticShotState? ObserveDiagnosticShotState(string actorId)
    {
        if (GetNativeShot(actorId) is not { } state)
        {
            return null;
        }
        return new(state.ActionId, ReadDiagnosticOriginalTargetWord(state.TargetRequest), state.InitialTimer, state.RemainingTimer, state.RemainingAmmo, state.Phase)
        {
            FireRequested = state.FireRequested,
            Widget = state.Widget,
            Ui = state.Ui
        };
    }

    public BattleDiagnosticDuelState? ObserveDiagnosticDuelState(string actorId)
    {
        if (GetNativeDuel(actorId) is not { } state)
        {
            return null;
        }
        return new(state.MoveSetId, ReadDiagnosticOriginalTargetWord(state.TargetRequest), state.InitialTimer, state.RemainingTimer, state.Phase)
        {
            Selection1D28DEF = state.Selection1D28DEF,
            InputRing = state.InputRing,
            InputCursor = state.InputCursor,
            AutomaticCountdown = state.AutomaticCountdown,
            PreviousButtons = state.PreviousButtons,
            PendingPulse = state.PendingPulse,
            WidgetPhase = state.WidgetPhase,
            PublicationLatch = state.PublicationLatch,
            WidgetRevealQ12 = state.WidgetRevealQ12,
            Ui = state.Ui
        };
    }

    private ushort ReadDiagnosticOriginalTargetWord(BattleTargetRequest request)
    {
        if (request.Selection is not BattleTargetSelection.Explicit selected || selected.Actors.Any(actor => ReadOriginalTargetSlot(actor) is null))
        {
            throw new InvalidOperationException("The expanded Limit diagnostic requires explicit targets with actual physical aliases.");
        }
        return NativeBattleTargetRequestAdapter.ProjectOriginalWord(request, ReadOriginalTargetSlot)
            ?? throw new InvalidOperationException("The original Limit diagnostic requires its physical target WORD.");
    }
}
