namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class TexWorkspaceManifest
{
    public const String FormatName = "albeoris.ff8.tex-workspace";
    public const Int32 CurrentVersion = 1;

    public String Format { get; init; } = FormatName;
    public Int32 Version { get; init; } = CurrentVersion;
    public required TexHeaderSettings Header { get; init; }
    public Int32? SelectedPaletteIndex { get; init; }
    public Int32?[]? PaletteColorKeyIndices { get; init; }
}