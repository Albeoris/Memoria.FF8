using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum ExpandedAiAdmission
{
    Supported,
    RequiresUnsupportedService,
    RequiresOriginalSlots
}

/// <summary>Checks reachable original AI bytecode against the services available in expanded battles.</summary>
public static class NativeBattleExpandedAi
{
    private readonly record struct ScriptPath(int Offset, bool MayTargetParty, bool PreparedEjection);

    public static ExpandedAiAdmission Evaluate(NativeBattleAiDefinition program, IReadOnlyList<BattleNativeAction> actions, int formation, ICollection<string>? blockers = null, bool originalEnemySlots = false, ICollection<string>? explicitModelReferences = null, ISet<int>? reserveSceneSlots = null, ISet<int>? explicitEnemyModelReferences = null, int? rosterModel = null, ISet<BattleEncounterRosterRestrictionReason>? rosterRestrictions = null, Func<ushort, bool>? admitsDonorEffect = null)
    {
        return Evaluate(program, actions, formation, blockers, originalEnemySlots, explicitModelReferences, reserveSceneSlots, explicitEnemyModelReferences, rosterModel, rosterRestrictions, admitsDonorEffect, default);
    }

    internal static ExpandedAiAdmission EvaluateRebound(NativeBattleAiDefinition program, IReadOnlyList<BattleNativeAction> actions, int formation, IReadOnlyList<uint> numbers, IReadOnlyDictionary<int, string> references)
    {
        return Evaluate(program, actions, formation, null, false, null, null, null, null, null, null, NativeBattleAiLocalFlow.RetainedLocals(numbers, references));
    }

    private static ExpandedAiAdmission Evaluate(NativeBattleAiDefinition program, IReadOnlyList<BattleNativeAction> actions, int formation, ICollection<string>? blockers, bool originalEnemySlots, ICollection<string>? explicitModelReferences, ISet<int>? reserveSceneSlots, ISet<int>? explicitEnemyModelReferences, int? rosterModel, ISet<BattleEncounterRosterRestrictionReason>? rosterRestrictions, Func<ushort, bool>? admitsDonorEffect, ImmutableArray<NativeBattleAiLocalFlow.Value> retainedLocals)
    {
        if (program.Scripts is not { Length: 5 } || program.Scripts.Any(script => script is null) ||
            program.LevelBand is < 0 or > 2 || program.Abilities is not { Length: 48 } || program.Abilities.Any(ability => ability is null) || program.Texts is null)
        {
            if (rosterRestrictions is not null)
            {
                throw new InvalidDataException($"Formation {formation}, model {rosterModel} has an invalid AI program layout.");
            }
            blockers?.Add("program-layout");
            return ExpandedAiAdmission.RequiresUnsupportedService;
        }
        HashSet<(byte Command, ushort Id)> supported = actions.Where(action => SupportsAction(action, originalEnemySlots) && AdmitsDonorEffect(action.MagicId)).Select(action => (action.Command, action.ActionId)).ToHashSet();
        HashSet<(byte Command, ushort Id)>? ejections = rosterRestrictions is null
            ? null : actions.Where(AppliesEject).Select(action => (action.Command, action.ActionId)).ToHashSet();
        NativeBattleAiAbility berserk = program.Abilities[16 * program.LevelBand];
        if (PreparesEjection(berserk.Command, berserk.Id))
        {
            RecordPartyDeparture(1, null, null);
        }
        ExpandedAiAdmission admission = SupportsCommand(berserk.Command, berserk.Id) ? ExpandedAiAdmission.Supported : ExpandedAiAdmission.RequiresUnsupportedService;
        if (admission != ExpandedAiAdmission.Supported)
        {
            blockers?.Add($"command:{berserk.Command}/{berserk.Id}");
        }
        bool checksLocalValues = false;
        int referenceWrites = 0;
        for (int callback = 0; callback < program.Scripts.Length; callback++)
        {
            CheckScript(program.Scripts[callback], callback);
        }
        if (checksLocalValues && (!originalEnemySlots || referenceWrites != 0))
        {
            NativeBattleAiLocalFlow.Check(program, formation, originalEnemySlots, referenceWrites, retainedLocals, RequireOriginalSlots, MarkUnsupportedService);
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
            HashSet<ScriptPath> visited = [];
            Stack<ScriptPath> pending = new();
            pending.Push(new(0, false, false));
            while (pending.TryPop(out ScriptPath path))
            {
                if (!visited.Add(path))
                {
                    continue;
                }
                if (!NativeBattleAiInstruction.TryDecode(script, path.Offset, out NativeBattleAiInstruction instruction, out string? decodeError))
                {
                    MarkMalformed(decodeError, callback, path.Offset);
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
                }
                switch (opcode)
                {
                    case 1 or 24 or 26 or 34 when originalEnemySlots && instruction[0] >= program.Texts.Length:
                        MarkMalformed($"text-operand:{opcode}/{instruction[0]}", callback, path.Offset);
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
                        if (subject == 15 && !originalEnemySlots || subject == 10 && selector == 203 && comparison is not (0 or 3))
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
                            pending.Push(path with { Offset = instruction.BranchOffset });
                        }
                        break;
                    case 8 when callback != 3 && !originalEnemySlots:
                        MarkUnsupportedService($"opcode:{opcode}");
                        break;
                    case 30:
                        if (!actions.Any(action => action is { Command: 8, ActionId: 1, CrisisLevel: null }))
                        {
                            MarkUnsupportedService("graphics-target-flags:8/1");
                        }
                        else if (!AdmitsDonorEffect(instruction[0]))
                        {
                            MarkUnsupportedService($"graphics-effect:{instruction[0]}");
                        }
                        break;
                    case 42:
                        if (actions.Any(action => action is { Command: 2, ActionId: > 0 } && (!ExpandedBattleActionRules.SupportsDrawnEnemyMagic(action) || !AdmitsDonorEffect(action.MagicId))))
                        {
                            MarkUnsupportedService("drawn-magic-contract");
                        }
                        if (rosterRestrictions is not null && actions.Any(action => IsPotentialPartyDrawnEjection(action)))
                        {
                            RecordPartyDeparture(callback, path.Offset, opcode);
                        }
                        break;
                    case 27 when instruction[0] >= NativeBattleSlots.EnemyCount:
                    case 29 when instruction[0] != 200 && instruction[0] >= NativeBattleSlots.EnemyCount:
                    case 31 or 52 when instruction[0] >= NativeBattleSlots.FormationSlotCount:
                    case 43 or 58 when !NativeBattleSlots.Contains(instruction[0]):
                    case 53 when instruction[0] != 209 && !NativeBattleSlots.Contains(instruction[0]):
                    case 59 when instruction[0] >= NativeBattleSlots.FormationSlotCount || instruction[1] != 0 && (instruction[1] < NativeBattleSlots.AllyCount || !NativeBattleSlots.Contains(instruction[1])):
                        MarkMalformed($"physical-operand:{opcode}/{instruction[0]}", callback, path.Offset);
                        break;
                    case 27 or 31 or 43 or 44 or 49 or 50 or 51 or 52 or 53 or 54 or 55 or 56 or 57 or 58 or 59 when !originalEnemySlots:
                    case 29 when instruction[0] != 200 && !originalEnemySlots:
                        RequireOriginalSlots($"physical-opcode:{opcode}");
                        break;
                    case 6:
                        if (path.MayTargetParty && path.PreparedEjection)
                        {
                            RecordPartyDeparture(callback, path.Offset, opcode);
                        }
                        break;
                    case 3 or 7:
                        byte command = opcode == 3 ? (byte)2 : (byte)8;
                        path = path with { PreparedEjection = PreparesEjection(command, instruction[0]) };
                        if (!SupportsCommand(command, instruction[0]))
                        {
                            MarkUnsupportedService($"command:{command}/{instruction[0]}");
                        }
                        break;
                    case 4:
                        if (rosterRestrictions is not null)
                        {
                            path = path with { MayTargetParty = SelectorMayTargetParty(instruction[0]) };
                        }
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
                        if (rosterRestrictions is not null)
                        {
                            // The packed ALL form can resolve to the party group when the filter is empty.
                            path = path with { MayTargetParty = instruction[0] != 0 || instruction[1] != 201 };
                        }
                        break;
                    case 43 when rosterRestrictions is not null:
                        path = path with { MayTargetParty = instruction[0] < NativeBattleSlots.AllyCount };
                        break;
                    case 11 or 12:
                        for (int i = 0; i < instruction.OperandCount; i++)
                        {
                            byte slot = instruction[i];
                            if (opcode == 11 && slot == 253)
                            {
                                if (rosterRestrictions is not null)
                                {
                                    pending.Push(path with { Offset = next });
                                }
                                continue;
                            }
                            if (slot >= 16)
                            {
                                MarkMalformed($"ability-slot:{slot}", callback, path.Offset);
                                continue;
                            }
                            NativeBattleAiAbility ability = program.Abilities[16 * program.LevelBand + slot];
                            if (!SupportsCommand(ability.Command, ability.Id))
                            {
                                MarkUnsupportedService($"command:{ability.Command}/{ability.Id}");
                            }
                            if (rosterRestrictions is not null)
                            {
                                bool ejects = PreparesEjection(ability.Command, ability.Id);
                                if (path.MayTargetParty && ejects)
                                {
                                    RecordPartyDeparture(callback, path.Offset, opcode);
                                }
                                pending.Push(path with { Offset = next, PreparedEjection = ejects });
                            }
                        }
                        if (rosterRestrictions is not null)
                        {
                            continue;
                        }
                        break;
                    case 14 or 18:
                        if (instruction[0] is < 220 or > 227)
                        {
                            if (instruction[1] == 203)
                            {
                                MarkUnsupportedService($"actor-reference-write:{opcode}/{instruction[0]}/{instruction[1]}");
                            }
                            else if (!originalEnemySlots)
                            {
                                RequireOriginalSlots($"physical-variable:{opcode}/{instruction[0]}");
                            }
                        }
                        else if (instruction[1] == 203 && (opcode != 14 || callback != 2))
                        {
                            MarkUnsupportedService($"actor-reference-write:{opcode}/{instruction[0]}/{instruction[1]}");
                        }
                        else if (instruction[1] == 203)
                        {
                            referenceWrites |= 1 << (instruction[0] - 220);
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
                pending.Push(path with { Offset = next });
            }
        }

        void MarkUnsupportedService(string reason)
        {
            blockers?.Add(reason);
            admission = ExpandedAiAdmission.RequiresUnsupportedService;
        }

        void MarkMalformed(string reason, int callback, int offset)
        {
            if (rosterRestrictions is not null)
            {
                throw new InvalidDataException($"Formation {formation}, model {rosterModel}, AI callback {callback}, byte {offset}: {reason}.");
            }
            MarkUnsupportedService(reason);
        }

        bool PreparesEjection(byte command, ushort id) => id != 0 && ejections?.Contains((command, id)) == true;

        void RecordPartyDeparture(int callback, int? offset, int? opcode)
        {
            rosterRestrictions?.Add(new(BattleEncounterRosterRestriction.ScriptedPartyMembership, rosterModel, program.LevelBand, callback, offset, opcode));
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

        bool AdmitsDonorEffect(ushort effect) => originalEnemySlots || admitsDonorEffect?.Invoke(effect) != false;
    }

    private static bool AppliesEject(BattleNativeAction action)
    {
        return !action.PresentationOnly && action.CrisisLevel is null && action.HitCount > 0 &&
            action.AttackType is not (0 or 3 or 4 or 5 or 6 or 21 or 25 or 32) && (action.Statuses1 & 0x10000) != 0;
    }

    private static bool IsPotentialPartyDrawnEjection(BattleNativeAction action)
    {
        if (action is not { Command: 2, ActionId: > 0 and < 64, TargetFlags: byte flags } || !AppliesEject(action))
        {
            return false;
        }
        return (flags & 0x30) switch
        {
            0x20 => (flags & 2) == 0,
            0x30 => (action.ActionId & 7) != 0,
            _ => (flags & 0x40) != 0
        };
    }

    private static bool SelectorMayTargetParty(int selector) => selector is not (>= 16 and < 201 or 202 or 205 or 207 or 209);

    internal static bool SupportsAction(BattleNativeAction action, bool originalEnemySlots)
    {
        if (action is { Command: 4, KernelSection: 7, AttackType: 4, TargetFlags: byte flags })
        {
            // Enemy AI provides the target and dispatches the Item record without a party inventory reservation.
            return (flags & 3) == 0 && ExpandedBattleActionRules.SupportsContacts(action);
        }
        return action.Command is 2 or 8 && ExpandedBattleActionRules.SupportsEnemyContacts(action) && action.AttackType is not (5 or 6) &&
            ((action.TargetFlags.GetValueOrDefault() & 3) == 0 || action is { Command: 2, ActionId: 16 } ||
             originalEnemySlots && action is { Command: 8, AttackType: 0, Power: 0, Statuses0: 0, Statuses1: 0, AttackFlags: 3, HitCount: 1 } && (action.TargetFlags.GetValueOrDefault() & 3) == 1);
    }
}
