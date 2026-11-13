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
    public static ExpandedAiAdmission Evaluate(NativeBattleAiDefinition program, IReadOnlyList<BattleNativeAction> actions, int formation, ICollection<string>? blockers = null)
    {
        if (program.Scripts.Length != 5 || program.LevelBand is < 0 or > 2 || program.Abilities.Length != 48)
        {
            blockers?.Add("program-layout");
            return ExpandedAiAdmission.RequiresUnsupportedService;
        }
        HashSet<(byte Command, ushort Id)> supported = actions.Where(SupportsAction).Select(action => (action.Command, action.ActionId)).ToHashSet();
        NativeBattleAiAbility berserk = program.Abilities[16 * program.LevelBand];
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
            if (!storedReferences.Contains(selector))
            {
                MarkUnsupportedService(description);
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

        void CheckScript(byte[] script, int callback)
        {
            if (script.Length == 0)
            {
                return;
            }
            HashSet<int> visited = [];
            Stack<int> pending = new();
            pending.Push(0);
            while (pending.TryPop(out int pc))
            {
                if ((uint)pc >= script.Length)
                {
                    MarkUnsupportedService("branch-outside-script");
                    continue;
                }
                if (!visited.Add(pc))
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
                    MarkUnsupportedService($"truncated-opcode:{opcode}");
                    continue;
                }
                if (operands < 0)
                {
                    MarkUnsupportedService($"unknown-opcode:{opcode}");
                    continue;
                }
                int next = pc + operands;
                switch (opcode)
                {
                    case 2:
                        int subject = script[pc];
                        int selector = script[pc + 1];
                        int comparison = script[pc + 2];
                        int value = ReadWord(script, pc + 3);
                        if (subject == 15 || subject == 10 && selector == 203 && comparison is not (0 or 3))
                        {
                            admission = ExpandedAiAdmission.RequiresOriginalSlots;
                            blockers?.Add($"physical-condition:{subject}/{selector}/{comparison}");
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
                            pending.Push(otherwise);
                        }
                        break;
                    case 8 when callback != 3:
                    case 30 or 44 or 49 or 50 or 51 or 54 or 55 or 56 or 57 or 61:
                        MarkUnsupportedService($"opcode:{opcode}");
                        break;
                    case 42:
                        if (actions.Any(action => action is { Command: 2, ActionId: > 0 } && !ExpandedBattleActionRules.SupportsDrawnEnemyMagic(action)))
                        {
                            MarkUnsupportedService("drawn-magic-contract");
                        }
                        break;
                    case 27 or 31 or 43 or 52 or 53 or 58 or 59:
                        admission = ExpandedAiAdmission.RequiresOriginalSlots;
                        blockers?.Add($"physical-opcode:{opcode}");
                        break;
                    case 29:
                        if (script[pc] != 200)
                        {
                            admission = ExpandedAiAdmission.RequiresOriginalSlots;
                            blockers?.Add($"physical-opcode:{opcode}");
                        }
                        break;
                    case 3 or 7:
                        byte command = opcode == 3 ? (byte)2 : (byte)8;
                        if (!SupportsCommand(command, script[pc]))
                        {
                            MarkUnsupportedService($"command:{command}/{script[pc]}");
                        }
                        break;
                    case 4:
                        if (script[pc] == 209)
                        {
                            MarkUnsupportedService($"actor-reference-target:{script[pc]}");
                        }
                        else if (script[pc] is >= 220 and <= 227)
                        {
                            referenceUses.Add((script[pc], $"actor-reference-target:{script[pc]}"));
                        }
                        break;
                    case 11 or 12:
                        for (int i = 0; i < operands; i++)
                        {
                            byte slot = script[pc + i];
                            if (opcode == 11 && slot == 253)
                            {
                                continue;
                            }
                            if (slot >= 16)
                            {
                                MarkUnsupportedService($"ability-slot:{slot}");
                                continue;
                            }
                            NativeBattleAiAbility ability = program.Abilities[16 * program.LevelBand + slot];
                            if (!SupportsCommand(ability.Command, ability.Id))
                            {
                                MarkUnsupportedService($"command:{ability.Command}/{ability.Id}");
                            }
                        }
                        break;
                    case 14 or 18:
                        if (script[pc] is < 220 or > 227 || script[pc + 1] == 203 && (opcode != 14 || callback != 2))
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
                pending.Push(next);
            }
        }

        void MarkUnsupportedService(string reason)
        {
            blockers?.Add(reason);
            if (admission == ExpandedAiAdmission.Supported)
            {
                admission = ExpandedAiAdmission.RequiresUnsupportedService;
            }
        }

        bool SupportsCommand(byte command, ushort id)
        {
            return id == 0 || supported.Contains((command, id));
        }
    }

    internal static bool SupportsAction(BattleNativeAction action)
    {
        return action.Command is 2 or 8 && ExpandedBattleActionRules.SupportsEnemyContacts(action) && action.AttackType is not (5 or 6) &&
            ((action.TargetFlags.GetValueOrDefault() & 3) == 0 || action is { Command: 2, ActionId: 16 }) && (action.Command != 8 || (action.AttackFlags & 0x80) == 0);
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
