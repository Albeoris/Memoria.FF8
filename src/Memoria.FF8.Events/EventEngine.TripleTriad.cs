using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed record NativeTripleTriadRequest(int Version, int FieldId, string ProgramHash, long Frame, int ObjectId, int ScriptId, int Operation, long FiberSequence, ImmutableArray<int> RawOperands, byte OpponentOwner, int NativeRuleMask, byte TradeRule, byte RareChance, byte Argument4, byte Argument5, byte CommonLevelMask, int AvailableCards)
{
    [JsonIgnore]
    public TripleTriadRules Rules => DecodeRules(NativeRuleMask);

    public static TripleTriadRules DecodeRules(int mask)
    {
        if ((mask & ~0xDF) != 0)
        {
            throw new NotSupportedException("CARDGAME rule mask contains an unsupported native controller, special mode or rule bit.");
        }
        TripleTriadRules rules = TripleTriadRules.None;
        foreach ((int native, TripleTriadRules rule) in new[] { (1, TripleTriadRules.Open), (2, TripleTriadRules.Same), (4, TripleTriadRules.Plus), (8, TripleTriadRules.Random), (16, TripleTriadRules.SuddenDeath), (64, TripleTriadRules.SameWall), (128, TripleTriadRules.Elemental) })
        {
            if ((mask & native) != 0)
            {
                rules |= rule;
            }
        }
        return rules;
    }
}

public sealed partial class EventEngine
{
    public Func<NativeTripleTriadRequest, string?>? ValidateTripleTriad { get; set; }
    public NativeTripleTriadRequest? PendingTripleTriad => Scene.TripleTriadRequest;

    /// <summary>Completes native CARDGAME: 0 win, 1 loss, 2 draw, -1 cancelled before play.</summary>
    public void ReturnFromTripleTriad(int nativeResult, Action? publishOutcome = null)
    {
        if (nativeResult is < -1 or > 2)
        {
            throw new ArgumentOutOfRangeException(nameof(nativeResult));
        }
        NativeTripleTriadRequest request = PendingTripleTriad ?? throw new InvalidOperationException("No CARDGAME owns the suspended field VM.");
        Fiber fiber = fibers.SingleOrDefault(fiber => fiber.Sequence == request.FiberSequence && !fiber.Complete && fiber.WaitReason == WaitReason.TripleTriad) ?? throw new InvalidDataException("CARDGAME lost its suspended issuing fiber.");
        publishOutcome?.Invoke();
        fiber.Results[1] = nativeResult;
        fiber.WaitReason = WaitReason.None;
        Scene.TripleTriadRequest = null;
        StepCore();
        OperationCompleted?.Invoke();
    }

    private NativeTripleTriadRequest PrepareTripleTriad(Fiber fiber)
    {
        ImmutableArray<int> raw = fiber.Stack.Take(7).Reverse().ToImmutableArray();
        int count = Enumerable.Range(0, NativeCardInventory.CardCount).Sum(card => Math.Max(0, services.CardInventory!.AvailableCount(card)));
        return new NativeTripleTriadRequest(1, FieldId, programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position, fiber.Sequence, raw, unchecked((byte)raw[0]), raw[1], unchecked((byte)raw[2]), unchecked((byte)raw[3]), unchecked((byte)raw[4]), unchecked((byte)raw[5]), unchecked((byte)raw[6]), count);
    }

    private string? UnsupportedTripleTriad(Fiber fiber, JsmOperation operation)
    {
        if (operation.Opcode != Jsm.Opcode.CARDGAME)
        {
            return null;
        }
        if (fiber.Stack.Count < 7 || fiber.Stack.Capture().Take(7).Any(value => !value.IsExact))
        {
            return "CARDGAME requires seven exact original DWORD operands";
        }
        if (services.CardInventory is null || !services.CanStoreCardInventory || EventMemory.Overlaps(1408, 115))
        {
            return "CARDGAME requires canonical saved card bytes and acquisition bits with atomic inventory storage";
        }
        if (PendingTripleTriad is not null || HasPendingBattle || transitionRequested || services.IsMoviePlaying || partyMenu is not null || fiber.Initialization && NativeRuntimeProfile is { IsVerified: true })
        {
            return "CARDGAME requires its ordinary issuing field fiber without a competing scene owner";
        }
        services.CardInventory.Validate();
        NativeTripleTriadRequest request = PrepareTripleTriad(fiber);
        if (request.AvailableCards < 5)
        {
            return null;
        }
        if (ValidateTripleTriad is null)
        {
            return "CARDGAME requires an installed interactive Triple Triad host";
        }
        try
        {
            _ = NeedsNativeSalaryTransitionReset();
            return ValidateTripleTriad(request);
        }
        catch (NotSupportedException exception)
        {
            return exception.Message;
        }
    }

    private int[] ExecuteTripleTriad(Fiber fiber)
    {
        // Execute's caller has advanced the PC; retain the original issuing operation.
        NativeTripleTriadRequest request = PrepareTripleTriad(fiber) with { Operation = fiber.Position - 1 };
        if (request.AvailableCards >= 5)
        {
            ResetNativeSalaryForTransition(fiber);
        }
        int[] operands = Pop(fiber, 7);
        fiber.Results[0] = request.AvailableCards;
        fiber.Results[1] = 0;
        if (request.AvailableCards >= 5)
        {
            Scene.TripleTriadRequest = request;
            fiber.WaitReason = WaitReason.TripleTriad;
        }
        return operands;
    }

    private void ValidateTripleTriadCheckpoint(Checkpoint saved, IReadOnlyDictionary<long, Fiber> bySequence)
    {
        NativeTripleTriadRequest? request = saved.Scene.TripleTriadRequest;
        Fiber[] waiting = bySequence.Values.Where(fiber => fiber.WaitReason == WaitReason.TripleTriad).ToArray();
        if (request is null)
        {
            if (waiting.Length != 0)
            {
                throw new InvalidDataException("Saved CARDGAME wait has no owning native request.");
            }
            return;
        }
        if (request.Version != 1 || request.FieldId != saved.FieldId || request.ProgramHash != saved.ProgramHash || request.Frame < 0 || request.Frame > saved.Frame || request.AvailableCards < 5 || request.RawOperands.IsDefault || request.RawOperands.Length != 7 || waiting.Length != 1 || waiting[0].Sequence != request.FiberSequence || waiting[0].Complete || waiting[0].ObjectId != request.ObjectId || waiting[0].Script.Id != request.ScriptId || waiting[0].Position != request.Operation + 1 || (uint)request.Operation >= program!.Operations.Count || program.Operations[request.Operation].Opcode != Jsm.Opcode.CARDGAME || !waiting[0].Results.TryGetValue(0, out NativeScalarValue? count) || count is null || !count.IsExact || (int)count != request.AvailableCards || !waiting[0].Results.TryGetValue(1, out NativeScalarValue? result) || result is null || !result.IsExact || (int)result != 0 || saved.CardInventory is null || saved.Scene.NativeBattleWaits?.Count > 0 || saved.TransitionRequested || saved.PartyMenu is not null)
        {
            throw new InvalidDataException("Saved CARDGAME request disagrees with its exact issuing program, fiber, result bank or scene owner.");
        }
        ImmutableArray<int> raw = request.RawOperands;
        if (request.OpponentOwner != unchecked((byte)raw[0]) || request.NativeRuleMask != raw[1] || request.TradeRule != unchecked((byte)raw[2]) || request.RareChance != unchecked((byte)raw[3]) || request.Argument4 != unchecked((byte)raw[4]) || request.Argument5 != unchecked((byte)raw[5]) || request.CommonLevelMask != unchecked((byte)raw[6]) || request.AvailableCards != Enumerable.Range(0, NativeCardInventory.CardCount).Sum(card => Math.Max(0, saved.CardInventory.AvailableCount(card))))
        {
            throw new InvalidDataException("Saved CARDGAME operands or original available-card count have been changed.");
        }
    }
}
