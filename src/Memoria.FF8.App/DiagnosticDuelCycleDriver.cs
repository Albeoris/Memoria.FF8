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

    internal bool NeedsInput => Receipt.Dispatches.Length >= 2 && Receipt.ReadyAfterBasic is null && (Receipt.Inputs.Length == 0 || Receipt.Inputs[^1].Choice.ActionId != Receipt.Declaration.MoveAction) && pendingChoice is null;

    internal void Observe(DiagnosticDuelObservation observation, bool drained)
    {
        BattleDiagnosticDuelState? state = observation.Duel;
        if (Receipt.Opening is null && state is { Phase: BattleNativeDuelPhase.Opening })
        {
            Receipt = Receipt with { Opening = observation };
        }
        if (pendingChoice is { } choice && state is not null && state.Phase == (choice.Finisher ? BattleNativeDuelPhase.Finisher : BattleNativeDuelPhase.AwaitingEffect) && state.MoveSetId == choice.MoveSetId)
        {
            if (!NativeBattleDuelRules.Matches(choice, state.InputRing, state.InputCursor))
            {
                throw new InvalidDataException("The native Duel did not accept the declared original button suffix.");
            }
            Receipt = Receipt with { Inputs = [.. Receipt.Inputs, new(choice, inputBefore!, observation)] };
            pendingChoice = null;
        }
        if (Receipt.Declaration.FinisherAction is byte)
        {
            if (Receipt.FocalSerial is not null && state is null && drained)
            {
                Receipt = Receipt with { Completed = Receipt.Completed ?? observation };
            }
            return;
        }
        if (Receipt.FocalSerial is not null && Receipt.Inputs is { Length: > 0 } && Receipt.Inputs[^1].Choice.ActionId == Receipt.Declaration.MoveAction && state is { Phase: BattleNativeDuelPhase.Ready })
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
        bool allowedMove = choice.Finisher ? choice.ActionId == Receipt.Declaration.FinisherAction : choice.ActionId <= 5;
        if (!NeedsInput || Receipt.Inputs.Length >= 8 || before.Duel is not { Phase: BattleNativeDuelPhase.Ready } || !choice.Learned || !allowedMove || choice.Buttons.IsDefaultOrEmpty || choice.Buttons.Length > 5)
        {
            throw new InvalidDataException("Duel diagnostics require bounded learned native input at a real ready boundary.");
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
        DiagnosticDuelCycleDeclaration declaration = Receipt.Declaration;
        bool finisher = declaration.FinisherAction.HasValue;
        bool allowedDispatch = dispatch is { NativeCommand: 11, ActionId: 65531, RequestedEffect: 274, ExecutedEffect: 274 } or
            { NativeCommand: 241, ActionId: >= 0 and <= 5, RequestedEffect: 274, ExecutedEffect: 274 } ||
            dispatch is { NativeCommand: 241, RequestedEffect: 274, ExecutedEffect: 274 } && dispatch.ActionId == (finisher ? 65530 : 65532) ||
            finisher && dispatch.NativeCommand == 239 && dispatch.ActionId == declaration.MoveAction && dispatch.RequestedEffect == declaration.Effect && dispatch.ExecutedEffect == declaration.Effect;
        if (Receipt.Dispatches.Length >= 16 || Receipt.Dispatches.Length != Receipt.DispatchPackets.Length || dispatch.ActorId != Receipt.ActorId || dispatch.NativeActorIndex != Receipt.ActorIndex || !allowedDispatch || dispatch.Reflection is not null)
        {
            throw new InvalidDataException("Duel dispatched outside its real command11/basic/timer-close lineage.");
        }
        bool focal = Receipt.FocalSerial is null && Receipt.Inputs is { Length: > 0 } && Receipt.Inputs[^1].Choice.ActionId == declaration.MoveAction && dispatch.NativeCommand == declaration.Command && dispatch.ActionId == declaration.MoveAction && dispatch.Callback >= Receipt.Inputs[^1].After.Callback;
        Receipt = Receipt with { Dispatches = [.. Receipt.Dispatches, dispatch], DispatchPackets = [.. Receipt.DispatchPackets, proof], FocalSerial = focal ? dispatch.Serial : Receipt.FocalSerial };
        return focal;
    }
}
