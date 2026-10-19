using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal sealed partial class GameWorld
{
    private readonly Dictionary<FieldBackgroundKey, bool> inspectionBackgrounds = [];
    private readonly Dictionary<int, bool> inspectionObjects = [];
    private readonly HashSet<(int Object, int Script)> inspectionDisabledScripts = [];

    public SceneInspectorSnapshot? ReadSceneInspection()
    {
        if (session.State.Scene != SceneKind.Field)
        {
            return null;
        }
        EnsureField();
        RefreshSceneMods();
        if (Field is null)
        {
            return null;
        }
        List<SceneInspectorEntry> entries = [];
        foreach (var group in Field.Background.GroupBy(Key).OrderBy(group => group.Key.Layer).ThenBy(group => group.Key.ModuleId).ThenBy(group => group.Key.State))
        {
            FieldBackgroundKey key = group.Key;
            BackgroundTile[] tiles = group.ToArray();
            bool native = tiles.Any(tile => tile.Draw) && (key.ModuleId == 255 || Events.BackgroundStates.GetValueOrDefault(key.ModuleId, -1) == key.State);
            bool fromMod = modBackgrounds.TryGetValue(key, out var mod);
            bool inspected = inspectionBackgrounds.TryGetValue(key, out bool overrideVisible);
            bool enabled = inspected ? overrideVisible : fromMod ? mod.Visible : native;
            string name = key.ModuleId == 255 ? $"Layer {key.Layer} / static / state {key.State}" : $"Layer {key.Layer} / module {key.ModuleId} / state {key.State}";
            entries.Add(new(key.ToString(), SceneInspectorKind.Layer, name, enabled,
            [
                $"Address: {key}",
                $"Tiles: {tiles.Length}; native selected: {native}",
                $"Module state: {(key.ModuleId == 255 ? "static" : Events.BackgroundStates.GetValueOrDefault(key.ModuleId, -1).ToString())}",
                $"Bounds: ({tiles.Min(tile => tile.X)}, {tiles.Min(tile => tile.Y)}) - ({tiles.Max(tile => tile.X + tile.Image.Width)}, {tiles.Max(tile => tile.Y + tile.Image.Height)})",
                $"Depths: {string.Join(", ", tiles.Select(tile => tile.Z).Distinct().Order().Take(12))}",
                $"Blend: {string.Join(", ", tiles.Select(tile => tile.BlendMode).Distinct())}",
                fromMod ? $"Mod: {mod.Package} / visible {mod.Visible}" : "Mod: none",
                inspected ? $"Temporary visibility: {overrideVisible}" : "Temporary visibility: inherited"
            ]));
        }
        foreach (ActorState actor in Events.Actors)
        {
            var visual = CreateVisual(actor, false);
            bool patched = modActors.TryGetValue(actor.ObjectId, out var mod);
            string modelName = visual.ModelId >= 0 && visual.ModelId < presentationField!.Models.Count ? presentationField.Models[visual.ModelId].Name : "no model";
            entries.Add(new($"object:{actor.ObjectId}", SceneInspectorKind.Object, $"Object {actor.ObjectId} / {modelName}", visual.Visible,
            [
                $"Object: {actor.ObjectId}; character: {actor.CharacterId}",
                $"Native active: {actor.Active}; visible: {actor.Visible}",
                $"Talk enabled: {actor.TalkEnabled}; native model: {actor.RenderModelId}",
                $"Render model: {visual.ModelId} / {modelName}",
                FormattableString.Invariant($"Position: {actor.X:0.##}, {actor.Y:0.##}, {actor.Z:0.##}; triangle: {actor.WalkmeshTriangle}"),
                FormattableString.Invariant($"Direction: {visual.Direction:0.##}; animation: {visual.AnimationId}; frame: {visual.AnimationFrame:0.##}"),
                patched ? $"Mod: {mod.Package}; condition: {IsActorPatchActive(mod.Patch)}" : "Mod: none",
                inspectionObjects.TryGetValue(actor.ObjectId, out bool visible) ? $"Temporary visibility: {visible}" : "Temporary visibility: inherited",
                "Visibility affects rendering. Events and collision keep their native ownership."
            ]));
        }
        foreach (FieldScriptInspection script in Events.InspectScripts())
        {
            bool enabled = !inspectionDisabledScripts.Contains((script.ObjectId, script.ScriptId));
            entries.Add(new($"event:{script.ObjectId}:{script.ScriptId}", SceneInspectorKind.Event, $"Object {script.ObjectId} / script {script.ScriptId}", enabled,
            [
                $"Object: {script.ObjectId}; script label: {script.ScriptId}",
                $"Operations: {script.Start}..{script.End - 1}",
                $"Status: {script.Status}; live fibers: {script.RunningCount}",
                $"Current operation: {script.Operation?.ToString() ?? "none"}",
                enabled ? "Execution: enabled" : "Execution: temporarily suspended",
                "Suspension preserves instructions, operands and pending requests.",
                "Other scripts may wait for this event. Existing timers continue when gameplay resumes."
            ]));
        }
        return new(Field.Id, Field.Name, Events.Frame, entries);
    }

    public void ApplySceneInspection(SceneInspectorCommand command)
    {
        SceneInspectorSnapshot snapshot = ReadSceneInspection() ?? throw new InvalidOperationException("Scene inspection requires an active field.");
        if (command.Reset)
        {
            ClearSceneInspection();
            return;
        }
        SceneInspectorEntry entry = snapshot.Entries.SingleOrDefault(entry => entry.Id == command.EntryId) ?? throw new ArgumentException("The selected scene entry no longer exists.", nameof(command));
        bool enabled = command.Enabled ?? !entry.Enabled;
        string[] parts = entry.Id.Split(':');
        switch (entry.Kind)
        {
            case SceneInspectorKind.Layer:
                inspectionBackgrounds[new(int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]))] = enabled;
                break;
            case SceneInspectorKind.Object:
                int objectId = int.Parse(parts[1]);
                ActorState actor = Events.Actors.Single(actor => actor.ObjectId == objectId);
                // Validate the proposed presentation before publishing an override to the inspector and renderer.
                CreateVisual(actor, true, enabled);
                inspectionObjects[objectId] = enabled;
                break;
            case SceneInspectorKind.Event:
                var key = (int.Parse(parts[1]), int.Parse(parts[2]));
                if (enabled)
                {
                    inspectionDisabledScripts.Remove(key);
                }
                else
                {
                    inspectionDisabledScripts.Add(key);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    private void ClearSceneInspection()
    {
        inspectionBackgrounds.Clear();
        inspectionObjects.Clear();
        inspectionDisabledScripts.Clear();
    }
}
