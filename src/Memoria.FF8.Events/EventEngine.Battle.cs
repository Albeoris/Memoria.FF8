using Albeoris.Games.FF8.Jsm;

namespace Memoria.FF8.Events;

public enum NativeBattleBackend
{
    AutomaticVictory,
    Interactive
}

public sealed record NativeBattleCompletion(int Version, int FieldId, string ProgramHash, long Frame, int ObjectId, int ScriptId, int Operation, int RawEncounter, int RawFlags, ushort Encounter, ushort Flags, bool Suppressed, NativeBattleBackend? Backend, long? ReturnFrame, byte? Result, int EmulatedCombatCallbacks, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NativeBattleFlagConsumerReceipt? FlagConsumer = null);

public sealed partial class EventEngine
{
    public Func<ushort, ushort, bool>? UseInteractiveBattle { get; set; }
    public Func<ushort, ushort, string?>? ValidateInteractiveBattle { get; set; }
    public NativeBattleCompletion? PendingInteractiveBattle => Scene.NativeBattleCompletions?.LastOrDefault(request => request.Backend == NativeBattleBackend.Interactive && request.ReturnFrame is null && request.Result is null);

    public void ReturnFromInteractiveBattle(int combatCallbacks)
    {
        NativeBattleCompletion request = PendingInteractiveBattle ?? throw new InvalidOperationException("No interactive battle owns the suspended field VM.");
        if (combatCallbacks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(combatCallbacks));
        }
        int index = Scene.NativeBattleCompletions!.IndexOf(request);
        Scene.NativeBattleCompletions[index] = request with { Result = 0, EmulatedCombatCallbacks = combatCallbacks };
        Step();
    }
    private string? UnsupportedBattle(Fiber fiber, JsmOperation operation)
    {
        string? modeAccess = UnsupportedBattleMode(fiber, operation);
        if (modeAccess is not null)
        {
            return modeAccess;
        }
        if (operation.Opcode == Jsm.Opcode.BATTLERESULT)
        {
            return Scene.NativeBattleResult is null ? "BATTLERESULT has no reached native battle outcome provenance" : null;
        }
        if (operation.Opcode != Jsm.Opcode.BATTLE)
        {
            return null;
        }
        if (fiber.Stack.Count < 2)
        {
            return "BATTLE requires its original encounter and flags operands";
        }
        if (services.WorldControls?.ScriptBattleDisabled20 is null)
        {
            return "BATTLE requires proved event DWORD104 mask20; random BATTLEOFF and ordinary field loading do not establish it";
        }
        if (Scene.NativeBattleWaits?.Count > 0 || transitionRequested || services.IsMoviePlaying)
        {
            return "BATTLE with a competing pending scene request requires its original transition arbitration; PC and operands preserved";
        }
        if (!services.WorldControls.ScriptBattleDisabled20)
        {
            try
            {
                _ = NeedsNativeSalaryTransitionReset();
                int[] operands = fiber.Stack.Take(2).Reverse().ToArray();
                if (UseInteractiveBattle?.Invoke(unchecked((ushort)operands[0]), unchecked((ushort)operands[1])) == true)
                {
                    return ValidateInteractiveBattle?.Invoke(unchecked((ushort)operands[0]), unchecked((ushort)operands[1]));
                }
            }
            catch (NotSupportedException exception)
            {
                return exception.Message;
            }
        }
        return null;
    }

    private int[] ExecuteMinimalBattle(Fiber fiber)
    {
        int[] operands = fiber.Stack.Take(2).Reverse().ToArray();
        bool suppressed = services.WorldControls!.ScriptBattleDisabled20;
        if (!suppressed)
        {
            // Accepted native battle-scene entry5225A0 resets a live salary UI;523270's suppressed branch does not enter it.
            ResetNativeSalaryForTransition(fiber);
        }
        if (!suppressed && services.ExamTimer is Memoria.FF8.Runtime.NativeExamTimerState timer)
        {
            try
            {
                services.SetExamTimer(timer with { SaveGlobalMask4 = (unchecked((ushort)operands[1]) & 4) != 0 });
            }
            catch (NotSupportedException exception)
            {
                fiber.Position--;
                string message = "Native BATTLE timer service rejected its active flags WORD write; PC and operands preserved.";
                LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, Jsm.Opcode.BATTLE.ToString(), message);
                throw new EventScriptException(message, exception);
            }
        }
        bool interactive = !suppressed && UseInteractiveBattle?.Invoke(unchecked((ushort)operands[0]), unchecked((ushort)operands[1])) == true;
        operands = Pop(fiber, 2);
        // TODO(TD-009): replace the authorized zero-duration automatic victory backend with native combat
        NativeBattleCompletion completion = new(1, FieldId, programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, operands[0], operands[1], unchecked((ushort)operands[0]), unchecked((ushort)operands[1]), suppressed, suppressed ? null : interactive ? NativeBattleBackend.Interactive : NativeBattleBackend.AutomaticVictory, null, null, 0);
        if (!suppressed)
        {
            ushort? persistentMode = Scene.NativeBattleFlagWrites is { Count: > 0 } history ? history[^1].PersistentFlags : null;
            completion = completion with { FlagConsumer = new NativeBattleFlagConsumerReceipt(completion.Flags, persistentMode, true, false) };
            if (persistentMode is ushort knownMode)
            {
                AppendBattleFlagWrite(NativeBattleFlagWriter.Battle, fiber, operands[1], knownMode, completion.Flags);
            }
        }
        Scene.NativeBattleWaits ??= [];
        Scene.NativeBattleCompletions ??= [];
        Scene.NativeBattleWaits.Add(fiber.Sequence, Scene.NativeBattleCompletions.Count);
        Scene.NativeBattleCompletions.Add(completion);
        // Original523270 returns3, consuming the operands and ending this entity's budget.
        // The minimal backend returns to the same field VM on its next ordinary callback.
        fiber.WaitUntil = Frame + 1;
        fiber.WaitReason = WaitReason.MinimalBattle;
        return operands;
    }

    private void CompleteNativeBattleReturnBeforeVm()
    {
        if (Scene.NativeBattleWaits is null)
        {
            return;
        }
        foreach (int index in Scene.NativeBattleWaits.Values)
        {
            NativeBattleCompletion request = Scene.NativeBattleCompletions![index];
            if (request.ReturnFrame is not null || Frame <= request.Frame)
            {
                continue;
            }
            byte? result = request.Suppressed ? null : request.Backend == NativeBattleBackend.Interactive ? request.Result : (byte)0;
            if (request.Backend == NativeBattleBackend.Interactive && result is null)
            {
                continue;
            }
            Scene.NativeBattleCompletions[index] = request with { ReturnFrame = Frame, Result = result };
            if (result is byte outcome)
            {
                Scene.NativeBattleResult = outcome;
            }
        }
    }

    private void ValidateNativeBattleCheckpoint(Checkpoint saved, IReadOnlyDictionary<long, Fiber> bySequence)
    {
        ValidateNativeBattleModeCheckpoint(saved);
        if (saved.Scene.NativeBattleCompletions is null)
        {
            if (saved.Scene.NativeBattleWaits is not null || saved.Scene.NativeBattleResult is not null || bySequence.Values.Any(fiber => fiber.WaitReason == WaitReason.MinimalBattle))
            {
                throw new InvalidDataException("Unknown battle history cannot contain a typed wait or result.");
            }
            return;
        }
        if (saved.Scene.NativeBattleWaits is null)
        {
            throw new InvalidDataException("Saved battle history requires its preserved typed wait collection.");
        }
        foreach (NativeBattleCompletion completion in saved.Scene.NativeBattleCompletions)
        {
            if (completion is null || completion.Version != 1 || completion.FieldId is < 0 or > ushort.MaxValue || completion.ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || completion.Frame <= 0 || completion.ObjectId < 0 || completion.ScriptId < 0 || completion.Operation < 0 || completion.Encounter != unchecked((ushort)completion.RawEncounter) || completion.Flags != unchecked((ushort)completion.RawFlags) || completion.EmulatedCombatCallbacks < 0 || completion.Backend != NativeBattleBackend.Interactive && completion.EmulatedCombatCallbacks != 0 || completion.ReturnFrame is long returned && returned <= completion.Frame)
            {
                throw new InvalidDataException("Saved minimal battle request changed its original source, WORD operands or declared zero combat callback policy.");
            }
            if (completion.Suppressed ? completion.FlagConsumer is not null : completion.FlagConsumer is not { ReplacedActiveFlags: true, NativeEncounterFlagMergeEmulated: false } consumer || consumer.RequestedFlags != completion.Flags)
            {
                throw new InvalidDataException("Saved native battle flag consumer changed its requested WORD or deferred encounter merge contract.");
            }
            if (completion.FlagConsumer?.PersistentModeFlags is ushort persistentMode && (saved.Scene.NativeBattleFlagWrites?.Count(write => write.Writer == NativeBattleFlagWriter.Battle && write.FieldId == completion.FieldId && write.ProgramHash == completion.ProgramHash && write.Frame == completion.Frame && write.ObjectId == completion.ObjectId && write.ScriptId == completion.ScriptId && write.Operation == completion.Operation && write.RawOperand == completion.RawFlags && write.ActiveFlags == completion.Flags && write.PersistentFlags == persistentMode) ?? 0) != 1)
            {
                throw new InvalidDataException("Saved battle flag consumer has no matching reached persistent-mode writer history.");
            }
            if (completion.Suppressed ? completion.Backend is not null || completion.Result is not null : completion.Backend is not (NativeBattleBackend.AutomaticVictory or NativeBattleBackend.Interactive) || (completion.ReturnFrame is null ? completion.Result is not null : completion.Result != 0))
            {
                throw new InvalidDataException("Saved minimal battle request has an invalid suppression, backend or native result byte.");
            }
        }
        byte? lastResult = saved.Scene.NativeBattleCompletions.LastOrDefault(completion => !completion.Suppressed && completion.ReturnFrame is not null)?.Result;
        if (saved.Scene.NativeBattleResult != lastResult)
        {
            throw new InvalidDataException("Saved BATTLERESULT byte lacks its completed reached native battle request.");
        }
        foreach ((long sequence, int index) in saved.Scene.NativeBattleWaits)
        {
            if ((uint)index >= saved.Scene.NativeBattleCompletions.Count || !bySequence.TryGetValue(sequence, out Fiber? owner) || owner.Complete || owner.WaitReason != WaitReason.MinimalBattle)
            {
                throw new InvalidDataException("Saved minimal battle yield has no reached request or original live owner.");
            }
            NativeBattleCompletion completion = saved.Scene.NativeBattleCompletions[index];
            if (completion.FieldId != saved.FieldId || completion.ProgramHash != saved.ProgramHash || completion.Frame > saved.Frame || completion.ReturnFrame is long returned && returned > saved.Frame || owner.ObjectId != completion.ObjectId || owner.Script.Id != completion.ScriptId || owner.Position != completion.Operation + 1 || owner.WaitUntil != completion.Frame + 1 || (uint)completion.Operation >= program!.Operations.Count || program.Operations[completion.Operation].Opcode != Jsm.Opcode.BATTLE || saved.Scene.NativeBattleWaits.Values.Count(value => value == index) != 1)
            {
                throw new InvalidDataException("Saved minimal battle yield changed its issuing operation, advanced PC or next-update return.");
            }
        }
        if (bySequence.Values.Any(fiber => fiber.WaitReason == WaitReason.MinimalBattle && !saved.Scene.NativeBattleWaits.ContainsKey(fiber.Sequence)) || saved.Scene.NativeBattleCompletions.Select((completion, index) => (completion, index)).Any(item => item.completion.ReturnFrame is null && !saved.Scene.NativeBattleWaits.ContainsValue(item.index)))
        {
            throw new InvalidDataException("Saved pending native battle has no preserved typed wait relationship.");
        }
    }
}
