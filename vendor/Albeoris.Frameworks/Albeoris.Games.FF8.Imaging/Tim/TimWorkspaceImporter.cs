using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;

namespace Albeoris.Games.FF8.Imaging.Tim;

public static class TimWorkspaceImporter
{
    public static TimFile Import(WorkspaceImportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        using (IWorkspaceReader input = WorkspaceStorage.CreateReader(spec.Source))
        {
            WorkspaceManifest manifest = WorkspaceManifestSerializer.Read(input, WorkspaceManifest.TimFormat);
            return TimWorkspaceCodec.Import(manifest, spec, input);
        }
    }
}
