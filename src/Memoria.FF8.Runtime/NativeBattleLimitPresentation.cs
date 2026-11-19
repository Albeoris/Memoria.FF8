using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleLimitPresentationBinding(string ActorId, BattleLimitFamily Family, NativeBattleGraphicsResource Resource);

public sealed partial record NativeBattleGraphicsDefinition
{
    public ImmutableArray<NativeBattleLimitPresentationBinding> LimitPresentations { get; init; } = [];
}
