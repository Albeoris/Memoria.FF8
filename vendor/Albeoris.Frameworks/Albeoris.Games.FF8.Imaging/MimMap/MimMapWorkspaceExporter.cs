using Albeoris.Games.FF8.Imaging.Map;
using Albeoris.Games.FF8.Imaging.Mim;
using Albeoris.Games.FF8.Imaging.Workspace;
using Albeoris.Games.FF8.Imaging.Workspace.Internal;

namespace Albeoris.Games.FF8.Imaging.MimMap;

public static class MimMapWorkspaceExporter
{
    public static void Export(MimMapFile file, WorkspaceExportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(spec);
        if (spec.Destination.Format != WorkspaceContainerFormat.OpenRaster)
            throw new ArgumentException("A MIM/MAP workspace must use the OpenRaster container format.", nameof(spec));
        using IWorkspaceWriter output = WorkspaceStorage.CreateWriter(spec.Destination);
        MimMapWorkspaceCodec.Export(file, output, spec.Scheduler);
    }

    public static void Export(MimFile mim, MapFile map, WorkspaceExportSpec spec) => Export(new MimMapFile(mim, map), spec);
}
