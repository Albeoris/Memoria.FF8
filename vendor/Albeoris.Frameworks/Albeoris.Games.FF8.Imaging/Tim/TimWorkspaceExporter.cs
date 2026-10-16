using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;

namespace Albeoris.Games.FF8.Imaging.Tim;

public static class TimWorkspaceExporter
{
    public static void Export(TimFile file, WorkspaceExportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(spec);
        using (IWorkspaceWriter output = WorkspaceStorage.CreateWriter(spec.Destination))
        {
            WorkspaceManifest manifest = TimWorkspaceCodec.Export(file, spec, output, WorkspaceManifest.TimFormat);
            WorkspaceManifestSerializer.Write(output, manifest);
        }
    }
}
