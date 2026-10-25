using System.Buffers.Binary;
using System.Security.Cryptography;
using Memoria.FF8.Assets;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public sealed record EnemyScenarioFormation(int FormationId, int StageId, int SceneSlot, bool Enabled, bool Hidden, bool Suspended, bool Untargetable, int SceneLevel);
public sealed record EnemyScenarioModel(int ModelId, string Name, NativeBattleAiDefinition Ai);
public sealed record EnemyScenarioActionOwner(int ModelId, string Name, int LevelBand, int AbilitySlot, byte Command, ushort ActionId, byte Sequence, BattleNativeAction? NativeAction, EnemyScenarioFormation[] Formations, string[] MappingEvidence, string[] Blockers);
public sealed record EnemyScenarioScriptedAction(int ModelId, string Name, int Callback, int Offset, int LevelBand, byte? Command, ushort? ActionId, int? NativeEffectId, byte? Sequence, BattleNativeAction? NativeAction, EnemyScenarioFormation[] Formations, string[] MappingEvidence, string[] Blockers);
public sealed record EnemyScenarioDiscoveryResult(EnemyScenarioActionOwner[] Owners, PlanIssue[] Issues)
{
    public EnemyScenarioScriptedAction[] ScriptedActions { get; init; } = [];
}

/// <summary>Joins installed monster ability banks to their exact kernel rows and all scene slots. Table membership does not prove an AI path is reachable.</summary>
public static class ScenarioEnemyDiscovery
{
    public static EnemyScenarioDiscoveryResult Discover(BattleAssets assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        List<PlanIssue> issues = [];
        List<BattleEncounter> formations = [];
        for (int id = 0; id < 1024; id++)
        {
            try
            {
                formations.Add(assets.LoadEncounter(id));
            }
            catch (Exception exception) when (IsResourceFailure(exception))
            {
                issues.Add(new($"scene:{id}", exception.Message));
            }
        }
        List<EnemyScenarioModel> models = [];
        foreach (int modelId in assets.EnemyModelIds)
        {
            try
            {
                // ReadEnemyAi returns the complete three-band, 48-row bank regardless of the requested level.
                models.Add(new(modelId, assets.ReadEnemyName(modelId), assets.ReadEnemyAi(modelId, 0)));
            }
            catch (Exception exception) when (IsResourceFailure(exception))
            {
                issues.Add(new($"enemy-model:{modelId}", exception.Message));
            }
        }
        EnemyScenarioDiscoveryResult result = Discover(formations, models, assets.NativeActions);
        return result with { Issues = issues.Concat(result.Issues).ToArray() };
    }

    public static EnemyScenarioDiscoveryResult Discover(IEnumerable<BattleEncounter> formations, IEnumerable<EnemyScenarioModel> models, IReadOnlyList<BattleNativeAction> actions)
    {
        ArgumentNullException.ThrowIfNull(formations);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(actions);
        Dictionary<int, EnemyScenarioFormation[]> ownersByModel = formations.SelectMany(formation => formation.Slots.Where(slot => slot.IsEnemyModel).Select(slot => new
        {
            slot.ModelId,
            Formation = new EnemyScenarioFormation(formation.Id, formation.StageId, slot.Slot, slot.Enabled, slot.Hidden, slot.InitiallySuspended, slot.Untargetable, slot.Level)
        })).GroupBy(row => row.ModelId).ToDictionary(group => group.Key, group => group.Select(row => row.Formation).OrderBy(row => row.FormationId).ThenBy(row => row.SceneSlot).ToArray());
        Dictionary<(byte Command, ushort ActionId), BattleNativeAction[]> kernel = actions.GroupBy(action => (action.Command, action.ActionId)).ToDictionary(group => group.Key, group => group.ToArray());
        List<EnemyScenarioActionOwner> owners = [];
        List<EnemyScenarioScriptedAction> scriptedActions = [];
        List<PlanIssue> issues = [];
        HashSet<int> scanned = [];
        foreach (EnemyScenarioModel model in models.OrderBy(model => model.ModelId))
        {
            if (!scanned.Add(model.ModelId))
            {
                throw new InvalidDataException($"Duplicate enemy model {model.ModelId}.");
            }
            if (model.Ai.Abilities.Length != 48 || model.Ai.Scripts.Length != 5)
            {
                issues.Add(new($"enemy-model:{model.ModelId}", "Enemy AI requires the original 48 ability rows and five callbacks."));
                continue;
            }
            EnemyScenarioFormation[] contexts = ownersByModel.GetValueOrDefault(model.ModelId, []);
            string[] scriptEvidence = model.Ai.Scripts.Select((script, callback) => $"battle/c0m{model.ModelId:000}.dat:ai-callback:{callback}:sha256:{Convert.ToHexString(SHA256.HashData(script))}").ToArray();
            DiscoverScripts(model, contexts, kernel, scriptedActions, issues);
            for (int index = 0; index < model.Ai.Abilities.Length; index++)
            {
                NativeBattleAiAbility ability = model.Ai.Abilities[index];
                List<string> blockers = ["An installed ability row does not establish AI reachability; an invocation adapter must establish callback, target and state prerequisites."];
                if (contexts.Length == 0)
                {
                    blockers.Add("No scene.out slot references this installed enemy model.");
                }
                BattleNativeAction? native = null;
                if (!kernel.TryGetValue((ability.Command, ability.Id), out BattleNativeAction[]? matches))
                {
                    blockers.Add($"No installed kernel mapping for command {ability.Command}, action {ability.Id}.");
                }
                else if (matches.Length != 1)
                {
                    blockers.Add($"Command {ability.Command}, action {ability.Id} requires an additional kernel variant selector.");
                }
                else
                {
                    native = matches[0];
                }
                List<string> evidence = [$"battle/c0m{model.ModelId:000}.dat:information:ability-row:{index}:band:{index / 16}:slot:{index % 16}:command:{ability.Command}:action:{ability.Id}:sequence:{ability.Sequence}"];
                evidence.AddRange(scriptEvidence);
                if (native is not null)
                {
                    evidence.Add($"main/kernel.bin:section:{native.KernelSection}:row:{native.KernelIndex}:effect:{native.MagicId}:sha256:{Convert.ToHexString(SHA256.HashData(native.RawRecord.AsSpan()))}");
                }
                owners.Add(new(model.ModelId, model.Name, index / 16, index % 16, ability.Command, ability.Id, ability.Sequence, native, contexts, evidence.ToArray(), blockers.ToArray()));
            }
        }
        foreach (int modelId in ownersByModel.Keys.Order().Where(modelId => !scanned.Contains(modelId)))
        {
            issues.Add(new($"enemy-model:{modelId}", "A scene.out slot references an enemy model whose installed ability bank was not discovered."));
        }
        return new(owners.ToArray(), issues.ToArray()) { ScriptedActions = scriptedActions.ToArray() };
    }

    private readonly record struct ScriptState(int Offset, int LevelBand, byte Command, ushort ActionId, byte Sequence);

    private static void DiscoverScripts(EnemyScenarioModel model, EnemyScenarioFormation[] contexts, Dictionary<(byte Command, ushort ActionId), BattleNativeAction[]> kernel, List<EnemyScenarioScriptedAction> output, List<PlanIssue> issues)
    {
        for (int callback = 0; callback < model.Ai.Scripts.Length; callback++)
        {
            byte[] code = model.Ai.Scripts[callback];
            string source = $"battle/c0m{model.ModelId:000}.dat:ai-callback:{callback}:sha256:{Convert.ToHexString(SHA256.HashData(code))}";
            HashSet<ScriptState> visited = [];
            HashSet<(int Offset, int Band, byte? Command, ushort? Id, int? Effect, byte? Sequence, string? Reason)> emitted = [];
            Queue<ScriptState> pending = new(Enumerable.Range(0, 3).Select(band => new ScriptState(0, band, 0, 0, 0)));
            // Empty script banks have no dispatch. Nonempty callbacks must terminate or remain in a bounded control-flow graph.
            while (code.Length != 0 && pending.TryDequeue(out ScriptState state))
            {
                if (!visited.Add(state))
                {
                    continue;
                }
                if (visited.Count > 65536)
                {
                    Unresolved(state, "Script control-flow analysis exceeded 65536 distinct prepared-command states.");
                    break;
                }
                if ((uint)state.Offset >= code.Length)
                {
                    Unresolved(state, "Script branch leaves the installed callback.");
                    continue;
                }
                byte opcode = code[state.Offset];
                int width = InstructionWidth(opcode);
                if (state.Offset > code.Length - width)
                {
                    Unresolved(state, $"Opcode {opcode} requires {width - 1} operand bytes; the callback ends before its operands.");
                    continue;
                }
                int next = state.Offset + width;
                ScriptState continuation = state with { Offset = next };
                switch (opcode)
                {
                    case 0:
                        continue;
                    case 2:
                        int skip = BinaryPrimitives.ReadUInt16LittleEndian(code.AsSpan(state.Offset + 6, 2));
                        pending.Enqueue(continuation with { Offset = next + skip });
                        break;
                    case 3:
                        continuation = continuation with { Command = 2, ActionId = code[state.Offset + 1] };
                        break;
                    case 5:
                        continuation = continuation with { Sequence = code[state.Offset + 1] };
                        break;
                    case 6:
                        EmitPrepared(state, state.Offset);
                        break;
                    case 7:
                        continuation = continuation with { Command = 8, ActionId = code[state.Offset + 1] };
                        break;
                    case 11:
                        foreach (byte choice in code.AsSpan(state.Offset + 1, 3))
                        {
                            if (choice == 253)
                            {
                                pending.Enqueue(continuation);
                            }
                            else
                            {
                                PrepareAbility(state, continuation, choice);
                            }
                        }
                        continue;
                    case 12:
                        PrepareAbility(state, continuation, code[state.Offset + 1]);
                        continue;
                    case 30:
                        Emit(state, 252, 1, code[state.Offset + 1], state.Sequence, null);
                        break;
                    case 35:
                        short displacement = BinaryPrimitives.ReadInt16LittleEndian(code.AsSpan(state.Offset + 1, 2));
                        continuation = continuation with { Offset = next + displacement };
                        break;
                    case 42:
                        Emit(state, 2, null, null, 11, "Draw-cast action depends on the executed drawn-spell byte and source actor inventory.");
                        break;
                }
                pending.Enqueue(continuation);
            }

            void PrepareAbility(ScriptState origin, ScriptState continuation, int slot)
            {
                if ((uint)slot >= 16)
                {
                    Unresolved(origin, $"Opcode {code[origin.Offset]} selects ability slot {slot}, outside the sixteen-row level band.");
                    return;
                }
                NativeBattleAiAbility ability = model.Ai.Abilities[origin.LevelBand * 16 + slot];
                ScriptState prepared = continuation with { Command = ability.Command, ActionId = ability.Id, Sequence = ability.Sequence };
                EmitPrepared(prepared, origin.Offset);
                pending.Enqueue(prepared);
            }

            void EmitPrepared(ScriptState state, int offset)
            {
                // The interpreter suppresses a zero prepared action ID before enqueuing.
                if (state.ActionId != 0)
                {
                    Emit(state with { Offset = offset }, state.Command, state.ActionId, null, state.Sequence, null);
                }
            }

            void Unresolved(ScriptState state, string reason)
            {
                string boundary = (uint)state.Offset < code.Length ? $"opcode:{code[state.Offset]}" : "opcode:outside-callback";
                string message = $"{boundary}: {reason}";
                if (emitted.Add((state.Offset, state.LevelBand, null, null, null, null, message)))
                {
                    output.Add(new(model.ModelId, model.Name, callback, state.Offset, state.LevelBand, null, null, null, null, null, contexts, [source, $"offset:{state.Offset}:{boundary}"], [message]));
                    issues.Add(new($"enemy-model:{model.ModelId}:callback:{callback}:offset:{state.Offset}:band:{state.LevelBand}", message));
                }
            }

            void Emit(ScriptState state, byte command, ushort? id, int? effect, byte sequence, string? reason)
            {
                BattleNativeAction? native = null;
                List<string> blockers = ["Static control-flow membership requires an invocation adapter to establish callback, branch conditions, target and state prerequisites."];
                if (reason is not null)
                {
                    blockers.Add(reason);
                }
                if (command != 252 && id is ushort actionId)
                {
                    if (kernel.TryGetValue((command, actionId), out BattleNativeAction[]? matches) && matches.Length == 1)
                    {
                        native = matches[0];
                        effect = native.MagicId;
                    }
                    else
                    {
                        blockers.Add($"No unique installed kernel mapping for command {command}, action {actionId}.");
                    }
                }
                if (contexts.Length == 0)
                {
                    blockers.Add("No scene.out slot references this installed enemy model.");
                }
                if (!emitted.Add((state.Offset, state.LevelBand, command, id, effect, sequence, reason)))
                {
                    return;
                }
                List<string> evidence = [source, $"offset:{state.Offset}:opcode:{code[state.Offset]}:band:{state.LevelBand}:command:{command}:action:{id}:sequence:{sequence}"];
                if (native is not null)
                {
                    evidence.Add($"main/kernel.bin:section:{native.KernelSection}:row:{native.KernelIndex}:effect:{native.MagicId}:sha256:{Convert.ToHexString(SHA256.HashData(native.RawRecord.AsSpan()))}");
                }
                else if (command == 252)
                {
                    evidence.Add($"direct-graphics:effect-byte:{effect}");
                }
                output.Add(new(model.ModelId, model.Name, callback, state.Offset, state.LevelBand, command, id, effect, sequence, native, contexts, evidence.ToArray(), blockers.ToArray()));
            }
        }
    }

    private static int InstructionWidth(byte opcode) => opcode switch
    {
        2 => 8,
        11 or 45 => 4,
        38 => 5,
        14 or 15 or 17 or 18 or 19 or 21 or 27 or 34 or 35 or 39 or 40 or 59 or 60 => 3,
        1 or 3 or 4 or 5 or 7 or 9 or 12 or 13 or 23 or 24 or 25 or 26 or 28 or 29 or 30 or 31 or 32 or 37 or 43 or 49 or 52 or 53 or 55 or 56 or 58 => 2,
        _ => 1
    };

    private static bool IsResourceFailure(Exception exception) => exception is IOException or InvalidDataException or NotSupportedException or ArgumentException or OverflowException;
}
