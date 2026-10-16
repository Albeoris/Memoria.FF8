using Albeoris.Games.FF8.Imaging.Tim;

namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class WorkspaceImageEntry
{
    public TimPixelMode PixelMode { get; init; }
    public Int16 X { get; init; }
    public Int16 Y { get; init; }
    public TimFlags AdditionalFlags { get; init; }
    public Int32? PaletteIndex { get; init; }
    public Int32? DeclaredBlockSizeOverride { get; init; }
    public Int16? EmptyWidthInWords { get; init; }
    public Int16 ClutX { get; init; }
    public Int16 ClutY { get; init; }
    public Int16? ClutWidth { get; init; }
    public UInt16[]? ClutTrailingColors { get; init; }
    public Int16? ClutDeclaredHeightOverride { get; init; }
}