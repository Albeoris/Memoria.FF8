namespace Albeoris.Games.FF8.Imaging.Workspace;

public sealed class WorkspaceExportSpec
{
    public WorkspaceExportSpec(WorkspaceLocation destination) => Destination = destination;

    public WorkspaceLocation Destination { get; }
    public Boolean GeneratePreviews { get; init; } = true;
    public Int32 DefaultPaletteIndex { get; init; }
    public IWorkspaceExportScheduler Scheduler { get; init; } = InlineWorkspaceExportScheduler.Instance;
    public IReadOnlyDictionary<Int32, Int32> PaletteIndices { get; init; } =
        new Dictionary<Int32, Int32>();

    internal Int32 GetPaletteIndex(Int32 imageIndex) =>
        PaletteIndices.TryGetValue(imageIndex, out Int32 paletteIndex)
            ? paletteIndex
            : DefaultPaletteIndex;
}
