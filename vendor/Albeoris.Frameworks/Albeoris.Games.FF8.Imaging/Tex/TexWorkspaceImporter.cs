using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;

namespace Albeoris.Games.FF8.Imaging.Tex;

public static class TexWorkspaceImporter
{
    public static TexFile Import(WorkspaceImportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        using (IWorkspaceReader input = WorkspaceStorage.CreateReader(spec.Source))
        {
            TexWorkspaceManifest manifest = WorkspaceJson.Read<TexWorkspaceManifest>(
                input,
                WorkspaceManifestSerializer.FileName);
            if (!String.Equals(manifest.Format, TexWorkspaceManifest.FormatName, StringComparison.Ordinal))
                throw new InvalidDataException($"Workspace format '{manifest.Format}' is not '{TexWorkspaceManifest.FormatName}'.");
            if (manifest.Version != TexWorkspaceManifest.CurrentVersion)
                throw new InvalidDataException($"Unsupported TEX workspace version {manifest.Version}.");
            return TexWorkspaceCodec.Import(manifest, spec, input);
        }
    }
}
