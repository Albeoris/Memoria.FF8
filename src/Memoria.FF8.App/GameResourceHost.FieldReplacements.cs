using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private IReadOnlyList<ModDiagnostic> ValidateFieldReplacementResources(ModCatalogSnapshot catalog, IReadOnlyList<EncodedResource> encodedResources)
    {
        IGrouping<string, EncodedResource>[] fields = encodedResources.Where(resource => JunctionEncodedResources.FieldGroup(resource.Key) is not null).GroupBy(resource => JunctionEncodedResources.FieldGroup(resource.Key)!, StringComparer.OrdinalIgnoreCase).ToArray();
        if (fields.Length == 0)
        {
            return [];
        }
        ResourceService candidate = new(source);
        candidate.Prepare(null, encodedResources: encodedResources).Commit();
        using GameAssets assets = new(gameDirectory, resources: candidate);
        List<ModDiagnostic> diagnostics = [];
        foreach (IGrouping<string, EncodedResource> field in fields)
        {
            ModFile[] owners = field.Select(resource => catalog.Files.First(file => $"{file.PackageId}:{file.RelativePath}" == resource.Content.Provenance)).ToArray();
            try
            {
                FieldLogicResources inputs = assets.ReadFieldLogicResources(field.Key);
                if (!inputs.HasColorCorrections)
                {
                    ModFile[] scopeOwners = field.Where(resource => resource.ExclusiveScope == new ResourceKey($"field/{field.Key}")).Select(resource => catalog.Files.First(file => $"{file.PackageId}:{file.RelativePath}" == resource.Content.Provenance)).ToArray();
                    foreach (ModFile owner in (scopeOwners.Length > 0 ? scopeOwners : owners).GroupBy(file => file.PackageId).Select(group => group.FirstOrDefault(file => Path.GetExtension(file.RelativePath).Equals(".fs", StringComparison.OrdinalIgnoreCase)) ?? group.First()))
                    {
                        diagnostics.Add(new("Junction.MissingFieldColors", $"{field.Key}: PCB color corrections are absent; models retain their own palettes.", owner.PackageId, owner.RelativePath));
                    }
                }
                JsmResourceValidationResult validation = JsmResourceValidation.Validate(inputs.Program, inputs.Messages.Count);
                foreach (JsmResourceValidationIssue issue in validation.Errors)
                {
                    foreach (ModFile owner in owners.DistinctBy(file => file.PackageId))
                    {
                        diagnostics.Add(new("Junction.InvalidFieldResource", $"{field.Key}, script {issue.ScriptId}, operation {issue.Operation} ({issue.Code}): {issue.Message}", owner.PackageId, owner.RelativePath));
                    }
                }
                if (validation.DynamicMessageReferences > 0)
                {
                    foreach (ModFile owner in owners.DistinctBy(file => file.PackageId))
                    {
                        diagnostics.Add(new("Junction.DynamicFieldTextReference", $"{field.Key}: {validation.DynamicMessageReferences} message references depend on runtime values and remain subject to runtime bounds checks.", owner.PackageId, owner.RelativePath));
                    }
                }
                if (field.Any(resource => Path.GetExtension(resource.Key.Path) is ".map" or ".mim" or ".id" or ".ca" or ".one" or ".msk"))
                {
                    assets.LoadField(inputs.Id);
                }
                if (field.Any(resource => Path.GetExtension(resource.Key.Path) == ".sfx"))
                {
                    assets.LoadFieldSoundEffects(inputs.Id);
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or NotSupportedException or OverflowException or UnauthorizedAccessException)
            {
                foreach (ModFile owner in owners.DistinctBy(file => file.PackageId))
                {
                    diagnostics.Add(new("Junction.InvalidFieldResource", $"{field.Key}: {exception.Message}", owner.PackageId, owner.RelativePath));
                }
            }
        }
        return diagnostics;
    }
}
