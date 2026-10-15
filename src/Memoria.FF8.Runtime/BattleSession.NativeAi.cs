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
        if (++nativeAiDepth > 16)
        {
            nativeAiDepth--;
            throw new NotSupportedException("Native battle AI activation exceeds the bounded callback stack.");
        }
        try
        {
            byte[] code = program.Scripts[callback];
            int pc = 0;
            byte targetSelector = 201;
            int sequenceOverride = 0;
            for (int dispatched = 0; dispatched < 4096; dispatched++)
            {
                int operation = pc;
                int opcode = ReadAiByte(code, ref pc);
                switch (opcode)
                {
                    case 0:
                        return;
                    case 1:
                    case 24:
                    case 26:
                        int text = ReadAiByte(code, ref pc);
                        if (text >= program.Texts.Length)
                        {
                            throw new InvalidDataException("Native battle AI text index exceeds its installed bank.");
                        }
                        Log("NativeText", actor.Id, null, 0, program.Texts[text]);
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
                    case 4:
                        targetSelector = (byte)ReadAiByte(code, ref pc);
                        break;
                    case 5:
                        sequenceOverride = ReadAiByte(code, ref pc);
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
                            QueueNativeAbility(actor, chosen, targetSelector);
                        }
                        break;
                    case 12:
                        QueueNativeAbility(actor, ReadAiByte(code, ref pc), targetSelector);
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
                    case 25:
                        // Native487DF0 deliberately consumes one operand and performs no operation.
                        _ = ReadAiByte(code, ref pc);
                        break;
                    case 27:
                        int sceneSlot = ReadAiByte(code, ref pc);
                        int sequence = ReadAiByte(code, ref pc);
                        BattleCombatant activation = FindFormationActor(sceneSlot);
                        Log("NativeSequence", actor.Id, activation.Id, sequence, $"Native entrance sequence {sequence}.");
                        ActivateNativeParticipant(activation);
                        break;
                    case 29:
                        int removal = ReadAiByte(code, ref pc);
                        BattleCombatant removed = removal == 200 ? actor : FindFormationActor(removal);
                        removed.Hidden = true;
                        // Graphics105 ->502ED0 ->50C5F0 ->48AE20 releases the actor allocation too.
                        removed.Untargetable = true;
                        removed.IsActive = false;
                        removed.Atb = 0;
                        removed.IsBusy = false;
                        Log("NativeModelRemoved", actor.Id, removed.Id, 0, $"{removed.Name} model removed.");
                        break;
                    case 30:
                        // Native252 sets the no-damage byte in48FE20. Its operand selects presentation.
                        int effect = ReadAiByte(code, ref pc);
                        Log("NativeSequence", actor.Id, null, effect, $"Native battle sequence {effect}, actor sequence {sequenceOverride}.");
                        break;
                    case 31:
                    case 52:
                        int reserve = ReadAiByte(code, ref pc);
                        BattleCombatant spawned = FindFormationActor(reserve);
                        if (spawned.IsActive)
                        {
                            throw new NotSupportedException("Native battle AI replacement requires an unoccupied actor allocation.");
                        }
                        spawned.IsActive = true;
                        spawned.NativeIndex = Enumerable.Range(3, 8).First(index => !combatants.Any(candidate => candidate.IsActive && candidate.NativeIndex == index));
                        NativeBattleEnemyTemplate template = spawned.NativeTemplate ?? throw new InvalidDataException("Native battle AI reserve requires its installed allocation template.");
                        int drawCount = NativeBattleEnemyLevelRules.RandomByteCount(template.SceneLevel);
                        byte firstRoll = drawCount > 0 ? (byte)NextRandomByte() : (byte)0;
                        byte? secondRoll = drawCount == 2 ? (byte)NextRandomByte() : null;
                        int level = NativeBattleEnemyLevelRules.Resolve(template.SceneLevel, combatants.Where(candidate => candidate.IsPlayer && candidate.IsActive).Select(candidate => checked((byte)candidate.Level)).ToArray(), firstRoll, secondRoll);
                        spawned.BindNativeLevel(level);
                        spawned.CurrentHp = spawned.MaximumHp;
                        spawned.Atb = 0;
                        Array.Clear(spawned.AiVariables);
                        Log("NativeSpawn", actor.Id, spawned.Id, reserve, $"{spawned.Name} allocated from formation slot {reserve}.");
                        if (opcode == 31)
                        {
                            ActivateNativeParticipant(spawned);
                        }
                        break;
                    case 35:
                        short displacement = unchecked((short)ReadAiWord(code, ref pc));
                        pc = checked(pc + displacement);
                        break;
                    case 44:
                        actor.IsActive = false;
                        actor.Untargetable = true;
                        actor.Atb = 0;
                        actor.IsBusy = false;
                        break;
                    case 47:
                        actor.Untargetable = false;
                        break;
                    case 48:
                        actor.Untargetable = true;
                        break;
                    case 53:
                        ActivateNativeParticipant(FindNativeActor(ReadAiByte(code, ref pc)));
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
                        throw new NotSupportedException($"Native battle AI model {actor.ModelId} callback {callback} byte {operation}: opcode {opcode} is unsupported.");
                }
                if ((uint)pc >= code.Length)
                {
                    throw new InvalidDataException("Native battle AI branch leaves its installed callback.");
                }
            }
            throw new NotSupportedException("Native battle AI exceeded its bounded instruction dispatch.");
        }
        finally
        {
            nativeAiDepth--;
        }
    }

    private bool EvaluateNativeAiCondition(BattleCombatant actor, int subject, int selector, int comparison, int right)
    {
        long left;
        if (subject is >= 80 and <= 87)
        {
            left = persistentAiVariables?[subject - 80] ?? throw new NotSupportedException("Native battle AI requires its executed persistent DWORD bank80..87.");
        }
        else if (subject is >= 96 and <= 103)
        {
            left = nativeAiVariables[subject - 96];
        }
        else if (subject is >= 220 and <= 227)
        {
            left = FindAiActor(actor, selector).AiVariables[subject - 220];
        }
        else
        {
            switch (subject)
            {
                case 0:
                    BattleCombatant hpActor = FindAiActor(actor, selector);
                    left = hpActor.CurrentHp;
                    right = right is >= 1 and <= 9 ? right * (hpActor.MaximumHp / 10) : right == 10 ? hpActor.MaximumHp >> 2 : right;
                    break;
                case 2:
                    if (selector == 0)
                    {
                        throw new InvalidDataException("Native battle AI random condition divides by zero.");
                    }
                    left = NextRandomByte() % selector;
                    break;
                case 3:
                    left = definition.EncounterId ?? throw new NotSupportedException("Native battle AI formation condition requires its installed encounter identity.");
                    break;
                case 6:
                    // Native487590 counts living physical party/enemy slots, or a particular model byte.
                    left = combatants.Count(candidate => candidate.IsAlive && (selector == 200 ? candidate.IsPlayer : selector == 201 ? !candidate.IsPlayer : NativeModelId(candidate) == selector));
                    break;
                case 4:
                    BattleCombatant statusActor = FindAiActor(actor, selector);
                    if (right is >= 0 and <= 47)
                    {
                        BattleStatusKind? status = right switch { 1 => BattleStatusKind.Poison, 3 => BattleStatusKind.Blind, 4 => BattleStatusKind.Silence, 16 => BattleStatusKind.Sleep, 17 => BattleStatusKind.Haste, 18 => BattleStatusKind.Slow, 20 => BattleStatusKind.Regen, 21 => BattleStatusKind.Protect, 22 => BattleStatusKind.Shell, _ => null };
                        bool statusPresent = right == 0 ? statusActor.CurrentHp == 0 : status is { } kind && HasStatus(statusActor, kind);
                        return comparison switch { 0 => statusPresent, 3 => !statusPresent, _ => throw new NotSupportedException("Native battle AI status condition requires equality or inequality.") };
                    }
                    throw new NotSupportedException($"Native battle AI status condition {right} is unsupported.");
                case 9:
                    bool present = combatants.Any(candidate => candidate.IsActive && candidate.CurrentHp > 0 && NativeModelId(candidate) == right);
                    return comparison switch { 0 => present, 3 => !present, _ => throw new NotSupportedException("Native battle AI model condition requires equality or inequality.") };
                case 10:
                    left = selector switch
                    {
                        0 => actor.LastAttackerAbility == BattleAbility.Magic ? 1 : 0,
                        2 => actor.AiTurnCount,
                        203 => actor.LastAttackerId is null ? 255 : NativeActorIndex(byId[actor.LastAttackerId]),
                        _ => throw new NotSupportedException($"Native battle AI action condition {selector} is unsupported.")
                    };
                    if (selector == 203)
                    {
                        right = right == 200 ? NativeActorIndex(actor) : NativeActorIndex(FindAiActor(actor, right));
                    }
                    break;
                case 14:
                    left = actor.NativeAi!.LevelBand;
                    break;
                default:
                    throw new NotSupportedException($"Native battle AI condition subject {subject} is unsupported.");
            }
        }
        return comparison switch
        {
            0 => left == right,
            1 => left < right,
            2 => left > right,
            3 => left != right,
            4 => left <= right,
            5 => left >= right,
            _ => throw new NotSupportedException($"Native battle AI comparison {comparison} is unsupported.")
        };
    }

    private void QueueNativeAbility(BattleCombatant actor, int index, int selector)
    {
        NativeBattleAiDefinition ai = actor.NativeAi!;
        if ((uint)index >= 16)
        {
            throw new InvalidDataException("Native battle AI ability index exceeds its level band.");
        }
        NativeBattleAiAbility ability = ai.Abilities[16 * ai.LevelBand + index];
        if (ability.Id == 0)
        {
            return;
        }
        BattleCombatant target = FindAiActor(actor, selector);
        actor.Atb = 0;
        actor.IsBusy = true;
        actions.Enqueue(new BattleAttackCommand(actor.Id, target.Id, BattleAbility.EnemyAbility, index, NativeTargetSelector: selector));
        Log("Queued", actor.Id, target.Id, index, $"{actor.Name}: native ability {index} queued.");
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

    private BattleCombatant FindFormationActor(int slot) => combatants.SingleOrDefault(actor => !actor.IsPlayer && actor.SceneSlot == slot) ?? throw new NotSupportedException($"Native battle AI formation slot {slot} has no retained resource definition.");
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

    private static NativeBattleParticipantSnapshot CaptureNativeParticipant(BattleCombatant actor) => new(actor.IsActive, actor.Hidden, actor.Untargetable, actor.Suspended, actor.NativeIndex, actor.ResolvedNativeLevel, actor.AiVariables.ToArray(), actor.AiTurnCount, actor.LastAttackerId, actor.LastAttackerAbility, actor.AiDeathHandled);

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
        if (saved.Variables is null || saved.Variables.Length != 8 || saved.ActorIndex is < -1 or > 10 || saved.Active && saved.ActorIndex < 0 || saved.LastAttackerId is not null && !byId.ContainsKey(saved.LastAttackerId) || saved.LastAttackerAbility is { } ability && !Enum.IsDefined(ability))
        {
            throw new InvalidDataException("Battle participant has invalid native allocation or AI owner state.");
        }
        actor.IsActive = saved.Active;
        actor.Hidden = saved.Hidden;
        actor.Untargetable = saved.Untargetable;
        actor.Suspended = saved.Suspended;
        actor.NativeIndex = saved.ActorIndex;
        if (saved.ResolvedLevel is int level)
        {
            actor.BindNativeLevel(level);
        }
        else if (actor.NativeTemplate is not null && saved.Active)
        {
            throw new InvalidDataException("Allocated native participant lacks its resolved level.");
        }
        saved.Variables.CopyTo(actor.AiVariables, 0);
        actor.AiTurnCount = saved.TurnCount;
        actor.LastAttackerId = saved.LastAttackerId;
        actor.LastAttackerAbility = saved.LastAttackerAbility;
        actor.AiDeathHandled = saved.DeathHandled;
    }

    private void ResolveNativeAbility(BattleCombatant actor, BattleCombatant target, BattleActionPresentation command)
    {
        NativeBattleAiDefinition ai = actor.NativeAi ?? throw new InvalidDataException("Native enemy command has no original AI ability bank.");
        if ((uint)command.ActionId >= 16)
        {
            throw new InvalidDataException("Native enemy action exceeds its original ability band.");
        }
        NativeBattleAiAbility ability = ai.Abilities[16 * ai.LevelBand + command.ActionId];
        if (ability.Command == 2)
        {
            ResolveAction(actor, target, command with { Ability = BattleAbility.Magic, ActionId = ability.Id });
            return;
        }
        if (ability.Command != 8 || ability.AttackType is not (0 or 1 or 2) || (ability.Statuses0 & ~0x1A) != 0 || (ability.Statuses1 & ~0x8077u) != 0)
        {
            throw new NotSupportedException($"Native enemy action command {ability.Command}, attack type {ability.AttackType}, statuses {ability.Statuses0:X4}/{ability.Statuses1:X8} requires an unsupported effect contract.");
        }
        if (ability.AttackType == 0)
        {
            Log("NativeSequence", actor.Id, target.Id, ability.Sequence, $"{actor.Name}: native sequence {ability.Sequence}.");
            return;
        }
        BattleCombatant[] targets = command.NativeTargetSelector is 204 or 205 ? combatants.Where(candidate => candidate.IsPlayer == (command.NativeTargetSelector == 204) && candidate.IsAlive && !candidate.Untargetable).ToArray() : [target];
        foreach (BattleCombatant recipient in targets)
        {
            int damage = ability.AttackType == 1 ? NativeBattleDamage.Physical(actor.Attack, recipient.Defense, ability.Power, NextRandomByte()) : NativeBattleDamage.Magic(actor.Magic, recipient.Spirit, ability.Power, NextRandomByte(), true);
            if (ability.Element != 0)
            {
                int element = System.Numerics.BitOperations.TrailingZeroCount((uint)ability.Element);
                damage = damage * (900 - recipient.ElementDefenses[element]) / 100;
            }
            int previousHp = recipient.CurrentHp;
            ApplySignedDamage(actor, recipient, damage, "Native enemy ability");
            if ((ability.Statuses1 & 0x8000) != 0 && recipient.CurrentHp < previousHp)
            {
                ApplyHealing(actor, (previousHp - recipient.CurrentHp) * ability.StatusAttack / 100, "Drain");
            }
            // TODO(TD-009): Reuse the existing shared status projection; native hit/resistance/duration rolls remain unsupported
            foreach ((int mask, BattleStatusKind status) in new[] { (2, BattleStatusKind.Poison), (8, BattleStatusKind.Blind), (16, BattleStatusKind.Silence) })
            {
                if ((ability.Statuses0 & mask) != 0)
                {
                    ApplyProjectedStatus(actor, recipient, status, "Native enemy ability", BattleActionEffect.Damage);
                }
            }
            foreach ((uint mask, BattleStatusKind status) in new[] { (1u, BattleStatusKind.Sleep), (2u, BattleStatusKind.Haste), (4u, BattleStatusKind.Slow), (16u, BattleStatusKind.Regen), (32u, BattleStatusKind.Protect), (64u, BattleStatusKind.Shell) })
            {
                if ((ability.Statuses1 & mask) != 0)
                {
                    ApplyProjectedStatus(actor, recipient, status, "Native enemy ability", BattleActionEffect.Damage);
                }
            }
        }
    }

    private static NativeBattleAiDefinition? CloneNativeAi(NativeBattleAiDefinition? source) => source is null ? null : source with { Scripts = source.Scripts.Select(script => script.ToArray()).ToArray(), Abilities = source.Abilities.ToArray(), Texts = source.Texts.ToArray() };
    private static NativeBattleEnemyTemplate? CloneNativeTemplate(NativeBattleEnemyTemplate? source) => source is null ? null : source with { Levels = source.Levels.Select(level => level with { ElementDefenses = level.ElementDefenses.ToArray(), DrawSpells = level.DrawSpells.ToArray(), DrawGuardianForces = level.DrawGuardianForces.ToArray() }).ToArray() };
    private static void ValidateNativeAi(BattleCombatantDefinition actor)
    {
        if (actor.DrawGuardianForces.Any(id => id is < 0 or > 15) || actor.DrawGuardianForces.Distinct().Count() != actor.DrawGuardianForces.Length)
        {
            throw new InvalidDataException("Native GF Draw metadata contains an invalid or repeated GF identity.");
        }
        if (actor.NativeTemplate is { } template && (template.Levels is null || template.Levels.Length != 101 || template.Levels.Any(level => level.MaximumHp < 0 || level.ElementDefenses.Length != 8 || level.AiLevelBand is < 0 or > 2)))
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
