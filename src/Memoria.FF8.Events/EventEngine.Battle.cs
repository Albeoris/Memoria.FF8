using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public enum NativeBattleBackend
{
    AutomaticVictory,
    Interactive
}

public sealed record NativeBattleCompletion(int Version, int FieldId, string ProgramHash, long Frame, int ObjectId, int ScriptId, int Operation, int RawEncounter, int RawFlags, ushort Encounter, ushort Flags, bool Suppressed, NativeBattleBackend? Backend, long? ReturnFrame, byte? Result, int EmulatedCombatCallbacks, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] NativeBattleFlagConsumerReceipt? FlagConsumer = null, [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] long? BattleClockCallsAtEntry = null);

public sealed partial class EventEngine
{
    public Func<bool>? HasPendingRandomBattle { get; set; }
    public Action<bool>? NativePlayerMovementProcessed { get; set; }
    public bool HostAutomaticBattles { get; set; }
    public bool HasPendingBattle => PendingHostedBattle is not null || HasPendingRandomBattle?.Invoke() == true;
    public void AdvanceRandomBattleCallbacks(int callbacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callbacks);
        if (HasPendingRandomBattle?.Invoke() != true || Scene.FieldCallbackClock is not { } clock)
        {
            throw new InvalidOperationException("Random combat callbacks require their suspended field owner.");
        }
        NativeRandomFieldBattleOwner owner = Scene.NativeRandomFieldBattle is { Result: null } random ? random : throw new InvalidOperationException("Random callbacks require their executed native entry owner.");
        services.SetExamTimer(services.ExamTimer!.AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks));
        services.SetWorldProcess(services.WorldProcess!.AdvanceMainLoopCallbacks(callbacks));
        Scene.FieldCallbackClock = clock with { RandomBattleCallbacks = checked(clock.RandomBattleCallbacks + callbacks) };
        Scene.NativeRandomFieldBattle = owner with { Callbacks = checked(owner.Callbacks + callbacks) };
    }
    public Func<ushort, ushort, bool>? UseInteractiveBattle { get; set; }
    public Func<ushort, ushort, string?>? ValidateInteractiveBattle { get; set; }
    public NativeBattleCompletion? PendingInteractiveBattle => Scene.NativeBattleCompletions?.LastOrDefault(request => request.Backend == NativeBattleBackend.Interactive && request.ReturnFrame is null && request.Result is null);
    public NativeBattleCompletion? PendingHostedBattle => Scene.NativeBattleCompletions?.LastOrDefault(request => (request.Backend == NativeBattleBackend.Interactive || HostAutomaticBattles && request.Backend == NativeBattleBackend.AutomaticVictory) && request.ReturnFrame is null && request.Result is null);

    public void AdvanceInteractiveBattleCallbacks(int callbacks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(callbacks);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(callbacks, 30000);
        NativeFieldCallbackClock clock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Battle callbacks require the actual suspended field clock owner.");
        if (PendingInteractiveBattle is null || clock.BattleCallbacks is null)
        {
            throw new InvalidOperationException("Battle callbacks require an executed interactive BATTLE entry.");
        }
        NativeExamTimerState timer = (services.ExamTimer ?? throw new NotSupportedException("Battle callbacks require the retained native process timer.")).AdvanceCallbacks(NativeTimerCallbackKind.Battle, callbacks);
        NativeWorldProcess process = (services.WorldProcess ?? throw new NotSupportedException("Battle callbacks require the retained outer main-loop process.")).AdvanceMainLoopCallbacks(callbacks);
        NativeFieldCallbackClock next = clock with { BattleCallbacks = checked(clock.BattleCallbacks.Value + callbacks) };
        services.SetExamTimer(timer);
        services.SetWorldProcess(process);
        Scene.FieldCallbackClock = next;
    }

    public void ReturnFromInteractiveBattle(int combatCallbacks, Action? publishCombatOutcome = null, byte nativeResult = 4)
    {
        if (PendingInteractiveBattle is null)
        {
            throw new InvalidOperationException("No interactive battle owns the suspended field VM.");
        }
        ReturnFromHostedBattle(combatCallbacks, publishCombatOutcome, nativeResult);
    }

    public void ReturnFromHostedBattle(int combatCallbacks, Action? publishCombatOutcome = null, byte nativeResult = 4)
    {
        NativeBattleCompletion request = PendingHostedBattle ?? throw new InvalidOperationException("No hosted battle owns the suspended field VM.");
        if (combatCallbacks < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(combatCallbacks));
        }
        if (request.Backend == NativeBattleBackend.Interactive)
        {
            if (nativeResult is < 1 or > 5)
            {
                throw new ArgumentOutOfRangeException(nameof(nativeResult), "Completed native battle results use BYTE1..5; zero is the battle bootstrap state.");
            }
            long allocationClock = Scene.NativeFieldInitialization?.TimerClockCallsAtAllocation ?? throw new NotSupportedException("Battle return requires its actual allocation clock.");
            long completedCallbacks = Scene.NativeBattleCompletions!.Where(completion => completion.Backend == NativeBattleBackend.Interactive && completion.ReturnFrame is not null && completion.BattleClockCallsAtEntry >= allocationClock).Sum(completion => (long)completion.EmulatedCombatCallbacks);
            if (Scene.FieldCallbackClock?.BattleCallbacks != completedCallbacks + combatCallbacks)
            {
                throw new InvalidOperationException("Battle return duration disagrees with its dispatched native clock callbacks.");
            }
        }
        else if (request.Version != 2 || combatCallbacks != 0 || nativeResult != 4)
        {
            throw new InvalidOperationException("Current automatic research victory requires completion version2, native result4 and zero simulated combat callbacks; pending legacy requests require explicit migration.");
        }
        publishCombatOutcome?.Invoke();
        int index = Scene.NativeBattleCompletions!.IndexOf(request);
        Scene.NativeBattleCompletions[index] = request with { Result = nativeResult, EmulatedCombatCallbacks = combatCallbacks };
        StepCore();
        OperationCompleted?.Invoke();
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
                ValidateNativeFieldRequestPacketStorage();
                _ = NeedsNativeSalaryTransitionReset();
                int[] operands = fiber.Stack.Take(2).Reverse().ToArray();
                if (UseInteractiveBattle?.Invoke(unchecked((ushort)operands[0]), unchecked((ushort)operands[1])) == true)
                {
                    if (services.ExamTimer is null || services.WorldProcess is null || Scene.NativeFieldInitialization?.TimerClockCallsAtAllocation is null)
                    {
                        return "Interactive BATTLE requires its actual retained timer/main-loop owners and allocation clock provenance";
                    }
                    return ValidateInteractiveBattle?.Invoke(unchecked((ushort)operands[0]), unchecked((ushort)operands[1]));
                }
            }
            catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
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
        if (!suppressed)
        {
            try
            {
                NativeExamTimerState? timer = services.ExamTimer is NativeExamTimerState currentTimer ? currentTimer with { SaveGlobalMask4 = (unchecked((ushort)operands[1]) & 4) != 0 } : null;
                if (services.CanStoreFieldRequestPacket)
                {
                    NativeFieldRequestPacket packet = (services.FieldRequestPacket ?? new NativeFieldRequestPacket()).WithBattleRequest(unchecked((ushort)operands[0]));
                    services.SetNativeBattleEntryOwners(timer, packet);
                }
                else if (timer is not null)
                {
                    services.SetExamTimer(timer);
                }
            }
            catch (NotSupportedException exception)
            {
                fiber.Position--;
                string message = "Native BATTLE service rejected its flags or request-packet update; PC and operands preserved.";
                LastFault = new EventFault(FieldId, fiber.ObjectId, fiber.Script.Id, fiber.Position, Jsm.Opcode.BATTLE.ToString(), message);
                throw new EventScriptException(message, exception);
            }
        }
        bool interactive = !suppressed && UseInteractiveBattle?.Invoke(unchecked((ushort)operands[0]), unchecked((ushort)operands[1])) == true;
        if (interactive)
        {
            NativeFieldCallbackClock clock = Scene.FieldCallbackClock ?? throw new NotSupportedException("Interactive BATTLE requires its current field callback clock.");
            // The executed entry produces a battle counter; automatic research battles declare zero combat callbacks under TD-009.
            Scene.FieldCallbackClock = clock with { BattleCallbacks = clock.BattleCallbacks ?? 0 };
        }
        operands = Pop(fiber, 2);
        // TODO(TD-009): replace the authorized zero-duration automatic victory backend with native combat.
        NativeBattleCompletion completion = new(!suppressed && !interactive ? 2 : 1, FieldId, programHash, Frame, fiber.ObjectId, fiber.Script.Id, fiber.Position - 1, operands[0], operands[1], unchecked((ushort)operands[0]), unchecked((ushort)operands[1]), suppressed, suppressed ? null : interactive ? NativeBattleBackend.Interactive : NativeBattleBackend.AutomaticVictory, null, null, 0);
        if (interactive)
        {
            completion = completion with { BattleClockCallsAtEntry = services.ExamTimer!.ClockCalls };
        }
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

    private void PublishNativeBattleRequestPacketAfterVm()
    {
        NativeBattleCompletion? request = Scene.NativeBattleCompletions?.LastOrDefault(item => !item.Suppressed && item.Frame == Frame && item.ReturnFrame is null);
        if (request is null || !services.CanStoreFieldRequestPacket || services.FieldRequestPacket is not NativeFieldRequestPacket packet)
        {
            return;
        }
        if (packet.Kind is null)
        {
            // Unknown arbitration cannot retain coordinates that a possible battle entry replaces.
            services.SetFieldRequestPacket(packet.WithBattleRequest(request.Encounter, null, null, null));
            return;
        }
        if (packet.Kind is not (3 or 8))
        {
            return;
        }
        short? x = null;
        short? y = null;
        ushort? triangle = null;
        if (PlayerActor is ActorState leader)
        {
            if (ReadNativeScriptPosition(leader) is FieldFixedPosition position)
            {
                //4767B0 uses signed division toward zero;470250's saved pose uses SAR12.
                x = unchecked((short)(position.X / 4096));
                y = unchecked((short)(position.Y / 4096));
            }
            if (leader.WalkmeshTriangle is >= 0 and <= ushort.MaxValue)
            {
                triangle = (ushort)leader.WalkmeshTriangle;
            }
        }
        services.SetFieldRequestPacket(packet.WithBattleRequest(request.Encounter, x, y, triangle));
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
            bool hosted = request.Backend == NativeBattleBackend.Interactive || HostAutomaticBattles && request.Backend == NativeBattleBackend.AutomaticVictory;
            if (request.Backend == NativeBattleBackend.AutomaticVictory && request.Version != 2)
            {
                throw new InvalidDataException("Pending legacy automatic battle requires explicit migration; completed historical result0 facts remain unchanged.");
            }
            byte? result = request.Suppressed ? null : hosted ? request.Result : (byte)4;
            if (hosted && result is null)
            {
                continue;
            }
            if (!request.Suppressed)
            {
                ConsumeNativeFieldRequestPacket();
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
        ValidateNativeRandomFieldBattleCheckpoint(saved);
        saved.Scene.NativeBattleFieldReload?.Validate();
        saved.Scene.NativeBattleFieldReloadState?.Validate();
        if (saved.Scene.NativeBattleFieldReloadState is { } pending && (saved.Scene.NativeBattleFieldReload is not null || pending.FieldId != saved.FieldId || pending.ProgramHash != saved.ProgramHash || CountPendingNativeFieldOwners(saved.Scene, pending.BattleEntryFrame) != 1))
        {
            throw new InvalidDataException("Pending kind3 cache reset lost its actual suspended battle or resource continuation.");
        }
        saved.Scene.NativeBattleFieldReturn?.Reload.Validate();
        saved.Scene.NativeBattleFieldReturn?.Music.Validate();
        if (saved.Scene.NativeBattleFieldReload is { } reload && (reload.FieldId != saved.FieldId || reload.ProgramHash != saved.ProgramHash || CountPendingNativeFieldOwners(saved.Scene, reload.BattleEntryFrame) != 1))
        {
            throw new InvalidDataException("Native kind3 reload completion lost its actual suspended field/battle source.");
        }
        ValidateNativeBattleModeCheckpoint(saved);
        if (saved.Scene.NativeBattleCompletions is null)
        {
            if (saved.Scene.NativeBattleWaits is not null || saved.Scene.NativeBattleResult is not null && saved.Scene.NativeRandomFieldBattle?.Result != saved.Scene.NativeBattleResult || bySequence.Values.Any(fiber => fiber.WaitReason == WaitReason.MinimalBattle))
            {
                throw new InvalidDataException("Unknown battle history cannot contain a typed wait or result.");
            }
            return;
        }
        if (saved.Scene.NativeBattleCompletions.Any(completion => completion is null))
        {
            throw new InvalidDataException("Saved battle history contains a missing completion.");
        }
        NativeBattleCompletion[] currentBattles = saved.Scene.NativeFieldInitialization?.TimerClockCallsAtAllocation is long allocationClock ? saved.Scene.NativeBattleCompletions.Where(completion => completion.Backend == NativeBattleBackend.Interactive && completion.BattleClockCallsAtEntry >= allocationClock).ToArray() : [];
        long completedBattleCallbacks = currentBattles.Where(completion => completion.ReturnFrame is not null).Sum(completion => (long)completion.EmulatedCombatCallbacks);
        bool pendingInteractive = currentBattles.Any(completion => completion.ReturnFrame is null);
        if (currentBattles.Length > 0 && (saved.Scene.FieldCallbackClock?.BattleCallbacks is not long callbacks || callbacks < completedBattleCallbacks || !pendingInteractive && callbacks != completedBattleCallbacks) || currentBattles.Length == 0 && (saved.Scene.FieldCallbackClock?.BattleCallbacks is not null || saved.Scene.NativeBattleCompletions.Any(completion => completion.Backend == NativeBattleBackend.Interactive && completion.ReturnFrame is null)))
        {
            throw new InvalidDataException("Interactive battle history lacks its executed native callback clock provenance.");
        }
        if (saved.Scene.NativeBattleWaits is null)
        {
            throw new InvalidDataException("Saved battle history requires its preserved typed wait collection.");
        }
        foreach (NativeBattleCompletion completion in saved.Scene.NativeBattleCompletions)
        {
            if (completion is null || completion.Version is not (1 or 2) || completion.Version == 2 && (completion.Backend != NativeBattleBackend.AutomaticVictory || completion.Suppressed) || completion.FieldId is < 0 or > ushort.MaxValue || completion.ProgramHash is not { Length: 64 } hash || !hash.All(Uri.IsHexDigit) || completion.Frame <= 0 || completion.ObjectId < 0 || completion.ScriptId < 0 || completion.Operation < 0 || completion.Encounter != unchecked((ushort)completion.RawEncounter) || completion.Flags != unchecked((ushort)completion.RawFlags) || completion.EmulatedCombatCallbacks < 0 || completion.Backend != NativeBattleBackend.Interactive && completion.EmulatedCombatCallbacks != 0 || completion.ReturnFrame is long returned && returned <= completion.Frame)
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
            if (completion.Backend == NativeBattleBackend.AutomaticVictory && completion.Version == 1 && completion.ReturnFrame is null)
            {
                throw new InvalidDataException("Pending legacy automatic battle requires explicit migration; completed historical result0 facts remain unchanged.");
            }
            if (completion.Suppressed ? completion.Backend is not null || completion.Result is not null : completion.Backend is not (NativeBattleBackend.AutomaticVictory or NativeBattleBackend.Interactive) || (completion.ReturnFrame is null ? completion.Result is not null : completion.Backend == NativeBattleBackend.AutomaticVictory ? completion.Result != (completion.Version == 1 ? 0 : 4) : completion.Result is null or < 1 or > 5))
            {
                throw new InvalidDataException("Saved minimal battle request has an invalid suppression, backend or native result byte.");
            }
            if (completion.Backend == NativeBattleBackend.Interactive ? completion.BattleClockCallsAtEntry is not long entryClock || entryClock < 0 || saved.ExamTimer is null || entryClock > saved.ExamTimer.ClockCalls : completion.BattleClockCallsAtEntry is not null)
            {
                throw new InvalidDataException("Saved battle callback clock requires its executed interactive entry provenance.");
            }
        }
        NativeBattleCompletion? lastScripted = saved.Scene.NativeBattleCompletions.LastOrDefault(completion => !completion.Suppressed && completion.ReturnFrame is not null);
        byte? lastResult = lastScripted?.Result;
        if (saved.Scene.NativeRandomFieldBattle is { Result: not null } randomResult)
        {
            if (lastScripted is null)
            {
                lastResult = randomResult.Result;
            }
            else if (randomResult.BattleClockCallsAtEntry is long randomEntry && lastScripted.BattleClockCallsAtEntry is long scriptedEntry)
            {
                if (randomEntry == scriptedEntry)
                {
                    throw new InvalidDataException("Completed random and scripted battles cannot share one executed entry clock.");
                }
                lastResult = randomEntry > scriptedEntry ? randomResult.Result : lastScripted.Result;
            }
            else
            {
                throw new NotSupportedException("Mixed random/scripted result history requires explicit actual battle-entry clock provenance.");
            }
        }
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
