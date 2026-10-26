using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BattleDiagnosticTerminalHoldDeclaration([property: JsonRequired] string Reason, [property: JsonRequired] ImmutableArray<string> PartyActorIds, [property: JsonRequired] long MaximumCallbacks)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Reason) || Reason.Length > 512 || PartyActorIds.IsDefaultOrEmpty || PartyActorIds.Length > 3 || PartyActorIds.Any(string.IsNullOrWhiteSpace) || PartyActorIds.Distinct(StringComparer.Ordinal).Count() != PartyActorIds.Length || MaximumCallbacks is < 1 or > 18000)
        {
            throw new InvalidDataException("A diagnostic defeat hold requires a reason, one through three explicit party owners and a finite callback budget.");
        }
    }
}

public enum BattleDiagnosticTerminalHoldPhase
{
    Armed,
    Held,
    Released,
    Expired
}

public sealed record BattleDiagnosticTerminalHoldReceipt([property: JsonRequired] Guid HoldId, [property: JsonRequired] Guid LifecycleId, [property: JsonRequired] string Kind, [property: JsonRequired] int Sequence, [property: JsonRequired] long Frame, [property: JsonRequired] long Callback, [property: JsonRequired] BattleOutcome Candidate, [property: JsonRequired] byte? NativeResult, [property: JsonRequired] bool Drained, [property: JsonRequired] long RestoreCount, [property: JsonRequired] long ResetCount, [property: JsonRequired] BattleDiagnosticRepairRandomState Random, [property: JsonRequired] BattleDiagnosticCombatant[] Party);
public sealed record BattleDiagnosticTerminalHoldState([property: JsonRequired] BattleDiagnosticTerminalHoldDeclaration Declaration, [property: JsonRequired] Guid HoldId, [property: JsonRequired] BattleDiagnosticTerminalHoldPhase Phase, [property: JsonRequired] int Sequence, [property: JsonRequired] int Rearms, [property: JsonRequired] long? HeldFrame, [property: JsonRequired] long? HeldCallback, [property: JsonRequired] ImmutableArray<BattleDiagnosticTerminalHoldReceipt> Receipts);

public sealed partial class BattleSession
{
    public BattleDiagnosticTerminalHoldState? DiagnosticTerminalHold { get; private set; }
    public IReadOnlyList<BattleDiagnosticTerminalHoldReceipt> DiagnosticTerminalHoldReceipts => DiagnosticTerminalHold is { } hold ? hold.Receipts : ImmutableArray<BattleDiagnosticTerminalHoldReceipt>.Empty;

    public void ConfigureDiagnosticTerminalHold(BattleDiagnosticTerminalHoldDeclaration declaration)
    {
        declaration.Validate();
        if (DiagnosticTerminalHold is not null || Frame != 0 || (NativeHostCallbacks ?? 0) != 0 || Outcome != BattleOutcome.Active || DebugOutcome is not null || HasPendingNativeOutcome)
        {
            throw new InvalidOperationException("A diagnostic defeat hold must be declared once before the first battle callback.");
        }
        ValidateDiagnosticTerminalParty(declaration);
        DiagnosticTerminalHold = new(declaration, Guid.NewGuid(), BattleDiagnosticTerminalHoldPhase.Armed, 0, 0, null, null, []);
    }

    private void ValidateDiagnosticTerminalParty(BattleDiagnosticTerminalHoldDeclaration declaration)
    {
        foreach (string id in declaration.PartyActorIds)
        {
            if (!byId.TryGetValue(id, out BattleCombatant? actor) || !actor.IsPlayer || !actor.IsActive || actor.NativeIndex is < 0 or > 2 || actor.GuardianFormVitals is not null || guardianForceCasts.ContainsKey(id) || (actor.NativeStatus1 & 0x80010000) != 0)
            {
                throw new InvalidDataException("Diagnostic defeat hold targets must retain their actual ordinary party allocation and HP owners.");
            }
        }
    }

    private bool DeferDiagnosticDefeat()
    {
        if (DiagnosticTerminalHold is not { Phase: BattleDiagnosticTerminalHoldPhase.Armed or BattleDiagnosticTerminalHoldPhase.Held } hold)
        {
            return false;
        }
        if (HasPendingNativeOutcome || DebugOutcome is not null || Outcome != BattleOutcome.Active)
        {
            throw new InvalidOperationException("A diagnostic hold cannot roll back an admitted terminal outcome.");
        }
        ValidateDiagnosticTerminalParty(hold.Declaration);
        long callback = NativeHostCallbacks ?? Frame;
        if (hold.Phase == BattleDiagnosticTerminalHoldPhase.Held)
        {
            if (callback - hold.HeldCallback!.Value <= hold.Declaration.MaximumCallbacks)
            {
                return true;
            }
            DiagnosticTerminalHold = hold with { Phase = BattleDiagnosticTerminalHoldPhase.Expired };
            AppendDiagnosticTerminalHoldReceipt("expired", false);
            return false;
        }
        if (hold.Sequence >= 2)
        {
            throw new InvalidOperationException("A diagnostic replay hold permits at most two terminal focal invocations.");
        }
        DiagnosticTerminalHold = hold with { Phase = BattleDiagnosticTerminalHoldPhase.Held, Sequence = hold.Sequence + 1, HeldFrame = Frame, HeldCallback = callback };
        AppendDiagnosticTerminalHoldReceipt("held", false);
        return true;
    }

    public void RearmDiagnosticTerminalHold(long expectedCallback)
    {
        BattleDiagnosticTerminalHoldState hold = DiagnosticTerminalHold ?? throw new InvalidOperationException("No diagnostic defeat hold was declared.");
        long callback = NativeHostCallbacks ?? Frame;
        if (hold.Phase != BattleDiagnosticTerminalHoldPhase.Held || hold.Sequence != 1 || hold.Rearms != 0 || callback - hold.HeldCallback!.Value > hold.Declaration.MaximumCallbacks || Outcome != BattleOutcome.Active || HasPendingNativeOutcome || !NativeEntryReady || callback != expectedCallback || ObserveDiagnosticDrainBlockers().Count != 0 || DetermineOutcome() != BattleOutcome.Active)
        {
            throw new InvalidOperationException("Diagnostic rearm requires the first held defeat, full normal drain and a genuine active outcome after declared combat repairs.");
        }
        ValidateDiagnosticTerminalParty(hold.Declaration);
        if (hold.Declaration.PartyActorIds.Any(id => (NativeSavedStatusBits(GetActor(id)) & 5) != 0))
        {
            throw new InvalidOperationException("Every declared diagnostic repair recipient must genuinely leave KO and Petrify before rearm.");
        }
        DiagnosticTerminalHold = hold with { Phase = BattleDiagnosticTerminalHoldPhase.Armed, Rearms = 1 };
        AppendDiagnosticTerminalHoldReceipt("rearmed", true);
    }

    public void ReleaseDiagnosticTerminalHold(long expectedCallback)
    {
        BattleDiagnosticTerminalHoldState hold = DiagnosticTerminalHold ?? throw new InvalidOperationException("No diagnostic defeat hold was declared.");
        if ((NativeHostCallbacks ?? Frame) != expectedCallback || hold.Phase is BattleDiagnosticTerminalHoldPhase.Released or BattleDiagnosticTerminalHoldPhase.Expired)
        {
            throw new InvalidOperationException("Diagnostic release requires its current callback and an unreleased hold.");
        }
        if (hold.Phase == BattleDiagnosticTerminalHoldPhase.Held && expectedCallback - hold.HeldCallback!.Value > hold.Declaration.MaximumCallbacks)
        {
            DiagnosticTerminalHold = hold with { Phase = BattleDiagnosticTerminalHoldPhase.Expired };
            AppendDiagnosticTerminalHoldReceipt("expired", false);
            return;
        }
        DiagnosticTerminalHold = hold with { Phase = BattleDiagnosticTerminalHoldPhase.Released };
        bool drained = NativeEntryReady && NativeGraphics is not null && NativeFileIo?.ActiveCount is not null && ObserveDiagnosticDrainBlockers().Count == 0;
        AppendDiagnosticTerminalHoldReceipt("released", drained);
    }

    private void AppendDiagnosticTerminalHoldReceipt(string kind, bool drained)
    {
        BattleDiagnosticTerminalHoldState hold = DiagnosticTerminalHold!;
        BattleDiagnosticCombatant[] party = hold.Declaration.PartyActorIds.Select(id => ObserveDiagnosticCombatant(GetActor(id))).ToArray();
        BattleDiagnosticTerminalHoldReceipt receipt = new(hold.HoldId, DiagnosticLifecycleId, kind, hold.Sequence, Frame, NativeHostCallbacks ?? Frame, BattleOutcome.Defeat, NativeGraphics?.NativeOutcome?.ResultByte6E7, drained, DiagnosticRestoreCount, DiagnosticResetCount, ObserveDiagnosticRepairRandom(), party);
        DiagnosticTerminalHold = hold with { Receipts = hold.Receipts.Add(receipt) };
    }

    private void RestoreDiagnosticTerminalHold(BattleSnapshot snapshot)
    {
        BattleDiagnosticTerminalHoldState? hold = snapshot.DiagnosticTerminalHold;
        if (hold is null)
        {
            return;
        }
        hold.Declaration.Validate();
        long callback = snapshot.NativeHostCallbacks ?? snapshot.Frame;
        if (hold.HoldId == Guid.Empty || !Enum.IsDefined(hold.Phase) || hold.Sequence is < 0 or > 2 || hold.Rearms is < 0 or > 1 || hold.Rearms > hold.Sequence || hold.Receipts.IsDefault || hold.Receipts.Length > 5 || hold.Phase == BattleDiagnosticTerminalHoldPhase.Held && (hold.Sequence < 1 || hold.HeldFrame is null || hold.HeldCallback is null) || hold.Sequence == 0 && (hold.HeldFrame is not null || hold.HeldCallback is not null) || hold.HeldFrame > snapshot.Frame || hold.HeldCallback > callback || (hold.Phase is BattleDiagnosticTerminalHoldPhase.Armed or BattleDiagnosticTerminalHoldPhase.Held) && (Outcome != BattleOutcome.Active || HasPendingNativeOutcome || DebugOutcome is not null))
        {
            throw new InvalidDataException("Saved diagnostic defeat hold contradicts its phase, callback or terminal owner.");
        }
        ValidateDiagnosticTerminalParty(hold.Declaration);
        long previous = -1;
        long previousFrame = -1;
        BattleDiagnosticTerminalHoldPhase phase = BattleDiagnosticTerminalHoldPhase.Armed;
        int sequence = 0;
        int rearms = 0;
        long heldCallback = -1;
        foreach (BattleDiagnosticTerminalHoldReceipt receipt in hold.Receipts)
        {
            if (receipt is null || receipt.HoldId != hold.HoldId || receipt.LifecycleId == Guid.Empty || receipt.Candidate != BattleOutcome.Defeat || receipt.NativeResult is > 0 || receipt.Callback < previous || receipt.Callback < 0 || receipt.Callback > callback || receipt.Frame < previousFrame || receipt.Frame < 0 || receipt.Frame > snapshot.Frame || receipt.ResetCount < 0 || receipt.RestoreCount < 0 || receipt.Party is null || receipt.Random is null || receipt.Party.Any(actor => actor is null) || !receipt.Party.Select(actor => actor.Id).SequenceEqual(hold.Declaration.PartyActorIds) || receipt.Party.Any(actor => actor.NativeIndex is not (>= 0 and < 3) || byId[actor.Id].NativeIndex != actor.NativeIndex || actor.CurrentHp < 0 || actor.CurrentHp > actor.MaximumHp || (actor.Statuses1 & 0x80010000) != 0))
            {
                throw new InvalidDataException("Saved diagnostic defeat hold receipt has inconsistent owner lineage.");
            }
            switch (receipt.Kind)
            {
                case "held" when phase == BattleDiagnosticTerminalHoldPhase.Armed && sequence < 2 && receipt.Party.All(actor => (actor.Statuses0 & 5) != 0):
                    phase = BattleDiagnosticTerminalHoldPhase.Held;
                    sequence++;
                    heldCallback = receipt.Callback;
                    break;
                case "rearmed" when phase == BattleDiagnosticTerminalHoldPhase.Held && sequence == 1 && rearms == 0 && receipt.Drained && receipt.Callback - heldCallback <= hold.Declaration.MaximumCallbacks && receipt.Party.All(actor => (actor.Statuses0 & 5) == 0):
                    phase = BattleDiagnosticTerminalHoldPhase.Armed;
                    rearms++;
                    break;
                case "released" when phase == BattleDiagnosticTerminalHoldPhase.Armed || phase == BattleDiagnosticTerminalHoldPhase.Held && receipt.Callback - heldCallback <= hold.Declaration.MaximumCallbacks:
                    phase = BattleDiagnosticTerminalHoldPhase.Released;
                    break;
                case "expired" when phase == BattleDiagnosticTerminalHoldPhase.Held && receipt.Callback - heldCallback > hold.Declaration.MaximumCallbacks:
                    phase = BattleDiagnosticTerminalHoldPhase.Expired;
                    break;
                default:
                    throw new InvalidDataException("Saved diagnostic defeat hold receipt order is invalid.");
            }
            if (receipt.Sequence != sequence)
            {
                throw new InvalidDataException("Saved diagnostic defeat hold sequence is inconsistent.");
            }
            previous = receipt.Callback;
            previousFrame = receipt.Frame;
        }
        if (phase != hold.Phase || sequence != hold.Sequence || rearms != hold.Rearms)
        {
            throw new InvalidDataException("Saved diagnostic defeat hold state lacks its transition receipts.");
        }
        if (hold.Sequence > 0 && (hold.Receipts.Last(receipt => receipt.Kind == "held").Callback != hold.HeldCallback || hold.Receipts.Last(receipt => receipt.Kind == "held").Frame != hold.HeldFrame))
        {
            throw new InvalidDataException("Saved diagnostic hold timing does not match its last admitted defeat.");
        }
        if (hold.Phase == BattleDiagnosticTerminalHoldPhase.Held && (hold.Receipts.IsEmpty || hold.Receipts[^1].Kind != "held" || hold.Receipts[^1].Callback != hold.HeldCallback))
        {
            throw new InvalidDataException("A held diagnostic defeat requires its actual admission receipt.");
        }
        DiagnosticTerminalHold = hold;
    }
}
