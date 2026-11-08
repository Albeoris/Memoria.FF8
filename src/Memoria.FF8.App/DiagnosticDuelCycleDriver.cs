using Memoria.FF8.BattleEffects;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed class DiagnosticDuelCycleDriver(DiagnosticDuelCycleReceipt receipt)
{
    internal DiagnosticDuelCycleReceipt Receipt { get; private set; } = receipt;
    internal bool Completed => Receipt.Completed is not null;
    private BattleNativeDuelChoice? pendingChoice;
    private DiagnosticDuelObservation? inputBefore;
    private int buttonIndex;
    private byte inputCursor;
    private bool awaitingButton;

    internal bool NeedsInput => Receipt.ReadyAfterBasic is null && (Receipt.Inputs.Length == 0 || Receipt.Inputs[^1].Choice.ActionId != Receipt.Declaration.BasicAction) && pendingChoice is null;

    internal void Observe(DiagnosticDuelObservation observation, bool drained)
    {
        BattleNativeDuelState? state = observation.Duel;
        if (Receipt.Opening is null && state is { Phase: BattleNativeDuelPhase.Opening })
        {
            Receipt = Receipt with { Opening = observation };
        }
        if (pendingChoice is { } choice && state is { Phase: BattleNativeDuelPhase.AwaitingEffect } && state.MoveSetId == choice.MoveSetId)
        {
            if (!NativeBattleDuelRules.Matches(choice, state.InputRing, state.InputCursor))
            {
                throw new InvalidDataException("The native Duel did not accept the declared original button suffix.");
            }
            Receipt = Receipt with { Inputs = [.. Receipt.Inputs, new(choice, inputBefore!, observation)] };
            pendingChoice = null;
        }
        if (Receipt.FocalSerial is not null && Receipt.Inputs is { Length: > 0 } && Receipt.Inputs[^1].Choice.ActionId == Receipt.Declaration.BasicAction && state is { Phase: BattleNativeDuelPhase.Ready })
        {
            Receipt = Receipt with { ReadyAfterBasic = Receipt.ReadyAfterBasic ?? observation };
        }
        if (Receipt.ReadyAfterBasic is not null && state is { Phase: BattleNativeDuelPhase.Finisher, Selection1D28DEF: 255, RemainingTimer: <= 0 })
        {
            Receipt = Receipt with { TimedOut = Receipt.TimedOut ?? observation };
        }
        if (Receipt.TimedOut is not null && state is null && drained)
        {
            Receipt = Receipt with { Completed = Receipt.Completed ?? observation };
        }
    }

    internal void BeginInput(BattleNativeDuelChoice choice, DiagnosticDuelObservation before)
    {
        if (!NeedsInput || Receipt.Inputs.Length >= 8 || before.Duel is not { Phase: BattleNativeDuelPhase.Ready } || !choice.Learned || choice.Finisher || choice.ActionId > 5 || choice.Buttons.IsDefaultOrEmpty || choice.Buttons.Length > 5)
        {
            throw new InvalidDataException("Duel diagnostics require one bounded learned native basic input at a real ready boundary.");
        }
        pendingChoice = choice;
        inputBefore = before;
        buttonIndex = 0;
        awaitingButton = false;
    }

    internal ushort Buttons(BattleNativeDuelState state)
    {
        if (pendingChoice is not { } choice || state.Phase != BattleNativeDuelPhase.Ready)
        {
            return 0;
        }
        if (awaitingButton)
        {
            if (state.InputCursor == inputCursor)
            {
                return choice.Buttons[buttonIndex];
            }
            buttonIndex++;
            awaitingButton = false;
            return 0;
        }
        if (buttonIndex >= choice.Buttons.Length || state.PreviousButtons != 0)
        {
            return 0;
        }
        inputCursor = state.InputCursor;
        awaitingButton = true;
        return choice.Buttons[buttonIndex];
    }

    internal bool ObserveDispatch(BattleDiagnosticEffectDispatch dispatch, DiagnosticDuelDispatchPackets proof)
    {
        if (Receipt.Dispatches.Length >= 16 || Receipt.Dispatches.Length != Receipt.DispatchPackets.Length || dispatch.ActorId != Receipt.ActorId || dispatch.NativeActorIndex != Receipt.ActorIndex || dispatch.RequestedEffect != 274 || dispatch.ExecutedEffect != 274 || dispatch.NativeCommand is not (11 or 241) || dispatch.ActionId is not (>= 0 and <= 5 or 65531 or 65532) || dispatch.Reflection is not null)
        {
            throw new InvalidDataException("Duel dispatched outside its real command11/basic/timer-close lineage.");
        }
        bool focal = Receipt.FocalSerial is null && Receipt.Inputs is { Length: > 0 } && Receipt.Inputs[^1].Choice.ActionId == Receipt.Declaration.BasicAction && dispatch.NativeCommand == 241 && dispatch.ActionId == Receipt.Declaration.BasicAction && dispatch.Callback >= Receipt.Inputs[^1].After.Callback;
        Receipt = Receipt with { Dispatches = [.. Receipt.Dispatches, dispatch], DispatchPackets = [.. Receipt.DispatchPackets, proof], FocalSerial = focal ? dispatch.Serial : Receipt.FocalSerial };
        return focal;
    }
}
