using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly Func<FieldSceneMods>? readSceneMods;
    private FieldSceneMods? appliedSceneMods;
    private FieldAssets? appliedSceneField;
    private FieldAssets? presentationField;
    private readonly Dictionary<BackgroundTile, (bool Visible, string Package)> modBackgrounds = [];
    private readonly Dictionary<BackgroundTile, bool> modBackgroundVisibility = [];
    private readonly Dictionary<int, (FieldActorPatch Patch, int ModelId, string Package)> modActors = [];

    private void RefreshSceneMods()
    {
        FieldSceneMods current = readSceneMods?.Invoke() ?? FieldSceneMods.Empty;
        if (Field is null)
        {
            return;
        }
        if (ReferenceEquals(current, appliedSceneMods) && ReferenceEquals(Field, appliedSceneField))
        {
            RevalidateDebugAnimations();
            return;
        }
        fieldPresentation.Reset();
        Dictionary<BackgroundTile, (bool Visible, string Package)> backgrounds = [];
        Dictionary<int, (FieldActorPatch Patch, int ModelId, string Package)> actors = [];
        byte? movementOffset = null;
        List<FieldModel> models = Field.Models.ToList();
        Dictionary<(int Field, int Model), int> importedModels = [];
        foreach (OwnedFieldScenePatch owned in current.Patches.Where(item => item.Patch.FieldId == Field.Id || item.Patch.FieldName.Equals(Field.Name, StringComparison.OrdinalIgnoreCase)))
        {
            FieldScenePatch patch = owned.Patch;
            if (patch.FieldId != Field.Id || !patch.FieldName.Equals(Field.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{owned.PackageId}: fieldId and fieldName must identify the same installed field.");
            }
            if (movementOffset is null && patch.Movement is { } movement)
            {
                movementOffset = unchecked((byte)(int)MathF.Round(movement.RotationDegrees * 256 / 360));
            }
            foreach (FieldBackgroundPatch background in patch.Backgrounds)
            {
                FieldBackgroundKey key = new(background.Layer, background.ModuleId, background.State);
                BackgroundTile[] selected = Field.Background.Where(tile => Key(tile) == key && (background.Region is null || background.Region.Contains(tile.X, tile.Y))).ToArray();
                if (selected.Length == 0)
                {
                    throw new InvalidDataException($"{owned.PackageId}: {Field.Name} has no background {key}.");
                }
                foreach (BackgroundTile tile in selected)
                {
                    backgrounds.TryAdd(tile, (background.Visible, owned.PackageId));
                }
            }
            foreach (FieldActorPatch actor in patch.Actors)
            {
                if (!Events.Actors.Any(item => item.ObjectId == actor.ObjectId))
                {
                    throw new InvalidDataException($"{owned.PackageId}: {Field.Name} has no object {actor.ObjectId}.");
                }
                if (actor.When is { } condition && !Field.Background.Any(tile => tile.AnimationId == condition.ModuleId && tile.AnimationState == condition.State))
                {
                    throw new InvalidDataException($"{owned.PackageId}: {Field.Name} has no background state {condition.ModuleId}:{condition.State}.");
                }
                FieldAssets source = actor.Model.FieldId == Field.Id ? Field : assets.LoadField(actor.Model.FieldId);
                source.GetAnimationFrameCount(actor.Model.ModelId, actor.AnimationId);
                int modelId = actor.Model.ModelId;
                if (source.Id != Field.Id && !importedModels.TryGetValue((source.Id, modelId), out modelId))
                {
                    modelId = models.Count;
                    models.Add(source.Models[actor.Model.ModelId]);
                    importedModels.Add((source.Id, actor.Model.ModelId), modelId);
                }
                actors.TryAdd(actor.ObjectId, (actor, modelId, owned.PackageId));
            }
        }
        FieldAssets next = Field with { Models = Array.AsReadOnly(models.ToArray()) };
        FieldRenderer nextRenderer = new(next, renderWidth, renderHeight);
        modBackgrounds.Clear();
        modBackgroundVisibility.Clear();
        foreach (var pair in backgrounds)
        {
            modBackgrounds.Add(pair.Key, pair.Value);
            modBackgroundVisibility.Add(pair.Key, pair.Value.Visible);
        }
        modActors.Clear();
        foreach (var pair in actors)
        {
            modActors.Add(pair.Key, pair.Value);
        }
        presentationField = next;
        Events.PlayerInputDirectionOffset = movementOffset ?? 0;
        renderer = nextRenderer;
        appliedSceneMods = current;
        appliedSceneField = Field;
        RevalidateDebugAnimations();
    }

    private static FieldBackgroundKey Key(BackgroundTile tile) => new(tile.Layer, tile.AnimationId, tile.AnimationState);

    private bool IsActorPatchActive(FieldActorPatch patch)
    {
        return patch.When is null || Events.BackgroundStates.GetValueOrDefault(patch.When.ModuleId, -1) == patch.When.State;
    }

    private bool IsPatchedPresentation(ActorState actor) => modActors.TryGetValue(actor.ObjectId, out var mod) && IsActorPatchActive(mod.Patch);

    private int PresentationModelId(ActorState actor) => IsPatchedPresentation(actor) ? modActors[actor.ObjectId].ModelId : actor.RenderModelId;

    private int PresentationAnimationId(ActorState actor) => IsPatchedPresentation(actor) ? modActors[actor.ObjectId].Patch.AnimationId : actor.AnimationId;

    private void RevalidateDebugAnimations()
    {
        if (session.State.DebugView is not { } view || presentationField is null)
        {
            return;
        }
        Dictionary<int, DebugActorView>? actors = null;
        foreach (var pair in view.Actors)
        {
            DebugActorView debug = pair.Value;
            if (debug.AnimationId is null && debug.Frame is null && !debug.Playing)
            {
                continue;
            }
            var actor = Events.Actors.SingleOrDefault(actor => actor.ObjectId == pair.Key);
            int modelId = actor is null ? -1 : PresentationModelId(actor);
            int animationId = debug.AnimationId ?? (actor is null ? -1 : PresentationAnimationId(actor));
            bool valid = (uint)modelId < presentationField.Models.Count && (uint)animationId < presentationField.Models[modelId].Animations.Count;
            int count = valid ? presentationField.GetAnimationFrameCount(modelId, animationId) : 0;
            if (!valid || debug.Frame >= count)
            {
                actors ??= new(view.Actors);
                actors[pair.Key] = debug with { AnimationId = null, Frame = null, Playing = false };
            }
        }
        if (actors is not null)
        {
            session.SetDebugView(view with { Actors = actors });
        }
    }

    private IReadOnlyDictionary<FieldBackgroundKey, bool>? SceneBackgroundVisibility()
    {
        return inspectionBackgrounds.Count == 0 ? null : inspectionBackgrounds;
    }
}
