using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;

namespace Albeoris.Games.FF8.Imaging.MimMap;

public static class MimMapWorkspaceImporter
{
    public static MimMapFile Import(WorkspaceImportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Source.Format != WorkspaceContainerFormat.OpenRaster)
            throw new ArgumentException("A MIM/MAP workspace must use the OpenRaster container format.", nameof(spec));
        using IWorkspaceReader input = WorkspaceStorage.CreateReader(spec.Source);
        return MimMapWorkspaceCodec.Import(spec, input);
    }
}
