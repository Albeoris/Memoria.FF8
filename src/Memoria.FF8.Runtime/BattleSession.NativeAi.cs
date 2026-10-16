using System.Buffers.Binary;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    // Native47CCB0 invokes486D90 before initial AI; it clears exactly32bytes at1D28D98.
    private readonly uint[] nativeAiVariables = new uint[8];
    private readonly List<BattleStock> scriptedRewards = [];
    public IReadOnlyList<BattleStock> ScriptedRewards => scriptedRewards.AsReadOnly();
    private int nativeAiDepth;
    private uint[]? persistentAiVariables;
    private int nativeLastAllocatedIndex = -1;
    public IReadOnlyList<uint>? PersistentAiVariables => persistentAiVariables?.ToArray();

    private void InitializeNativeParticipants()
    {
        int partySlot = 0;
        int enemySlot = 3;
        foreach (BattleCombatant actor in combatants)
        {
            actor.NativeIndex = actor.IsPlayer ? partySlot++ : actor.IsActive ? enemySlot++ : -1;
        }
        foreach (BattleCombatant actor in combatants.Where(actor => actor.IsActive && !actor.Suspended && actor.NativeAi is not null).ToArray())
        {
            RunNativeAi(actor, 0);
        }
    }

    private void ActivateNativeParticipant(BattleCombatant actor)
    {
        // Native487670 retains the allocated statistics/resources, clears2/40/80 and queues init.
        actor.IsActive = true;
        actor.Hidden = false;
        actor.Untargetable = false;
        actor.Suspended = false;
        actor.AiDeathHandled = false;
        Log("Activated", actor.Id, null, 0, $"{actor.Name} enters battle.");
        RunNativeAi(actor, 0);
    }

    private void RunNativeAi(BattleCombatant actor, int callback)
    {
        NativeBattleAiDefinition? program = actor.NativeAi;
        if (program is null)
        {
            return;
        }
        if (TryRunNativeEnemyBerserk(actor, callback))
        {
            return;
        }
        if (++nativeAiDepth > 16)
        {
            nativeAiDepth--;
            throw new NotSupportedException("Native battle AI activation exceeds the bounded callback stack.");
        }
        int operation = 0;
        try
        {
            byte[] code = program.Scripts[callback];
            int pc = 0;
            ushort targetMask = 0;
            byte preparedCommand = 0;
            ushort preparedId = 0;
            byte sequenceOverride = 0;
            for (int dispatched = 0; dispatched < 4096; dispatched++)
            {
                operation = pc;
                int opcode = ReadAiByte(code, ref pc);
                switch (opcode)
                {
                    case 0:
                        return;
                    case 1:
                    case 24:
                    case 26:
                        int text = ReadAiByte(code, ref pc);
                        QueueNativeText(actor, program, text, opcode);
                        break;
                    case 2:
                        int subject = ReadAiByte(code, ref pc);
                        int selector = ReadAiByte(code, ref pc);
                        int comparison = ReadAiByte(code, ref pc);
                        int value = ReadAiWord(code, ref pc);
                        int skip = ReadAiWord(code, ref pc);
                        if (!EvaluateNativeAiCondition(actor, subject, selector, comparison, value))
                        {
                            pc = checked(pc + skip);
                        }
                        break;
                    case 3:
                        preparedCommand = 2;
                        preparedId = (byte)ReadAiByte(code, ref pc);
                        break;
                    case 4:
                        targetMask = ResolveNativeAiTargetMask(actor, (byte)ReadAiByte(code, ref pc));
                        break;
                    case 5:
                        sequenceOverride = (byte)ReadAiByte(code, ref pc);
                        break;
                    case 6:
                        QueueNativeCommand(actor, preparedCommand, preparedId, sequenceOverride, targetMask);
                        break;
                    case 7:
                        preparedCommand = 8;
                        preparedId = (byte)ReadAiByte(code, ref pc);
                        break;
                    case 9:
                        int damagePresentation = ReadAiByte(code, ref pc);
                        Log("NativeDamagePresentation", actor.Id, null, damagePresentation, $"Native damage presentation {damagePresentation}.");
                        break;
                    case 11:
                        int first = ReadAiByte(code, ref pc);
                        int second = ReadAiByte(code, ref pc);
                        int third = ReadAiByte(code, ref pc);
                        int chosen = (NextRandomByte() % 3) switch { 0 => first, 1 => second, _ => third };
                        if (chosen != 253)
                        {
                            PrepareNativeAiAbility(actor, chosen, ref preparedCommand, ref preparedId, ref sequenceOverride);
                            QueueNativeCommand(actor, preparedCommand, preparedId, sequenceOverride, targetMask);
                        }
                        break;
                    case 12:
                        PrepareNativeAiAbility(actor, ReadAiByte(code, ref pc), ref preparedCommand, ref preparedId, ref sequenceOverride);
                        QueueNativeCommand(actor, preparedCommand, preparedId, sequenceOverride, targetMask);
                        break;
                    case 14:
                    case 15:
                    case 18:
                    case 19:
                    case 17:
                    case 21:
                        int variable = ReadAiByte(code, ref pc);
                        uint operand = (uint)ReadAiByte(code, ref pc);
                        int variableIndex = opcode is 14 or 18 ? variable - 220 : opcode is 17 or 21 ? variable - 80 : variable - 96;
                        if ((uint)variableIndex >= 8)
                        {
                            throw new NotSupportedException("Native battle AI variable lies outside its proved eight-DWORD bank.");
                        }
                        if (opcode is 14 or 18 && operand == 203)
                        {
                            operand = (uint)NativeActorIndex(FindAiActor(actor, 203));
                        }
                        if (opcode is 15 or 19)
                        {
                            uint previous = opcode == 19 ? nativeAiVariables[variableIndex] : 0;
                            nativeAiVariables[variableIndex] = unchecked(previous + operand);
                        }
                        else
                        {
                            uint[] bank = opcode is 14 or 18 ? actor.AiVariables : persistentAiVariables ?? throw new NotSupportedException("Native battle AI requires its executed persistent DWORD bank80..87.");
                            bank[variableIndex] = opcode is 18 or 21 ? unchecked(bank[variableIndex] + operand) : operand;
                        }
                        break;
                    case 22:
                        // Native22 assigns the current HP DWORD from the allocated maximum, including death AI.
                        actor.CurrentHp = actor.MaximumHp;
                        actor.AiDeathHandled = false;
                        break;
                    case 23:
                        bool sceneResultFlag = ReadAiByte(code, ref pc) == 1;
                        nativeScriptState = nativeScriptState with { ResultFlags = (byte)((nativeScriptState.ResultFlags & ~1) | (sceneResultFlag ? 1 : 0)) };
                        break;
                    case 13:
                    case 25:
                        // Native487DF0 deliberately consumes one operand and performs no operation.
                        _ = ReadAiByte(code, ref pc);
                        break;
                    case 27:
                        int entranceIndex = ReadAiByte(code, ref pc) + 3;
                        int sequence = ReadAiByte(code, ref pc);
                        BattleCombatant activation = FindNativeActor(entranceIndex);
                        Log("NativeSequence", actor.Id, activation.Id, sequence, $"Native entrance sequence {sequence}.");
                        ActivateNativeParticipant(activation);
                        break;
                    case 29:
                        int removal = ReadAiByte(code, ref pc);
                        BattleCombatant removed = removal == 200 ? actor : FindNativeActor(removal + 3);
                        ReleaseNativeParticipant(removed);
                        break;
                    case 30:
                        // Native252 sets the no-damage byte in48FE20. Its operand selects presentation.
                        int effect = ReadAiByte(code, ref pc);
                        Log("NativeSequence", actor.Id, null, effect, $"Native battle sequence {effect}, actor sequence {sequenceOverride}.");
                        break;
                    case 28:
                        QueueNativeWait(30 * ReadAiByte(code, ref pc));
                        break;
                    case 32:
                        QueueNativeWait(15 * ReadAiByte(code, ref pc));
                        break;
                    case 31:
                    case 52:
                        int reserve = ReadAiByte(code, ref pc);
                        AllocateNativeEnemy(actor, reserve, 0, opcode == 31);
                        break;
                    case 59:
                        int replacementScene = ReadAiByte(code, ref pc);
                        int replacementIndex = ReadAiByte(code, ref pc);
                        AllocateNativeEnemy(actor, replacementScene, replacementIndex, true);
                        break;
                    case 35:
                        short displacement = unchecked((short)ReadAiWord(code, ref pc));
                        pc = checked(pc + displacement);
                        break;
                    case 34:
                        int delayedText = ReadAiByte(code, ref pc);
                        int textDelay = ReadAiByte(code, ref pc);
                        QueueNativeText(actor, program, delayedText, opcode, textDelay);
                        break;
                    case 40:
                        int statistic = ReadAiByte(code, ref pc);
                        int multiplier = ReadAiByte(code, ref pc);
                        SetNativeAiStatMultiplier(actor, statistic, multiplier);
                        break;
                    case 36:
                        actor.Atb = AtbMaximum;
                        break;
                    case 37:
                        int nameIndex = ReadAiByte(code, ref pc);
                        actor.SetNativeDisplayName(nameIndex == 255 ? null : nameIndex);
                        break;
                    case 39:
                        int status = ReadAiByte(code, ref pc);
                        SetNativeAiStatus(actor, status, ReadAiByte(code, ref pc));
                        break;
                    case 38:
                        byte allTargets = (byte)ReadAiByte(code, ref pc);
                        byte group = (byte)ReadAiByte(code, ref pc);
                        byte targetComparison = (byte)ReadAiByte(code, ref pc);
                        byte criterion = (byte)ReadAiByte(code, ref pc);
                        targetMask = FilterNativeAiTargetMask(allTargets, group, targetComparison, criterion);
                        break;
                    case 43:
                        int physicalTarget = ReadAiByte(code, ref pc);
                        targetMask = unchecked((ushort)(1 << physicalTarget));
                        break;
                    case 41:
                        DrawNativeAiSpell(actor, false);
                        break;
                    case 42:
                        CastNativeAiDrawnSpell(actor);
                        break;
                    case 46:
                        DrawNativeAiSpell(actor, true);
                        break;
                    case 44:
                        actor.IsActive = false;
                        actor.Untargetable = true;
                        actor.Atb = 0;
                        actor.IsBusy = false;
                        break;
                    case 45:
                        int elementIndex = ReadAiByte(code, ref pc);
                        actor.SetNativeElementDefense(elementIndex, ReadAiWord(code, ref pc));
                        break;
                    case 47:
                        actor.Untargetable = false;
                        break;
                    case 48:
                        actor.Untargetable = true;
                        break;
                    case 49:
                        AcquireNativeGuardianForce((byte)ReadAiByte(code, ref pc));
                        break;
                    case 50:
                        nativeScriptState = nativeScriptState with { DisableAutomaticTargetReplacement = true };
                        break;
                    case 55:
                        AwardNativeCard((byte)ReadAiByte(code, ref pc));
                        break;
                    case 54:
                        CompleteNativeSavedBattleFlags();
                        break;
                    case 61:
                        UnlockNativeTutorial(127);
                        break;
                    case 53:
                        int activationIndex = ReadAiByte(code, ref pc);
                        ActivateNativeParticipant(FindNativeActor(activationIndex == 209 ? nativeLastAllocatedIndex : activationIndex));
                        break;
                    case 58:
                        FindNativeActor(ReadAiByte(code, ref pc)).Untargetable = false;
                        break;
                    case 56:
                        int item = ReadAiByte(code, ref pc);
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
                        if (opcode is >= 0 and <= 61 and not (10 or 16 or 20 or 33))
                        {
                            throw new NotSupportedException($"Native battle AI model {actor.ModelId} callback {callback} byte {operation}: opcode {opcode} requires its original service owner.");
                        }
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
        bool present = combatants.Any(candidate => candidate.IsActive && candidate.NativeIndex is >= 0 and < 7 && (selector == 200 ? candidate.NativeIndex < 3 : selector == 201 ? candidate.NativeIndex >= 3 : NativeModelId(candidate) == selector) && (status == 0 || (NativeSavedStatusBits(candidate) & 1) == 0) && NativeAiStatusPresent(candidate, status));
        // Native forces equality for each candidate, then inverts the group result for any nonzero comparison.
        return comparison == 0 ? present : !present;
    }

    private bool NativeAiStatusPresent(BattleCombatant actor, int status)
    {
        if ((uint)status >= 48)
        {
            throw new NotSupportedException($"Native battle AI status {status} lies outside its WORD/DWORD banks.");
        }
        return status < 16 ? (NativeSavedStatusBits(actor) & (1u << status)) != 0 : (NativeBattleStatusBits(actor) & (1u << (status - 16))) != 0;
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
    private BattleCombatant FindAiActor(BattleCombatant actor, int selector)
    {
        if (selector == 200)
        {
            return actor;
        }
        if (selector == 203)
        {
            return actor.LastAttackerId is string id ? byId[id] : throw new InvalidDataException("Native battle AI requires an executed last-attacker owner.");
        }
        if (selector == 201)
        {
            BattleCombatant[] party = combatants.Where(candidate => candidate.IsPlayer && candidate.IsAlive && !candidate.Untargetable).ToArray();
            if (party.Length == 0)
            {
                throw new InvalidDataException("Native battle AI has no living party target.");
            }
            // Native201 samples the three physical party slots with rejection.
            for (int attempt = 0; attempt < 4096; attempt++)
            {
                int slot = NextRandomByte() % 3;
                BattleCombatant? selected = party.SingleOrDefault(candidate => combatants.Where(member => member.IsPlayer).ToList().IndexOf(candidate) == slot);
                if (selected is not null)
                {
                    return selected;
                }
            }
            throw new NotSupportedException("Native battle AI target rejection exceeded its bounded sampling.");
        }
        if (selector is 204 or 205)
        {
            // The native mask is retained by the action resolver, with this actor as the queue owner.
            return actor;
        }
        if (selector is >= 220 and <= 227)
        {
            return FindNativeActor(checked((int)actor.AiVariables[selector - 220]));
        }
        return combatants.FirstOrDefault(candidate => candidate.IsActive && NativeModelId(candidate) == selector) ?? throw new NotSupportedException($"Native battle AI model selector {selector} has no allocated actor.");
    }

    private BattleCombatant FindNativeActor(int index) => combatants.SingleOrDefault(actor => NativeActorIndex(actor) == index && actor.IsActive) ?? throw new NotSupportedException($"Native battle AI actor index {index} has no current allocation.");
    private int NativeActorIndex(BattleCombatant actor) => actor.NativeIndex;
    private static int NativeModelId(BattleCombatant actor) => actor.IsPlayer ? actor.ModelId : actor.ModelId + 16;
    private static int ReadAiByte(byte[] code, ref int pc)
    {
        if ((uint)pc >= code.Length)
        {
            throw new InvalidDataException("Native battle AI operand exceeds its original callback.");
        }
        return code[pc++];
    }
    private static int ReadAiWord(byte[] code, ref int pc)
    {
        if (pc < 0 || pc > code.Length - 2)
        {
            throw new InvalidDataException("Native battle AI WORD operand exceeds its original callback.");
        }
        int value = BinaryPrimitives.ReadUInt16LittleEndian(code.AsSpan(pc, 2));
        pc += 2;
        return value;
    }

    private static NativeBattleParticipantSnapshot CaptureNativeParticipant(BattleCombatant actor) => new(actor.IsActive, actor.Hidden, actor.Untargetable, actor.Suspended, actor.NativeIndex, actor.ResolvedNativeLevel, actor.AiVariables.ToArray(), actor.AiTurnCount, actor.LastAttackerId, actor.LastAttackerAbility, actor.AiDeathHandled) { StatMultipliers = actor.NativeStatMultipliers.ToArray(), LastAction = actor.LastNativeAction, NativeCombatantFlags = actor.NativeCombatantFlags, KillCount = actor.NativeKillCount, Growth = actor.NativeGrowth, DisplayNameIndex = actor.NativeDisplayNameIndex, ElementDefenses = actor.ElementDefenses.ToArray(), PreviousTargetMask = actor.NativePreviousTargetMask, DrawnSpell = actor.NativeDrawnSpell };

    private void RestoreNativeParticipant(BattleCombatant actor, NativeBattleParticipantSnapshot? saved)
    {
        if (saved is null)
        {
            if (actor.NativeAi is not null || actor.SceneSlot is not null)
            {
                throw new InvalidDataException("Battle participant lacks its explicit native lifecycle state.");
            }
            return;
        }
        if (saved.Variables is null || saved.Variables.Length != 8 || saved.ActorIndex is < -1 or > 6 || saved.Active && saved.ActorIndex < 0 || saved.Active && actor.IsPlayer != (saved.ActorIndex < 3) || saved.LastAttackerId is not null && !byId.ContainsKey(saved.LastAttackerId) || saved.LastAttackerAbility is { } ability && !Enum.IsDefined(ability))
        {
            throw new InvalidDataException("Battle participant has invalid native allocation or AI owner state.");
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
        actor.NativeIndex = saved.ActorIndex;
        if (saved.ResolvedLevel is int level)
        {
            actor.BindNativeLevel(level);
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
        actor.NativePreviousTargetMask = saved.PreviousTargetMask;
        actor.NativeDrawnSpell = saved.DrawnSpell;
        saved.Growth?.Validate();
        actor.NativeGrowth = saved.Growth ?? actor.NativeGrowth;
        actor.SetNativeDisplayName(saved.DisplayNameIndex);
        if (saved.ElementDefenses is { } defenses)
        {
            actor.SetNativeElementDefenses(defenses);
        }
        actor.LastNativeAction = saved.LastAction;
        actor.NativeKillCount = saved.KillCount ?? actor.SourceDefinition.NativeKillCount;
        saved.Variables.CopyTo(actor.AiVariables, 0);
        actor.AiTurnCount = saved.TurnCount;
        actor.LastAttackerId = saved.LastAttackerId;
        actor.LastAttackerAbility = saved.LastAttackerAbility;
        actor.AiDeathHandled = saved.DeathHandled;
    }

    private static NativeBattleAiDefinition? CloneNativeAi(NativeBattleAiDefinition? source) => source is null ? null : source with { Scripts = source.Scripts.Select(script => script.ToArray()).ToArray(), Abilities = source.Abilities.ToArray(), Texts = source.Texts.ToArray(), NameBuffers = source.NameBuffers.Select(buffer => buffer.ToArray()).ToArray(), DisplayNames = source.DisplayNames.ToArray() };
    private static NativeBattleEnemyTemplate? CloneNativeTemplate(NativeBattleEnemyTemplate? source) => source is null ? null : source with { Levels = source.Levels.Select(level => level with { BaseStatistics = level.BaseStatistics.ToArray(), ElementDefenses = level.ElementDefenses.ToArray(), DrawSpells = level.DrawSpells.ToArray(), DrawGuardianForces = level.DrawGuardianForces.ToArray() }).ToArray() };
    private static void ValidateNativeAi(BattleCombatantDefinition actor)
    {
        if ((actor.NativeRetainedStatusBits & ~0x80) != 0)
        {
            throw new InvalidDataException("Native retained statuses require the separately owned saved WORD bit7.");
        }
        if (actor.DrawGuardianForces.Any(id => id is < 0 or > 15) || actor.DrawGuardianForces.Distinct().Count() != actor.DrawGuardianForces.Length)
        {
            throw new InvalidDataException("Native GF Draw metadata contains an invalid or repeated GF identity.");
        }
        if (actor.NativeTemplate is { } template && (template.Levels is null || template.Levels.Length != 101 || template.Levels.Any(level => level.MaximumHp < 0 || level.ElementDefenses.Length != 8 || level.AiLevelBand is < 0 or > 2 || level.Evasion is < 0 or > 255 || level.BaseStatistics is null || level.BaseStatistics.Length is not (0 or 6))))
        {
            throw new InvalidDataException("Native enemy resource template lacks its complete level projections.");
        }
        if (actor.SceneSlot is < 0 or > 7 || actor.IsPlayer && actor.SceneSlot is not null || !actor.InitialActive && actor.SceneSlot is null)
        {
            throw new InvalidDataException("Native battle formation slot/participant allocation is invalid.");
        }
        if (actor.NativeAi is not { } ai)
        {
            return;
        }
        if (actor.IsPlayer || ai.Scripts is null || ai.Scripts.Length != 5 || ai.Scripts.Any(script => script is null || script.Length is < 1 or > 65536) || ai.Abilities is null || ai.Abilities.Length != 48 || ai.LevelBand is < 0 or > 2 || ai.Texts is null || ai.Texts.Any(text => text is null))
        {
            throw new InvalidDataException("Native battle AI requires its complete installed callback/ability/text banks.");
        }
    }
}
