using System.Text.Json;

namespace Memoria.FF8.BattleEffects;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 0 || args[0] is "help" or "--help")
            {
                Print(new { tool = "battle-effects", stage = "external_scenario_runner", commands = new[] { "capabilities", "discover --game-dir --output [--fixture] [--source-commit] [--patch-hash]", "inventory --input", "plan", "validate-plan", "seal", "pass-create", "validate-record", "validate-live --input [--case]", "run --input", "pilot --input", "validate-pilot --input", "resume-plan --input --history --output --report", "claim-next", "claim-specific", "heartbeat", "release", "recover-stale", "status", "supervise", "records", "show", "export" }, guide = "docs/developers/battle-effects-validation.md" });
                return 0;
            }

            Dictionary<string, string> options = ParseOptions(args.Skip(1).ToArray());
            string Get(string name) => options.TryGetValue(name, out string? value) ? value : throw new ArgumentException($"Missing --{name}.");
            string? Optional(string name) => options.GetValueOrDefault(name);

            if (args[0] == "capabilities")
            {
                Print(new
                {
                    stage = "external_scenario_runner",
                    runner_available = true,
                    campaign_acceptance_available = false,
                    runtime_copy_instrumentation = new { version = Memoria.FF8.Runtime.NativeBattleTelemetry.InstrumentationVersion, scope = Memoria.FF8.Runtime.NativeBattleTelemetry.MeasurementScope, sites = Memoria.FF8.Runtime.NativeBattleTelemetry.Sites, blind_spots = Memoria.FF8.Runtime.NativeBattleTelemetry.KnownBlindSpots },
                    live_validation_command = "validate-live --input [--case]",
                    scenario_discovery_command = "discover --game-dir --output [--fixture] [--source-commit] [--patch-hash]",
                    execution_command = "run --input",
                    blockers = new[] { "Discovery records unsupported action paths and unproved fixtures as explicit planning blockers; discovery is not playable certification.", "Typed preparation, arbitrary capabilities, timed limits and between-execution repairs remain bounded by current engine contracts.", "Telemetry admission is separate from independent fixture provenance, reviewer acceptance and a final campaign sweep." }
                });
                return 0;
            }

            if (args[0] == "validate-record")
            {
                SchemaValidation.ValidateFile(Get("input"), Get("schema"));
                Print(new { valid = true });
                return 0;
            }

            if (args[0] == "inventory")
            {
                SchemaValidation.ValidateFile(Get("input"), "inventory");
                InventoryRecord inventory = RecordJson.Read<InventoryRecord>(Get("input"));
                Print(new { mode = "validate_planner_input", installed_discovery_performed = false, inventory });
                return 0;
            }

            if (args[0] == "validate-live")
            {
                LiveEvidenceVerdict result = LiveEvidenceValidation.ValidateFile(Get("input"), Optional("case") is { } casePath ? RecordJson.Read<CaseRecipe>(casePath) : null);
                Print(result);
                return result.Verdict == "PASS" ? 0 : result.Verdict == "FAIL" ? 2 : 4;
            }

            if (args[0] == "discover")
            {
                Print(ScenarioDiscovery.Generate(Get("game-dir"), Get("output"), Optional("fixture"), Optional("source-commit"), Optional("patch-hash")));
                return 0;
            }

            if (args[0] == "resume-plan")
            {
                SchemaValidation.ValidateFile(Get("input"), "pilot");
                PilotManifest requested = RecordJson.Read<PilotManifest>(Get("input"));
                PilotRunner.ValidateManifest(requested);
                string output = Path.GetFullPath(Get("output"));
                string report = Path.GetFullPath(Get("report"));
                PrivateEvidencePath.Validate(Path.GetDirectoryName(output)!);
                PrivateEvidencePath.Validate(Path.GetDirectoryName(report)!);
                if (File.Exists(output) || File.Exists(report) || output.Equals(report, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException("Resume outputs must be distinct fresh files.");
                }
                PilotResumePlan plan = PilotResume.Plan(requested, Get("history"));
                if (plan.PendingCases.Length > 0)
                {
                    PilotManifest pending = requested with { Cases = plan.PendingCases };
                    SchemaValidation.ValidateRecord(pending, "pilot");
                    OwnedChildSupervisor.PublishTransientImmutable(output, JsonSerializer.SerializeToUtf8Bytes(pending, RecordJson.Options));
                }
                OwnedChildSupervisor.PublishTransientImmutable(report, JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 1, requested_manifest = Path.GetFullPath(Get("input")), requested_cases = requested.Cases.Length, pending_manifest = plan.PendingCases.Length > 0 ? output : null, pending_cases = plan.PendingCases.Length, reused_cases = plan.ReusedCases, diagnostics = plan.Diagnostics, current_build_fingerprint = requested.BuildFingerprint, campaign_acceptance = false }, RecordJson.Options));
                Print(new { requested_cases = requested.Cases.Length, pending_cases = plan.PendingCases.Length, reused_cases = plan.ReusedCases.Length, report });
                return 0;
            }

            if (args[0] is "run" or "pilot" or "validate-pilot")
            {
                SchemaValidation.ValidateFile(Get("input"), "pilot");
                PilotManifest manifest = RecordJson.Read<PilotManifest>(Get("input"));
                PilotRunner.ValidateManifest(manifest);
                if (args[0] == "validate-pilot")
                {
                    Print(new { valid = true, cases = manifest.Cases.Length, output = manifest.OutputRoot });
                    return 0;
                }
                PilotBatchSummary summary = PilotRunner.RunAsync(manifest, completed: result => Print(new { case_id = result.Id, verdict = result.Verdict, result.Executions, total_ms = result.TotalMs, effect_duration_ms = result.EffectDurationMs, execution_window_ms = result.ExecutionWindowMs, overhead_ms = result.OverheadMs, overhead_within_budget = result.OverheadWithinBudget, error_count = result.Errors.Length, first_error = result.Errors.FirstOrDefault() })).GetAwaiter().GetResult();
                Print(new { summary.Verdict, summary.Requested, summary.Passed, summary.Failed, summary.Errors, total_ms = summary.TotalMs, output = manifest.OutputRoot });
                return summary.Verdict == "PASS" ? 0 : summary.Verdict == "FAIL" ? 2 : 4;
            }

            EvidenceStore store = new(Get("store"));
            CampaignPlanning planning = new(store);
            switch (args[0])
            {
                case "records":
                    Print(store.ListPaths(Optional("prefix") ?? ""));
                    return 0;
                case "show":
                {
                    using JsonDocument record = JsonDocument.Parse(store.ReadBytes(Get("key")));
                    Print(record.RootElement);
                    return 0;
                }
                case "export":
                {
                    byte[] bytes = store.ReadBytes(Get("key"));
                    using FileStream output = new(Get("output"), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    output.Write(bytes);
                    output.Flush(true);
                    Print(new { exported = true, bytes = bytes.LongLength });
                    return 0;
                }
                case "plan":
                    SchemaValidation.ValidateFile(Get("inventory"), "inventory");
                    SchemaValidation.ValidateFile(Get("profile"), "profile");
                    SchemaValidation.ValidateFile(Get("resources"), "resources");
                    SchemaValidation.ValidateFile(Get("cases"), "cases");
                    planning.Create(Get("campaign"), int.Parse(Optional("revision") ?? "1", System.Globalization.CultureInfo.InvariantCulture), Optional("parent-campaign"), RecordJson.Read<InventoryRecord>(Get("inventory")), CaptureBuild(Get("build"), Get("source-commit"), Optional("patch-hash")), RecordJson.Read<PerformanceProfile>(Get("profile")), RecordJson.Read<ResourceRecord>(Get("resources")), RecordJson.Read<CaseRecipe[]>(Get("cases")));
                    Print(new { created = true, store = store.Root, validation_scope = "declared_schema_and_plan_consistency_only", validation = planning.Validate() });
                    return 0;
                case "validate-plan":
                    PlanValidation validation = planning.Validate();
                    Print(new { validation_scope = "declared_schema_and_plan_consistency_only", native_fixture_verified = false, validation });
                    return validation.Valid ? 0 : 2;
                case "seal":
                    Print(new { sealed_plan = planning.Seal(), execution_ready = false });
                    return 0;
                case "pass-create":
                    PassRecord pass = planning.CreatePass(Get("pass"), Get("kind"), CaptureBuild(Get("build"), Get("source-commit"), Optional("patch-hash")), Optional("cases")?.Split(',', StringSplitOptions.RemoveEmptyEntries));
                    RegisterPass(store, planning, pass);
                    Print(pass);
                    return 0;
                case "claim-next":
                case "claim-specific":
                {
                    PassRecord selectedPass = ReadPass(store, planning, Get("pass"));
                    LocalCoordinator coordinator = new(store.Root);
                    CoordinationScope scope = Scope(selectedPass);
                    RegisterPass(store, planning, selectedPass);
                    EffectClaim? claim = args[0] == "claim-specific" ? coordinator.ClaimSpecific(scope, Get("effect"), Get("agent"), ProcessIdentity.Current) : coordinator.ClaimNext(scope, Get("agent"), ProcessIdentity.Current);
                    Print(claim);
                    return claim == null ? 3 : 0;
                }
                case "heartbeat":
                {
                    EffectClaim claim = ReadClaim(store, planning, Get("claim"));
                    Print(new LocalCoordinator(store.Root).Heartbeat(claim));
                    return 0;
                }
                case "release":
                {
                    EffectClaim claim = ReadClaim(store, planning, Get("claim"));
                    new LocalCoordinator(store.Root).Release(claim);
                    Print(new { released = true });
                    return 0;
                }
                case "recover-stale":
                {
                    PassRecord selectedPass = ReadPass(store, planning, Get("pass"));
                    LocalCoordinator coordinator = new(store.Root);
                    bool slotRecovered = coordinator.RecoverStalePerformanceSlot(Environment.MachineName);
                    int recovered = coordinator.RecoverStale(Scope(selectedPass));
                    Print(new { recovered, slot_recovered = slotRecovered });
                    return 0;
                }
                case "status":
                {
                    PassRecord selectedPass = ReadPass(store, planning, Get("pass"));
                    CaseRecipe[] cases = planning.ReadCases().Where(c => selectedPass.RequiredCases.Contains(c.CaseId)).ToArray();
                    LocalCoordinator coordinator = new(store.Root);
                    Print(new { execution_ready = false, pass = selectedPass, coordination = coordinator.GetStatus(Scope(selectedPass)), receipts = coordinator.GetReceipts(Scope(selectedPass)), required_cases = cases.Select(c => new { c.CaseId, c.EffectKey }), reviewed_accepted_cases = 0, overall = "UNRESOLVED_PREPARATION" });
                    return 0;
                }
                case "supervise":
                {
                    EffectClaim claim = ReadClaim(store, planning, Get("claim"));
                    string caseId = Get("case");
                    PassRecord selectedPass = planning.ReadPass(claim.Scope.RunPassId);
                    CaseRecipe recipe = planning.ReadCases().Single(c => c.CaseId == caseId && c.EffectKey == claim.EffectKey && selectedPass.RequiredCases.Contains(c.CaseId));
                    SchemaValidation.ValidateFile(Get("request"), "supervisor-launch");
                    SupervisorLaunch template = RecordJson.Read<SupervisorLaunch>(Get("request"));
                    BuildRecord build = store.Read<BuildRecord>($"builds/{selectedPass.BuildHash}.json");
                    BuildFile[] actual = EvidenceStore.DescribeFiles(template.WorkingDirectory);
                    if (!actual.SequenceEqual(build.Files.OrderBy(file => file.Path, StringComparer.Ordinal)))
                    {
                        throw new InvalidDataException("The selected immutable build directory changed or differs from its pass.");
                    }
                    string executable = Path.GetFullPath(template.Executable);
                    string relativeExecutable = Path.GetRelativePath(Path.GetFullPath(template.WorkingDirectory), executable).Replace('\\', '/');
                    if (!build.Files.Any(file => file.Path == relativeExecutable))
                    {
                        throw new InvalidDataException("The supervised executable must belong to the pinned build directory.");
                    }
                    LocalCoordinator coordinator = new(store.Root);
                    AllocatedAttempt attempt = coordinator.AllocateAttempt(claim, caseId);
                    string settings = store.Resolve($"channels/{attempt.AttemptId}/settings");
                    string control = store.Resolve($"channels/{attempt.AttemptId}/control");
                    string save = store.Resolve($"channels/{attempt.AttemptId}/saves/checkpoint.json");
                    CaseTimeouts t = recipe.Timeouts;
                    SupervisionBudgets budgets = new(TimeSpan.FromMilliseconds(t.PreparationMs), TimeSpan.FromMilliseconds(t.ActionMs), TimeSpan.FromMilliseconds(t.CleanupMs), TimeSpan.FromMilliseconds(t.ActionMs + t.CleanupMs), TimeSpan.FromMilliseconds(t.TeardownMs), TimeSpan.FromMilliseconds(t.ProcessMs), TimeSpan.FromMilliseconds(template.NoProgressMs));
                    string[] hostArguments = template.Arguments.ToArray();
                    int recipeArgument = Array.IndexOf(hostArguments, "--battle-test");
                    if (recipeArgument < 0 || recipeArgument + 1 >= hostArguments.Length)
                    {
                        throw new InvalidDataException("A preparation launch must name its --battle-test recipe.");
                    }
                    byte[] bootstrapRecipe = File.ReadAllBytes(hostArguments[recipeArgument + 1]);
                    store.WriteBytesNew($"{attempt.RelativeDirectory}/bootstrap.json", bootstrapRecipe);
                    string transientRecipe = store.Resolve($"channels/{attempt.AttemptId}/bootstrap.json");
                    Directory.CreateDirectory(Path.GetDirectoryName(transientRecipe)!);
                    File.WriteAllBytes(transientRecipe, bootstrapRecipe);
                    hostArguments[recipeArgument + 1] = transientRecipe;
                    store.WriteNew($"{attempt.RelativeDirectory}/launch.json", template);
                    SupervisionRequest request = new(attempt.AttemptId, template.Executable, [.. hostArguments, "--settings-dir", settings, "--control-dir", control, "--save-state", save], template.WorkingDirectory, settings, store.Resolve(attempt.RelativeDirectory), control, budgets, template.Environment);
                    using CancellationTokenSource cancellation = new();
                    object leaseLock = new();
                    PerformanceSlotLease? slot = null;
                    string? heartbeatError = null;
                    using Timer heartbeat = new(_ =>
                    {
                        lock (leaseLock)
                        {
                            try
                            {
                                claim = coordinator.Heartbeat(claim);
                                if (slot != null)
                                {
                                    slot = coordinator.HeartbeatPerformanceSlot(slot);
                                }
                            }
                            catch (Exception exception) when (exception is InvalidOperationException or IOException or InvalidDataException or TimeoutException)
                            {
                                heartbeatError = exception.Message;
                                cancellation.Cancel();
                            }
                        }
                    }, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
                    SupervisionOutcome outcome;
                    try
                    {
                        outcome = OwnedChildSupervisor.RunAsync(request, cancellation.Token, process =>
                        {
                            lock (leaseLock)
                            {
                                slot = coordinator.TryAcquirePerformanceSlot(claim, Environment.MachineName, process);
                                return slot != null;
                            }
                        }, payload => store.WriteBytesNew($"{attempt.RelativeDirectory}/started.json", payload)).GetAwaiter().GetResult();
                    }
                    finally
                    {
                        heartbeat.DisposeAsync().AsTask().GetAwaiter().GetResult();
                    }
                    if (outcome.Status == "candidate_ready")
                    {
                        OwnedChildSupervisor.PublishValidatedCandidate(outcome, payload => ValidateSupervisedLiveCandidate(payload, store.Resolve(attempt.RelativeDirectory), attempt.AttemptId, selectedPass.RunPassId, recipe));
                    }
                    if (outcome.ProcessExited)
                    {
                        ImportTransientArtifacts(store, attempt.RelativeDirectory);
                        ImportTransientArtifacts(store, $"channels/{attempt.AttemptId}");
                        if (slot != null)
                        {
                            coordinator.ReleasePerformanceSlot(slot);
                        }
                        coordinator.Release(claim);
                    }
                    string verdict = "ERROR";
                    if (store.Exists(attempt.RelativeDirectory + "/result.json"))
                    {
                        using JsonDocument result = JsonDocument.Parse(store.ReadBytes(attempt.RelativeDirectory + "/result.json"));
                        verdict = result.RootElement.GetProperty("verdict").GetString() ?? "ERROR";
                    }
                    Print(new { stage = "live_evidence", execution_verdict = verdict, attempt, outcome = outcome with { CandidatePayload = null }, heartbeat_error = heartbeatError, publication_accepted = false, claim_released = outcome.ProcessExited });
                    return verdict == "PASS" ? 0 : verdict == "FAIL" ? 2 : 4;
                }
                case "worker":
                case "replay":
                case "retry":
                case "validate-results":
                case "compare":
                case "report":
                    Print(new { error = "PREPARATION_ONLY", command = args[0], message = "Scored execution and acceptance are unavailable. Run capabilities for the remaining contracts; no game was launched and no case received a verdict." });
                    return 4;
                default:
                    throw new ArgumentException($"Unknown command: {args[0]}");
            }
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException or InvalidDataException or JsonException or NotSupportedException or FormatException or OverflowException or TimeoutException)
        {
            Print(new { error = exception.GetType().Name, message = exception.Message });
            return 1;
        }
    }

    private static BuildRecord CaptureBuild(string directory, string commit, string? patchHash) => new(1, commit, patchHash != null, patchHash, "Release", EvidenceStore.DescribeFiles(directory));

    internal static byte[] ValidateSupervisedLiveCandidate(byte[] payload, string outputDirectory, string attemptId, string passId, CaseRecipe recipe)
    {
        using JsonDocument candidate = JsonDocument.Parse(payload);
        JsonElement summary = candidate.RootElement;
        if (!summary.TryGetProperty("telemetry_report", out JsonElement reportName) || reportName.ValueKind != JsonValueKind.String || reportName.GetString() != "telemetry-report.json")
        {
            throw new InvalidDataException("Candidate must reference the fixed local live telemetry artifact.");
        }
        string path = Path.Combine(outputDirectory, "telemetry-report.json");
        FileInfo info = new(path);
        if (info.Length is <= 0 or > 64 * 1024 * 1024 || (info.Attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException("Live telemetry artifact must be a bounded ordinary local file.");
        }
        byte[] bytes = File.ReadAllBytes(path);
        string hash = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
        if (!summary.TryGetProperty("telemetry_sha256", out JsonElement digest) || digest.ValueKind != JsonValueKind.String || digest.GetString() != hash)
        {
            throw new InvalidDataException("Candidate live telemetry digest differs from the retained artifact.");
        }
        using JsonDocument report = JsonDocument.Parse(bytes);
        if (!summary.TryGetProperty("battle_instance_id", out JsonElement candidateBattle) || candidateBattle.ValueKind != JsonValueKind.String || !report.RootElement.TryGetProperty("battle_instance_id", out JsonElement reportBattle) || reportBattle.ValueKind != JsonValueKind.String || candidateBattle.GetString() != reportBattle.GetString())
        {
            throw new InvalidDataException("Candidate and live report identify different battles.");
        }
        LiveEvidenceVerdict validation = LiveEvidenceValidation.Validate(report.RootElement, recipe);
        return JsonSerializer.SerializeToUtf8Bytes(new { schema_version = 1, attempt_id = attemptId, run_pass_id = passId, case_id = recipe.CaseId, verdict = validation.Verdict, stage = "live_evidence", telemetry_sha256 = hash, candidate_sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(payload)), validation, publication_accepted = false }, RecordJson.Options);
    }

    private static PassRecord ReadPass(EvidenceStore store, CampaignPlanning planning, string id)
    {
        return planning.ReadPass(id);
    }

    private static void RegisterPass(EvidenceStore store, CampaignPlanning planning, PassRecord pass)
    {
        string[] effects = planning.ReadCases().Where(c => pass.RequiredCases.Contains(c.CaseId)).Select(c => c.EffectKey).Distinct().ToArray();
        new LocalCoordinator(store.Root).RegisterEffects(Scope(pass), effects);
    }

    private static CoordinationScope Scope(PassRecord pass) => new(pass.CampaignId, pass.PlanHash, pass.RunPassId);

    private static EffectClaim ReadClaim(EvidenceStore store, CampaignPlanning planning, string token)
    {
        EffectClaim claim = new LocalCoordinator(store.Root).GetClaim(token) ?? throw new InvalidDataException("No current claim has this token.");
        PassRecord pass = ReadPass(store, planning, claim.Scope.RunPassId);
        if (claim.Scope != Scope(pass))
        {
            throw new InvalidDataException("Claim scope differs from the requested store.");
        }

        return claim;
    }

    private static void ImportTransientArtifacts(EvidenceStore store, string relativeDirectory)
    {
        string directory = store.Resolve(relativeDirectory);
        if (!Directory.Exists(directory))
        {
            return;
        }
        foreach (BuildFile artifact in EvidenceStore.DescribeFiles(directory))
        {
            string key = relativeDirectory + "/" + artifact.Path;
            string path = store.Resolve(key);
            byte[] bytes = File.ReadAllBytes(path);
            if (store.Exists(key))
            {
                if (!store.ReadBytes(key).AsSpan().SequenceEqual(bytes))
                {
                    throw new InvalidDataException("Transient artifact conflicts with an immutable database record.");
                }
            }
            else
            {
                store.WriteBytesNew(key, bytes);
            }
            File.Delete(path);
        }
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        Dictionary<string, string> options = new(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 == args.Length || !options.TryAdd(args[i][2..], args[i + 1]))
            {
                throw new ArgumentException("Options require unique --name value pairs.");
            }
        }

        return options;
    }

    private static void Print<T>(T value) => Console.WriteLine(JsonSerializer.Serialize(value, RecordJson.Options));
}
