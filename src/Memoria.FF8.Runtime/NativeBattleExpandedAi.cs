using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum ExpandedAiAdmission
{
    Supported,
    RequiresUnsupportedService,
    RequiresOriginalSlots
}

public readonly record struct NativeBattleAiAllocationSource(int SceneSlot, byte? RequestedModelOperand);

/// <summary>Checks reachable original AI bytecode against the services available in expanded battles.</summary>
public static class NativeBattleExpandedAi
{
    public static ExpandedAiAdmission Evaluate(NativeBattleAiDefinition program, IReadOnlyList<BattleNativeAction> actions, int formation, ICollection<string>? blockers = null, bool originalEnemySlots = false, ICollection<string>? explicitModelReferences = null, ISet<int>? reserveSceneSlots = null, ISet<int>? explicitEnemyModelReferences = null, int? rosterModel = null, bool requireValidInstalledSource = false, ISet<ushort>? reachedEffects = null, ISet<byte>? originalModelOperands = null, ISet<NativeBattleAiAllocationSource>? allocationSources = null)
    {
        return Evaluate(program, actions, formation, blockers, originalEnemySlots, explicitModelReferences, reserveSceneSlots, explicitEnemyModelReferences, rosterModel, requireValidInstalledSource, reachedEffects, originalModelOperands, allocationSources, default);
    }

    internal static ExpandedAiAdmission EvaluateRebound(NativeBattleAiDefinition program, IReadOnlyList<BattleNativeAction> actions, int formation, IReadOnlyList<uint> numbers, IReadOnlyDictionary<int, NativeBattleAiActorValue> references)
    {
        return Evaluate(program, actions, formation, null, false, null, null, null, null, false, null, null, null, NativeBattleAiLocalFlow.RetainedLocals(numbers, references));
    }

    private static ExpandedAiAdmission Evaluate(NativeBattleAiDefinition program, IReadOnlyList<BattleNativeAction> actions, int formation, ICollection<string>? blockers, bool originalEnemySlots, ICollection<string>? explicitModelReferences, ISet<int>? reserveSceneSlots, ISet<int>? explicitEnemyModelReferences, int? rosterModel, bool requireValidInstalledSource, ISet<ushort>? reachedEffects, ISet<byte>? originalModelOperands, ISet<NativeBattleAiAllocationSource>? allocationSources, ImmutableArray<NativeBattleAiLocalFlow.Value> retainedLocals)
    {
        if (program.Scripts is not { Length: 5 } || program.Scripts.Any(script => script is null) ||
            program.LevelBand is < 0 or > 2 || program.Abilities is not { Length: 48 } || program.Abilities.Any(ability => ability is null) || program.Texts is null)
        {
            if (requireValidInstalledSource)
            {
                throw new InvalidDataException($"Formation {formation}, model {rosterModel} has an invalid AI program layout.");
            }
            blockers?.Add("program-layout");
            return ExpandedAiAdmission.RequiresUnsupportedService;
        }
        HashSet<(byte Command, ushort Id)> supported = actions.Where(SupportsAction).Select(action => (action.Command, action.ActionId)).ToHashSet();
        NativeBattleAiAbility berserk = program.Abilities[16 * program.LevelBand];
        RecordActionEffects(berserk.Command, berserk.Id);
        ExpandedAiAdmission admission = SupportsCommand(berserk.Command, berserk.Id) ? ExpandedAiAdmission.Supported : ExpandedAiAdmission.RequiresUnsupportedService;
        if (admission != ExpandedAiAdmission.Supported)
        {
            blockers?.Add($"command:{berserk.Command}/{berserk.Id}");
        }
        bool checksLocalValues = false;
        for (int callback = 0; callback < program.Scripts.Length; callback++)
        {
            CheckScript(program.Scripts[callback], callback);
        }
        if (checksLocalValues && !originalEnemySlots)
        {
            NativeBattleAiLocalFlow.Check(program, formation, retainedLocals, RequireOriginalSlots);
        }
        return admission;

        void RecordModelReference(int model, string description)
        {
            explicitModelReferences?.Add(description);
            if (model is >= 16 and < 200)
            {
                explicitEnemyModelReferences?.Add(model);
            }
        }

        void CheckScript(byte[] script, int callback)
        {
            if (script.Length == 0)
            {
                return;
            }
            HashSet<int> visited = [];
            Stack<int> pending = new();
            pending.Push(0);
            while (pending.TryPop(out int offset))
            {
                if (!visited.Add(offset))
                {
                    continue;
                }
                if (!NativeBattleAiInstruction.TryDecode(script, offset, out NativeBattleAiInstruction instruction, out string? decodeError))
                {
                    MarkMalformed(decodeError, callback, offset);
                    continue;
                }
                int opcode = instruction.Opcode;
                if (opcode == 0)
                {
                    continue;
                }
                if (!instruction.IsKnownOpcode)
                {
                    MarkUnsupportedService($"unknown-opcode:{opcode}");
                    continue;
                }
                int next = instruction.NextOffset;
                if (opcode is 31 or 52 or 59)
                {
                    reserveSceneSlots?.Add(instruction[0]);
                    allocationSources?.Add(new(instruction[0], opcode == 59 && instruction[1] != 0 ? instruction[1] : null));
                }
                if (originalModelOperands is not null)
                {
                    switch (opcode)
                    {
                        case 27 or 29 when instruction[0] < NativeBattleSlots.EnemyCount:
                            originalModelOperands.Add(checked((byte)(instruction[0] + NativeBattleSlots.AllyCount)));
                            break;
                        case 43 or 53 or 58 when NativeBattleSlots.Contains(instruction[0]):
                            originalModelOperands.Add(instruction[0]);
                            break;
                    }
                }
                switch (opcode)
                {
                    case 1 or 24 or 26 or 34 when originalEnemySlots && instruction[0] >= program.Texts.Length && program.AdmittedHeapTextDirectory is null:
                        MarkMalformed($"text-operand:{opcode}/{instruction[0]}", callback, offset);
                        break;
                    case 2:
                        NativeBattleAiCondition condition = instruction.Condition;
                        var (subject, selector, comparison, value) = condition;
                        if (selector < 200 && subject is 0 or 1 or 4 or 5 or 6 or 7 or >= 220 and <= 227)
                        {
                            RecordModelReference(selector, $"condition-model:{subject}/{selector}");
                        }
                        else if (value < 200 && (subject is 8 or 9 || subject == 10 && selector == 203))
                        {
                            RecordModelReference(value, $"condition-model:{subject}/{value}");
                        }
                        if (subject == 15 && !originalEnemySlots)
                        {
                            RequireOriginalSlots($"physical-condition:{subject}/{selector}/{comparison}");
                        }
                        checksLocalValues |= subject is >= 220 and <= 227 || condition.UsesActorSelector && selector is >= 220 and <= 227;
                        bool? known = condition.EvaluateKnownSubject(formation, program.LevelBand);
                        if (known == false)
                        {
                            next = instruction.BranchOffset;
                        }
                        else if (known is null)
                        {
                            pending.Push(instruction.BranchOffset);
                        }
                        break;
                    case 8 when callback != 3 && !originalEnemySlots:
                        MarkUnsupportedService($"opcode:{opcode}");
                        break;
                    case 30:
                        if (instruction[0] != 0)
                        {
                            reachedEffects?.Add(instruction[0]);
                        }
                        if (!actions.Any(action => action is { Command: 8, ActionId: 1, CrisisLevel: null }))
                        {
                            MarkUnsupportedService("graphics-target-flags:8/1");
                        }
                        break;
                    case 42:
                        foreach (BattleNativeAction drawn in actions.Where(action => action is { Command: 2, ActionId: > 0 }))
                        {
                            if (drawn.MagicId != 0)
                            {
                                reachedEffects?.Add(drawn.MagicId);
                            }
                            if (!ExpandedBattleActionRules.SupportsDrawnEnemyMagic(drawn))
                            {
                                MarkUnsupportedService("drawn-magic-contract");
                            }
                        }
                        break;
                    case 27 when instruction[0] >= NativeBattleSlots.EnemyCount:
                    case 29 when instruction[0] != 200 && instruction[0] >= NativeBattleSlots.EnemyCount:
                    case 31 or 52 when instruction[0] >= NativeBattleSlots.FormationSlotCount:
                    case 43 or 58 when !NativeBattleSlots.Contains(instruction[0]):
                    case 53 when instruction[0] != 209 && !NativeBattleSlots.Contains(instruction[0]):
                    case 59 when instruction[0] >= NativeBattleSlots.FormationSlotCount || instruction[1] != 0 && (instruction[1] < NativeBattleSlots.AllyCount || !NativeBattleSlots.Contains(instruction[1])):
                        MarkMalformed($"physical-operand:{opcode}/{instruction[0]}", callback, offset);
                        break;
                    case 27 or 31 or 43 or 44 or 49 or 50 or 51 or 52 or 53 or 54 or 55 or 56 or 57 or 58 or 59 when !originalEnemySlots:
                    case 29 when instruction[0] != 200 && !originalEnemySlots:
                        RequireOriginalSlots($"physical-opcode:{opcode}");
                        break;
                    case 3 or 7:
                        byte command = opcode == 3 ? (byte)2 : (byte)8;
                        RecordActionEffects(command, instruction[0]);
                        if (!SupportsCommand(command, instruction[0]))
                        {
                            MarkUnsupportedService($"command:{command}/{instruction[0]}");
                        }
                        break;
                    case 4:
                        if (instruction[0] == 209 && !originalEnemySlots)
                        {
                            RequireOriginalSlots($"actor-reference-target:{instruction[0]}");
                        }
                        else if (instruction[0] is >= 220 and <= 227)
                        {
                            checksLocalValues = true;
                        }
                        else if (instruction[0] < 200)
                        {
                            RecordModelReference(instruction[0], $"target-model:{instruction[0]}");
                        }
                        break;
                    case 38:
                        if (instruction[1] < 200)
                        {
                            RecordModelReference(instruction[1], $"target-filter-model:{instruction[1]}");
                        }
                        break;
                    case 11 or 12:
                        for (int i = 0; i < instruction.OperandCount; i++)
                        {
                            byte slot = instruction[i];
                            if (opcode == 11 && slot == 253)
                            {
                                continue;
                            }
                            if (slot >= 16)
                            {
                                MarkMalformed($"ability-slot:{slot}", callback, offset);
                                continue;
                            }
                            NativeBattleAiAbility ability = program.Abilities[16 * program.LevelBand + slot];
                            RecordActionEffects(ability.Command, ability.Id);
                            if (!SupportsCommand(ability.Command, ability.Id))
                            {
                                MarkUnsupportedService($"command:{ability.Command}/{ability.Id}");
                            }
                        }
                        break;
                    case 14 or 18:
                        if (instruction[0] is < 220 or > 227)
                        {
                            if (!originalEnemySlots)
                            {
                                RequireOriginalSlots($"physical-variable:{opcode}/{instruction[0]}");
                            }
                        }
                        else if (opcode == 18)
                        {
                            checksLocalValues = true;
                        }
                        break;
                    case 35:
                        next = instruction.BranchOffset;
                        break;
                }
                pending.Push(next);
            }
        }

        void MarkUnsupportedService(string reason)
        {
            blockers?.Add(reason);
            admission = ExpandedAiAdmission.RequiresUnsupportedService;
        }

        void MarkMalformed(string reason, int callback, int offset)
        {
            if (requireValidInstalledSource)
            {
                throw new InvalidDataException($"Formation {formation}, model {rosterModel}, AI callback {callback}, byte {offset}: {reason}.");
            }
            MarkUnsupportedService(reason);
        }

        void RequireOriginalSlots(string reason)
        {
            blockers?.Add(reason);
            if (admission == ExpandedAiAdmission.Supported)
            {
                admission = ExpandedAiAdmission.RequiresOriginalSlots;
            }
        }

        bool SupportsCommand(byte command, ushort id)
        {
            return id == 0 || supported.Contains((command, id));
        }

        void RecordActionEffects(byte command, ushort id)
        {
            if (reachedEffects is null || id == 0)
            {
                return;
            }
            foreach (BattleNativeAction action in actions.Where(action => action.Command == command && action.ActionId == id && action.MagicId != 0))
            {
                reachedEffects.Add(action.MagicId);
            }
        }
    }

    internal static bool SupportsAction(BattleNativeAction action)
    {
        if (action is { Command: 4, KernelSection: 7, AttackType: 4, TargetFlags: byte flags })
        {
            // Enemy AI provides the target and dispatches the Item record without a party inventory reservation.
            return (flags & 3) == 0 && ExpandedBattleActionRules.SupportsContacts(action);
        }
        if (action.Command == 236)
        {
            //48D200/48FE20 share enemy records and formulas with command8. The special graphics route uses the authored placement.
            return action is { KernelSection: 3, TargetFlags: null } && ExpandedBattleActionRules.SupportsEnemyContacts(action);
        }
        return action.Command is 2 or 8 && ExpandedBattleActionRules.SupportsEnemyContacts(action) && action.AttackType is not (5 or 6) &&
            ((action.TargetFlags.GetValueOrDefault() & 3) == 0 || action is { Command: 2, ActionId: 16 } ||
             action is { Command: 8, AttackType: 0, Power: 0, Statuses0: 0, Statuses1: 0, AttackFlags: 3, HitCount: 1 } && (action.TargetFlags.GetValueOrDefault() & 3) == 1);
    }
}
