using Memoria.FF8.Resources;

namespace Memoria.FF8.Assets;

/// <summary>Searchable source metadata. Entries never retain decoded pixels or GPU resources.</summary>
public sealed record TextureCatalogEntry(TextureTarget Target, string Name, string Category, string Layout, int LogicalWidth, int LogicalHeight)
{
    public string SearchText => $"{Name} {Category} {Target.Owner} {Target.Subresource} palette {Target.Palette}";
    public string? LoadError { get; init; }
    public string ResourcePath => Target.Owner;
    public string SourceUnit => Reader switch
    {
        "model" => "model:" + Model?.Id,
        "cinematic" => $"cinematic:{Section}",
        "world-section" => $"world-section:{Section}",
        _ => $"{Reader}:{ResourcePath}"
    };
    internal string Reader { get; init; } = "";
    internal ModelCatalogEntry? Model { get; init; }
    internal int Index { get; init; }
    internal int Section { get; init; }

    public bool Matches(string? query) => string.IsNullOrWhiteSpace(query) || query.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(word => SearchText.Contains(word, StringComparison.OrdinalIgnoreCase));
}

public sealed record TextureCatalogDiagnostic(string ResourcePath, string Operation, string Message);
