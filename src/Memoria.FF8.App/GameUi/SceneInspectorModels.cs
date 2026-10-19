namespace Memoria.FF8.GameUi;

public enum SceneInspectorKind
{
    Layer,
    Object,
    Event
}

public sealed record SceneInspectorEntry(string Id, SceneInspectorKind Kind, string Name, bool Enabled, IReadOnlyList<string> Details);

public sealed record SceneInspectorSnapshot(int FieldId, string FieldName, long Frame, IReadOnlyList<SceneInspectorEntry> Entries);

public sealed record SceneInspectorCommand(string? EntryId = null, bool? Enabled = null, bool Reset = false);

public enum SceneInspectorFilter
{
    All,
    Enabled,
    Disabled
}

public enum SceneInspectorFocus
{
    Entries,
    Filter,
    Reset,
    Back
}
