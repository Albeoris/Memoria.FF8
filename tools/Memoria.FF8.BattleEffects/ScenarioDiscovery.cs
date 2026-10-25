using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Albeoris.Games.FF8.KernelBin;
using Albeoris.Games.FF8.TextEncoding;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public sealed record DiscoveredBinding(int EffectId, uint Entry, uint Initializer, int[] SharedBindingRows, string Disposition, string Reason, string[] ScenarioIds)
{
    public ImplementationBindingEvidence? Implementation { get; init; }
}
public sealed record DiscoveredAction(string Id, string Family, BattleNativeAction Native, string MappingEvidence)
{
    public int? MappedEffectId { get; init; }
}
public sealed record DiscoveredCommand(int Command, string? Name, int AbilityId, string[] ActionIds, string Disposition, string Reason);
public sealed record DiscoveredScenario(string CaseId, int? EffectId, string ActionId, string Family, string Status, string Disposition, bool CampaignAcceptance, string? DiagnosticRecipe, int? FormationId, int? StageId, string Caster, string? CommandOwner, byte? Command, int? NativeActionId, byte? CrisisLevel, byte? TargetFlags, string TargetPolicy, string[] Preparation, string[] Invocation, int Executions, string[] RepeatPolicy, string[] MappingEvidence, string[] ExecutionBlockers, string[] AcceptanceBlockers, string[] OwnerContextIds)
{
    public string[] CoveredByCaseIds { get; init; } = [];
}
public sealed record CompiledImplementationSummary(string EvidenceFile, string Scope, string DispatcherIlSha256, string RuntimeAssemblySha256, int DirectRegistrations, int BindingRowsWithObservedDirectBranch, int BindingRowsUnknown, int OpaqueCalledMethods, bool PlayableSupportEstablished);
public sealed record InstalledScenarioInventory(int SchemaVersion, string ExecutableSha256, string KernelSha256, string SceneSha256, DateTimeOffset CreatedUtc, DiscoveredBinding[] Bindings, DiscoveredAction[] Actions, DiscoveredCommand[] Commands, PlanIssue[] Issues)
{
    public CompiledImplementationSummary? ImplementationSummary { get; init; }
}
public sealed record ScenarioDiscoverySummary(int BindingRows, int NativeActions, int EnemyAbilityRows, int Scenarios, int RunnableDiagnostics, int BlockedScenarios, int ExcludedBindingRows, bool CampaignAcceptance, string OutputDirectory);

/// <summary>Installed-data planning only. A runnable diagnostic is distinct from accepted campaign coverage.</summary>
public static partial class ScenarioDiscovery
{
    private const string PhaseBlocker = "FINAL_PHASE_PROVENANCE: direct initialized formation participants and opening completion do not certify final-Ultimecia progression/phase initialization, subsidiary allocation and required AI state.";

    public static ScenarioDiscoverySummary Generate(string gameDirectory, string outputDirectory, string? canonicalTemplatePath = null, string? sourceCommit = null, string? sourcePatchHash = null)
    {
        string output = Path.GetFullPath(outputDirectory);
        string gameRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(gameDirectory));
        if (output.Equals(gameRoot, StringComparison.OrdinalIgnoreCase) || output.StartsWith(gameRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Discovery output must be outside the installed game directory.");
        }
        if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
        {
            throw new IOException("Discovery needs a new empty revision directory; earlier inventories remain immutable.");
        }
        // Reuse the store's repository/worktree/reparse-point boundary before reading or writing derived data.
        EvidenceStore outputBoundary = new(output);
        byte[] executable = File.ReadAllBytes(Path.Combine(gameDirectory, "FF8_EN.exe"));
        NativeBattleEffectBinding[] bindings = NativeBattleEffectCatalogReader.Read(executable).ToArray();
        ImplementationDiscoveryEvidence implementation = ScenarioImplementationDiscovery.Discover(bindings);
        Dictionary<int, ImplementationBindingEvidence> implementationByEffect = implementation.Bindings.ToDictionary(binding => binding.EffectId);
        using IResourceSource source = GameDataSourceFactory.Open(gameDirectory);
        ResourceService resources = new(source);
        byte[] kernelBytes = resources.Read(new ResourceKey("main/kernel.bin"));
        byte[] initialBytes = resources.Read(new ResourceKey("main/init.out"));
        NativeCharacterVitals initialVitals = NativeCharacterInitialization.Read(initialBytes, kernelBytes);
        NativeBattleExperienceDefinition experienceDefinition = NativeBattleExperienceDefinitionReader.Read(kernelBytes);
        KernelData kernel = KernelData.ReadFromArray(kernelBytes, FF8Encoding.CreateEuropean());
        using BattleAssets assets = new(gameDirectory, resources);
        List<BuildFile> resourceFiles = [new("FF8_EN.exe", executable.LongLength, Hash(executable)), new("main/kernel.bin", kernelBytes.LongLength, Hash(kernelBytes))];
        byte[] sceneBytes = resources.Read(new ResourceKey("battle/scene.out"));
        resourceFiles.Add(new("battle/scene.out", sceneBytes.LongLength, Hash(sceneBytes)));
        foreach (int model in assets.EnemyModelIds)
        {
            string key = $"battle/c0m{model:000}.dat";
            byte[] bytes = resources.Read(new ResourceKey(key));
            resourceFiles.Add(new(key, bytes.LongLength, Hash(bytes)));
        }
        EnemyScenarioDiscoveryResult enemies = ScenarioEnemyDiscovery.Discover(assets);
        DiscoveredAction[] actions = assets.NativeActions.Select(action => new DiscoveredAction(ActionId(action), Family(action), action, $"main/kernel.bin:section:{action.KernelSection}:row:{action.KernelIndex}:sha256:{Hash(action.RawRecord.ToArray())}" + (action.SupplementalKernelIndex is int supplemental ? $":supplemental-row:{supplemental}:sha256:{Hash(action.SupplementalRecord.ToArray())}" : "") + (action.KernelSection == 7 && action.AttackType == 14 ? "; independent-GF item effect word is a kernel9/command244 action index; PocketStation selection is dynamic; BattleSession.IndependentGuardianForces.cs:PrepareIndependentGuardianItem" : "")) { MappedEffectId = ResolveCatalogEffect(action, assets.NativeActions) }).ToArray();
        List<DiscoveredScenario> scenarios = [];
        Dictionary<string, string> candidates = ReadCandidateRecipes(canonicalTemplatePath);
        foreach (KeyValuePair<string, string> candidate in candidates)
        {
            if (!actions.Any(action => action.Native.Command == 2 && $"{action.Native.ActionId}:{action.Native.MagicId}" == candidate.Key))
            {
                throw new InvalidDataException("Candidate recipe action/effect identity disagrees with the installed kernel.");
            }
        }
        string? templatePath = candidates.Values.FirstOrDefault();
        Directory.CreateDirectory(output);
        Dictionary<(int ModelId, int SceneLevel, int PartyLevel), int[]> possibleEnemyBands = [];
        Dictionary<int, NativeCharacterVitals> preparedLevels = [];
        foreach (DiscoveredAction action in actions)
        {
            BattleNativeAction native = action.Native;
            int? mappedEffect = action.MappedEffectId;
            string caseId = "action-" + action.Id;
            string? recipePath = null;
            List<string> blockers = ActionBlockers(native);
            string targetPolicy = TargetPolicy(native);
            bool ordinaryMagic = native.Command == 2 && native.ActionId is >= 1 and <= 63 && native.MagicId is >= 1 and <= 400 && native.TargetFlags is not null;
            BattleActionDefinition? definition = assets.ActionDefinitions.SingleOrDefault(definition => definition.Ability == BattleAbility.Magic && definition.Id == native.ActionId);
            ordinaryMagic &= definition is not null;
            bool requiresRevival = native.AttackType is 5 or 6;
            if (ordinaryMagic && !requiresRevival && templatePath is not null)
            {
                string sourceRecipe = candidates.GetValueOrDefault($"{native.ActionId}:{native.MagicId}") ?? templatePath;
                JsonObject recipe = JsonNode.Parse(File.ReadAllText(sourceRecipe))!.AsObject();
                bool partyTarget = definition!.Allies;
                ushort targetMask = NativeBattleLimitRules.PlayerTargetMask(native.TargetFlags!.Value, partyTarget ? 0 : 3);
                recipe["driver"]!["input"]!["action_id"] = native.ActionId;
                recipe["driver"]!["input"]!["target_id"] = partyTarget ? "party-0" : "enemy-6";
                recipe["driver"]!["expected_effect"] = native.MagicId;
                recipe["driver"]!["expected_native_action"] = native.ActionId;
                recipe["driver"]!["expected_native_command"] = native.Command;
                recipe["fixture"]!["characters"]![0]!["stocks"]![0]!["action"] = native.ActionId;
                int preparedHitPoints = recipe["fixture"]!["characters"]![0]!["hit_points"]!.GetValue<int>();
                recipe["fixture"]!["characters"]![0]!["hit_points"] = partyTarget && native.AttackType is 3 or 32 ? 1 : preparedHitPoints;
                string copied = Path.Combine(output, "recipes", caseId + ".json");
                Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
                File.WriteAllText(copied, recipe.ToJsonString(RecordJson.Options));
                recipePath = copied;
                blockers.Clear();
                targetPolicy = "Installed BattleActionDefinition.Allies and NativeBattleLimitRules.PlayerTargetMask: target=" + recipe["driver"]!["input"]!["target_id"]!.GetValue<string>() + $"; mask=0x{targetMask:X4}; template-sha256={Hash(File.ReadAllBytes(sourceRecipe))}; initialHP derives from the verified imported template's declared preparation.";
            }
            if (ordinaryMagic && requiresRevival)
            {
                blockers.Clear();
                blockers.Add("REVIVAL_REPEAT_PRECONDITION: a legal KO party recipient and a narrow typed rearm to recreate KO after execution one are unavailable; current typed fixture accepts positive HP only. Do not target a living enemy or kill Squall merely to fabricate invocation.");
            }
            if (recipePath is null && templatePath is not null && native.KernelSection is 2 or 7 or 10)
            {
                blockers.Clear();
                if (TryPrepareOrdinaryPlayerRecipe(assets, initialVitals, native, templatePath, out JsonObject? ordinaryRecipe, out string? ordinaryBlocker))
                {
                    string copied = Path.Combine(output, "recipes", caseId + ".json");
                    Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
                    File.WriteAllText(copied, ordinaryRecipe!.ToJsonString(RecordJson.Options));
                    recipePath = copied;
                    targetPolicy = "Installed ordinary command/semantic action and shared target rules; actual target=" + ordinaryRecipe["driver"]!["input"]!["target_id"]!.GetValue<string>();
                }
                else
                {
                    blockers.Add(ordinaryBlocker ?? "PLAYER_COMMAND_PRECONDITION: exact ordinary command prerequisites remain unresolved.");
                }
            }
            if (recipePath is null && templatePath is not null && native.KernelSection == 19 && native.Command == 15)
            {
                blockers.Clear();
                if (TryPrepareBlueMagicRecipe(assets, initialVitals, native, templatePath, out JsonObject? blueRecipe, out string? blueBlocker))
                {
                    string copied = Path.Combine(output, "recipes", caseId + ".json");
                    Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
                    File.WriteAllText(copied, blueRecipe!.ToJsonString(RecordJson.Options));
                    recipePath = copied;
                    targetPolicy = "Diagnostic borrowed original Blue Magic command owner; shared installed target flags; target=" + blueRecipe["driver"]!["input"]!["target_id"]!.GetValue<string>();
                }
                else
                {
                    blockers.Add(blueBlocker ?? "BLUE_MAGIC_PRECONDITION: original crisis/learned/target prerequisites remain unresolved.");
                }
            }
            bool enemy = native.KernelSection == 3;
            string[] ownerIds = enemies.Owners.Where(owner => owner.Command == native.Command && owner.ActionId == native.ActionId).Select(OwnerId).ToArray();
            if (enemy && ownerIds.Length == 0)
            {
                blockers.Add("OWNER_UNRESOLVED: no installed DAT ability-bank owner was found for this exact command/action variant; do not substitute a generic monster.");
            }
            scenarios.Add(new(caseId, mappedEffect, action.Id, action.Family, recipePath is null ? "PLANNED_BLOCKED" : "RUNNABLE", recipePath is null ? "unresolved" : "scheduled", false, recipePath, enemy ? null : 982, enemy ? null : 142, enemy ? "Actual installed owner; see owner_context_ids" : "Squall", native.CharacterId?.ToString(System.Globalization.CultureInfo.InvariantCulture), native.Command, native.ActionId, native.CrisisLevel, native.TargetFlags, targetPolicy, Preparation(native), [$"native command={native.Command}; action={native.ActionId}; kernel={native.KernelSection}/{native.KernelIndex}; crisis={native.CrisisLevel?.ToString() ?? "not applicable"}", recipePath is null ? "Invocation adapter/prerequisites must be resolved before execution." : "Submit the copied recipe's declared semantic input and typed adapters twice."], 2, ["One uninterrupted BattleSession and owner lineage; no snapshot restore/reseed/allocator reset.", "No generic between-cast repair; terminal/KO/stock/trigger prerequisites require a proved typed repair if needed."], [action.MappingEvidence, "BattleActionKernelReader.Native.cs:ReadNativeActions; BattleSession.NativeActionAdmission.cs:BuildNativeActionHeaderPreparation"], blockers.Distinct().ToArray(), enemy ? ["ENEMY_REACHABILITY: table membership is not proof of a legal selectable AI/trigger path."] : [PhaseBlocker], ownerIds));
            if (native.TargetFlags is byte targetFlags && (targetFlags & 0x30) is 0 or 0x20 && (targetFlags & 2) == 0)
            {
                scenarios.Add(scenarios[^1] with { CaseId = caseId + "-multiple-targets", Status = "PLANNED_BLOCKED", Disposition = "unresolved", DiagnosticRecipe = null, TargetPolicy = $"Multiple legal actual recipients required by installed group selector 0x{targetFlags:X2}; NativeBattleLimitRules.PlayerTargetMask and BattleHudController.SelectingTargetGroup preserve native group mode.", ExecutionBlockers = ["MULTI_TARGET_PRECONDITION: prove at least two legitimate actual recipients and their active targetable/life-state conditions in the required canonical phase or original enemy context. The current one-party/direct-participant recipe does not certify this branch; no synthetic enemy or arbitrary target-count override is substituted."] });
            }
        }
        foreach (EnemyScenarioActionOwner owner in enemies.Owners)
        {
            string ownerId = OwnerId(owner);
            string caseId = "enemy-" + ownerId;
            BattleNativeAction? native = owner.NativeAction;
            scenarios.Add(new(caseId, native?.MagicId, $"command-{owner.Command}-action-{owner.ActionId}", "enemy_owned_ability", "PLANNED_BLOCKED", "unresolved", false, null, null, null, $"Enemy model {owner.ModelId}: {owner.Name}", $"DAT model {owner.ModelId}; level band {owner.LevelBand}; ability slot {owner.AbilitySlot}; sequence {owner.Sequence}", owner.Command, owner.ActionId, null, native?.TargetFlags, "Hostile: Squall; self/ally/revival/counter recipients require actual AI selector and state proof.", ["Choose an exact formation/slot from enemy-owners.json; preserve level band, hidden/suspended/targetable flags, companions and reinforcement allocation.", "Run mandatory AI initialization, reactions and death callbacks; table membership alone does not authorize invocation."], [$"Select existing DAT ability row {owner.AbilitySlot} in level band {owner.LevelBand}, command {owner.Command}, action {owner.ActionId}, sequence {owner.Sequence} through normal native dispatch; adapter currently unavailable."], 2, ["Repeat the same focal effect twice in one battle, driving triggers separately from focal dispatches.", "No state reconstruction, RNG rewind or suppression of mandatory reactions."], owner.MappingEvidence, owner.Blockers, ["Enemy invocation, target legality and repeat trigger remain unverified."], [ownerId]));
            if (templatePath is not null)
            {
                DiscoveredScenario candidate = scenarios[^1];
                if (TryPrepareEnemyRecipe(assets, initialVitals, experienceDefinition, owner, enemies.ScriptedActions, possibleEnemyBands, preparedLevels, templatePath, out JsonObject? enemyRecipe, out EnemyScenarioFormation? context, out EnemyScenarioScriptedAction? selectorProof, out string? enemyBlocker))
                {
                    string path = Path.Combine(output, "recipes", caseId + ".json");
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.WriteAllText(path, enemyRecipe!.ToJsonString(RecordJson.Options));
                    scenarios[^1] = candidate with { Status = "RUNNABLE", Disposition = "scheduled", DiagnosticRecipe = path, FormationId = context!.FormationId, StageId = context.StageId, ExecutionBlockers = [], TargetPolicy = $"Actual installed AI selector {selectorProof!.TargetSelector} at callback {selectorProof.Callback}, offset {selectorProof.Offset}; semantic target={enemyRecipe["driver"]!["input"]!["target_id"]!.GetValue<string>()}; live owner/band/row guard before both native queue submissions.", Invocation = [$"Normal diagnostic native ability-row selector: model={owner.ModelId}, scene={context.SceneSlot}, band={owner.LevelBand}, slot={owner.AbilitySlot}, command={owner.Command}, action={owner.ActionId}, sequence={owner.Sequence}, target_selector={selectorProof.TargetSelector}."], MappingEvidence = candidate.MappingEvidence.Concat(selectorProof.MappingEvidence).ToArray(), AcceptanceBlockers = ["An actual native ability-row diagnostic does not establish all phase/trigger variants or independent native outcome equivalence."] };
                }
                else
                {
                    scenarios[^1] = candidate with { ExecutionBlockers = [enemyBlocker ?? "ENEMY_CONTEXT_PRECONDITION: exact legal live context remains unresolved."] };
                }
            }
            if (owner.ActionId == 0)
            {
                scenarios[^1] = scenarios[^1] with { Status = "EXCLUDED", Disposition = "excluded_with_reason", ExecutionBlockers = [], AcceptanceBlockers = ["Original QueuePreparedNativeAiCommand explicitly skips zero WORD action IDs, including this authenticated unused ability row."] };
            }
        }
        for (int index = 0; index < scenarios.Count; index++)
        {
            DiscoveredScenario scenario = scenarios[index];
            if (scenario.Family != "enemy_kernel_action" || scenario.Status == "RUNNABLE")
            {
                continue;
            }
            string[] covering = scenarios.Where(candidate => candidate.Family == "enemy_owned_ability" && candidate.Status == "RUNNABLE" && candidate.Command == scenario.Command && candidate.NativeActionId == scenario.NativeActionId && candidate.EffectId == scenario.EffectId).Select(candidate => candidate.CaseId).ToArray();
            if (covering.Length != 0)
            {
                scenarios[index] = scenario with { Status = "COVERED_BY_CASE", Disposition = "covered_by_case", ExecutionBlockers = [], CoveredByCaseIds = covering };
            }
        }
        for (int index = 0; index < enemies.ScriptedActions.Length; index++)
        {
            EnemyScenarioScriptedAction scripted = enemies.ScriptedActions[index];
            string caseId = $"script-{index:D6}-m{scripted.ModelId:D3}-cb{scripted.Callback}-off{scripted.Offset}-band{scripted.LevelBand}";
            scenarios.Add(new(caseId, scripted.NativeEffectId, $"scripted-row-{index}", "enemy_scripted_dispatch", "PLANNED_BLOCKED", "unresolved", false, null, null, null, $"Enemy model {scripted.ModelId}: {scripted.Name}", $"AI callback {scripted.Callback}; byte offset {scripted.Offset}; level band {scripted.LevelBand}; sequence {scripted.Sequence?.ToString() ?? "unresolved"}", scripted.Command, scripted.ActionId, null, scripted.NativeAction?.TargetFlags, "Preserve the AI selected recipient mask and real trigger conditions; unresolved until branch execution is proved.", ["Retain exact installed scene contexts in enemy-owners.json ScriptedActions at the matching row.", "Drive initialization/counter/death/turn callback and its branch prerequisites; direct graphics are not selectable generic abilities."], [$"Normal AI callback {scripted.Callback}, offset {scripted.Offset}, command {scripted.Command?.ToString() ?? "dynamic"}, action {scripted.ActionId?.ToString() ?? "dynamic"}; deterministic selector currently unavailable."], 2, ["Repeat original trigger twice without resetting owner lineage; dynamic selection must produce the same focal effect both times."], scripted.MappingEvidence, scripted.Blockers.Length == 0 ? ["SCRIPTED_SELECTOR_UNSUPPORTED: CFG discovery proves resource references, not legal branch reachability or repeated live invocation."] : scripted.Blockers, ["Static control-flow discovery is not a live ownership, targeting or repeatability receipt."], [$"scripted-row-{index}"]));
            scenarios[^1] = scenarios[^1] with { ExecutionBlockers = [scripted.Callback switch
            {
                0 => "INITIALIZATION_REPEAT_PRECONDITION: the original effect is dispatched by actor initialization; a second legitimate initialization trigger preserving the same battle/process owners and effect cleanup has not been proved.",
                2 => "COUNTER_TRIGGER_PRECONDITION: this original damage/reaction callback requires its real preceding attack, attacker/HP/status/AI conditions and the same focal response twice; a selectable ability-row or turn callback cannot replace that trigger.",
                3 => "DEATH_REPEAT_PRECONDITION: the original effect belongs to death processing. A legitimate second death/revival/rearm sequence preserving owner lineage and terminal semantics has not been proved.",
                1 => "TURN_SCRIPT_TARGET_OR_MAPPING_PRECONDITION: the normal turn adapter exists; this exact dispatch still needs an original legal recipient/effect mapping or genuine phase/level context. Dynamic filters, physical selectors and direct graphics are not replaced by guessed ability rows.",
                _ => $"CALLBACK_TRIGGER_PRECONDITION: the original callback{scripted.Callback} event owner, legal trigger, state and repeated focal dispatch remain unresolved."
            }] };
            if (templatePath is not null && scripted.Callback == 1 && scripted.NativeAction is { MagicId: >= 1 and <= 400 } scriptedNative && scripted.Command is byte scriptedCommand && scripted.ActionId is ushort scriptedAction && scripted.Sequence is byte scriptedSequence)
            {
                EnemyScenarioActionOwner scriptedOwner = new(scripted.ModelId, scripted.Name, scripted.LevelBand, -1, scriptedCommand, scriptedAction, scriptedSequence, scriptedNative, scripted.Formations, scripted.MappingEvidence, scripted.Blockers);
                if (TryPrepareEnemyRecipe(assets, initialVitals, experienceDefinition, scriptedOwner, enemies.ScriptedActions, possibleEnemyBands, preparedLevels, templatePath, out JsonObject? turnRecipe, out EnemyScenarioFormation? turnContext, out EnemyScenarioScriptedAction? turnProof, out string? turnBlocker, useTurnScript: true))
                {
                    string path = Path.Combine(output, "recipes", caseId + ".json");
                    File.WriteAllText(path, turnRecipe!.ToJsonString(RecordJson.Options));
                    scenarios[^1] = scenarios[^1] with { Status = "RUNNABLE", Disposition = "scheduled", DiagnosticRecipe = path, FormationId = turnContext!.FormationId, StageId = turnContext.StageId, ExecutionBlockers = [], TargetPolicy = $"The original complete turn callback owns target selection; observed selector {turnProof!.TargetSelector}, callback1 offset{turnProof.Offset}. Actual targets and branch dispatch must be verified in each execution.", Invocation = ["Queue the existing normal stage1 callback producer twice on the original owner. Do not directly invoke the effect or force the conditional/random branch."], AcceptanceBlockers = ["A valid native turn attempt is not proof the seeded branch reaches the focal effect twice; unreached/wrong branch produces ERROR."] };
                }
                else
                {
                    scenarios[^1] = scenarios[^1] with { ExecutionBlockers = [turnBlocker ?? "TURN_SCRIPT_PRECONDITION: original legal owner/target/band remains unresolved."] };
                }
            }
        }
        for (int index = 0; index < enemies.Issues.Length; index++)
        {
            PlanIssue issue = enemies.Issues[index];
            scenarios.Add(new($"discovery-boundary-{index:D6}", null, issue.Identity, "unresolved_installed_resource", "UNRESOLVED", "unresolved", false, null, null, null, issue.Identity, null, null, null, null, null, "Unknown until this installed resource boundary is decoded.", [], [], 2, ["Original invocation and repetition cannot be guessed across an unresolved resource boundary."], [issue.Identity], [issue.Message], ["Resource discovery is incomplete for this exact owner/path."], [issue.Identity]));
        }
        // Command rows without a projected action are obligations too; no guessed effect ID is assigned.
        DiscoveredCommand[] commands = kernel.BattleCommands.Select((command, index) =>
        {
            string[] ids = actions.Where(action => action.Native.Command == index || action.Native.DispatchCommand == index).Select(action => action.Id).ToArray();
            if (ids.Length == 0)
            {
                scenarios.Add(new($"command-{index}-unresolved", null, $"command-{index}", "command_or_scripted_dispatch", "PLANNED_BLOCKED", "unresolved", false, null, 982, 142, "Squall", null, checked((byte)index), command.AbilityId, null, null, "Unresolved native command targeting; no target invented.", ["Resolve command admission owner, dynamic/scripted dispatch and original resources."], [$"Installed command row {index}, ability reference {command.AbilityId}; not a proved effect identifier."], 2, ["Resolve original command-specific repetition contract."], [$"main/kernel.bin:section:0:row:{index}"], ["COMMAND_MAPPING_UNRESOLVED: no projected static action family; dynamic/physical/menu/scripted effect dispatch must be resolved independently."], [PhaseBlocker], []));
            }
            return new DiscoveredCommand(index, command.Name, command.AbilityId, ids, ids.Length == 0 ? "unresolved" : "covered_by_case", ids.Length == 0 ? "Dynamic command/action/effect mapping unresolved; explicit command scenario emitted." : "All projected native action variants retained as separate scenarios; this disposition claims planning coverage only.");
        }).ToArray();
        DiscoveredBinding[] rows = bindings.Select(binding =>
        {
            DiscoveredScenario[] mapped = scenarios.Where(scenario => scenario.EffectId == binding.Effect).ToArray();
            bool empty = binding.Entry == 0 && binding.Initializer == 0;
            string disposition = empty ? "excluded_with_reason" : mapped.Length == 0 ? "unresolved" : "covered_by_case";
            string reason = empty ? "Authenticated executable binding row has null entry and initializer. The row is inventoried; mapped actions, if any, remain visible independently." : mapped.Length == 0 ? "No installed kernel/DAT mapping established; scripted/dynamic/trigger dispatch owner remains unresolved. No arbitrary callback case is invented." : "Action mappings retained. Runnable diagnostics are not accepted canonical campaign evidence; unresolved action variants remain obligations.";
            string bindingCase = $"binding-{binding.Effect:D3}";
            bool needsBindingScenario = !empty && mapped.Length == 0;
            if (needsBindingScenario)
            {
                scenarios.Add(new(bindingCase, binding.Effect, bindingCase, "native_binding", "PLANNED_BLOCKED", disposition, false, null, null, null, "Unresolved until an owning action scenario is proved", null, null, null, null, null, "Use the owning action's legitimate target selector.", [], [], 2, ["Binding identity alone does not establish invocability or repeatability."], [$"FF8_EN.exe:effect-binding-row:{binding.Effect}:entry:{binding.Entry:X8}:initializer:{binding.Initializer:X8}"], ["BINDING_NOT_INVOCATION: no installed action/owner mapping was established; resolve original scripted/trigger path rather than invoking an arbitrary callback."], [], []));
            }
            return new DiscoveredBinding(binding.Effect, binding.Entry, binding.Initializer, bindings.Where(other => other.Effect != binding.Effect && other.Entry == binding.Entry && other.Initializer == binding.Initializer).Select(other => (int)other.Effect).ToArray(), disposition, reason, needsBindingScenario ? [bindingCase] : mapped.Select(scenario => scenario.CaseId).ToArray()) { Implementation = implementationByEffect[binding.Effect] };
        }).ToArray();
        CompiledImplementationSummary implementationSummary = new("implementation-evidence.json", implementation.Scope, implementation.DispatcherIlSha256, implementation.RuntimeAssemblySha256, implementation.Registrations.Length, implementation.Bindings.Count(binding => binding.Disposition == "direct_equality_branch_observed"), implementation.Bindings.Count(binding => binding.Disposition == "unknown"), implementation.OpaqueCalledMethods.Length, false);
        InstalledScenarioInventory inventory = new(1, Hash(executable), Hash(kernelBytes), assets.EncounterSourceHash.ToLowerInvariant(), DateTimeOffset.UtcNow, rows, actions, commands, enemies.Issues) { ImplementationSummary = implementationSummary };
        ValidateCoverage(inventory, scenarios);
        SchemaValidation.ValidateRecord(inventory, "scenario-discovery");
        SchemaValidation.ValidateRecord(scenarios, "scenario-plans");
        Write(output, "installed-inventory.json", inventory);
        Write(output, "resources.json", new ResourceRecord(1, "installed-original-battle-scenarios", resourceFiles.ToArray(), []));
        Write(output, "implementation-evidence.json", implementation);
        Write(output, "enemy-owners.json", enemies);
        Write(output, "scenarios.json", scenarios);
        Write(output, "cases-manifest.json", scenarios.Where(scenario => scenario.Disposition is not ("covered_by_case" or "excluded_with_reason")).Select(scenario => new PilotCase(scenario.CaseId, scenario.DiagnosticRecipe, scenario.Status, scenario.ExecutionBlockers, scenario.EffectId, scenario.DiagnosticRecipe is string path ? Hash(File.ReadAllBytes(path)) : null)).ToArray());
        foreach (DiscoveredBinding row in rows)
        {
            Write(output, $"effects/effect-{row.EffectId:D3}/scenarios.json", new { schema_version = 1, binding = row, scenarios = scenarios.Where(scenario => scenario.EffectId == row.EffectId).ToArray() });
        }
        Write(output, "unmapped-scenarios.json", scenarios.Where(scenario => scenario.EffectId is null or 0 or > 400).ToArray());
        Write(output, "coverage-reconciliation.json", new { schema_version = 1, inventory_binding_rows = rows.Length, native_action_rows = actions.Length, enemy_ability_rows = enemies.Owners.Length, enemy_scripted_dispatch_rows = enemies.ScriptedActions.Length, command_rows = commands.Length, generated_scenarios = scenarios.Count, binding_rows_without_plan = rows.Where(row => row.Disposition != "excluded_with_reason" && row.ScenarioIds.Length == 0).Select(row => row.EffectId).ToArray(), action_rows_without_plan = actions.Where(action => !scenarios.Any(scenario => scenario.ActionId == action.Id)).Select(action => action.Id).ToArray(), implementation_summary = implementationSummary, dispositions = scenarios.GroupBy(scenario => scenario.Disposition).ToDictionary(group => group.Key, group => group.Count()), campaign_acceptance = false, issues = enemies.Issues });
        ScenarioDiscoverySummary summary = new(rows.Length, actions.Length, enemies.Owners.Length, scenarios.Count, scenarios.Count(scenario => scenario.Status == "RUNNABLE"), scenarios.Count(scenario => scenario.Status is "PLANNED_BLOCKED" or "UNRESOLVED"), rows.Count(row => row.Disposition == "excluded_with_reason"), false, output);
        Write(output, "discovery-summary.json", summary);
        string toolAssembly = typeof(ScenarioDiscovery).Assembly.Location;
        string? informationalVersion = System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(ScenarioDiscovery).Assembly)?.InformationalVersion;
        BuildFile[] planFiles = EvidenceStore.DescribeFiles(output);
        Write(output, "plan-seal.json", new { schema_version = 1, planner_revision = 1, revision_id = Guid.NewGuid().ToString("N"), created_utc = DateTimeOffset.UtcNow, source_commit = sourceCommit, dirty_patch_sha256 = sourcePatchHash, source_identity_availability = sourceCommit is null ? "unavailable; caller did not provide source identity" : "caller supplied; tool binary identity is independently hashed", tool_informational_version = informationalVersion, tool_assembly_sha256 = Hash(File.ReadAllBytes(toolAssembly)), template_sha256 = canonicalTemplatePath is null ? null : Hash(File.ReadAllBytes(canonicalTemplatePath)), init_out_sha256 = Hash(initialBytes), inventory.ExecutableSha256, inventory.KernelSha256, inventory.SceneSha256, plan_sha256 = EvidenceStore.HashRecord(planFiles), files = planFiles, campaign_acceptance = false });
        return summary;
    }

    public static void ValidateCoverage(InstalledScenarioInventory inventory, IReadOnlyList<DiscoveredScenario> scenarios)
    {
        if (!inventory.Bindings.Select(row => row.EffectId).Order().SequenceEqual(Enumerable.Range(1, 400)))
        {
            throw new InvalidDataException("Discovery must retain every authenticated binding row exactly once.");
        }
        if (scenarios.Select(scenario => scenario.CaseId).Distinct(StringComparer.Ordinal).Count() != scenarios.Count || inventory.Actions.Select(action => action.Id).Distinct(StringComparer.Ordinal).Count() != inventory.Actions.Length)
        {
            throw new InvalidDataException("Discovery contains duplicate scenario or action identities.");
        }
        foreach (DiscoveredBinding binding in inventory.Bindings)
        {
            if (binding.Implementation is { } implementation && (implementation.EffectId != binding.EffectId || implementation.Entry != binding.Entry || implementation.Registrations.Any(registration => registration.Entry != binding.Entry) || implementation.Disposition == "direct_equality_branch_observed" && implementation.Registrations.Length == 0 || implementation.Disposition == "unknown" && implementation.Registrations.Length != 0))
            {
                throw new InvalidDataException($"Compiled registration evidence disagrees with binding {binding.EffectId}.");
            }
            if (binding.Disposition == "excluded_with_reason")
            {
                if (binding.Entry != 0 || binding.Initializer != 0 || string.IsNullOrWhiteSpace(binding.Reason))
                {
                    throw new InvalidDataException($"Excluded binding {binding.EffectId} is not an explained authenticated null row.");
                }
            }
            else if (binding.ScenarioIds.Length == 0)
            {
                throw new InvalidDataException($"Binding {binding.EffectId} has no owning action or unresolved scenario.");
            }
            foreach (string scenarioId in binding.ScenarioIds)
            {
                if (!scenarios.Any(scenario => scenario.CaseId == scenarioId && scenario.EffectId == binding.EffectId))
                {
                    throw new InvalidDataException($"Binding {binding.EffectId} references a missing or mismatched scenario: {scenarioId}.");
                }
            }
        }
        if (inventory.ImplementationSummary is { } summary && (summary.PlayableSupportEstablished || summary.BindingRowsWithObservedDirectBranch != inventory.Bindings.Count(binding => binding.Implementation?.Disposition == "direct_equality_branch_observed") || summary.BindingRowsUnknown != inventory.Bindings.Count(binding => binding.Implementation?.Disposition == "unknown") || summary.BindingRowsWithObservedDirectBranch + summary.BindingRowsUnknown != inventory.Bindings.Length))
        {
            throw new InvalidDataException("Compiled registration counts disagree or incorrectly claim playable support.");
        }
        foreach (DiscoveredAction action in inventory.Actions)
        {
            if (!scenarios.Any(scenario => scenario.ActionId == action.Id && scenario.EffectId == action.MappedEffectId))
            {
                throw new InvalidDataException($"Missing exact action/effect scenario: {action.Id}.");
            }
        }
        foreach (DiscoveredScenario scenario in scenarios)
        {
            if (scenario.CampaignAcceptance || scenario.Status == "RUNNABLE" && (scenario.DiagnosticRecipe is null || scenario.ExecutionBlockers.Length != 0) || scenario.Status is "PLANNED_BLOCKED" or "UNRESOLVED" && scenario.ExecutionBlockers.Length == 0 || scenario.Status == "COVERED_BY_CASE" && (scenario.CoveredByCaseIds.Length == 0 || scenario.CoveredByCaseIds.Any(id => !scenarios.Any(candidate => candidate.CaseId == id && candidate.Status == "RUNNABLE" && candidate.EffectId == scenario.EffectId && candidate.Command == scenario.Command && candidate.NativeActionId == scenario.NativeActionId))))
            {
                throw new InvalidDataException($"Unproved readiness/acceptance or missing blocker: {scenario.CaseId}.");
            }
        }
    }

    public static string ActionId(BattleNativeAction action) => $"c{action.Command:D3}-a{action.ActionId:D3}-s{action.KernelSection:D2}-r{action.KernelIndex:D3}" + (action.CrisisLevel is byte crisis ? $"-crisis{crisis}" : "");

    public static int? ResolveCatalogEffect(BattleNativeAction action, IReadOnlyList<BattleNativeAction> actions)
    {
        if (action.KernelSection == 7 && action.AttackType == 14)
        {
            if (action.ActionId == 30)
            {
                return null;
            }
            BattleNativeAction[] children = actions.Where(child => child.Command == 244 && child.ActionId == action.MagicId).ToArray();
            return children.Length == 1 ? children[0].MagicId : null;
        }
        return action.MagicId;
    }

    public static string Family(BattleNativeAction action) => action.KernelSection switch
    {
        1 => action.Command == 2 ? "magic" : "magic_command_variant",
        2 => "guardian_force",
        3 => "enemy_kernel_action",
        5 => "squall_finisher",
        7 => "battle_item",
        9 => "independent_guardian_force",
        10 => "character_command",
        18 => "npc_limit_shared_table",
        19 => "blue_magic_crisis_variant",
        21 => "shot_limit",
        22 => "duel_limit",
        25 => "angelo_limit",
        _ => "unresolved_kernel_family"
    };

    private static List<string> ActionBlockers(BattleNativeAction action)
    {
        List<string> blockers = [];
        if (action.MagicId == 0)
        {
            blockers.Add("NO_STATIC_EFFECT: installed action has effect 0; physical/dynamic/invisible dispatch semantics require separate proof, never substitute an effect.");
        }
        if (action.KernelSection == 3)
        {
            blockers.Add("ENEMY_OWNERSHIP_PRECONDITION: select a proved actual DAT owner/level/scene invocation from owner_context_ids. A normal row/turn adapter exists; unresolved phase, trigger and target contexts cannot be substituted by a generic enemy.");
        }
        else if (action.KernelSection == 5)
        {
            blockers.Add("RENZOKUKEN_BRANCH_PRECONDITION: resolve the actual final target's seven-entry native Renzokuken effect table, crisis-dependent two-entry base selection, weapon finisher flags and naturally advancing RNG/input sequence that reaches this same finisher twice. The normal own-Squall adapter does not force random branches or invent supporting effect IDs.");
        }
        else if (action.KernelSection is 18 or 19 or 21 or 22 or 25)
        {
            blockers.Add("OWNER_DEPENDENT: this original command requires a proved Squall diagnostic capability with original owner/pose/weapon resources, crisis/learned/input prerequisites and a twice-reachable branch. Ordinary own-Squall and the explicit borrowed Blue Magic adapter do not authorize unrelated limit owners.");
        }
        else if (action.KernelSection == 7)
        {
            blockers.Add("ITEM_INVOCATION_PRECONDITION: provide the verified canonical fixture and establish the installed ordinary item command/stock, legal recipient, consumption and repetition prerequisites; command variants retain their own trigger requirements.");
        }
        else if (action.KernelSection is 2 or 9)
        {
            blockers.Add("SUMMON_PREPARATION_UNRESOLVED: provide the verified canonical fixture and prove original acquired/junctioned GF, learned command, positive HP and semantic-to-native action mapping; independent summon rows retain their actual trigger owners.");
        }
        else if (action.KernelSection == 1 && action.Command == 2)
        {
            blockers.Add("MAGIC_PREPARATION_UNPROVED: a valid installed stock row and canonical recipe template are required, including legal recipient/precondition and two executions.");
        }
        else
        {
            blockers.Add("COMMAND_ADAPTER_UNRESOLVED: exact native command admission, learned/stock/input state, targets and repetition need a proved typed fixture; do not treat a shared kernel row as a legal invocation.");
        }
        return blockers;
    }

    private static string[] Preparation(BattleNativeAction action) => [$"Retain installed target flags {(action.TargetFlags is byte flags ? $"0x{flags:X2}" : "absent; selector belongs to owner/AI")}; original status masks {action.Statuses0:X4}/{action.Statuses1:X8}; attack type {action.AttackType}.", action.KernelSection == 3 ? "Select actual owner/context from enemy-owners.json; preserve AI bank, sequence and phase." : "Canonical opponent is final Ultimecia; same opposing encounter with legal party recipients for restricted actions. Preserve original immunities/absorption/reflection."];

    private static string TargetPolicy(BattleNativeAction action) => action.TargetFlags is byte flags ? $"Installed target flags 0x{flags:X2}; hostile target final Ultimecia only where native admission allows; self/party/revival recipients and prerequisites require proof." : "Target selector belongs to native command/AI owner; unresolved until that owner is proved.";

    private static string OwnerId(EnemyScenarioActionOwner owner) => $"m{owner.ModelId:D3}-band{owner.LevelBand}-slot{owner.AbilitySlot:D2}";

    private static Dictionary<string, string> ReadCandidateRecipes(string? templatePath)
    {
        Dictionary<string, string> result = [];
        if (templatePath is null)
        {
            return result;
        }
        foreach (string path in new[] { templatePath })
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(path));
            JsonElement root = document.RootElement;
            JsonElement input = root.GetProperty("driver").GetProperty("input");
            int action = input.GetProperty("action_id").GetInt32();
            int effect = root.GetProperty("driver").GetProperty("expected_effect").GetInt32();
            if (root.GetProperty("encounter").GetInt32() != 982 || root.GetProperty("target").GetProperty("stage").GetInt32() != 142 || input.GetProperty("actor_id").GetString() != "party-0" || input.GetProperty("ability").GetString() != "magic" || action is not (>= 1 and <= 63))
            {
                throw new InvalidDataException("Candidate recipe is outside the proved ordinary magic diagnostic adapter.");
            }
            ValidateCandidateTemplate(root, action, effect);
            result.Add($"{action}:{effect}", Path.GetFullPath(path));
        }
        return result;
    }

    public static void ValidateCandidateTemplate(JsonElement root, int action, int effect)
    {
        JsonElement target = root.GetProperty("target");
        JsonElement fixture = root.GetProperty("fixture");
        JsonElement characters = fixture.GetProperty("characters");
        JsonElement guardians = fixture.GetProperty("guardian_forces");
        JsonElement driver = root.GetProperty("driver");
        JsonElement input = driver.GetProperty("input");
        bool valid = target.GetProperty("actor_id").GetString() == "enemy-6" && target.GetProperty("model").GetInt32() == 126 && target.GetProperty("scene_slot").GetInt32() == 6 && target.GetProperty("physical_index").GetInt32() == 3 && target.GetProperty("flags_mask").GetUInt32() == 195 && target.GetProperty("flags_value").GetUInt32() == 1 && target.GetProperty("opening_action").GetInt32() == 355 && target.GetProperty("opening_effect").GetInt32() == 76 && fixture.GetProperty("hold_enemy_commands").GetBoolean() && characters.GetArrayLength() == 1 && guardians.GetArrayLength() == 1 && guardians[0].GetProperty("id").GetInt32() == 0 && guardians[0].GetProperty("acquire").GetBoolean();
        JsonElement character = characters[0];
        JsonElement stocks = character.GetProperty("stocks");
        JsonElement assignments = character.GetProperty("guardian_forces");
        JsonElement magicCommand = character.GetProperty("magic_command");
        valid &= character.GetProperty("row").GetInt32() == 0 && character.GetProperty("character_id").GetInt32() == 0 && character.GetProperty("hit_points").GetInt32() is > 0 and <= ushort.MaxValue && stocks.GetArrayLength() == 1 && stocks[0].GetProperty("action").GetInt32() == action && stocks[0].GetProperty("count").GetInt32() is >= 2 and <= 100 && assignments.GetArrayLength() == 1 && assignments[0].GetInt32() == 0 && magicCommand.GetProperty("ability").GetInt32() == 20 && magicCommand.GetProperty("slot").GetInt32() == 0;
        valid &= root.GetProperty("encounter").GetInt32() == 982 && target.GetProperty("stage").GetInt32() == 142 && root.GetProperty("seed").GetInt32() == 0 && root.GetProperty("flags").GetInt32() == 128 && root.GetProperty("field_battle_mode").GetInt32() == 0 && root.GetProperty("elapsed_counter").GetInt32() == 0 && root.GetProperty("battle_music").GetInt32() == 5 && root.GetProperty("primary_music_channel").GetInt32() == -1 && root.GetProperty("prepare_entry_before_presentation").GetBoolean();
        valid &= input.GetProperty("kind").GetString() == "action" && input.GetProperty("actor_id").GetString() == "party-0" && input.GetProperty("ability").GetString() == "magic" && input.GetProperty("action_id").GetInt32() == action && input.GetProperty("target_id").GetString() is "party-0" or "enemy-6" && driver.GetProperty("expected_effect").GetInt32() == effect && driver.GetProperty("tail_frames").GetInt32() >= 2;
        if (!valid)
        {
            throw new InvalidDataException("Candidate template differs from the bounded canonical participant, stock, GF, command or ordinary magic diagnostic contract.");
        }
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void Write<T>(string output, string relative, T record)
    {
        string path = Path.Combine(output, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, record, RecordJson.Options);
        stream.Flush(true);
    }
}
