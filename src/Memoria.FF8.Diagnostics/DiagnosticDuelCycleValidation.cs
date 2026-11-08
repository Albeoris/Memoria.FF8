using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public static class DiagnosticDuelCycleValidation
{
    public static void ValidateReceipt(DiagnosticDuelCycleReceipt receipt, BattleInput input, ushort effect, BattleDiagnosticEffectDispatch[] dispatches, long startCallback, long endCallback)
    {
        receipt.Declaration.Validate(input, effect, receipt.Declaration.BasicAction, 241);
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
            // The initial secondary header can retain the ordinary opening's semantic owner;
            // without that actor-matched owner, the header resolver reports NativeCommand.
            bool abilityMatches = first ? dispatch.Ability == BattleAbility.LimitBreak : index == 1 ? dispatch.Ability is BattleAbility.LimitBreak or BattleAbility.NativeCommand : dispatch.Ability == BattleAbility.NativeCommand;
            if (dispatch.Serial <= previousSerial || dispatch.Callback < previousCallback || dispatch.ActorId != receipt.ActorId || dispatch.NativeActorIndex != receipt.ActorIndex || dispatch.RequestedEffect != 274 || dispatch.ExecutedEffect != 274 || dispatch.Reflection is not null || dispatch.NativeCommand != expectedCommand || dispatch.ActionId != expectedAction || (!first && !last && expectedAction is < 0 or > 5) || !abilityMatches || dispatch.Callback > completed.Callback || proof.Serial != dispatch.Serial || proof.Callback != dispatch.Callback || proof.HeaderIndex != dispatch.NativeHeaderIndex || proof.HeaderGeneration != dispatch.NativeHeaderGeneration || proof.HeaderGeneration <= 0 || proof.HeaderIndex is < 0 or >= NativeBattleActionPacketsState.HeaderCapacity || !owners.Add((proof.HeaderIndex, proof.HeaderGeneration)) || proof.HeaderEffect != expectedHeaderEffect || proof.RetainedEffect != 274 || proof.Parameter80 != expectedParameter)
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

    private static void ValidatePackets(DiagnosticDuelDispatchPackets proof, BattleDiagnosticEffectDispatch dispatch, byte actor, byte command, int action, ushort effect)
    {
        try
        {
            if (proof.Packets is null || proof.Packets.ActiveHeaderIndex != proof.HeaderIndex)
            {
                throw new InvalidDataException("Duel packet proof lost its synchronous active header owner.");
            }
            proof.Packets.Validate();
            NativeBattleActionHeaderSlot header = proof.Packets.Headers[proof.HeaderIndex];
            int Word(int offset) => header.ReadByte(offset) | header.ReadByte(offset + 1) << 8;
            NativeBattleActionGraphicsState normalized = NativeBattleActionGraphics.Normalize(proof.Packets, proof.HeaderIndex);
            if (header.ReadByte(0) != actor || header.ReadByte(1) != command || Word(4) != action || Word(6) != effect || normalized.ActorIndex != actor || normalized.HeaderIndex != proof.HeaderIndex || normalized.TargetMask != dispatch.NativeTargetMask)
            {
                throw new InvalidDataException("Duel dispatch changed its physical header bytes or exact recipient normalization.");
            }
        }
        catch (NotSupportedException exception)
        {
            throw new InvalidDataException("Duel packet proof contains unknown bytes or unsupported native contacts.", exception);
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
