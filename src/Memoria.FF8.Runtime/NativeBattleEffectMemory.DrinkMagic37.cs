using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed partial record NativeBattleEffectMemory
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleDrinkMagic37State? DrinkMagic37 { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public NativeBattleCuragaExecutableState? DrinkMagic37Executable { get; init; }

    public NativeBattleDrinkMagic37State RequireDrinkMagic37()
    {
        return DrinkMagic37 ?? throw new NotSupportedException("Drink Magic requires its actual retained mutable mesh and module state.");
    }

    public NativeBattleEffectMemory WithDrinkMagic37(NativeBattleDrinkMagic37State state)
    {
        state.Validate();
        return this with { DrinkMagic37 = state };
    }
}
