namespace Memoria.FF8.Runtime;

/// <summary>A stable original MAP group, independent of tile order and texture resolution.</summary>
public readonly record struct FieldBackgroundKey(int Layer, int ModuleId, int State)
{
    public override string ToString() => $"layer:{Layer}:{ModuleId}:{State}";
}
