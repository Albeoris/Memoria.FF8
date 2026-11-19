using System.Numerics;
using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.Mods;
using Memoria.FF8.Rendering;

namespace Memoria.FF8;

internal static partial class CatalogPreviewPresentation
{
    internal static ModPreviewComparison CompareFieldScene(FieldAssets before, FieldAssets after, IReadOnlyList<FieldScenePatch> patches, Func<int, FieldAssets> loadField, CancellationToken cancellationToken)
    {
        Dictionary<int, int> selectedStates = [];
        Dictionary<BackgroundTile, bool> tileVisibility = [];
        foreach (FieldBackgroundPatch patch in patches.SelectMany(patch => patch.Backgrounds))
        {
            cancellationToken.ThrowIfCancellationRequested();
            BackgroundTile[] tiles = after.Background.Where(patch.Matches).ToArray();
            if (tiles.Length == 0)
            {
                throw new InvalidDataException($"{after.Name} has no background tiles for layer {patch.Layer}, module {patch.ModuleId}, state {patch.State}.");
            }
            if (patch.ModuleId != 255)
            {
                selectedStates.TryAdd(patch.ModuleId, patch.State);
            }
            foreach (BackgroundTile tile in tiles)
            {
                tileVisibility.TryAdd(tile, patch.Visible);
            }
        }

        List<ActorVisual> originalActors = [];
        List<ActorVisual> modifiedActors = [];
        List<int> unavailableActors = [];
        List<FieldModel> models = after.Models.ToList();
        Dictionary<(int Field, int Model), int> importedModels = [];
        FieldActorPatch[] actors = patches.SelectMany(patch => patch.Actors).DistinctBy(actor => actor.ObjectId).ToArray();
        JsmProgram? originalProgram = actors.Length == 0 ? null : JsmProgram.Read(before.Jsm);
        JsmProgram? modifiedProgram = originalProgram is null || before.Jsm.AsSpan().SequenceEqual(after.Jsm) ? originalProgram : JsmProgram.Read(after.Jsm);
        foreach (FieldActorPatch patch in actors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (patch.When is { } condition)
            {
                if (!after.Background.Any(tile => tile.AnimationId == condition.ModuleId && tile.AnimationState == condition.State))
                {
                    throw new InvalidDataException($"{after.Name} has no background state {condition.ModuleId}:{condition.State}.");
                }
                selectedStates.TryAdd(condition.ModuleId, condition.State);
                if (selectedStates[condition.ModuleId] != condition.State)
                {
                    continue;
                }
            }
            FieldAssets modelSource = patch.Model.FieldId == after.Id ? after : loadField(patch.Model.FieldId);
            _ = modelSource.GetAnimationFrameCount(patch.Model.ModelId, patch.AnimationId);
            if (ReadActorSetup(modifiedProgram!, patch.ObjectId) is not { } placement)
            {
                unavailableActors.Add(patch.ObjectId);
                continue;
            }
            int modelId = patch.Model.ModelId;
            if (modelSource.Id != after.Id && !importedModels.TryGetValue((modelSource.Id, modelId), out modelId))
            {
                modelId = models.Count;
                models.Add(modelSource.Models[patch.Model.ModelId]);
                importedModels.Add((modelSource.Id, patch.Model.ModelId), modelId);
            }
            modifiedActors.Add(Actor(patch.ObjectId, modelId, patch.AnimationId, placement.Position, patch.Direction ?? placement.Direction));
            if (ReadActorSetup(originalProgram!, patch.ObjectId) is { Visible: true, ModelId: int originalModel } original
                && (uint)originalModel < before.Models.Count && (uint)original.Animation < before.Models[originalModel].Animations.Count)
            {
                originalActors.Add(Actor(patch.ObjectId, originalModel, original.Animation, original.Position, original.Direction));
            }
        }

        string states = selectedStates.Count == 0 ? "" : "\n" + string.Join(" · ", selectedStates.OrderBy(pair => pair.Key).Select(pair => $"Module {pair.Key}, state {pair.Value}"));
        string shown = modifiedActors.Count == 0 ? "" : $"\n{modifiedActors.Count} changed {(modifiedActors.Count == 1 ? "actor" : "actors")} shown at native setup positions";
        string unavailable = unavailableActors.Count == 0 ? "" : $"\nScene placement unavailable for objects {string.Join(", ", unavailableActors)}";
        string movement = string.Join("", patches.Where(patch => patch.Movement is not null).Select(patch => $"\nControl rotation {patch.Movement!.RotationDegrees:g}°"));
        PixelImage originalImage = RenderScene(before, originalActors, null);
        cancellationToken.ThrowIfCancellationRequested();
        PixelImage modifiedImage = RenderScene(after with { Models = models }, modifiedActors, tileVisibility);
        return new(new(before.Name + states, originalImage), new(after.Name + states + shown + unavailable + movement, modifiedImage));

        PixelImage RenderScene(FieldAssets field, IReadOnlyList<ActorVisual> actorsToDraw, IReadOnlyDictionary<BackgroundTile, bool>? visibility)
        {
            FieldRenderer renderer = new(field, FieldRenderer.Width * 2, FieldRenderer.Height * 2);
            var statesToDraw = field.Background.Where(tile => tile.AnimationId != 255).GroupBy(tile => tile.AnimationId).ToDictionary(group => group.Key, group => group.First().AnimationState);
            foreach ((int module, int state) in selectedStates)
            {
                statesToDraw[module] = state;
            }
            byte[] rgba = renderer.Render(statesToDraw, actorsToDraw, TimeSpan.Zero, backgroundTileVisibility: visibility);
            return new(renderer.OutputWidth, renderer.OutputHeight, MemoryMarshal.Cast<byte, Rgba32>(rgba).ToArray());
        }

        static ActorVisual Actor(int objectId, int model, int animation, Vector3 position, float direction) =>
            new(objectId, model, position.X, position.Y, position.Z, direction, animation, 0, true, PolygonColor: EventEngine.NativeFieldActorInitialPolygonColor, GroundShadowEnabled: false);
    }

    private readonly record struct FieldActorSetup(Vector3 Position, byte Direction, int? ModelId, int Animation, bool Visible);

    private static FieldActorSetup? ReadActorSetup(JsmProgram program, int objectId)
    {
        if ((uint)objectId >= program.Objects.Count || !program.NativeObjects.Take(program.NativeActorCount).Contains(objectId)
            || program.Objects[objectId].Scripts.FirstOrDefault() is not { } initialize)
        {
            return null;
        }
        JsmOperation[] operations = initialize.ReachableOperations.Order().Select(index => program.Operations[index]).ToArray();
        if (operations.Any(operation => operation.Opcode is not (Jsm.Opcode.LBL or Jsm.Opcode.RET or Jsm.Opcode.PSHN_L or Jsm.Opcode.PSHM_B or Jsm.Opcode.PSHM_W or Jsm.Opcode.PSHM_L or Jsm.Opcode.PSHSM_B or Jsm.Opcode.PSHSM_W or Jsm.Opcode.PSHSM_L
            or Jsm.Opcode.CAL or Jsm.Opcode.JPF or Jsm.Opcode.JMP or Jsm.Opcode.SET or Jsm.Opcode.SET3 or Jsm.Opcode.SETMODEL or Jsm.Opcode.BASEANIME or Jsm.Opcode.DIR or Jsm.Opcode.HIDE or Jsm.Opcode.SHOW
            or Jsm.Opcode.USE or Jsm.Opcode.UNUSE or Jsm.Opcode.TALKON or Jsm.Opcode.TALKOFF or Jsm.Opcode.TALKRADIUS or Jsm.Opcode.PUSHON or Jsm.Opcode.PUSHOFF or Jsm.Opcode.PUSHRADIUS or Jsm.Opcode.THROUGHON or Jsm.Opcode.THROUGHOFF)))
        {
            return null;
        }
        JsmOperation[] placements = operations.Where(operation => operation.Opcode is Jsm.Opcode.SET or Jsm.Opcode.SET3).ToArray();
        if (placements.Length != 1)
        {
            return null;
        }
        JsmOperation setup = placements[0];
        int coordinates = setup.Opcode == Jsm.Opcode.SET3 ? 3 : 2;
        if (ReadLiterals(setup, coordinates) is not { } position)
        {
            return null;
        }
        // SET changes X/Y and retains the fresh actor's Z. SET3 writes XYZ.
        Vector3 vector = new(position[0], position[1], coordinates == 3 ? position[2] : 0);
        JsmOperation[] directions = operations.Where(operation => operation.Opcode == Jsm.Opcode.DIR).ToArray();
        int[]? facing = directions.Length == 1 ? ReadLiterals(directions[0], 1) : null;
        if (directions.Length > 1 || directions.Length == 1 && facing is null)
        {
            return null;
        }
        int[] modelIds = operations.Where(operation => operation.Opcode == Jsm.Opcode.SETMODEL).Select(operation => operation.Parameter).Distinct().ToArray();
        int[] animations = operations.Where(operation => operation.Opcode == Jsm.Opcode.BASEANIME).Select(operation => operation.Parameter).Distinct().ToArray();
        bool visible = operations.All(operation => operation.Opcode != Jsm.Opcode.HIDE);
        return new(vector, unchecked((byte)(facing?[0] ?? 0)), modelIds.Length == 1 ? modelIds[0] : null, animations.Length == 1 ? animations[0] : 0, visible);

        int[]? ReadLiterals(JsmOperation operation, int count)
        {
            int start = operation.Index - count;
            if (start < initialize.Start || operations.Any(candidate => candidate.Opcode is Jsm.Opcode.JMP or Jsm.Opcode.JPF
                && JsmProgram.JumpTarget(candidate.Index, candidate.Parameter) > start && JsmProgram.JumpTarget(candidate.Index, candidate.Parameter) <= operation.Index))
            {
                return null;
            }
            int[] values = new int[count];
            for (int index = 0; index < values.Length; index++)
            {
                int pc = start + index;
                if (!initialize.ReachableOperations.Contains(pc) || program.Operations[pc] is not { Opcode: Jsm.Opcode.PSHN_L, Parameter: >= short.MinValue and <= short.MaxValue } literal)
                {
                    return null;
                }
                values[index] = literal.Parameter;
            }
            return values;
        }
    }
}
