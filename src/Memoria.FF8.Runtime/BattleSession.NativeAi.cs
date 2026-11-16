namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    // Native47CCB0 invokes486D90 before initial AI; it clears exactly32bytes at1D28D98.
    private readonly uint[] nativeAiVariables = new uint[8];
    private readonly List<BattleStock> scriptedRewards = [];
    public IReadOnlyList<BattleStock> ScriptedRewards => scriptedRewards.AsReadOnly();
    private int nativeAiDepth;
    private uint[]? persistentAiVariables;
    private BattlePreviousTarget? nativeLastAllocatedTarget;

    private void RecordLastNativeAllocation(BattleCombatant actor)
    {
        nativeLastAllocatedTarget = actor.NativeActorIndex is int original
            ? new NativeBattleTargetRequestAdapter.OriginalPreviousOperand(original)
            : new BattlePreviousTarget.Actor(TargetActorReference(actor));
    }

    private BattleCombatant ResolveLastNativeAllocation()
    {
        BattleActorReference reference = nativeLastAllocatedTarget switch
        {
            NativeBattleTargetRequestAdapter.OriginalPreviousOperand original => NativeBattleTargetRequestAdapter.ResolvePrevious(original, ReadOriginalTargetOccupant),
            BattlePreviousTarget.Actor captured => captured.Reference,
            _ => throw new NotSupportedException("The AI has not allocated a participant.")
        };
        return FindTargetActor(reference) ?? throw new NotSupportedException("The last allocated participant is no longer present.");
    }
    public IReadOnlyList<uint>? PersistentAiVariables => persistentAiVariables?.ToArray();

    private void InitializeNativeParticipants()
    {
        if (NativeGraphics is not null)
        {
            return;
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsActive && !actor.Suspended && actor.NativeAi is not null).ToArray())
        {
            if (UsesExpandedRoster)
            {
                QueueExpandedAiCallback(actor, 0);
            }
            else
            {
                QueueNativeParticipantInitialization(actor);
            }
        }
    }

    private void ActivateNativeParticipant(BattleCombatant actor)
    {
        StopTurnAdvance();
        // Native487670 retains the allocated statistics/resources, clears2/40/80 and queues init.
        actor.IsActive = true;
        actor.Hidden = false;
        actor.Untargetable = false;
        actor.Suspended = false;
        actor.AiDeathHandled = false;
        Log("Activated", actor.Id, null, 0, $"{actor.Name} enters battle.");
        if (!UsesExpandedRoster)
        {
            QueueNativeParticipantInitialization(actor);
        }
        else if (actor.NativeAi is not null)
        {
            QueueExpandedAiCallback(actor, 0);
        }
    }

    private void InterpretNativeAi(BattleCombatant actor, int callback)
    {
        if (TryRunEncounterPolicy(actor, callback))
        {
            return;
        }
        NativeBattleAiDefinition? program = actor.NativeAi;
        if (program is null)
        {
            return;
        }
        //4877F0 admits enemy stage2 using saved lowBYTE25, battle lowBYTE9 and actor flag4000.
        if (!actor.IsActive || callback == 2 && ((NativeSavedStatusBits(actor) & 0x25) != 0 || (NativeBattleStatusBits(actor) & 9) != 0 || (actor.NativeCombatantFlags & 0x4000) != 0))
        {
            ObserveDiagnosticCounterAi(actor, callback, BattleDiagnosticCounterCallbackPhase.AiRejected);
            return;
        }
        if (TryRunNativeEnemyBerserk(actor, callback))
        {
            ObserveDiagnosticCounterAi(actor, callback, BattleDiagnosticCounterCallbackPhase.AiRejected);
            return;
        }
        if (++nativeAiDepth > 16)
        {
            nativeAiDepth--;
            throw new NotSupportedException("Native battle AI activation exceeds the bounded callback stack.");
        }
        var previousDiagnosticAiScope = diagnosticCounterAiScope;
        int operation = 0;
        try
        {
            if (ObservingDiagnosticCounter)
            {
                diagnosticCounterAiScope = (++diagnosticCounterAiSerial, callback, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(program.Scripts[callback])));
            }
            if (diagnosticCounterAiScope is { } diagnosticScope)
            {
                ObserveDiagnosticCounterAi(actor, callback, BattleDiagnosticCounterCallbackPhase.AiEntered, diagnosticScope.Serial, diagnosticScope.Sha);
            }
            byte[] code = program.Scripts[callback];
            int pc = 0;
            AiTargetSelection selected = EmptyAiTargets;
            byte preparedCommand = 0;
            ushort preparedId = 0;
            byte sequenceOverride = 0;
            int? hostedOperationsAtStart = producingExpandedAiCallback ? expandedOperationCursor.Operations.Length : null;
            for (int dispatched = 0; dispatched < 4096; dispatched++)
            {
                operation = pc;
                if (!NativeBattleAiInstruction.TryDecode(code, pc, out NativeBattleAiInstruction instruction, out string? decodeError))
                {
                    throw new InvalidDataException($"Malformed native battle AI instruction ({decodeError}).");
                }
                pc = instruction.NextOffset;
                int opcode = instruction.Opcode;
                switch (opcode)
                {
                    case 0:
                        return;
                    case 1:
                    case 24:
                    case 26:
                        int text = instruction[0];
                        QueueNativeText(actor, program, text, opcode);
                        break;
                    case 2:
                        NativeBattleAiCondition condition = instruction.Condition;
                        bool passed = EvaluateNativeAiCondition(actor, condition.Subject, condition.Selector, condition.Comparison, condition.Value);
                        int falsePc = instruction.BranchOffset;
                        if (callback == 1 && Difficulty >= BattleDifficulty.Hard && condition.Subject == 2)
                        {
                            // Only side-effect-free random exits are omitted; state guards and reaction callbacks retain their authored control flow.
                            bool trueReturns = NativeAiPathOnlyReturns(code, pc);
                            bool falseReturns = NativeAiPathOnlyReturns(code, falsePc);
                            if (trueReturns != falseReturns)
                            {
                                passed = !trueReturns;
                            }
                        }
                        pc = passed ? pc : falsePc;
                        break;
                    case 3:
                        preparedCommand = 2;
                        preparedId = instruction[0];
                        break;
                    case 4:
                        byte selectorTarget = instruction[0];
                        selected = ResolveAiTargets(actor, selectorTarget);
                        break;
                    case 5:
                        sequenceOverride = instruction[0];
                        break;
                    case 6:
                        if (!QueuePreparedNativeAiCommand(actor, preparedCommand, preparedId, sequenceOverride, selected))
                        {
                            return;
                        }
                        break;
                    case 7:
                        preparedCommand = 8;
                        preparedId = instruction[0];
                        break;
                    case 8:
                        if (producingExpandedAiCallback)
                        {
                            StageExpandedHostedModelControl(106, actor);
                            actor.NativeStatus0 |= 1;
                            actor.NativeCombatantFlags &= ~0xCu;
                            actor.Atb = 0;
                        }
                        else if (UsesExpandedRoster)
                        {
                            CompleteExpandedEnemyDeath(actor);
                        }
                        else
                        {
                            QueueNativeGraphicsDeath(actor);
                        }
                        break;
                    case 9:
                        int damagePresentation = instruction[0];
                        SetNativeContactSequence((byte)damagePresentation);
                        Log("NativeDamagePresentation", actor.Id, null, damagePresentation, $"Native damage presentation {damagePresentation}.");
                        break;
                    case 11:
                        int first = instruction[0];
                        int second = instruction[1];
                        int third = instruction[2];
                        int chosen = (NextRandomByte() % 3) switch { 0 => first, 1 => second, _ => third };
                        if (chosen != 253)
                        {
                            PrepareNativeAiAbility(actor, chosen, ref preparedCommand, ref preparedId, ref sequenceOverride);
                            if (!QueuePreparedNativeAiCommand(actor, preparedCommand, preparedId, sequenceOverride, selected))
                            {
                                return;
                            }
                        }
                        break;
                    case 12:
                        PrepareNativeAiAbility(actor, instruction[0], ref preparedCommand, ref preparedId, ref sequenceOverride);
                        if (!QueuePreparedNativeAiCommand(actor, preparedCommand, preparedId, sequenceOverride, selected))
                        {
                            return;
                        }
                        break;
                    case 14:
                    case 15:
                    case 18:
                    case 19:
                    case 17:
                    case 21:
                        int variable = instruction[0];
                        uint operand = instruction[1];
                        if (opcode is 14 or 18)
                        {
                            if (operand == 203)
                            {
                                if (UsesExpandedRoster)
                                {
                                    StoreExpandedAiLastAttacker(actor, variable, opcode == 18);
                                    break;
                                }
                                operand = checked((uint)NativeConditionLastAttackerIndex(actor));
                            }
                            WriteNativeActorVariable(actor, variable, operand, opcode == 18);
                            break;
                        }
                        int variableIndex = opcode is 17 or 21 ? variable - 80 : variable - 96;
                        if ((uint)variableIndex >= 8)
                        {
                            throw new NotSupportedException("Native battle AI variable lies outside its proved eight-DWORD bank.");
                        }
                        if (opcode is 15 or 19)
                        {
                            uint previous = opcode == 19 ? nativeAiVariables[variableIndex] : 0;
                            nativeAiVariables[variableIndex] = unchecked(previous + operand);
                        }
                        else
                        {
                            uint[] bank = persistentAiVariables ?? throw new NotSupportedException("Native battle AI requires its executed persistent DWORD bank80..87.");
                            bank[variableIndex] = opcode == 21 ? unchecked(bank[variableIndex] + operand) : operand;
                        }
                        break;
                    case 22:
                        // Native22 assigns the current HP DWORD from the allocated maximum, including death AI.
                        actor.CurrentHp = actor.MaximumHp;
                        actor.AiDeathHandled = false;
                        break;
                    case 23:
                        bool sceneResultFlag = instruction[0] == 1;
                        nativeScriptState = nativeScriptState with { ResultFlags = (byte)((nativeScriptState.ResultFlags & ~1) | (sceneResultFlag ? 1 : 0)), EscapeDisabled = sceneResultFlag };
                        break;
                    case 13:
                    case 25:
                        // Native487DF0 deliberately consumes one operand and performs no operation.
                        break;
                    case 27:
                        int entranceIndex = instruction[0] + NativeBattleSlots.AllyCount;
                        byte entranceSequence = instruction[1];
                        QueueNativeEnemyEntrance(actor, FindNativeActor(entranceIndex), entranceSequence);
                        break;
                    case 29:
                        int removal = instruction[0];
                        if (producingExpandedAiCallback)
                        {
                            StageExpandedHostedModelControl(105, removal == 200 ? actor : FindNativeActor(removal + NativeBattleSlots.AllyCount));
                            actor.Atb = 0;
                            break;
                        }
                        if (HasExpandedAiDescriptors)
                        {
                            QueueExpandedAiDescriptor(actor, new(BattleExpandedAiDescriptorKind.Remove, removal));
                            actor.Atb = 0;
                            break;
                        }
                        BattleCombatant removed = removal == 200 ? actor : FindNativeActor(removal + 3);
                        if (NativeGraphics is not null)
                        {
                            QueueNativeGraphicsRemoval(removed);
                        }
                        else if (UsesExpandedRoster && HasExpandedAiAction(removed))
                        {
                            removed.ExpandedAiRemovalPending = true;
                        }
                        else
                        {
                            ReleaseNativeParticipant(removed);
                        }
                        //487DF0 calls493B60 for the executing actor, not the removed target.
                        actor.Atb = 0;
                        actor.IsBusy = actor.ExpandedAiRemovalPending;
                        break;
                    case 30:
                        byte effect = instruction[0];
                        if (UsesExpandedRoster)
                        {
                            QueueExpandedGraphicCommand(actor, effect, sequenceOverride, selected);
                        }
                        else
                        {
                            QueueNativeGraphicCommand(actor, effect, sequenceOverride, selected);
                        }
                        break;
                    case 28:
                        QueueNativeWait(30 * instruction[0]);
                        break;
                    case 32:
                        QueueNativeWait(15 * instruction[0]);
                        break;
                    case 31:
                    case 52:
                        int reserve = instruction[0];
                        QueueAiEnemyAllocation(actor, reserve, 0, opcode == 31);
                        break;
                    case 59:
                        int replacementScene = instruction[0];
                        int replacementIndex = instruction[1];
                        QueueAiEnemyAllocation(actor, replacementScene, replacementIndex, true);
                        break;
                    case 35:
                        pc = instruction.BranchOffset;
                        break;
                    case 34:
                        int delayedText = instruction[0];
                        int textDelay = instruction[1];
                        QueueNativeText(actor, program, delayedText, opcode, textDelay);
                        break;
                    case 40:
                        int statistic = instruction[0];
                        int multiplier = instruction[1];
                        SetNativeAiStatMultiplier(actor, statistic, multiplier);
                        break;
                    case 36:
                        actor.Atb = AtbMaximum;
                        break;
                    case 37:
                        int nameIndex = instruction[0];
                        actor.SetNativeDisplayName(nameIndex == 255 ? null : nameIndex);
                        break;
                    case 39:
                        int status = instruction[0];
                        SetNativeAiStatus(actor, status, instruction[1]);
                        break;
                    case 38:
                        byte allTargets = instruction[0];
                        byte group = instruction[1];
                        byte targetComparison = instruction[2];
                        byte criterion = instruction[3];
                        selected = FilterAiTargets(allTargets, group, targetComparison, criterion);
                        break;
                    case 43:
                        int physicalTarget = instruction[0];
                        selected = SelectDirectAiActor(physicalTarget);
                        break;
                    case 41:
                        DrawNativeAiSpell(actor, false, hostedOperationsAtStart);
                        break;
                    case 42:
                        CastNativeAiDrawnSpell(actor);
                        break;
                    case 46:
                        DrawNativeAiSpell(actor, true);
                        break;
                    case 44:
                        StopTurnAdvance();
                        actor.NativeStatus0 |= 1;
                        actor.IsActive = false;
                        actor.Untargetable = true;
                        actor.Atb = 0;
                        actor.IsBusy = false;
                        break;
                    case 45:
                        int elementIndex = instruction[0];
                        actor.SetNativeElementDefense(elementIndex, instruction.ReadWord(1));
                        break;
                    case 47:
                        actor.Untargetable = false;
                        break;
                    case 48:
                        actor.Untargetable = true;
                        break;
                    case 49:
                        AcquireNativeGuardianForce(instruction[0]);
                        break;
                    case 50:
                        nativeScriptState = nativeScriptState with { DisableAutomaticTargetReplacement = true };
                        break;
                    case 51:
                        if (producingExpandedAiCallback)
                        {
                            StageExpandedHostedControl(new HostedDispatchControl.Barrier());
                        }
                        else if (UsesExpandedRoster)
                        {
                            QueueExpandedAiDescriptor(actor, new(BattleExpandedAiDescriptorKind.Barrier));
                        }
                        else
                        {
                            QueueNativeGraphicsBarrier();
                        }
                        break;
                    case 55:
                        AwardNativeCard(instruction[0]);
                        break;
                    case 54:
                        CompleteNativeSavedBattleFlags();
                        break;
                    case 61:
                        UnlockNativeTutorial(127);
                        break;
                    case 53:
                        int activationIndex = instruction[0];
                        BattlePreviousTarget? activation = activationIndex == 209 ? nativeLastAllocatedTarget : new NativeBattleTargetRequestAdapter.OriginalPreviousOperand(activationIndex);
                        if (producingExpandedAiCallback)
                        {
                            StageHostedNativeActivation(activation);
                        }
                        else if (HasExpandedAiDescriptors)
                        {
                            QueueExpandedAiDescriptor(actor, new(BattleExpandedAiDescriptorKind.Activate, activationIndex));
                        }
                        else
                        {
                            QueueNativeActivation(activation);
                        }
                        break;
                    case 58:
                        FindNativeActor(instruction[0]).Untargetable = false;
                        break;
                    case 57:
                        //4863F0 consumes the request as internalkind3 and saved/resultBYTE1, without victory rewards.
                        nativeScriptState = nativeScriptState with { ScriptedExitRequested = true };
                        break;
                    case 60:
                        ushort hpOperand = instruction.ReadWord(0);
                        int hpBefore = actor.CurrentHp;
                        actor.AddNativeHitPointWord(unchecked((short)hpOperand));
                        ObserveDiagnosticNativeHpWord(actor, callback, operation, code, hpOperand, hpBefore);
                        break;
                    case 56:
                        int item = instruction[0];
                        if (nativeRewardItems.Count >= 24)
                        {
                            throw new NotSupportedException("Native AI item reward exceeds the twenty-four-pair reward bank and would overwrite adjacent native globals.");
                        }
                        nativeRewardItems.Add(new BattleStock(item, 1));
                        if (item == 0)
                        {
                            break;
                        }
                        int stock = scriptedRewards.FindIndex(reward => reward.Id == item);
                        if (stock < 0)
                        {
                            scriptedRewards.Add(new BattleStock(item, 1));
                        }
                        else
                        {
                            scriptedRewards[stock] = scriptedRewards[stock] with { Count = checked(scriptedRewards[stock].Count + 1) };
                        }
                        break;
                    default:
                        //487DF0's default consumes only the opcode, without operands or an effect.
                        break;
                }
                if ((uint)pc >= code.Length)
                {
                    throw new InvalidDataException("Native battle AI branch leaves its installed callback.");
                }
            }
            throw new NotSupportedException("Native battle AI exceeded its bounded instruction dispatch.");
        }
        catch (Exception exception) when (exception is NotSupportedException or InvalidDataException)
        {
            string message = $"Native battle AI encounter {definition.EncounterId}, actor {actor.Id}, model {actor.ModelId}, callback {callback}, byte {operation}: {exception.Message}";
            if (exception is InvalidDataException)
            {
                throw new InvalidDataException(message, exception);
            }
            throw new NotSupportedException(message, exception);
        }
        finally
        {
            nativeAiDepth--;
            var diagnosticScope = diagnosticCounterAiScope;
            diagnosticCounterAiScope = previousDiagnosticAiScope;
            if (diagnosticScope is { } completedScope)
            {
                ObserveDiagnosticCounterAi(actor, callback, BattleDiagnosticCounterCallbackPhase.AiExited, completedScope.Serial, completedScope.Sha);
            }
        }
    }

    private void QueueAiEnemyAllocation(BattleCombatant actor, int sceneSlot, int physicalIndex, bool activate)
    {
        if (producingExpandedAiCallback)
        {
            _ = AllocateNativeEnemy(actor, sceneSlot, physicalIndex, activate);
        }
        else if (HasExpandedAiDescriptors)
        {
            QueueExpandedAiDescriptor(actor, new(BattleExpandedAiDescriptorKind.Allocate, sceneSlot, physicalIndex, activate));
        }
        else
        {
            AllocateNativeEnemy(actor, sceneSlot, physicalIndex, activate);
        }
    }

    private bool EvaluateNativeAiGroupStatus(int selector, int comparison, int status)
    {
        // Native487DF0 ->48A830/48A900: group selectors differ from action targets.
        // 200 scans physical party slots0..2;201 scans enemy slots3..6, even in an eight-enemy formation.
        if ((uint)status >= 48)
        {
            throw new NotSupportedException($"Native battle AI status {status} lies outside its WORD/DWORD banks.");
        }
        bool present = NativeConditionGroup(selector).Any(candidate => candidate.IsActive && (status == 0 || (NativeSavedStatusBits(candidate) & 1) == 0) && NativeAiStatusPresent(candidate, status));
        // Native forces equality for each candidate, then inverts the group result for any nonzero comparison.
        return comparison == 0 ? present : !present;
    }

    private bool NativeAiStatusPresent(BattleCombatant actor, int status)
    {
        if (status != 0)
        {
            RequireNativeAllocationStatistics(actor);
        }
        if ((uint)status >= 48)
        {
            throw new NotSupportedException($"Native battle AI status {status} lies outside its WORD/DWORD banks.");
        }
        return status < 16 ? (NativeSavedStatusBits(actor) & (1u << status)) != 0 : (NativeBattleStatusBits(actor) & (1u << (status - 16))) != 0;
    }

    private bool QueuePreparedNativeAiCommand(BattleCombatant actor, byte command, ushort id, byte sequence, AiTargetSelection selected)
    {
        //487DF0 LABEL374 skips a zero prepared WORD ID, including zero-filled ability rows.
        //This guard belongs to the AI interpreter: ordinary player physical command1 legitimately uses ID0.
        if (id == 0)
        {
            return true;
        }
        return UsesExpandedRoster ? QueueExpandedAiAction(actor, command, id, sequence, selected) : QueueNativeCommand(actor, command, id, sequence, selected);
    }

    private static bool NativeAiPathOnlyReturns(byte[] code, int pc)
    {
        HashSet<int> visited = [];
        while (visited.Add(pc) && NativeBattleAiInstruction.TryDecode(code, pc, out NativeBattleAiInstruction instruction, out _))
        {
            if (instruction.Opcode == 0)
            {
                return true;
            }
            if (instruction.Opcode != 35)
            {
                return false;
            }
            pc = instruction.BranchOffset;
        }
        return false;
    }

    private static void PrepareNativeAiAbility(BattleCombatant actor, int index, ref byte command, ref ushort id, ref byte sequence)
    {
        NativeBattleAiDefinition ai = actor.NativeAi ?? throw new InvalidDataException("Native ability slot requires its original AI bank.");
        if ((uint)index >= 16)
        {
            throw new InvalidDataException("Native battle AI ability index exceeds its level band.");
        }
        NativeBattleAiAbility ability = ai.Abilities[16 * ai.LevelBand + index];
        command = ability.Command;
        id = ability.Id;
        sequence = ability.Sequence;
    }
    private BattleCombatant FindNativeActor(int index)
    {
        return FindActiveNativeActor(index) ?? throw new NotSupportedException($"Native battle actor index {index} has no current allocation.");
    }

    private BattleCombatant? FindActiveNativeActor(int index)
    {
        BattleCombatant? actor = FindAllocatedNativeActor(index);
        return actor is { IsActive: true } ? actor : null;
    }

    private BattleCombatant? FindAllocatedNativeActor(int index)
    {
        return NativeBattleSlots.Contains(index)
            ? Roster(NativeBattleSlots.Decode(index).Side).SingleOrDefault(candidate => candidate.NativeActorIndex == index)
            : null;
    }

    private IReadOnlyList<BattleCombatant> Roster(BattleSide side) => side == BattleSide.Allies ? allies : enemies;
    private static int NativeModelId(BattleCombatant actor) => actor.NativeAllocationPendingStatistics ? 255 : actor.IsPlayer ? actor.IsActive ? actor.ModelId : 255 : actor.ModelId + 16;

    private bool IsAddedEnemy(BattleCombatant actor) => IsAddedEnemy(actor.SourceDefinition);

    private bool IsAddedEnemy(BattleCombatantDefinition actor) => !actor.IsPlayer && actor.SceneSlot is int slot && definition.AddedEnemySources?.ContainsKey(slot) == true;

    private bool RetainsNativeActorIndex(BattleCombatant actor) => !UsesExpandedRoster || UsesOriginalEnemySlots && !actor.IsPlayer && !IsAddedEnemy(actor);

    private NativeBattleParticipantSnapshot CaptureNativeParticipant(BattleCombatant actor) => new(actor.IsActive, actor.Hidden, actor.Untargetable, actor.Suspended, RetainsNativeActorIndex(actor) ? actor.NativeActorIndex ?? NativeBattleSlots.Unassigned : NativeBattleSlots.Unassigned, actor.ResolvedNativeLevel, actor.AiVariables.ToArray(), actor.AiTurnCount, actor.LastAttackerId, actor.LastAttackerAbility, actor.AiDeathHandled) { ReprojectedEncounterCopy = actor.NativeLevelReprojected && nativeAllocations.Any(allocation => allocation.Id == actor.Id && allocation.ResolvedDefinition is not null), MaximumHitPoints = actor.IsPlayer && actor.NativePlayerStatRules is not null && actor.GuardianFormVitals is null ? actor.MaximumHp : null, StatMultipliers = actor.NativeStatMultipliers.ToArray(), LastAction = actor.LastNativeAction, LastHealing = actor.LastNativeHealing, AllocationPendingStatistics = actor.NativeAllocationPendingStatistics, NativeCombatantFlags = actor.NativeCombatantFlags, KillCount = actor.NativeKillCount, DeathCount = actor.NativeDeathCount, Growth = actor.NativeGrowth, DisplayNameIndex = actor.NativeDisplayNameIndex, ElementDefenses = actor.HumanElementDefenses.ToArray(), PreviousTarget = actor.PreviousAiTarget, ExpandedAiRemovalPending = actor.ExpandedAiRemovalPending, DrawnSpell = actor.NativeDrawnSpell, ReturnDamage = actor.NativeReturnDamage, PendingReaction = actor.NativePendingReaction, RemainingHits = actor.NativeRemainingHits, ActorReferences = actor.AiActorReferences.Count == 0 ? null : new(actor.AiActorReferences) };

    private void RestoreNativeParticipant(BattleCombatant actor, NativeBattleParticipantSnapshot? saved)
    {
        if (saved is null)
        {
            actor.ExpandedAiRemovalPending = false;
            actor.NativeReturnDamage = null;
            actor.NativePendingReaction = new(null, null);
            actor.NativeRemainingHits = null;
            if (actor.NativeAi is not null || actor.SceneSlot is not null)
            {
                throw new InvalidDataException("Battle participant lacks its explicit native lifecycle state.");
            }
            return;
        }
        bool retainsPhysicalSlot = RetainsNativeActorIndex(actor);
        if (saved.Variables is null || saved.Variables.Length != 8 || saved.ActorIndex is < NativeBattleSlots.Unassigned or >= NativeBattleSlots.Count || saved.Active && saved.ActorIndex < 0 && retainsPhysicalSlot || !retainsPhysicalSlot && saved.ActorIndex != NativeBattleSlots.Unassigned || saved.ActorIndex >= 0 && actor.Side != NativeBattleSlots.Decode(saved.ActorIndex).Side || saved.LastAttackerId is not null && !byId.ContainsKey(saved.LastAttackerId) || saved.LastAttackerAbility is { } ability && !Enum.IsDefined(ability))
        {
            throw new InvalidDataException("Battle participant has invalid native allocation or AI owner state.");
        }
        actor.AiActorReferences.Clear();
        if (saved.ActorReferences is { } references)
        {
            foreach ((int index, string id) in references)
            {
                if (!UsesExpandedRoster || actor.NativeAi is null || (uint)index >= actor.AiVariables.Length || id is null || !byId.ContainsKey(id) || saved.Variables[index] != 0)
                {
                    throw new InvalidDataException("Battle participant has an invalid logical AI actor reference.");
                }
                actor.AiActorReferences.Add(index, id);
            }
        }
        actor.IsActive = saved.Active;
        actor.Hidden = saved.Hidden;
        actor.Untargetable = saved.Untargetable;
        actor.Suspended = saved.Suspended;
        if (saved.NativeCombatantFlags is uint flags)
        {
            uint lifecycle = (saved.Active ? 1u : 0) | (saved.Hidden ? 2u : 0) | (saved.Untargetable ? 0x40u : 0) | (saved.Suspended ? 0x80u : 0);
            if ((flags & 0xC3) != lifecycle)
            {
                throw new InvalidDataException("Native combatant flags disagree with the saved allocation lifecycle.");
            }
            actor.NativeCombatantFlags = flags;
        }
        if (retainsPhysicalSlot)
        {
            actor.BindRosterSlot(saved.ActorIndex == NativeBattleSlots.Unassigned ? null : NativeBattleSlots.Decode(saved.ActorIndex));
        }
        BattleCombatantDefinition? copy = nativeAllocations.SingleOrDefault(allocation => allocation.Id == actor.Id)?.ResolvedDefinition;
        if (saved.ReprojectedEncounterCopy && (copy is null || saved.ResolvedLevel is null))
        {
            throw new InvalidDataException("Encounter level reprojection requires its resolved copy and current level.");
        }
        if (saved.ResolvedLevel is int level)
        {
            if (copy is not null && !saved.ReprojectedEncounterCopy && level == copy.Level)
            {
                actor.RestoreResolvedEncounterCopy(copy);
            }
            else
            {
                if (UsesExpandedRoster && (actor.NativeTemplate is null || copy is not null) && (level is < 1 or > 100 || !CanRebindExpandedEnemyLevel(actor, level, saved.Variables)))
                {
                    throw new InvalidDataException("Saved expanded enemy level is outside its installed level or AI contract.");
                }
                actor.BindNativeLevel(level);
            }
        }
        else if (actor.NativeTemplate is not null && saved.Active)
        {
            throw new InvalidDataException("Allocated native participant lacks its resolved level.");
        }
        RestoreNativeStatMultipliers(actor, saved.StatMultipliers);
        if (saved.LastAction is { } action && (action.AttackType is > 3 || action.AttackerIndex > 10))
        {
            throw new InvalidDataException("Native battle action memory has an invalid attack type or physical attacker index.");
        }
        actor.NativeReturnDamage = saved.ReturnDamage;
        saved.PendingReaction?.Validate();
        actor.NativePendingReaction = saved.PendingReaction ?? new(null, null);
        actor.NativeRemainingHits = saved.RemainingHits;
        if (saved.PreviousTarget is BattlePreviousTarget.Actor previous && (!UsesExpandedRoster || actor.NativeAi is null || !byId.TryGetValue(previous.Reference.ActorId, out BattleCombatant? previousActor) || previousActor.Side != previous.Reference.Side))
        {
            throw new InvalidDataException("Battle participant has an invalid previous logical AI target.");
        }
        actor.PreviousAiTarget = saved.PreviousTarget;
        actor.ExpandedAiRemovalPending = saved.ExpandedAiRemovalPending;
        actor.NativeDrawnSpell = saved.DrawnSpell;
        saved.Growth?.Validate();
        actor.NativeGrowth = saved.Growth ?? actor.NativeGrowth;
        actor.SetNativeDisplayName(saved.DisplayNameIndex);
        if (saved.ElementDefenses is { } defenses)
        {
            actor.SetNativeElementDefenses(defenses);
        }
        if (saved.LastHealing > 1)
        {
            throw new InvalidDataException("Native last healing flag must retain the original494410 BYTE boolean.");
        }
        actor.NativeAllocationPendingStatistics = saved.AllocationPendingStatistics;
        ValidateNativeActionMemoryProjection(actor, saved);
        actor.NativeKillCount = saved.KillCount ?? actor.SourceDefinition.NativeKillCount;
        if (actor.SourceDefinition.NativeDeathCount is not null && saved.DeathCount is null)
        {
            throw new InvalidDataException("Battle snapshot requires its explicitly migrated native player death counter.");
        }
        actor.NativeDeathCount = saved.DeathCount;
        saved.Variables.CopyTo(actor.AiVariables, 0);
        actor.AiTurnCount = saved.TurnCount;
        actor.LastAttackerId = saved.LastAttackerId;
        actor.LastAttackerAbility = saved.LastAttackerAbility;
        actor.AiDeathHandled = saved.DeathHandled;
    }

    private static NativeBattleAiDefinition? CloneNativeAi(NativeBattleAiDefinition? source) => source is null ? null : source with { Scripts = source.Scripts.Select(script => script.ToArray()).ToArray(), Abilities = source.Abilities.ToArray(), Texts = source.Texts.ToArray(), NameBuffers = source.NameBuffers.Select(buffer => buffer.ToArray()).ToArray(), DisplayNames = source.DisplayNames.ToArray() };
    private static NativeBattleEnemyTemplate? CloneNativeTemplate(NativeBattleEnemyTemplate? source) => source is null ? null : source with { NativeDrawTables = source.NativeDrawTables?.Select(table => table.ToArray()).ToArray(), Levels = source.Levels.Select(level => level with { BaseStatistics = level.BaseStatistics.ToArray(), ElementDefenses = level.ElementDefenses.ToArray(), DrawSpells = level.DrawSpells.ToArray(), DrawGuardianForces = level.DrawGuardianForces.ToArray() }).ToArray() };
    private static void ValidateNativeAi(BattleCombatantDefinition actor, int enemySlotCount)
    {
        if ((actor.NativeRetainedStatusBits & ~0x80) != 0)
        {
            throw new InvalidDataException("Native retained statuses require the separately owned saved WORD bit7.");
        }
        if (actor.DrawGuardianForces.Any(id => id is < 0 or > 15) || actor.DrawGuardianForces.Distinct().Count() != actor.DrawGuardianForces.Length)
        {
            throw new InvalidDataException("Native GF Draw metadata contains an invalid or repeated GF identity.");
        }
        if (actor.NativeTemplate?.NativeDrawTables is { } drawTables)
        {
            if (drawTables.Length != 3)
            {
                throw new InvalidDataException("Native enemy Draw template requires its three original level banks.");
            }
            foreach (NativeBattleDrawSlot[] table in drawTables)
            {
                BattleCombatant.ValidateDrawSlots(table);
            }
        }
        if (actor.NativeTemplate is { } template && (template.Levels is null || template.Levels.Length != 101 || template.Levels.Any(level => level.MaximumHp < 0 || level.ElementDefenses.Length != 8 || level.AiLevelBand is < 0 or > 2 || level.Evasion is < 0 or > 255 || level.BaseStatistics is null || level.BaseStatistics.Length is not (0 or 6))))
        {
            throw new InvalidDataException("Native enemy resource template lacks its complete level projections.");
        }
        if (actor.SceneSlot is int slot && ((uint)slot >= enemySlotCount || actor.IsPlayer) || !actor.InitialActive && actor.SceneSlot is null)
        {
            throw new InvalidDataException("Native battle formation slot/participant allocation is invalid.");
        }
        if (actor.NativeAi is not { } ai)
        {
            return;
        }
        if (actor.IsPlayer || ai.Scripts is null || ai.Scripts.Length != 5 || ai.Scripts.Any(script => script is null || script.Length == 0) || ai.Abilities is null || ai.Abilities.Length != 48 || ai.LevelBand is < 0 or > 2 || ai.Texts is null || ai.Texts.Any(text => text is null))
        {
            throw new InvalidDataException("Native battle AI requires its complete installed callback/ability/text banks.");
        }
        if (ai.SequenceTracks.IsDefault || ai.SequenceTracks.Any(track => track is null || track.Program.IsDefaultOrEmpty || track.Messages.IsDefault || track.CompletionFrame < 0 || track.Messages.Any(message => message is null || message.Frame < 0 || message.Frame > track.CompletionFrame || message.Text >= ai.Texts.Length)) || ai.SequenceTracks.Select(track => track.Sequence).Distinct().Count() != ai.SequenceTracks.Length)
        {
            throw new InvalidDataException("Native local sequence tracks require unique sequence identities, original programs and bounded message cues.");
        }
    }
}
