using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.BattleEffects;

public static class RecordJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    public static T Read<T>(string path) where T : notnull => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path), Options) ?? throw new InvalidDataException($"Empty record: {path}");
}

public sealed record InventoryEntry(string EntryId, string ActionId, string EffectKey, int NativeEffectId, string Disposition, string[] CaseIds, string[] MappingEvidence, string? Reason);
public sealed record InventoryRecord(int SchemaVersion, string ResourceHash, InventoryEntry[] Entries);
public sealed record BuildFile(string Path, long Bytes, string Sha256);
public sealed record BuildRecord(int SchemaVersion, string SourceCommit, bool Dirty, string? PatchHash, string Configuration, BuildFile[] Files);
public sealed record ResourceRecord(int SchemaVersion, string ProfileId, BuildFile[] Files, string[] EnabledMods);
public sealed record PerformanceProfile(int SchemaVersion, string ProfileId, int Width, int Height, string Renderer, double HostFps, string AudioMode, string WindowMode, string Vsync, string Pacing, string UiOverlays, string InstrumentationVersion, string[] Mods);
public sealed record InvocationRecipe(string Command, string CommandOwner, string InvocationPath, string[] Inputs, string RngPolicy, string InitialRngState, bool DiagnosticCapability, string[] AuxiliaryResources);
public sealed record ScenarioRecipe(int FormationId, string Phase, int StageId, string Caster, string TargetPolicy, string[] ExpectedTargets, string[] ActorResources, string[] SceneSlots, string LevelBand, string[] AiPrerequisites, string InitializationRecipe, string[] Preparation, string[] MappingEvidence, string[] LegalityEvidence);
public sealed record CaseTimeouts(double PreparationMs, double ActionMs, double CleanupMs, double TeardownMs, double ProcessMs, string Basis);
public sealed record CaseRecipe(int SchemaVersion, string CaseId, string EffectKey, string ActionId, int RequestedEffectId, string Variant, string ScenarioKind, ScenarioRecipe Scenario, InvocationRecipe Invocation, int Executions, string[] AllowedRepairs, string TerminalPolicy, string[] Milestones, string[] CompletionConditions, string[] IntegrityChecks, string[] PermittedOutcomes, string[] RequiredInstrumentation, CaseTimeouts Timeouts, string[] Blockers);
public sealed record EffectPlan(int SchemaVersion, string EffectKey, CaseRecipe[] Cases);
public sealed record CampaignRecord(int SchemaVersion, string CampaignId, int PlanRevision, string? ParentCampaign, string PlanHash, string BuildHash, string ProfileHash, string ResourceHash, string InstrumentationVersion, DateTimeOffset CreatedUtc);
public sealed record PassRecord(int SchemaVersion, string RunPassId, string CampaignId, string PlanHash, string RunKind, string BuildHash, string ProfileHash, string ResourceHash, string InstrumentationVersion, string[] RequiredCases, DateTimeOffset CreatedUtc);
public sealed record PlanIssue(string Identity, string Message);
public sealed record PlanValidation(bool Valid, PlanIssue[] Issues, int Effects, int Cases, int Unresolved);
public sealed record SupervisorLaunch(int SchemaVersion, string Executable, string[] Arguments, string WorkingDirectory, double NoProgressMs, Dictionary<string, string>? Environment);
