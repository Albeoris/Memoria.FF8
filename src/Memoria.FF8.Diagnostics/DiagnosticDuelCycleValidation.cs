using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class DiagnosticDuelCycleValidation
{
    public static void ValidateReceipt(DiagnosticDuelCycleReceipt receipt, BattleInput input, ushort effect, BattleDiagnosticEffectDispatch[] dispatches, long startCallback, long endCallback)
    {
        receipt.Declaration.Validate(input, effect, receipt.Declaration.MoveAction, receipt.Declaration.Command);
        if (receipt.Declaration.FinisherAction is byte finisher)
        {
            ValidateFinisher(receipt, input, effect, dispatches, startCallback, endCallback, finisher);
            return;
        }
        BattleDiagnosticLimitSelectionReceipt selection = receipt.Selection;
        if (receipt.Execution is < 1 or > 2 || receipt.ActorId != input.ActorId || receipt.TargetId != input.TargetId || receipt.ActorIndex >= 3 || receipt.TargetIndex is < 3 or >= 7 || selection.ActorId != receipt.ActorId || !selection.Accepted || selection.CommandOwner != 1 || selection.Command != 11 || selection.CrisisSelections.IsDefaultOrEmpty || selection.CrisisSelections.Length > 256 || selection.CrisisSelections.Any(crisis => crisis > 4) || selection.CrisisSelections[^1] is < 1 or > 4 || selection.RandomBefore is null || selection.RandomAfter is null || receipt.BeforeOpening.Duel is not null || receipt.BeforeOpening.Callback < startCallback || receipt.Opening?.Duel is not { Phase: BattleNativeDuelPhase.Opening, Selection1D28DEF: 254 } opening || opening.InitialTimer <= 0 || opening.RemainingTimer != opening.InitialTimer || opening.TargetMask != 1 << receipt.TargetIndex || receipt.Opening.Callback < receipt.BeforeOpening.Callback || receipt.Inputs is not { Length: >= 1 and <= 8 } || receipt.ReadyAfterBasic?.Duel is not { Phase: BattleNativeDuelPhase.Ready } ready || ready.RemainingTimer <= 0 || receipt.TimedOut?.Duel is not { Phase: BattleNativeDuelPhase.Finisher, Selection1D28DEF: 255 } ending || ending.RemainingTimer > 0 || receipt.Completed is not { Duel: null } completed || completed.Callback > endCallback || completed.Callback < receipt.TimedOut.Callback || receipt.ReadyAfterBasic.Callback >= receipt.TimedOut.Callback || receipt.FocalSerial is not > 0 || receipt.Dispatches.Length != receipt.Inputs.Length + 3 || receipt.DispatchPackets.Length != receipt.Dispatches.Length || !receipt.Dispatches.SequenceEqual(dispatches))
        {
            throw new InvalidDataException("Duel evidence lacks its real command11 selection, opening, accepted input, ready continuation, timer expiry or drained completion.");
        }
        selection.RandomBefore.Validate();
        selection.RandomAfter.Validate();
        long previousInput = receipt.Opening.Callback;
        foreach (DiagnosticDuelInputReceipt accepted in receipt.Inputs)
        {
            if (!accepted.Choice.Learned || accepted.Choice.Finisher || accepted.Choice.ActionId > 5 || accepted.Choice.Buttons.IsDefaultOrEmpty || accepted.Choice.Buttons.Length > 5 || accepted.Before.Duel is not { Phase: BattleNativeDuelPhase.Ready } before || accepted.After.Duel is not { Phase: BattleNativeDuelPhase.AwaitingEffect } after || before.InitialTimer != opening.InitialTimer || after.InitialTimer != opening.InitialTimer || before.TargetMask != opening.TargetMask || after.TargetMask != opening.TargetMask || after.MoveSetId != accepted.Choice.MoveSetId || accepted.Before.Callback < previousInput || accepted.After.Callback < accepted.Before.Callback || accepted.After.Callback > receipt.ReadyAfterBasic.Callback || before.RemainingTimer <= 0 || after.RemainingTimer <= 0 || before.RemainingTimer > opening.InitialTimer || after.RemainingTimer > before.RemainingTimer || !NativeBattleDuelRules.Matches(accepted.Choice, after.InputRing, after.InputCursor))
            {
                throw new InvalidDataException("Duel input is not an ordered actually accepted original learned button sequence.");
            }
            previousInput = accepted.After.Callback;
        }
        if (receipt.Inputs[^1].Choice.ActionId != receipt.Declaration.BasicAction || receipt.ReadyAfterBasic.Callback < previousInput || ready.MoveSetId != receipt.Inputs[^1].Choice.MoveSetId || ready.RemainingTimer > receipt.Inputs[^1].After.Duel!.RemainingTimer || ending.InitialTimer != opening.InitialTimer || ending.TargetMask != opening.TargetMask || ending.MoveSetId != ready.MoveSetId || ready.InitialTimer != opening.InitialTimer || ready.TargetMask != opening.TargetMask)
        {
            throw new InvalidDataException("Duel closed before the declared native basic completed normally.");
        }
        long previousSerial = 0;
        long previousCallback = receipt.BeforeOpening.Callback;
        HashSet<(int Header, long Generation)> owners = [];
        bool attached = receipt.DispatchPackets[0].AttachedPresentation;
        for (int index = 0; index < receipt.Dispatches.Length; index++)
        {
            BattleDiagnosticEffectDispatch dispatch = receipt.Dispatches[index];
            DiagnosticDuelDispatchPackets proof = receipt.DispatchPackets[index];
            bool first = index == 0;
            bool last = index == receipt.Dispatches.Length - 1;
            int expectedAction = first ? 65531 : last ? 65532 : index == 1 ? dispatch.ActionId ?? -1 : receipt.Inputs[index - 2].Choice.ActionId;
            byte expectedCommand = first ? (byte)11 : (byte)241;
            ushort expectedHeaderEffect = last ? (ushort)0 : (ushort)274;
            byte expectedParameter = first ? (byte)0 : last ? (byte)8 : expectedAction is >= 0 and <= 5 ? (byte)(expectedAction + 2) : (byte)0;
            // Attached moves retain the logical Limit owner across the original command241 packet cycle.
            // The original physical mode can also retain the opening's semantic owner for its first move.
            bool abilityMatches = proof.AttachedPresentation == attached && (first
                ? dispatch.Ability == BattleAbility.LimitBreak
                : dispatch.Ability == BattleAbility.NativeCommand || (attached || index == 1) && dispatch.Ability == BattleAbility.LimitBreak);
            if (dispatch.Serial <= previousSerial || dispatch.Callback < previousCallback || dispatch.ActorId != receipt.ActorId || dispatch.NativeActorIndex != receipt.ActorIndex || dispatch.RequestedEffect != 274 || dispatch.ExecutedEffect != 274 || dispatch.Reflection is not null || dispatch.NativeCommand != expectedCommand || dispatch.ActionId != expectedAction || (!first && !last && expectedAction is < 0 or > 5) || !abilityMatches || dispatch.Callback > completed.Callback || proof.Serial != dispatch.Serial || proof.Callback != dispatch.Callback || proof.HeaderIndex != dispatch.NativeHeaderIndex || proof.HeaderGeneration != dispatch.NativeHeaderGeneration || proof.HeaderGeneration <= 0 || proof.HeaderIndex is < 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity || !owners.Add((proof.HeaderIndex, proof.HeaderGeneration)) || proof.HeaderEffect != expectedHeaderEffect || proof.RetainedEffect != 274 || proof.Parameter80 != expectedParameter)
            {
                throw new InvalidDataException($"Duel dispatch is outside its ordered original command11/241 owned cycle. execution={receipt.Execution}; row={index}/{receipt.Dispatches.Length}; serial={dispatch.Serial},previous={previousSerial},proof={proof.Serial}; callback={dispatch.Callback},previous={previousCallback},end={completed.Callback},proof={proof.Callback}; actor={dispatch.ActorId}/{dispatch.NativeActorIndex},expected={receipt.ActorId}/{receipt.ActorIndex}; ability={dispatch.Ability},expected={(first ? "LimitBreak" : index == 1 ? "LimitBreak|NativeCommand" : "NativeCommand")}; command={dispatch.NativeCommand},expected={expectedCommand}; action={dispatch.ActionId},expected={expectedAction}; requested={dispatch.RequestedEffect},executed={dispatch.ExecutedEffect},reflection={dispatch.Reflection is not null}; header={dispatch.NativeHeaderIndex}/{dispatch.NativeHeaderGeneration},proof={proof.HeaderIndex}/{proof.HeaderGeneration}; rawEffect={proof.HeaderEffect},expected={expectedHeaderEffect},retained={proof.RetainedEffect}; parameter80={proof.Parameter80},expected={expectedParameter},parameter82={proof.Parameter82}.");
            }
            ValidatePackets(proof, dispatch, receipt.ActorIndex, expectedCommand, expectedAction, expectedHeaderEffect);
            if (index <= 1 && dispatch.Callback > receipt.Inputs[0].Before.Callback)
            {
                throw new InvalidDataException("Duel opening dispatch followed accepted player input.");
            }
            if (index >= 2 && !last)
            {
                int inputIndex = index - 2;
                long nextBoundary = inputIndex + 1 < receipt.Inputs.Length ? receipt.Inputs[inputIndex + 1].Before.Callback : receipt.ReadyAfterBasic.Callback;
                if (dispatch.Callback < receipt.Inputs[inputIndex].After.Callback || dispatch.Callback > nextBoundary)
                {
                    throw new InvalidDataException("Duel basic dispatch is outside its actual input and next ready boundary.");
                }
            }
            if (last && (dispatch.Callback < receipt.TimedOut.Callback || proof.Parameter82 != 0 || proof.Duel is not { Phase: BattleNativeDuelPhase.Finisher, Selection1D28DEF: 254 } closing || closing.RemainingTimer > 0 || closing.InitialTimer != ending.InitialTimer || closing.TargetMask != ending.TargetMask || closing.MoveSetId != ending.MoveSetId))
            {
                throw new InvalidDataException("Duel control alias requires raw header effect0 and the actual expired retained274 selector8 owner after selector reset.");
            }
            previousSerial = dispatch.Serial;
            previousCallback = dispatch.Callback;
        }
        if (receipt.FocalSerial != receipt.Dispatches[^2].Serial)
        {
            throw new InvalidDataException("Duel focal dispatch is not the declared final basic before timed closure.");
        }
    }

    private static void ValidateFinisher(DiagnosticDuelCycleReceipt receipt, BattleInput input, ushort effect, BattleDiagnosticEffectDispatch[] dispatches, long startCallback, long endCallback, byte finisher)
    {
        BattleDiagnosticLimitSelectionReceipt selection = receipt.Selection;
        if (receipt.Execution is < 1 or > 2 || receipt.ActorId != input.ActorId || receipt.TargetId != input.TargetId || receipt.ActorIndex >= 3 || receipt.TargetIndex is < 3 or >= 7 || selection.ActorId != receipt.ActorId || !selection.Accepted || selection.CommandOwner != 1 || selection.Command != 11 || selection.CrisisSelections.IsDefaultOrEmpty || selection.CrisisSelections.Length > 256 || selection.CrisisSelections.Any(crisis => crisis > 4) || selection.CrisisSelections[^1] != 1 || selection.RandomBefore is null || selection.RandomAfter is null || receipt.BeforeOpening.Duel is not null || receipt.BeforeOpening.Callback < startCallback || receipt.Opening?.Duel is not { Phase: BattleNativeDuelPhase.Opening, Selection1D28DEF: 254 } opening || opening.InitialTimer <= 0 || opening.RemainingTimer != opening.InitialTimer || opening.TargetMask != 1 << receipt.TargetIndex || receipt.Opening.Callback < receipt.BeforeOpening.Callback || receipt.Inputs is not { Length: >= 1 and <= 8 } || receipt.ReadyAfterBasic is not null || receipt.TimedOut is not null || receipt.Completed is not { Duel: null } completed || completed.Callback > endCallback || receipt.FocalSerial is not > 0 || receipt.Dispatches.Length != receipt.Inputs.Length + 3 || receipt.DispatchPackets.Length != receipt.Dispatches.Length || !receipt.Dispatches.SequenceEqual(dispatches))
        {
            throw new InvalidDataException("Duel finisher evidence lacks original opening, accepted inputs, owned dispatches or drained completion.");
        }
        selection.RandomBefore.Validate();
        selection.RandomAfter.Validate();
        long previousInput = receipt.Opening.Callback;
        for (int index = 0; index < receipt.Inputs.Length; index++)
        {
            DiagnosticDuelInputReceipt accepted = receipt.Inputs[index];
            bool final = index == receipt.Inputs.Length - 1;
            if (!accepted.Choice.Learned || accepted.Choice.Finisher != final || (final ? accepted.Choice.ActionId != finisher : accepted.Choice.ActionId > 5) || accepted.Choice.Buttons.IsDefaultOrEmpty || accepted.Choice.Buttons.Length > 5 || accepted.Before.Duel is not { Phase: BattleNativeDuelPhase.Ready, Selection1D28DEF: 254 } before || accepted.After.Duel is not { } after || after.Phase != (final ? BattleNativeDuelPhase.Finisher : BattleNativeDuelPhase.AwaitingEffect) || after.Selection1D28DEF != accepted.Choice.MoveSetId || before.InitialTimer != opening.InitialTimer || after.InitialTimer != opening.InitialTimer || before.TargetMask != opening.TargetMask || after.TargetMask != opening.TargetMask || after.MoveSetId != accepted.Choice.MoveSetId || accepted.Before.Callback < previousInput || accepted.After.Callback < accepted.Before.Callback || accepted.After.Callback > completed.Callback || before.RemainingTimer <= 0 || after.RemainingTimer <= 0 || before.RemainingTimer > opening.InitialTimer || after.RemainingTimer > before.RemainingTimer || !NativeBattleDuelRules.Matches(accepted.Choice, after.InputRing, after.InputCursor))
            {
                throw new InvalidDataException("Duel finisher input is outside the ordered learned native moves and their original button pulses.");
            }
            previousInput = accepted.After.Callback;
        }
        long previousSerial = 0;
        long previousCallback = receipt.BeforeOpening.Callback;
        HashSet<(int Header, long Generation)> owners = [];
        for (int index = 0; index < receipt.Dispatches.Length; index++)
        {
            BattleDiagnosticEffectDispatch dispatch = receipt.Dispatches[index];
            DiagnosticDuelDispatchPackets proof = receipt.DispatchPackets[index];
            bool first = index == 0;
            bool focal = index == receipt.Dispatches.Length - 1;
            bool closing = index == receipt.Dispatches.Length - 2;
            int action = first ? 65531 : focal ? finisher : closing ? 65530 : index == 1 ? dispatch.ActionId ?? -1 : receipt.Inputs[index - 2].Choice.ActionId;
            byte command = first ? (byte)11 : focal ? (byte)239 : (byte)241;
            ushort dispatchEffect = focal ? effect : (ushort)274;
            ushort headerEffect = closing ? finisher : dispatchEffect;
            if (dispatch.Serial <= previousSerial || dispatch.Callback < previousCallback || dispatch.Callback > completed.Callback || dispatch.ActorId != receipt.ActorId || dispatch.NativeActorIndex != receipt.ActorIndex || dispatch.NativeCommand != command || dispatch.ActionId != action || dispatch.RequestedEffect != dispatchEffect || dispatch.ExecutedEffect != dispatchEffect || dispatch.Reflection is not null || proof.Serial != dispatch.Serial || proof.Callback != dispatch.Callback || proof.HeaderIndex != dispatch.NativeHeaderIndex || proof.HeaderGeneration != dispatch.NativeHeaderGeneration || proof.HeaderGeneration <= 0 || proof.HeaderIndex is < 0 or >= NativeBattleRetainedActionPackets.HeaderCapacity || !owners.Add((proof.HeaderIndex, proof.HeaderGeneration)) || proof.HeaderEffect != headerEffect || proof.RetainedEffect != dispatchEffect || proof.AttachedPresentation != receipt.DispatchPackets[0].AttachedPresentation)
            {
                throw new InvalidDataException($"Duel finisher dispatch {index} is outside its original ordered command11/241/239 owned cycle.");
            }
            if (index <= 1 ? dispatch.Callback > receipt.Inputs[0].Before.Callback : closing || focal ? dispatch.Callback < receipt.Inputs[^1].After.Callback : dispatch.Callback < receipt.Inputs[index - 2].After.Callback || dispatch.Callback > receipt.Inputs[index - 1].Before.Callback)
            {
                throw new InvalidDataException("Duel finisher dispatch is outside its selected native input boundary.");
            }
            if (closing && (proof.Parameter80 != 8 || proof.Parameter82 != finisher - 5 || proof.Duel is not { Phase: BattleNativeDuelPhase.Finisher, Selection1D28DEF: 254 } widget || widget.MoveSetId != receipt.Inputs[^1].Choice.MoveSetId || widget.TargetMask != opening.TargetMask || widget.InitialTimer != opening.InitialTimer || widget.RemainingTimer <= 0))
            {
                throw new InvalidDataException("Duel finisher control lacks its retained selector8 and original chosen move parameter.");
            }
            if (focal && !FinisherTargetsMatch(proof, dispatch, receipt.TargetId, receipt.TargetIndex, finisher))
            {
                throw new InvalidDataException("Duel finisher recipients differ from the actual selected target or expanded group.");
            }
            ValidatePackets(proof, dispatch, receipt.ActorIndex, command, action, headerEffect);
            previousSerial = dispatch.Serial;
            previousCallback = dispatch.Callback;
        }
        if (receipt.FocalSerial != receipt.Dispatches[^1].Serial)
        {
            throw new InvalidDataException("Duel finisher receipt does not designate the actual final command239 dispatch.");
        }
    }

    private static bool FinisherTargetsMatch(DiagnosticDuelDispatchPackets proof, BattleDiagnosticEffectDispatch dispatch, string target, byte targetIndex, byte finisher)
    {
        if (proof.AttachedPresentation && proof.HostedTargets is null)
        {
            return false;
        }
        if (finisher is 6 or 9)
        {
            return !proof.AttachedPresentation || dispatch.NativeTargetMask is null && proof.HostedTargets!.Recipients.Length == 5;
        }
        return dispatch.NativeTargetMask == 1 << targetIndex && (!proof.AttachedPresentation || proof.HostedTargets!.Recipients is [var recipient] && recipient == target);
    }

    private static void ValidatePackets(DiagnosticDuelDispatchPackets proof, BattleDiagnosticEffectDispatch dispatch, byte actor, byte command, int action, ushort effect)
    {
        try
        {
            if (proof.Packets is null || proof.Packets.ActiveHeaderIndex != proof.HeaderIndex)
            {
                throw new InvalidDataException("Duel packet proof lost its synchronous active header owner.");
            }
            proof.Packets.Validate();
            NativeBattleRetainedActionHeaderSlot header = proof.Packets.Headers[proof.HeaderIndex];
            int Word(int offset) => header.ReadOriginalByte(offset) | header.ReadOriginalByte(offset + 1) << 8;
            bool targetsMatch = proof.HostedTargets is { } targets
                ? proof.AttachedPresentation && command == 239 && action is >= 6 and <= 9 && targets.Token > 0 && targets.FirstContact >= 0 && !targets.Recipients.IsDefaultOrEmpty && targets.Recipients.All(id => !string.IsNullOrWhiteSpace(id)) && targets.Recipients.Distinct().Count() == targets.Recipients.Length && header.ReadOriginalByte(16) == targets.Recipients.Length && (header.FirstContactIndex is null || header.FirstContactIndex == targets.FirstContact)
                : DiagnosticCounterCycleValidation.ReadTargetMask(proof.Packets, proof.HeaderIndex) == dispatch.NativeTargetMask;
            if (header.ReadOriginalByte(0) != actor || header.ReadOriginalByte(1) != command || Word(4) != action || Word(6) != effect || !targetsMatch)
            {
                throw new InvalidDataException("Duel dispatch changed its physical header bytes or exact recipient normalization.");
            }
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException("Duel packet proof contains unknown bytes or unsupported native contacts.", exception);
        }
        catch (InvalidDataException exception)
        {
            throw new InvalidDataException($"Duel packet proof failed at command {command}/{action}, header {proof.HeaderIndex}, active {proof.Packets?.ActiveHeaderIndex}, counts {proof.Packets?.ActionCount}/{proof.Packets?.ContactCount}. {exception.Message}", exception);
        }
    }

    public static void ValidateChain(DiagnosticDuelCycleReceipt first, DiagnosticDuelCycleReceipt second)
    {
        if (first.Execution != 1 || second.Execution != 2 || first.ActorId != second.ActorId || first.TargetId != second.TargetId || first.ActorIndex != second.ActorIndex || first.TargetIndex != second.TargetIndex || first.Declaration != second.Declaration || first.Completed is not { Duel: null } completed || second.BeforeOpening.Callback <= completed.Callback || first.BeforeOpening.Duel is not null || second.BeforeOpening.Duel is not null || second.Opening?.Duel?.InitialTimer != first.Opening?.Duel?.InitialTimer || first.Dispatches.Length == 0 || second.Dispatches.Length == 0 || second.Dispatches[0].Serial <= first.Dispatches[^1].Serial || first.Dispatches.Any(previous => second.Dispatches.Any(current => current.NativeHeaderIndex == previous.NativeHeaderIndex && current.NativeHeaderGeneration == previous.NativeHeaderGeneration)))
        {
            throw new InvalidDataException("Duel repeat did not rearm a new real command11 cycle after the first native closure with fresh dispatch ownership.");
        }
    }
}
