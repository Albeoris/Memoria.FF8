using System.Collections.Immutable;

namespace Memoria.FF7.Runtime;

public enum Ff7BattleGuestCompositeTargetPolicy
{
    Selected,
    RandomSelected,
    Source
}

/// <summary>One ordered origin action phase. Target masks are selected before numerical contacts are resolved.</summary>
public sealed record Ff7BattleGuestCompositePhase(Ff7BattleGuestPhysicalHit Description, int Repetitions = 1, Ff7BattleGuestCompositeTargetPolicy TargetPolicy = Ff7BattleGuestCompositeTargetPolicy.Selected);

public sealed record Ff7BattleGuestCompositeAction(IReadOnlyList<Ff7BattleGuestCompositePhase> Phases);

/// <summary>A numerical contact occurrence, independent of its recipient's identity.</summary>
public sealed record Ff7BattleGuestCompositeContact(int Ordinal, int Phase, int Hit, Ff7BattleGuestPhysicalHit HitDescription)
{
    public bool Reflected { get; init; }
    public ImmutableArray<Ff7BattleGuestCompositeReflection> Reflections { get; init; } = [];
}

public sealed record Ff7BattleGuestCompositeReflection(int Slot, byte Remaining, bool Exhausted);

/// <summary>Retains packet order separately from presentation order. NextContact counts settled occurrences.</summary>
public sealed record Ff7BattleGuestCompositeState(ImmutableArray<Ff7BattleGuestCompositeContact> Contacts, int NextContact = 0)
{
    public bool GroupPublication { get; init; }
    public ImmutableArray<int> SettledContacts { get; init; } = [];
}

public interface IFf7BattleGuestPreparationTransaction : IDisposable
{
    void Commit();
}

/// <summary>Supplies an origin program and consumes host-prepared contacts without selecting targets or resolving damage in presentation callbacks.</summary>
public interface IFf7BattleGuestCompositeActions
{
    IFf7BattleGuestPreparationTransaction? BeginCompositePreparation(ushort ability) => null;
    Ff7BattleGuestCompositeAction? DescribeCompositeAction(ushort ability);
    string? PrepareCompositeUnsupportedReason(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> contacts);
    void PrepareComposite(Ff7BattleGuestCompositeAction action, IReadOnlyList<Ff7BattleGuestCompositeContact> contacts);
}
