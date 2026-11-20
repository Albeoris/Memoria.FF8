using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.Resources;

namespace Memoria.FF8.Mods;

public static partial class ModPreviewResources
{
    private const string FieldGameplayPrefix = "gameplay:field/";

    private static bool IsFieldLightingDocument(string path) => path.Equals(FieldLightingSceneIO.ComponentPath, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("lighting/fields/", StringComparison.OrdinalIgnoreCase) && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

    private static ModPreviewComparison CompareFieldLighting(ModCatalogSnapshot catalog, ModFile file)
    {
        if (!FieldLightingMods.TryPrepare(catalog, out FieldLightingMods lighting, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw PreparationError(diagnostics);
        }
        if (file.RelativePath.Equals(FieldLightingSceneIO.ComponentPath, StringComparison.OrdinalIgnoreCase))
        {
            string[] fields = catalog.Files.Where(candidate => candidate != file && IsFieldLightingDocument(candidate.RelativePath))
                .Select(candidate => Path.GetFileNameWithoutExtension(candidate.RelativePath)).ToArray();
            return new(new("Fields use native lighting."), new(fields.Length == 0 ? "Native lighting is used throughout." : $"Lighting changes for {fields.Length} fields\n\n{string.Join("\n", fields)}"));
        }
        string field = Path.GetFileNameWithoutExtension(file.RelativePath);
        FieldLightingScene scene = lighting.ResolveScene(field) ?? throw new InvalidDataException("The field lighting document has no active component.");
        List<string> after = [field];
        foreach (FieldSpotLight light in scene.Lights)
        {
            string enabled = light.Enabled ? "enabled" : "disabled";
            after.Add($"{light.Name} · {enabled}\nPosition {light.Position} · Direction {light.Direction}\nColor {light.Color} · Intensity {light.Intensity:g} · Range {light.Range:g}\nBeam {light.InnerConeDegrees:g}°–{light.OuterConeDegrees:g}°");
        }
        if (scene.PrimaryLightId is { } primary)
        {
            after.Add($"Primary light {primary}\nShadow opacity {scene.GroundShadow.Opacity:P0} · Softness {scene.GroundShadow.Softness:g} texels");
        }
        after.AddRange(scene.ShadowReceivers.Select(receiver => $"{receiver.Id} · {receiver.Kind} shadow receiver · {(receiver.Enabled ? "enabled" : "disabled")}"));
        return new(new($"{field}\nNative field lighting"), new(string.Join("\n\n", after)));
    }

    internal static IEnumerable<ModPreviewEntry> DescribeFieldGameplay(ModCatalogSnapshot catalog)
    {
        if (!FieldSceneMods.TryPrepare(catalog, out FieldSceneMods scenes, out _))
        {
            yield break;
        }
        foreach (OwnedFieldScenePatch owned in scenes.Patches)
        {
            FieldScenePatch field = owned.Patch;
            foreach (FieldActorPatch actor in field.Actors)
            {
                yield return new($"{FieldGameplayPrefix}{field.FieldName}/actor/{actor.ObjectId}", "Fields", field.FieldName, $"Object {actor.ObjectId} model and animation");
            }
            if (field.Movement is not null)
            {
                yield return new($"{FieldGameplayPrefix}{field.FieldName}/movement", "Fields", field.FieldName, "Movement direction");
            }
            foreach (var group in AuthoredFieldStates(field).GroupBy(state => state.Module).Where(group => group.Count() > 1))
            {
                foreach ((int module, int state) in group)
                {
                    yield return new($"{FieldGameplayPrefix}{field.FieldName}/state/{module}/{state}", "Fields", field.FieldName, $"Module {module}, state {state}");
                }
            }
        }
    }

    private static ModPreviewComparison? CompareFieldGameplay(ModCatalogSnapshot catalog, ModPreviewEntry entry, ModPreviewResourcesProfile profile, CancellationToken cancellationToken)
    {
        if (!FieldSceneMods.TryPrepare(catalog, out FieldSceneMods scenes, out IReadOnlyList<ModDiagnostic> diagnostics))
        {
            throw PreparationError(diagnostics);
        }
        string[] selector = entry.Id[FieldGameplayPrefix.Length..].Split('/');
        OwnedFieldScenePatch? owner = scenes.Patches.FirstOrDefault(owned => owned.Patch.FieldName.Equals(selector[0], StringComparison.OrdinalIgnoreCase));
        if (owner is null)
        {
            return null;
        }
        FieldScenePatch change = owner.Patch;
        using IResourceSource source = OpenSource(profile);
        using GameAssets assets = new(profile.GameDirectory, resources: new(source, profile.Language));
        FieldAssets field = assets.LoadField(change.FieldId);
        if (!field.Name.Equals(change.FieldName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The field change does not match the selected installation.");
        }
        if (selector is [_, "state", string moduleSelector, string stateSelector]
            && int.TryParse(moduleSelector, NumberStyles.None, CultureInfo.InvariantCulture, out int module)
            && int.TryParse(stateSelector, NumberStyles.None, CultureInfo.InvariantCulture, out int state)
            && AuthoredFieldStates(change).Contains((module, state))
            && profile.CompareFieldScene is { } compare)
        {
            FieldScenePatch context = change with
            {
                Backgrounds = change.Backgrounds.Where(patch => patch.ModuleId != module || patch.State == state).ToArray(),
                Actors = change.Actors.Where(actor => actor.When is not { } when || when.ModuleId != module || when.State == state).ToArray()
            };
            return compare(field, field, [context], assets.LoadField, cancellationToken);
        }
        if (selector is [_, "movement"] && change.Movement is { } movement)
        {
            byte turn = unchecked((byte)(int)MathF.Round(movement.RotationDegrees * 256 / 360));
            string camera = movement.CameraIndex is int index ? $"Camera {index}" : "All cameras";
            if (movement.CameraIndex is int cameraIndex)
            {
                _ = field.GetCamera(cameraIndex);
            }
            return new(new($"{field.Name}\n{camera}\nNative movement direction"), new($"{field.Name}\n{camera}\nControl rotation {movement.RotationDegrees:g}° · {turn}/256 turn"));
        }
        if (selector is not [_, "actor", string objectId] || !int.TryParse(objectId, NumberStyles.None, CultureInfo.InvariantCulture, out int id)
            || change.Actors.FirstOrDefault(actor => actor.ObjectId == id) is not { } actor)
        {
            return null;
        }
        if (actor.When is { } requirement && !field.Background.Any(tile => tile.AnimationId == requirement.ModuleId && tile.AnimationState == requirement.State))
        {
            throw new InvalidDataException($"{field.Name} has no background state {requirement.ModuleId}:{requirement.State}.");
        }
        FieldAssets modelField = actor.Model.FieldId == field.Id ? field : assets.LoadField(actor.Model.FieldId);
        int frames = modelField.GetAnimationFrameCount(actor.Model.ModelId, actor.AnimationId);
        FieldModel model = modelField.Models[actor.Model.ModelId];
        int? original = profile.ResolveFieldActorModel?.Invoke(field, id);
        FieldModel? originalModel = original is int originalId && (uint)originalId < field.Models.Count ? field.Models[originalId] : null;
        string condition = actor.When is { } required ? $"\nBackground module {required.ModuleId}, state {required.State}" : string.Empty;
        string direction = actor.Direction is float facing ? $"\nDirection {facing * 360 / 256:g}°" : string.Empty;
        string originalText = originalModel is null ? "Model and animation selected by the field script" : $"{originalModel.Name}\nAnimations selected by the field script";
        return new(new($"Object {id}\n{originalText}") { FieldModel = originalModel },
            new($"Object {id}\n{model.Name} from {modelField.Name}\nAnimation {actor.AnimationId} · {frames} frames{direction}{condition}") { FieldModel = model, FieldAnimation = actor.AnimationId });
    }

    private static IEnumerable<(int Module, int State)> AuthoredFieldStates(FieldScenePatch patch) =>
        patch.Backgrounds.Where(background => background.ModuleId != 255).Select(background => (background.ModuleId, background.State))
            .Concat(patch.Actors.Where(actor => actor.When is not null).Select(actor => (actor.When!.ModuleId, actor.When.State))).Distinct();
}
