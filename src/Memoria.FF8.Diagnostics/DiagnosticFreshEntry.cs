using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.BattleEffects;

public sealed record DiagnosticFreshEntrySourceFile([property: JsonRequired] string RelativePath, [property: JsonRequired] string Sha256)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RelativePath) || Path.IsPathRooted(RelativePath) || RelativePath.Contains(':') || RelativePath.Split('/', '\\').Any(part => part is "" or "." or "..") || !ValidHash(Sha256))
        {
            throw new InvalidDataException("Fresh-entry source pins require explicit relative installed files and SHA256.");
        }
    }

    internal static bool ValidHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiagnosticFreshEntryDeclaration([property: JsonRequired] string Kind, [property: JsonRequired] ImmutableArray<DiagnosticFreshEntrySourceFile> SourceFiles)
{
    public const string SingleUse = "single-use-fresh-entry-v1";

    public void Validate()
    {
        if (Kind != SingleUse || SourceFiles.IsDefaultOrEmpty || SourceFiles.Length > 16 || SourceFiles.Any(file => file is null || string.IsNullOrWhiteSpace(file.RelativePath)) || SourceFiles.Select(file => file.RelativePath.Replace('\\', '/')).Distinct(StringComparer.OrdinalIgnoreCase).Count() != SourceFiles.Length)
        {
            throw new InvalidDataException("Fresh entry requires its closed single-use protocol and 1..16 distinct installed source pins.");
        }
        foreach (DiagnosticFreshEntrySourceFile file in SourceFiles)
        {
            file.Validate();
        }
    }
}

public sealed record DiagnosticFreshEntryHostReceipt(Guid BattleInstanceId, Guid LifecycleId, Guid HostInstanceId, int ProcessId, long ProcessStartedUtcTicks, string ManagedHostSha256, string ManagedRuntimeSha256, string ExecutionStackSha256, ImmutableArray<DiagnosticFreshEntrySourceFile> SourceFiles);
