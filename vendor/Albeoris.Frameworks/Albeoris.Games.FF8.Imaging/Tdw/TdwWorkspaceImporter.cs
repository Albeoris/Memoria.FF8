using Albeoris.Games.FF8.Imaging.Tim;
using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;

namespace Albeoris.Games.FF8.Imaging.Tdw;

public static class TdwWorkspaceImporter
{
    public static TdwFile Import(WorkspaceImportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        using (IWorkspaceReader input = WorkspaceStorage.CreateReader(spec.Source))
        {
            WorkspaceManifest manifest = WorkspaceManifestSerializer.Read(input, WorkspaceManifest.TdwFormat);
            Byte[] widths = TdwWidthTable.Read(input);
            TimFile? tim = manifest.Images.Count == 0 ? null : TimWorkspaceCodec.Import(manifest, spec, input);
            return new TdwFile(widths, tim);
        }
    }
}
