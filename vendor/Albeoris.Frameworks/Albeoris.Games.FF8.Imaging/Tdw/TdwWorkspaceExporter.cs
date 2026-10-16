using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;

namespace Albeoris.Games.FF8.Imaging.Tdw;

public static class TdwWorkspaceExporter
{
    public static void Export(TdwFile file, WorkspaceExportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(spec);
        using (IWorkspaceWriter output = WorkspaceStorage.CreateWriter(spec.Destination))
        {
            TdwWidthTable.Write(output, file.Widths);
            WorkspaceManifest manifest = file.Tim is null
                ? new WorkspaceManifest { Format = WorkspaceManifest.TdwFormat, Images = [] }
                : TimWorkspaceCodec.Export(file.Tim, spec, output, WorkspaceManifest.TdwFormat);
            WorkspaceManifestSerializer.Write(output, manifest);
        }
    }
}
