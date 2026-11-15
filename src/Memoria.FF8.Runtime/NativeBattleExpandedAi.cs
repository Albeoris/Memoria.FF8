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
        if (program.Scripts.Length != 5 || program.LevelBand is < 0 or > 2 || program.Abilities.Length != 48)
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
        HashSet<int> storedReferences = [];
        List<(int Selector, string Description)> referenceUses = [];
        List<(int Variable, int Comparison, int Value)> localConditions = [];
        List<(int Variable, int Value)> localConstants = [];
        HashSet<int> incrementedLocals = [];
        for (int callback = 0; callback < program.Scripts.Length; callback++)
        {
            CheckScript(program.Scripts[callback], callback);
        }
        foreach ((int selector, string description) in referenceUses)
        {
            if (!originalEnemySlots && !storedReferences.Contains(selector))
            {
                RequireOriginalSlots(description);
            }
        }
        foreach ((int variable, int comparison, int value) in localConditions)
        {
            if (storedReferences.Contains(variable) && (comparison is not (0 or 3) || value < NativeBattleSlots.Count))
            {
                MarkUnsupportedService($"actor-reference-comparison:{variable}/{comparison}/{value}");
            }
        }
        foreach (int variable in storedReferences.Intersect(incrementedLocals))
        {
            MarkUnsupportedService($"actor-reference-arithmetic:{variable}");
        }
        foreach ((int variable, int value) in localConstants)
        {
            if (storedReferences.Contains(variable) && (value & 31) < 16)
            {
                MarkUnsupportedService($"actor-reference-constant:{variable}/{value}");
            }
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
                int pc = path.Offset;
                if ((uint)pc >= script.Length)
                {
                    MarkMalformed("branch-outside-script", callback, pc);
                    continue;
                }
                if (!visited.Add(path))
                {
                    continue;
                }
                int opcode = script[pc++];
                if (opcode == 0)
                {
                    continue;
                }
                int operands = opcode switch
                {
                    6 or 8 or 10 or 16 or 20 or 22 or 33 or 36 or 41 or 42 or 44 or 46 or 47 or 48 or 50 or 51 or 54 or 57 or 61 => 0,
                    1 or 3 or 4 or 5 or 7 or 9 or 12 or 13 or 24 or 25 or 26 or 28 or 32 => 1,
                    14 or 15 or 17 or 18 or 19 or 21 or 34 or 35 or 39 or 40 or 60 => 2,
                    11 or 45 => 3,
                    27 or 59 => 2,
                    23 or 29 or 30 or 31 or 37 or 43 or 49 or 52 or 53 or 55 or 56 or 58 => 1,
                    38 => 4,
                    2 => 7,
                    _ => -1
                };
                if (pc + operands > script.Length)
                {
                    MarkMalformed($"truncated-opcode:{opcode}", callback, path.Offset);
                    continue;
                }
                if (operands < 0)
                {
                    MarkUnsupportedService($"unknown-opcode:{opcode}");
                    continue;
                }
                int next = pc + operands;
                if (opcode is 31 or 52 or 59)
                {
                    reserveSceneSlots?.Add(script[pc]);
                }
                switch (opcode)
                {
                    case 1 or 24 or 26 or 34 when originalEnemySlots && script[pc] >= program.Texts.Length:
                        MarkMalformed($"text-operand:{opcode}/{script[pc]}", callback, path.Offset);
                        break;
                    case 2:
                        int subject = script[pc];
                        int selector = script[pc + 1];
                        int comparison = script[pc + 2];
                        int value = ReadWord(script, pc + 3);
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
                        else if ((subject is 0 or 4 or 7 or >= 220 and <= 227) && selector is >= 220 and <= 227)
                        {
                            referenceUses.Add((selector, $"actor-reference-condition:{subject}/{selector}"));
                        }
                        if (subject is >= 220 and <= 227)
                        {
                            localConditions.Add((subject, comparison, value));
                        }
                        int otherwise = next + ReadWord(script, pc + 5);
                        if (subject is 3 or 14)
                        {
                            next = Compare(subject == 3 ? formation : program.LevelBand, comparison, value) ? next : otherwise;
                        }
                        else
                        {
                            pending.Push(path with { Offset = otherwise });
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
                        else if (!AdmitsDonorEffect(script[pc]))
                        {
                            MarkUnsupportedService($"graphics-effect:{script[pc]}");
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
                    case 27 or 31 or 43 or 44 or 49 or 50 or 51 or 52 or 53 or 54 or 55 or 56 or 57 or 58 or 59 when !originalEnemySlots:
                        RequireOriginalSlots($"physical-opcode:{opcode}");
                        break;
                    case 27 when script[pc] >= NativeBattleSlots.EnemyCount:
                        MarkMalformed($"physical-operand:{opcode}/{script[pc]}", callback, path.Offset);
                        break;
                    case 29:
                        if (script[pc] != 200 && !originalEnemySlots)
                        {
                            RequireOriginalSlots($"physical-opcode:{opcode}");
                        }
                        else if (script[pc] != 200 && script[pc] >= NativeBattleSlots.EnemyCount)
                        {
                            MarkMalformed($"physical-operand:{opcode}/{script[pc]}", callback, path.Offset);
                        }
                        break;
                    case 31 or 52 when script[pc] >= NativeBattleSlots.FormationSlotCount:
                    case 43 or 58 when !NativeBattleSlots.Contains(script[pc]):
                    case 53 when script[pc] != 209 && !NativeBattleSlots.Contains(script[pc]):
                    case 59 when script[pc] >= NativeBattleSlots.FormationSlotCount || script[pc + 1] != 0 && (script[pc + 1] < NativeBattleSlots.AllyCount || !NativeBattleSlots.Contains(script[pc + 1])):
                        MarkMalformed($"physical-operand:{opcode}", callback, path.Offset);
                        break;
                    case 6:
                        if (path.MayTargetParty && path.PreparedEjection)
                        {
                            RecordPartyDeparture(callback, path.Offset, opcode);
                        }
                        break;
                    case 3 or 7:
                        byte command = opcode == 3 ? (byte)2 : (byte)8;
                        path = path with { PreparedEjection = PreparesEjection(command, script[pc]) };
                        if (!SupportsCommand(command, script[pc]))
                        {
                            MarkUnsupportedService($"command:{command}/{script[pc]}");
                        }
                        break;
                    case 4:
                        if (rosterRestrictions is not null)
                        {
                            path = path with { MayTargetParty = SelectorMayTargetParty(script[pc]) };
                        }
                        if (script[pc] == 209 && !originalEnemySlots)
                        {
                            RequireOriginalSlots($"actor-reference-target:{script[pc]}");
                        }
                        else if (script[pc] is >= 220 and <= 227)
                        {
                            referenceUses.Add((script[pc], $"actor-reference-target:{script[pc]}"));
                        }
                        else if (script[pc] < 200)
                        {
                            RecordModelReference(script[pc], $"target-model:{script[pc]}");
                        }
                        break;
                    case 38:
                        if (script[pc + 1] < 200)
                        {
                            RecordModelReference(script[pc + 1], $"target-filter-model:{script[pc + 1]}");
                        }
                        if (rosterRestrictions is not null)
                        {
                            // The packed ALL form can resolve to the party group when the filter is empty.
                            path = path with { MayTargetParty = script[pc] != 0 || script[pc + 1] != 201 };
                        }
                        break;
                    case 43 when rosterRestrictions is not null:
                        path = path with { MayTargetParty = script[pc] < NativeBattleSlots.AllyCount };
                        break;
                    case 11 or 12:
                        for (int i = 0; i < operands; i++)
                        {
                            byte slot = script[pc + i];
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
                        if (script[pc] is < 220 or > 227)
                        {
                            if (script[pc + 1] == 203)
                            {
                                MarkUnsupportedService($"actor-reference-write:{opcode}/{script[pc]}/{script[pc + 1]}");
                            }
                            else if (!originalEnemySlots)
                            {
                                RequireOriginalSlots($"physical-variable:{opcode}/{script[pc]}");
                            }
                        }
                        else if (script[pc + 1] == 203 && (opcode != 14 || callback != 2))
                        {
                            MarkUnsupportedService($"actor-reference-write:{opcode}/{script[pc]}/{script[pc + 1]}");
                        }
                        else if (script[pc + 1] == 203)
                        {
                            storedReferences.Add(script[pc]);
                        }
                        else if (opcode == 18)
                        {
                            incrementedLocals.Add(script[pc]);
                        }
                        else
                        {
                            localConstants.Add((script[pc], script[pc + 1]));
                        }
                        break;
                    case 35:
                        next += unchecked((short)ReadWord(script, pc));
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

    private static int ReadWord(byte[] script, int offset) => script[offset] | script[offset + 1] << 8;

    private static bool Compare(int left, int comparison, int right) => comparison switch
    {
        0 => left == right,
        1 => left < right,
        2 => left > right,
        3 => left != right,
        4 => left <= right,
        5 => left >= right,
        _ => false
    };
}
