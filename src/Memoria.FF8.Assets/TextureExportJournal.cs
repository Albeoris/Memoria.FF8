using System.Text;
using System.Text.Json;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

public sealed record TextureExportIndexEntry(string Path, TextureTarget? Target, TextureTargetContract? Contract, string Companion, string? SourceFingerprint, string? Baseline, string Kind, IReadOnlyList<string>? SourceAliases = null, string? CompanionRole = null);

/// <summary>Bounded authoring bookkeeping. Disposal always writes an honest incomplete report unless Complete succeeded.</summary>
public sealed class TextureExportJournal : IDisposable
{
    private readonly string root;
    private readonly string scope;
    private readonly string provider;
    private readonly string language;
    private readonly bool resolved;
    private readonly List<TextureExportIndexEntry> entries = [];
    private readonly List<TextureCatalogDiagnostic> diagnostics = [];
    private bool complete;
    private bool disposed;
    private int aliases;
    private long bytes;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public TextureExportJournal(string destination, string scope, string provider, string language, bool resolved)
    {
        root = Path.GetFullPath(destination);
        this.scope = scope;
        this.provider = provider;
        this.language = language;
        this.resolved = resolved;
        Directory.CreateDirectory(root);
        if (File.Exists(TextureExchange.SafeOutput(root, "texture-export-index.json")))
        {
            throw new IOException("This working folder already has an export index. Choose a fresh folder to preserve edited artwork and its baselines.");
        }
        Write();
    }

    public void AddImage(string path, TextureExchangeMetadata metadata, long writtenBytes, string? companionRole = null)
    {
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        entries.Add(new(relative, metadata.Target, metadata.Contract, relative + ".texture.json", metadata.SourceSha256, metadata.ExportedSemanticSha256 ?? metadata.ExportedPixelSha256, "image", metadata.SourceAliases, companionRole));
        bytes += writtenBytes;
    }

    public void AddWorkspace(string path)
    {
        string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        entries.Add(new(relative, null, null, relative + ".texture.json", null, null, "layered-field-workspace"));
        bytes += new FileInfo(path).Length + new FileInfo(path + ".texture.json").Length;
    }

    public void AddAlias() => aliases++;
    public void AddDiagnostic(TextureCatalogDiagnostic diagnostic) => diagnostics.Add(diagnostic);

    public void Complete()
    {
        complete = true;
        Write();
    }

    private void Write()
    {
        var outcomes = diagnostics.GroupBy(diagnostic => diagnostic.Outcome).ToDictionary(group => group.Key, group => group.Count());
        bool catalogTotalKnown = !diagnostics.Any(diagnostic => diagnostic.Outcome == "failed" && diagnostic.Operation.StartsWith("Discovering", StringComparison.Ordinal));
        byte[] index = JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, game = "ff8", scope, provider, language, resolved, complete, catalogTotalKnown, exported = entries.Count, deduplicatedAliases = aliases, bytesWritten = bytes, outcomes, entries, diagnostics }, Json);
        TextureExchange.WriteAtomic(TextureExchange.SafeOutput(root, "texture-export-index.json"), index, CancellationToken.None);
        string status = complete ? "Completed selected export scope." : "INCOMPLETE: export was cancelled, failed, or is still running. Completed files remain editable and importable.";
        string report = $"FFVIII texture export\n{status}\nScope: {scope}\nProvider: {provider}\nLanguage: {language}\nSource: {(resolved ? "With enabled texture mods" : "Original game textures")}\nWritten resources: {entries.Count}\nDeduplicated aliases: {aliases}\nBytes written: {bytes}\nDiscovery/processing diagnostics: {diagnostics.Count}\n\nTotals describe this resolved selection. Failed or unscanned archive contents are not a known complete game total.\n" + string.Join("\n", diagnostics.Select(diagnostic => $"{diagnostic.Operation}: {diagnostic.ResourcePath}: {diagnostic.Message}"));
        TextureExchange.WriteAtomic(TextureExchange.SafeOutput(root, "texture-export-report.txt"), Encoding.UTF8.GetBytes(report), CancellationToken.None);
        TextureExchange.WriteAtomic(TextureExchange.SafeOutput(root, "EDITING.txt"), Encoding.UTF8.GetBytes("Edit the PNG artwork or Pupu layers in the ORA document. Keep .texture.json companions beside their files. Preserve canonical textures/ paths to survive PNG metadata removal. Keep every Pupu layer identity, native opacity/blend and layer position; resize the entire ORA canvas uniformly by an integer factor. Hidden source/palette layers are engine-managed; source recovery uses the verified installed source and companion. Reference previews are not replacements. Use F2 > Textures > Import files/folder, review targets, choose a new or existing native mod, then Import and apply. Installed mod textures are watched; external working folders require Reimport changed files. Export to a fresh folder to preserve existing artwork.\n"), CancellationToken.None);
    }

    public void Dispose()
    {
        if (!disposed)
        {
            disposed = true;
            Write();
        }
    }
}
