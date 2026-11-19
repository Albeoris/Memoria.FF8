using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLimitPresentationBinding(string ActorId, BattleLimitFamily Family, NativeBattleGraphicsResource Resource);

public enum NativeBattleEquipmentPresentationRole
{
    Equipped,
    FreeHands
}

public sealed partial record NativeBattleGraphicsResource
{
    public NativeBattleEquipmentPresentationRole EquipmentPresentationRole { get; init; }
}

public sealed partial record NativeBattleGraphicsModel
{
    [System.Text.Json.Serialization.JsonIgnore]
    public NativeBattleEquipmentPresentationRole EquipmentPresentationRole { get; init; }
}

public sealed partial record NativeBattleGraphicsDefinition
{
    public ImmutableArray<NativeBattleLimitPresentationBinding> LimitPresentations { get; init; } = [];
}
