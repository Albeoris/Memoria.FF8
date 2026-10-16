namespace Albeoris.Games.FF8.Imaging.Workspace.Internal;

internal static class WorkspaceManifestSerializer
{
    public const String FileName = "manifest.json";

    public static void Write(IWorkspaceWriter output, WorkspaceManifest manifest) =>
        WorkspaceJson.Write(output, FileName, manifest);

    public static WorkspaceManifest Read(IWorkspaceReader input, String expectedFormat)
    {
        WorkspaceManifest manifest = WorkspaceJson.Read<WorkspaceManifest>(input, FileName);
        if (!String.Equals(manifest.Format, expectedFormat, StringComparison.Ordinal))
            throw new InvalidDataException($"Workspace format '{manifest.Format}' is not '{expectedFormat}'.");
        if (manifest.Version != WorkspaceManifest.CurrentVersion)
            throw new InvalidDataException($"Unsupported workspace version {manifest.Version}.");
        if (manifest.Images is null)
            throw new InvalidDataException("The workspace manifest has no images collection.");
        if (expectedFormat == WorkspaceManifest.TimFormat && manifest.Images.Count == 0)
            throw new InvalidDataException("A TIM workspace must describe at least one image.");
        return manifest;
    }
}