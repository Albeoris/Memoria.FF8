using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private readonly EncodedResourceStore voiceStore = new();
    private readonly Dictionary<ModFile, EncodedResource> voiceSnapshots = [];

    private string? preparedVoiceSignature;
    private ExternalVoiceResolver? preparedVoiceResolver;

    public ExternalVoiceResolver? VoiceResolver { get; private set; }

    private bool PrepareVoices(ModCatalogSnapshot catalog, IReadOnlyList<EncodedResource> fieldResources, out ExternalVoiceResolver? resolver, out IReadOnlyList<EncodedResource> resources, out IReadOnlyList<ModDiagnostic> diagnostics)
    {
        HashSet<string> suppliedGroups = catalog.Files.Where(file => file.Format == ModFormat.Junction).Select(file => (File: file, Key: JunctionEncodedResources.Address(file.RelativePath))).Where(item => item.Key is ResourceKey key && JunctionEncodedResources.FieldGroup(key) is not null).Select(item => item.File.PackageId + "\0" + JunctionEncodedResources.FieldGroup(item.Key!.Value)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        HashSet<string> selectedGroups = fieldResources.Where(resource => JunctionEncodedResources.FieldGroup(resource.Key) is not null).Select(resource => (Resource: resource, File: catalog.Files.First(file => $"{file.PackageId}:{file.RelativePath}" == resource.Content.Provenance))).Select(item => item.File.PackageId + "\0" + JunctionEncodedResources.FieldGroup(item.Resource.Key)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool Eligible(ModFile file)
        {
            string[] parts = file.RelativePath.Split('/');
            string group = file.PackageId + "\0" + (parts.Length > 1 ? parts[1] : "");
            return !suppliedGroups.Contains(group) || selectedGroups.Contains(group);
        }
        diagnostics = ExternalVoiceResolver.Validate(catalog, Eligible);
        resolver = null;
        resources = [];
        if (diagnostics.Count != 0)
        {
            return false;
        }
        string signature = string.Join("\n", catalog.Files.Where(file => file.RelativePath.StartsWith("voice/", StringComparison.OrdinalIgnoreCase) && Eligible(file) && catalog.Packages.Any(package => package.Id == file.PackageId && package.Status == ModEffectiveStatus.Active)).Select(file => $"{file.PackageId}:{file.RelativePath}:{file.Revision}"));
        if (preparedVoiceResolver is null || preparedVoiceSignature != signature)
        {
            preparedVoiceResolver = new ExternalVoiceResolver(catalog, Eligible);
            preparedVoiceSignature = signature;
        }
        resolver = preparedVoiceResolver;
        List<EncodedResource> frozen = [];
        foreach (ModFile file in resolver.Files)
        {
            try
            {
                if (!voiceSnapshots.TryGetValue(file, out EncodedResource? resource))
                {
                    ResourceKey key = ExternalVoiceResolver.Address(file)!.Value;
                    resource = voiceStore.Store(key, "ogg", file.Revision, $"{file.PackageId}:{file.RelativePath}", file.CopyTo);
                    voiceSnapshots.Add(file, resource);
                }
                frozen.Add(resource);
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                diagnostics = [new("Junction.InvalidVoiceResource", exception.Message, file.PackageId, file.RelativePath)];
                return false;
            }
        }
        resources = frozen;
        return true;
    }
}
