using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;

namespace Albeoris.Games.FF8.Imaging.Tex;

public static class TexWorkspaceExporter
{
    public static void Export(TexFile file, WorkspaceExportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(spec);
        using (IWorkspaceWriter output = WorkspaceStorage.CreateWriter(spec.Destination))
        {
            TexWorkspaceManifest manifest = TexWorkspaceCodec.Export(file, spec, output);
            WorkspaceJson.Write(output, WorkspaceManifestSerializer.FileName, manifest);
        }
    }
}
