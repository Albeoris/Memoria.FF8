namespace Memoria.FF8.Resources.Text;

/// <summary>An explicitly selected native resource namespace; language roles are independent of this value.</summary>
public sealed record NativeTextContext(string Edition, string ResourceFamily, string? Field = null, string? DictionaryLanguage = null);
public sealed record NativeTextDomain(string Edition, string ResourceFamily, string? Field = null, string? DictionaryLanguage = null)
{
    public bool Matches(NativeTextContext context) => Edition == context.Edition && ResourceFamily == context.ResourceFamily && (Field is null || Field == context.Field) && (DictionaryLanguage is null || DictionaryLanguage == context.DictionaryLanguage);
}

public sealed record NativeFontAtlas(string Id, string Path, int Width, int Height, string? Sha256 = null, int Palette = 0);
public sealed record NativeGlyphImage(int Index, string AtlasId, int X, int Y, int Width, int Height, float Advance, float BearingX = 0, float BearingY = 0);
public sealed record NativeGlyphMapping(string Text, int ImageIndex, string? Field = null);
public sealed record NativeCodecMapping(string Hex, string Text, bool Canonical = true, bool Control = false, string? Field = null);
public sealed record NativeProfileDependency(string Path, string? Sha256 = null);
public sealed record NativeProfileAssignment(string Role, string Dataset, string ProfileId, NativeTextContext Context);

/// <summary>Image identity is profile scoped and independent of native bytes. One profile can use many atlases.</summary>
public sealed record NativeFontProfile
{
    public int FormatVersion { get; init; } = 1;
    public string Id { get; init; } = Guid.NewGuid().ToString("D");
    public string DisplayName { get; init; } = "New profile";
    public float LineHeight { get; init; } = 12;
    public int LogicalWidth { get; init; } = 320;
    public int LogicalHeight { get; init; } = 240;
    public IReadOnlyList<NativeTextDomain> Domains { get; init; } = [];
    public IReadOnlyList<NativeFontAtlas> Atlases { get; init; } = [];
    public IReadOnlyList<NativeGlyphImage> Images { get; init; } = [];
    public IReadOnlyList<NativeGlyphMapping> Glyphs { get; init; } = [];
    public IReadOnlyList<NativeCodecMapping> Codec { get; init; } = [];
    public IReadOnlyList<string> ReservedPrefixes { get; init; } = [];
    public IReadOnlyList<NativeProfileDependency> Dependencies { get; init; } = [];
    public IReadOnlyList<NativeProfileAssignment> Assignments { get; init; } = [];
}

public sealed record NativeTextDiagnostic(string Code, string Message, int Position = -1, int Length = 0);
public sealed record NativeGlyphRun(int TextPosition, int TextLength, NativeGlyphImage Image, float X, float Y, int Page);
public sealed record NativeFontLayoutResult(IReadOnlyList<NativeGlyphRun> Glyphs, float Width, float Height, int Pages, IReadOnlyList<NativeTextDiagnostic> Diagnostics)
{
    /// <summary>Pen position after the final line, distinct from the rightmost painted pixel edge.</summary>
    public float FinalAdvance { get; init; }
    public bool Success => Diagnostics.Count == 0;
}
