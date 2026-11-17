using Memoria.FF8.Mods;

namespace Memoria.FF8.GameUi;

/// <summary>Document checkpoints keep selection, pivot, visibility and other editor state out of dirty tracking.</summary>
public sealed class LightingEditorHistory
{
    private readonly List<FieldLightingScene> states = [];
    private FieldLightingScene saved;
    private int cursor;

    public LightingEditorHistory(FieldLightingScene scene)
    {
        states.Add(FieldLightingSceneIO.Clone(scene));
        saved = FieldLightingSceneIO.Clone(scene);
    }

    public bool CanUndo => cursor > 0;
    public bool CanRedo => cursor + 1 < states.Count;
    public int CommandCount => states.Count - 1;

    public bool IsDirty(FieldLightingScene scene)
    {
        return !Equal(saved, scene);
    }

    public void Commit(FieldLightingScene scene)
    {
        if (Equal(states[cursor], scene))
        {
            return;
        }
        states.RemoveRange(cursor + 1, states.Count - cursor - 1);
        states.Add(FieldLightingSceneIO.Clone(scene));
        cursor++;
    }

    public FieldLightingScene Undo()
    {
        if (CanUndo)
        {
            cursor--;
        }
        return FieldLightingSceneIO.Clone(states[cursor]);
    }

    public FieldLightingScene Redo()
    {
        if (CanRedo)
        {
            cursor++;
        }
        return FieldLightingSceneIO.Clone(states[cursor]);
    }

    public void MarkSaved(FieldLightingScene scene)
    {
        saved = FieldLightingSceneIO.Clone(scene);
    }

    public static bool Equal(FieldLightingScene first, FieldLightingScene second)
    {
        return first.SchemaVersion == second.SchemaVersion && first.FieldName == second.FieldName && first.PrimaryLightId == second.PrimaryLightId && first.GroundShadow == second.GroundShadow && first.Lights.SequenceEqual(second.Lights) && first.ShadowReceivers.SequenceEqual(second.ShadowReceivers);
    }
}
