using Memoria.FF8.Runtime;

namespace Memoria.FF8.Events;

public sealed partial class EventEngine
{
    private Dictionary<int, Dictionary<int, GameplaySaveModelPartOffset>>? CaptureGameplayModelPartOffsets()
    {
        Dictionary<int, Dictionary<int, GameplaySaveModelPartOffset>> offsets = [];
        foreach ((int model, NativeModelPartResourceState state) in Scene.ModelParts?.Resources ?? [])
        {
            if (state.Translations.Count == 0)
            {
                continue;
            }
            NativeModelPartsDefinition definition = ReadNativeModelParts?.Invoke(model) ?? throw new NotSupportedException("Gameplay model-part offsets require the selected current model definition.");
            state.Validate(definition);
            offsets.Add(model, state.Translations.ToDictionary(pair => pair.Key, pair => GameplaySaveModelPartOffset.Capture(pair.Value, definition.Parts[pair.Key])));
        }
        return offsets.Count == 0 ? null : offsets;
    }

    private void RestoreGameplayModelPartOffsets(GameplaySaveFieldActors saved, Dictionary<int, NativeModelPartResourceState> resources)
    {
        foreach ((int model, Dictionary<int, GameplaySaveModelPartOffset> offsets) in saved.ModelPartOffsets ?? [])
        {
            if (!resources.ContainsKey(model))
            {
                throw new NotSupportedException("Gameplay model-part offsets have no current actor resource binding.");
            }
            NativeModelPartsDefinition definition = ReadNativeModelParts?.Invoke(model) ?? throw new NotSupportedException("Gameplay model-part offsets require the selected current model definition.");
            definition.Validate();
            if (offsets.Keys.Any(index => (uint)index >= definition.Parts.Count))
            {
                throw new NotSupportedException("Gameplay model-part index is absent from the selected current model.");
            }
            NativeModelPartResourceState state = new(offsets.ToDictionary(pair => pair.Key, pair => pair.Value.Restore(definition.Parts[pair.Key])));
            state.Validate(definition);
            resources[model] = state;
        }
    }
}
