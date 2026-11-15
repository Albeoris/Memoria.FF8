using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorCache(
    ushort Flags0,
    NativeBattleVector? Position28,
    short? Vertical34,
    short? Rotation12,
    short? Rotation14,
    short? Rotation16,
    NativeBattleVector? Scale148,
    short? Scale154);

/// <summary>Actor fields and retained selection state for the scripted-effect header.</summary>
public sealed record NativeBattleCinematicActorContext
{
    private readonly ImmutableArray<BattleModelOwner> ownerOrder;

    private NativeBattleCinematicActorContext(BattleActorModels models)
    {
        ownerOrder = models.Owners.ToImmutableArray();
        CachedActors = CaptureActors(models);
        ActorOrdinals = ownerOrder.ToImmutableDictionary(owner => owner, _ => (int?)null);
    }

    public IEnumerable<BattleModelOwner> Owners => ownerOrder;
    public ImmutableDictionary<BattleModelOwner, NativeBattleCinematicActorCache> CachedActors { get; init; }
    public ImmutableArray<BattleModelOwner> CurrentActors { get; init; } = [];
    public BattleModelOwner? FirstActor { get; init; }
    public ImmutableArray<BattleModelOwner>? SavedActors { get; init; }
    public ImmutableDictionary<BattleModelOwner, int?> ActorOrdinals { get; init; }
    // Number of contacts already processed within the active header.
    public int ContactsProcessed { get; init; }

    public static NativeBattleCinematicActorContext FromGraphics(BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        return new(models);
    }

    public void Validate()
    {
        if (CachedActors is null || ActorOrdinals is null || CachedActors.Count != ownerOrder.Length || ActorOrdinals.Count != ownerOrder.Length)
        {
            throw new InvalidDataException("Cinematic actor cache and ordinals must cover the model owners.");
        }
        foreach (BattleModelOwner owner in ownerOrder)
        {
            if (!CachedActors.TryGetValue(owner, out NativeBattleCinematicActorCache? cached) || cached is null ||
                !ActorOrdinals.TryGetValue(owner, out int? ordinal) || ordinal is < 0)
            {
                throw new InvalidDataException("Cinematic actor cache or ordinal disagrees with its model owner.");
            }
        }
        ValidateList(CurrentActors);
        if (!CurrentActors.IsEmpty && FirstActor != CurrentActors[0])
        {
            throw new InvalidDataException("Cinematic selection disagrees with its retained first actor.");
        }
        if (FirstActor is { } first)
        {
            RequireOwner(first);
        }
        if (SavedActors is { } saved)
        {
            ValidateList(saved);
        }
        ValidateContactsProcessed(ContactsProcessed);
    }

    public NativeBattleCinematicActorCache ReadCachedActor(BattleModelOwner owner)
    {
        return RequireOwner(owner);
    }

    public int ReadActorOrdinal(BattleModelOwner owner)
    {
        RequireOwner(owner);
        if (!ActorOrdinals.TryGetValue(owner, out int? ordinal))
        {
            throw new InvalidDataException("Cinematic actor lacks its ordinal entry.");
        }
        return ordinal ?? throw new NotSupportedException("Cinematic actor has no retained ordinal.");
    }

    public BattleModelOwner ReadFirstActor() => FirstActor ?? throw new NotSupportedException("Cinematic selection has no retained first actor.");

    public ImmutableArray<BattleModelOwner> ReadSavedActors() => SavedActors ?? throw new NotSupportedException("Cinematic selection has no saved actor list.");

    public NativeBattleCinematicActorContext CaptureCurrentContacts(ImmutableArray<BattleModelOwner> occurrences, BattleModelOwner firstHeaderFirstActor)
    {
        RequireOwner(firstHeaderFirstActor);
        if (occurrences.IsDefault)
        {
            throw new InvalidDataException("Cinematic contact capture requires its ordered occurrences.");
        }
        HashSet<BattleModelOwner> seen = [];
        ImmutableArray<BattleModelOwner>.Builder actors = ImmutableArray.CreateBuilder<BattleModelOwner>();
        ImmutableDictionary<BattleModelOwner, int?>.Builder ordinals = ActorOrdinals.ToBuilder();
        foreach (BattleModelOwner owner in occurrences)
        {
            RequireOwner(owner);
            if (seen.Add(owner))
            {
                ordinals[owner] = actors.Count;
                actors.Add(owner);
            }
        }
        BattleModelOwner first = actors.Count == 0 ? firstHeaderFirstActor : actors[0];
        return this with { CurrentActors = actors.ToImmutable(), FirstActor = first, ActorOrdinals = ordinals.ToImmutable() };
    }

    public NativeBattleCinematicActorContext WithCurrentActors(ImmutableArray<BattleModelOwner> actors)
    {
        ValidateList(actors);
        return this with { CurrentActors = actors, FirstActor = actors.IsEmpty ? FirstActor : actors[0] };
    }

    public NativeBattleCinematicActorContext WithSavedActors(ImmutableArray<BattleModelOwner> actors)
    {
        ValidateList(actors);
        return this with { SavedActors = actors };
    }

    public NativeBattleCinematicActorContext WithContactsProcessed(int count)
    {
        ValidateContactsProcessed(count);
        return this with { ContactsProcessed = count };
    }

    public NativeBattleCinematicActorContext RefreshCache(BattleActorModels models)
    {
        RequireModelOrder(models);
        return this with { CachedActors = CaptureActors(models) };
    }

    public NativeBattleCinematicActorContext RefreshFlags(BattleActorModels models)
    {
        RequireModelOrder(models);
        ImmutableDictionary<BattleModelOwner, NativeBattleCinematicActorCache>.Builder cached = CachedActors.ToBuilder();
        foreach (BattleModelEntry<BattleModelOwner> entry in models.Entries)
        {
            cached[entry.Owner] = RequireOwner(entry.Owner) with { Flags0 = entry.State.Flags0 };
        }
        return this with { CachedActors = cached.ToImmutable() };
    }

    private static ImmutableDictionary<BattleModelOwner, NativeBattleCinematicActorCache> CaptureActors(BattleActorModels models)
    {
        var cached = ImmutableDictionary.CreateBuilder<BattleModelOwner, NativeBattleCinematicActorCache>();
        foreach (BattleModelEntry<BattleModelOwner> entry in models.Entries)
        {
            NativeBattleGraphicsModel model = entry.State;
            NativeBattleModelTransform? transform = model.Transform;
            cached.Add(entry.Owner, new(model.Flags0, transform?.Position28, transform?.Vertical34, model.Rotation12, model.Rotation14, model.Rotation16, transform?.Scale148, transform?.Scale154));
        }
        return cached.ToImmutable();
    }

    private void RequireModelOrder(BattleActorModels models)
    {
        ArgumentNullException.ThrowIfNull(models);
        Validate();
        if (!ownerOrder.SequenceEqual(models.Owners))
        {
            throw new InvalidDataException("Cinematic cache requires the same ordered model owners.");
        }
    }

    private void ValidateList(ImmutableArray<BattleModelOwner> actors)
    {
        if (actors.IsDefault)
        {
            throw new InvalidDataException("Cinematic selection requires its ordered actor list.");
        }
        foreach (BattleModelOwner owner in actors)
        {
            RequireOwner(owner);
        }
    }

    private NativeBattleCinematicActorCache RequireOwner(BattleModelOwner owner)
    {
        if (owner is null || !CachedActors.TryGetValue(owner, out NativeBattleCinematicActorCache? cached) || cached is null)
        {
            throw new InvalidDataException("Cinematic actor has no cached model owner.");
        }
        return cached;
    }

    private static void ValidateContactsProcessed(int count)
    {
        if (count < 0)
        {
            throw new InvalidDataException("Cinematic contact progress cannot be negative.");
        }
    }
}
