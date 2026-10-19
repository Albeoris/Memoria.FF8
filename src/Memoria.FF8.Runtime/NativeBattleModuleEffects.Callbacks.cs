namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleModuleTask
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public uint? Callback { get; init; }
}
