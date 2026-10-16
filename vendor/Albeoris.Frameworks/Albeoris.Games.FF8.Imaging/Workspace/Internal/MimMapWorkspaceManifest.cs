namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal sealed class MimMapWorkspaceManifest
{
    public const String FormatName = "albeoris.ff8.mim-map-workspace";
    public const Int32 CurrentVersion = 2;

    public String Format { get; init; } = FormatName;
    public Int32 Version { get; init; } = CurrentVersion;
}
