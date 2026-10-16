namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class WorkspaceManifest
{
    public const Int32 CurrentVersion = 2;
    public const String TimFormat = "albeoris.ff8.tim-workspace";
    public const String TdwFormat = "albeoris.ff8.tdw-workspace";

    public required String Format { get; init; }
    public Int32 Version { get; init; } = CurrentVersion;
    public required List<WorkspaceImageEntry> Images { get; init; }
}