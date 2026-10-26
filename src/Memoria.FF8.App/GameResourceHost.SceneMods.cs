using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Resources;

namespace Memoria.FF8;

internal sealed partial class GameResourceHost
{
    private IReadOnlyList<ModDiagnostic> ValidateSceneModResources(FieldSceneMods mods, IReadOnlyList<EncodedResource> encodedResources)
    {
        if (mods.Patches.Count == 0)
        {
            return [];
        }
        ResourceService candidate = new(source);
        candidate.Prepare(null, encodedResources: encodedResources).Commit();
        using GameAssets sceneValidationAssets = new(gameDirectory, resources: candidate);
        List<ModDiagnostic> diagnostics = [];
        foreach (OwnedFieldScenePatch owned in mods.Patches)
        {
            try
            {
                FieldScenePatch patch = owned.Patch;
                FieldAssets target = sceneValidationAssets.LoadField(patch.FieldId);
                if (!target.Name.Equals(patch.FieldName, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("fieldId and fieldName do not identify the same installed field.");
                }
                foreach (FieldBackgroundPatch background in patch.Backgrounds)
                {
                    if (!target.Background.Any(tile => tile.Layer == background.Layer && tile.AnimationId == background.ModuleId && tile.AnimationState == background.State && (background.Region is null || background.Region.Contains(tile.X, tile.Y))))
                    {
                        throw new InvalidDataException($"Background group {background.Layer}:{background.ModuleId}:{background.State} does not exist in {target.Name}.");
                    }
                }
                JsmProgram program = JsmProgram.Read(target.Jsm);
                foreach (FieldActorPatch actor in patch.Actors)
                {
                    if (!program.Objects.Any(obj => obj.Index == actor.ObjectId))
                    {
                        throw new InvalidDataException($"Object {actor.ObjectId} does not exist in {target.Name}.");
                    }
                    if (actor.When is { } when && !target.Background.Any(tile => tile.AnimationId == when.ModuleId && tile.AnimationState == when.State))
                    {
                        throw new InvalidDataException($"Background condition {when.ModuleId}:{when.State} does not exist in {target.Name}.");
                    }
                    FieldAssets modelSource = actor.Model.FieldId == target.Id ? target : sceneValidationAssets.LoadField(actor.Model.FieldId);
                    if (modelSource.GetAnimationFrameCount(actor.Model.ModelId, actor.AnimationId) == 0)
                    {
                        throw new InvalidDataException("A presentation model requires a nonempty animation.");
                    }
                }
            }
            catch (Exception exception) when (exception is InvalidDataException or IOException or ArgumentException or NotSupportedException or UnauthorizedAccessException)
            {
                diagnostics.Add(new("Memoria.InvalidFieldResource", exception.Message, owned.PackageId, owned.RelativePath));
            }
        }
        return diagnostics;
    }
}
