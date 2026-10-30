using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.BattleEffects;

public sealed record FreshEntryFilePin(string Path, string Sha256);
public sealed record FreshEntryRunPins(FreshEntryFilePin Report, FreshEntryFilePin Initialization);
public sealed record FreshEntryPairPins(FreshEntryFilePin Recipe, FreshEntryFilePin OriginalExecutable, FreshEntryFilePin ExecutionStack, string GameDirectory, string CandidateDirectory, string CandidateFingerprint, ImmutableArray<FreshEntryRunPins> Entries);
public sealed record FreshEntryPairVerdict(bool FunctionalPass, string[] Errors, LiveEvidenceVerdict[] Entries, bool CampaignAcceptance = false);

/// <summary>Two separately initialized genuine single-use entries. Existing same-battle validation remains a different protocol.</summary>
public static class FreshEntryPairValidation
{
    private static readonly JsonSerializerOptions Json = new(RecordJson.Options) { Converters = { new JsonStringEnumConverter() } };

    internal static void ValidateProtocol(JsonElement report, bool expected)
    {
        bool declared = report.TryGetProperty("fresh_entry", out JsonElement declaration) && declaration.ValueKind != JsonValueKind.Null;
        JsonElement driver = default;
        JsonElement original = default;
        bool initialized = report.TryGetProperty("initialization", out JsonElement initialization) && initialization.ValueKind == JsonValueKind.Object && initialization.TryGetProperty("preparation", out JsonElement preparation) && preparation.ValueKind == JsonValueKind.Object && preparation.TryGetProperty("driver", out driver) && driver.TryGetProperty("fresh_entry", out original) && original.ValueKind != JsonValueKind.Null;
        Require(declared == expected && initialized == expected, "Fresh-entry protocol must match its explicit validator and initialized declaration.");
        if (!expected)
        {
            return;
        }
        Require(JsonElement.DeepEquals(declaration, original), "Fresh-entry declaration differs from actual initialization.");
        DiagnosticFreshEntryDeclaration typed = declaration.Deserialize<DiagnosticFreshEntryDeclaration>(Json) ?? throw new InvalidDataException("Fresh-entry declaration is absent.");
        typed.Validate();
        Require(initialization.GetProperty("initializer_version").GetString() == "direct-new-game-v2" && initialization.GetProperty("battle_instance_id").GetString() == report.GetProperty("battle_instance_id").GetString(), "Fresh entry requires its actual ordinary initialized battle.");
        Require(!Present(initialization.GetProperty("preparation"), "prelude"), "Fresh entry cannot warm its focal native effect through a prelude.");
        foreach (string name in new[] { "shot_cycle", "counter_cycle", "enemy_ability", "terminal_hold" })
        {
            Require(!Present(driver, name), "Fresh entry cannot compose another cycle, owner adapter or terminal hold.");
        }
        foreach (string name in new[] { "repairs", "supporting_effects", "supporting_invocations" })
        {
            Require(driver.GetProperty(name).GetArrayLength() == 0, "Fresh entry cannot repair, rearm or substitute its native invocation.");
        }
        Require(report.GetProperty("declared_repairs").GetArrayLength() == 0 && !Present(report, "declared_terminal_hold") && report.GetProperty("supporting_effects").GetArrayLength() == 0 && report.GetProperty("supporting_invocations").GetArrayLength() == 0, "Fresh-entry observed protocol differs from its native single-use declaration.");
        Require(!Present(driver, "expected_native_command") || driver.GetProperty("expected_native_command").GetInt32() != 250, "Fresh entry does not provide an automatic or Renzokuken observation adapter.");
        Require(JsonElement.DeepEquals(driver.GetProperty("input"), report.GetProperty("input")) && JsonElement.DeepEquals(driver.GetProperty("expected_effect"), report.GetProperty("expected_effect")), "Fresh-entry input or effect differs from its initialized driver.");
        foreach (string name in new[] { "expected_native_action", "expected_native_command" })
        {
            Require(SameOptional(driver, report, name), "Fresh-entry native command/action differs from actual initialization.");
        }
        DiagnosticFreshEntryHostReceipt host = initialization.GetProperty("fresh_entry_host").Deserialize<DiagnosticFreshEntryHostReceipt>(Json) ?? throw new InvalidDataException("Fresh-entry actual host/source receipt is absent.");
        Require(host.BattleInstanceId == Guid.Parse(report.GetProperty("battle_instance_id").GetString()!) && host.LifecycleId != Guid.Empty && host.HostInstanceId != Guid.Empty && host.ProcessId > 0 && host.ProcessStartedUtcTicks > 0 && HashValid(host.ManagedHostSha256) && HashValid(host.ManagedRuntimeSha256) && HashValid(host.ExecutionStackSha256), "Fresh-entry host source identity is invalid.");
        Require(SameSources(typed.SourceFiles, host.SourceFiles), "Actual initialized installed source differs from sealed declaration.");
        JsonElement executions = report.GetProperty("executions");
        Require(executions.GetArrayLength() == 1, "Fresh entry requires exactly one actual invocation window.");
        foreach (string boundary in new[] { "start_owners", "end_owners" })
        {
            JsonElement owner = executions[0].GetProperty(boundary);
            Require(owner.GetProperty("retained_memory_owner_id").GetString() == host.LifecycleId.ToString() && owner.GetProperty("battle_instance_id").GetString() == host.BattleInstanceId.ToString(), "Invocation owner differs from independently initialized fresh-entry source receipt.");
        }
        JsonElement stages = initialization.GetProperty("ordered_stages");
        foreach (string stage in new[] { "installed-cold-process", "canonical-encounter-construction", "resource-derived-new-game" })
        {
            Require(stages.EnumerateArray().Any(value => value.GetString() == stage), "Fresh entry lacks its original initialization stages.");
        }
    }

    public static FreshEntryPairVerdict ValidateFiles(FreshEntryPairPins pins)
    {
        List<LiveEvidenceVerdict> verdicts = [];
        List<string> errors = [];
        try
        {
            Require(pins.Entries.Length == 2 && pins.Entries.All(entry => entry is not null), "Exactly two independently retained entries are required.");
            Require(!string.Equals(pins.Entries[0].Report.Sha256, pins.Entries[1].Report.Sha256, StringComparison.OrdinalIgnoreCase) && !string.Equals(pins.Entries[0].Initialization.Sha256, pins.Entries[1].Initialization.Sha256, StringComparison.OrdinalIgnoreCase), "Pair cannot reuse one retained report or initialization receipt.");
            using JsonDocument recipe = ReadJson(pins.Recipe);
            JsonElement bootstrap = recipe.RootElement;
            DiagnosticFreshEntryDeclaration declaration = bootstrap.GetProperty("driver").GetProperty("fresh_entry").Deserialize<DiagnosticFreshEntryDeclaration>(Json) ?? throw new InvalidDataException("Pinned recipe has no single-use declaration.");
            declaration.Validate();
            string root = System.IO.Path.GetFullPath(pins.GameDirectory);
            long totalSourceBytes = 0;
            foreach (DiagnosticFreshEntrySourceFile file in declaration.SourceFiles)
            {
                string path = System.IO.Path.GetFullPath(file.RelativePath, root);
                Require(path.StartsWith(System.IO.Path.TrimEndingDirectorySeparator(root) + System.IO.Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal), "Pinned source leaves installed game directory.");
                totalSourceBytes = checked(totalSourceBytes + new FileInfo(path).Length);
                Require(totalSourceBytes <= 8589934592L, "Selected installed sources exceed their total bounded size.");
                CheckFile(new(path, file.Sha256), 2147483648L);
            }
            CheckFile(pins.OriginalExecutable, 2147483648L);
            using JsonDocument stackJson = ReadJson(pins.ExecutionStack);
            NativeBattleExecutionStack stack = stackJson.RootElement.Deserialize<NativeBattleExecutionStack>() ?? throw new InvalidDataException("Pinned execution stack is absent.");
            stack.Validate();
            Require(string.Equals(stack.ExecutableHash, pins.OriginalExecutable.Sha256, StringComparison.OrdinalIgnoreCase), "Execution-stack source names a different original executable than the sealed actual EXE.");
            string stackHash = Hash(JsonSerializer.SerializeToUtf8Bytes(stack));
            BuildFile[] files = EvidenceStore.DescribeFiles(pins.CandidateDirectory);
            Require(Hash(JsonSerializer.SerializeToUtf8Bytes(files, RecordJson.Options)) == pins.CandidateFingerprint, "Actual candidate files differ from the sealed build fingerprint.");
            string hostHash = CheckAssembly("Memoria.FF8.dll");
            string runtimeHash = CheckAssembly("Memoria.FF8.Runtime.dll");
            HashSet<Guid> battles = [];
            HashSet<Guid> lifecycles = [];
            HashSet<Guid> hosts = [];
            HashSet<(int Process, long Started)> processes = [];
            foreach (FreshEntryRunPins entry in pins.Entries)
            {
                using JsonDocument document = ReadJson(entry.Report);
                using JsonDocument independentInitialization = ReadJson(entry.Initialization);
                JsonElement report = document.RootElement;
                JsonElement initialization = report.GetProperty("initialization");
                Require(JsonElement.DeepEquals(initialization, independentInitialization.RootElement), "Report substituted its separately retained actual initialization receipt.");
                Require(JsonElement.DeepEquals(initialization.GetProperty("preparation"), bootstrap), "Initialized source/recipe differs from the sealed identical recipe.");
                Require(string.Equals(initialization.GetProperty("executable_sha256").GetString(), pins.OriginalExecutable.Sha256, StringComparison.OrdinalIgnoreCase), "Initialized original executable differs from sealed source.");
                ValidateProtocol(report, true);
                DiagnosticFreshEntryHostReceipt host = initialization.GetProperty("fresh_entry_host").Deserialize<DiagnosticFreshEntryHostReceipt>(Json)!;
                Require(host.ManagedHostSha256 == hostHash && host.ManagedRuntimeSha256 == runtimeHash && host.ExecutionStackSha256 == stackHash, "Actual initialized host/runtime/execution stack differs from sealed source.");
                Require(hosts.Add(host.HostInstanceId) && processes.Add((host.ProcessId, host.ProcessStartedUtcTicks)), "Entries must come from distinct actual process starts.");
                Guid battle = Guid.Parse(report.GetProperty("battle_instance_id").GetString()!);
                Require(battle != Guid.Empty && battles.Add(battle), "Entries must have independent initialized battles.");
                JsonElement executions = report.GetProperty("executions");
                Require(executions.GetArrayLength() == 1, "Each fresh entry must contain exactly one actual use.");
                Guid lifecycle = Guid.Parse(executions[0].GetProperty("start_owners").GetProperty("retained_memory_owner_id").GetString()!);
                Require(lifecycle == host.LifecycleId && lifecycle != Guid.Empty && lifecycles.Add(lifecycle), "Entries must have independent retained native owners.");
                LiveEvidenceVerdict verdict = LiveEvidenceValidation.ValidateFreshEntry(report);
                verdicts.Add(verdict);
                Require(verdict.Executions.Length == 1 && verdict.Errors.All(error => error == "PRESENTATION_BELOW_30_FPS"), "Fresh entry failed strict one-use native/semantic/integrity validation.");
            }

            string CheckAssembly(string name)
            {
                string path = System.IO.Path.Combine(pins.CandidateDirectory, name);
                Require(files.Any(file => System.IO.Path.GetFileName(file.Path) == name), "Pinned build lacks its actual managed host/runtime assembly.");
                using FileStream stream = File.OpenRead(path);
                return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or ArgumentException or InvalidOperationException or KeyNotFoundException or OverflowException or NotSupportedException or NullReferenceException)
        {
            errors.Add(exception.Message);
        }
        return new(errors.Count == 0 && verdicts.Count == 2, errors.ToArray(), verdicts.ToArray());
    }

    private static bool Present(JsonElement value, string name) => value.TryGetProperty(name, out JsonElement child) && child.ValueKind != JsonValueKind.Null;
    private static bool SameOptional(JsonElement first, JsonElement second, string name) => first.TryGetProperty(name, out JsonElement a) && second.TryGetProperty(name, out JsonElement b) ? JsonElement.DeepEquals(a, b) : !Present(first, name) && !Present(second, name);
    private static bool SameSources(ImmutableArray<DiagnosticFreshEntrySourceFile> first, ImmutableArray<DiagnosticFreshEntrySourceFile> second) => !second.IsDefault && first.Length == second.Length && first.Zip(second).All(pair => pair.Second is not null && pair.First.RelativePath.Replace('\\', '/') == pair.Second.RelativePath.Replace('\\', '/') && string.Equals(pair.First.Sha256, pair.Second.Sha256, StringComparison.OrdinalIgnoreCase));
    private static bool HashValid(string? hash) => hash is { Length: 64 } && hash.All(Uri.IsHexDigit);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static JsonDocument ReadJson(FreshEntryFilePin pin)
    {
        CheckFile(pin, 64 * 1024 * 1024);
        byte[] bytes = File.ReadAllBytes(pin.Path);
        Require(Hash(bytes).Equals(pin.Sha256, StringComparison.OrdinalIgnoreCase), "Retained evidence changed while reading.");
        JsonDocument document = JsonDocument.Parse(bytes);
        try
        {
            LiveEvidenceValidation.RejectDuplicateProperties(document.RootElement);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void CheckFile(FreshEntryFilePin pin, long maximum)
    {
        Require(System.IO.Path.IsPathFullyQualified(pin.Path) && HashValid(pin.Sha256), "Evidence requires explicit absolute input paths and SHA256 pins.");
        FileInfo file = new(pin.Path);
        Require(file.Exists && file.Length > 0 && file.Length <= maximum, "Evidence file is absent or outside its bounded size.");
        for (FileSystemInfo? current = file; current is not null; current = current is FileInfo item ? item.Directory : ((DirectoryInfo)current).Parent)
        {
            Require(!current.Exists || (current.Attributes & FileAttributes.ReparsePoint) == 0, "Evidence cannot use links or junctions.");
        }
        using FileStream stream = File.OpenRead(pin.Path);
        Require(Convert.ToHexString(SHA256.HashData(stream)).Equals(pin.Sha256, StringComparison.OrdinalIgnoreCase), "Evidence differs from sealed hash.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
}
