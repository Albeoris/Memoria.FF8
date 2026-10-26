using System.Text.Json;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Resources.Localization;

public sealed record LocalizationProject
{
    public LocalizationManifest Manifest { get; init; } = new();
    public IReadOnlyList<LocalizationRecord> Entries { get; init; } = [];
}

public sealed record LocalizationManifest
{
    public int SchemaVersion { get; init; } = 1;
    public string PayloadKind { get; init; } = "localization";
    public string ProjectId { get; init; } = Guid.NewGuid().ToString("D");
    public string? BaseProjectId { get; init; }
    public string SourceLanguage { get; init; } = "en";
    public string TargetLanguage { get; init; } = "";
    public string FieldScriptBaseLanguage { get; init; } = "en";
    public string SourceColumn { get; init; } = "Source [en]";
    public string TargetColumn { get; init; } = "Target";
    public IReadOnlyDictionary<string, string> ReferenceLanguages { get; init; } = new Dictionary<string, string>();
    public string MappingVersion { get; init; } = "1";
    public IReadOnlyList<string> CompatibleVariants { get; init; } = [];
    public string Format { get; init; } = "json";
    public string Layout { get; init; } = "single";
    public IReadOnlyList<LocalizationPayload> Payloads { get; init; } = [];
    public string? ExtensionsPath { get; init; }
    [JsonExtensionData]
    public Dictionary<string, JsonElement> ExtensionData { get; init; } = new(StringComparer.Ordinal);
}

public sealed record LocalizationPayload
{
    public string Path { get; init; } = "";
    public string? Category { get; init; }
    public string? File { get; init; }
    public IReadOnlyDictionary<string, string> Worksheets { get; init; } = new Dictionary<string, string>();
}

public sealed record LocalizationRecord
{
    public string Category { get; init; } = "";
    public string File { get; init; } = "";
    public string Key { get; init; } = "";
    public string? Source { get; init; }
    public bool SourcePresent { get; init; } = true;
    public string? Target { get; init; }
    public IReadOnlyDictionary<string, string?> References { get; init; } = new Dictionary<string, string?>();
    public IReadOnlyDictionary<string, bool> ReferencePresence { get; init; } = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, JsonElement> ExtensionData { get; init; } = new Dictionary<string, JsonElement>();
}
