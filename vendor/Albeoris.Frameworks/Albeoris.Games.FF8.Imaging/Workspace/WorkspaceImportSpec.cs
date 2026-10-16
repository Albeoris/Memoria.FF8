namespace Albeoris.Games.FF8.Imaging.Workspace;

public sealed class WorkspaceImportSpec
{
    public WorkspaceImportSpec(WorkspaceLocation source) => Source = source;

    public WorkspaceLocation Source { get; }
    public PaletteColorHandling MissingPaletteColor { get; init; } = PaletteColorHandling.RequireExactMatch;
    public PaletteDistanceAlgorithm DistanceAlgorithm { get; init; } = PaletteDistanceAlgorithm.Oklab;
}
