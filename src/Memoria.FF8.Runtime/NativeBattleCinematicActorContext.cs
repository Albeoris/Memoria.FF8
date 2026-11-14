using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public sealed record NativeBattleCinematicActorCache(
    [property: JsonRequired] uint ModelAddress,
    [property: JsonRequired] ushort Flags0,
    NativeBattleVector? Position28,
    short? Vertical34,
    short? Rotation12,
    short? Rotation14,
    short? Rotation16,
    NativeBattleVector? Scale148,
    short? Scale154);

/// <summary>Hosted actor fields and retained selection state for the scripted-effect header.</summary>
public sealed record NativeBattleCinematicActorContext(
    [property: JsonRequired] NativeBattleGraphicsRoster Roster,
    [property: JsonRequired] ImmutableArray<NativeBattleCinematicActorCache?> CachedActors,
    [property: JsonRequired] ImmutableArray<byte> CurrentActors,
    [property: JsonRequired] byte? FirstActor,
    [property: JsonRequired] ImmutableArray<byte>? SavedActors,
    [property: JsonRequired] ImmutableArray<byte?> ActorOrdinals,
    [property: JsonRequired] int ContactCursor)
{
    public static NativeBattleCinematicActorContext FromGraphics(NativeBattleGraphicsRoster roster, ImmutableArray<NativeBattleGraphicsModel> models)
    {
        ArgumentNullException.ThrowIfNull(roster);
        roster.Validate();
        ImmutableArray<NativeBattleCinematicActorCache?> cached = CaptureRows(roster, models);
        return new(roster, cached, [], null, null, ImmutableArray.CreateRange(new byte?[roster.ActorCount]), 0);
    }

    public void Validate()
    {
        if (Roster is null)
        {
            throw new InvalidDataException("Hosted cinematic actors require their graphics roster.");
        }
        Roster.Validate();
        if (CachedActors.IsDefault || CachedActors.Length != Roster.ActorCount || ActorOrdinals.IsDefault || ActorOrdinals.Length != Roster.ActorCount)
        {
            throw new InvalidDataException("Hosted cinematic actor cache and ordinals must cover the graphics bank.");
        }
        for (int row = 0; row < Roster.ActorCount; row++)
        {
            NativeBattleCinematicActorCache? cached = CachedActors[row];
            byte? ordinal = ActorOrdinals[row];
            if (Roster.Rows[row] is null)
            {
                if (cached is not null || ordinal.HasValue)
                {
                    throw new InvalidDataException("An unbound graphics row cannot own cinematic actor data.");
                }
            }
            else if (cached is null || cached.ModelAddress != NativeBattleGraphicsModelAddress.Encode(row) || ordinal >= Roster.ActorCount)
            {
                throw new InvalidDataException("Hosted cinematic actor cache or ordinal disagrees with its graphics row.");
            }
        }
        ValidateList(CurrentActors);
        if (!CurrentActors.IsEmpty && FirstActor != CurrentActors[0])
        {
            throw new InvalidDataException("Hosted cinematic selection disagrees with its retained first actor.");
        }
        if (FirstActor is byte first)
        {
            RequireBoundRow(first);
        }
        if (SavedActors is { } saved)
        {
            ValidateList(saved);
        }
        ValidateCursor(ContactCursor);
    }

    public NativeBattleCinematicActorCache ReadCachedActor(int row)
    {
        RequireBoundRow(row);
        return CachedActors[row] ?? throw new InvalidDataException("Hosted cinematic actor lacks its cached graphics row.");
    }

    public byte ReadActorOrdinal(int row)
    {
        RequireBoundRow(row);
        return ActorOrdinals[row] ?? throw new NotSupportedException("Hosted cinematic actor has no retained ordinal.");
    }

    public byte ReadFirstActor() => FirstActor ?? throw new NotSupportedException("Hosted cinematic selection has no retained first actor.");

    public ImmutableArray<byte> ReadSavedActors() => SavedActors ?? throw new NotSupportedException("Hosted cinematic selection has no saved actor list.");

    public NativeBattleCinematicActorContext CaptureCurrentContacts(ImmutableArray<byte> occurrences, byte firstHeaderFirstActor)
    {
        RequireBoundRow(firstHeaderFirstActor);
        if (occurrences.IsDefault)
        {
            throw new InvalidDataException("Hosted cinematic contact capture requires its ordered occurrences.");
        }
        NativeBattleGraphicsActorSet seen = default;
        ImmutableArray<byte>.Builder actors = ImmutableArray.CreateBuilder<byte>();
        ImmutableArray<byte?>.Builder ordinals = ActorOrdinals.ToBuilder();
        foreach (byte actor in occurrences)
        {
            RequireBoundRow(actor);
            if (seen.Contains(actor))
            {
                continue;
            }
            seen = seen.Add(actor);
            ordinals[actor] = checked((byte)actors.Count);
            actors.Add(actor);
        }
        byte first = actors.Count == 0 ? firstHeaderFirstActor : actors[0];
        return this with { CurrentActors = actors.ToImmutable(), FirstActor = first, ActorOrdinals = ordinals.MoveToImmutable() };
    }

    public NativeBattleCinematicActorContext WithCurrentActors(ImmutableArray<byte> actors)
    {
        ValidateList(actors);
        return this with { CurrentActors = actors, FirstActor = actors.IsEmpty ? FirstActor : actors[0] };
    }

    public NativeBattleCinematicActorContext WithSavedActors(ImmutableArray<byte> actors)
    {
        ValidateList(actors);
        return this with { SavedActors = actors };
    }

    public NativeBattleCinematicActorContext WithContactCursor(int cursor)
    {
        ValidateCursor(cursor);
        return this with { ContactCursor = cursor };
    }

    public NativeBattleCinematicActorContext RefreshCache(ImmutableArray<NativeBattleGraphicsModel> models) => this with { CachedActors = CaptureRows(Roster, models) };

    private static ImmutableArray<NativeBattleCinematicActorCache?> CaptureRows(NativeBattleGraphicsRoster roster, ImmutableArray<NativeBattleGraphicsModel> models)
    {
        if (models.IsDefault || models.Length != roster.ActorCount)
        {
            throw new InvalidDataException("Hosted cinematic cache requires the complete graphics bank.");
        }
        ImmutableArray<NativeBattleCinematicActorCache?>.Builder cached = ImmutableArray.CreateBuilder<NativeBattleCinematicActorCache?>(models.Length);
        for (int row = 0; row < models.Length; row++)
        {
            NativeBattleGraphicsModel model = models[row];
            if (model is null || model.Index != row)
            {
                throw new InvalidDataException("Hosted cinematic cache requires graphics models in row order.");
            }
            NativeBattleModelTransform? transform = model.Transform;
            cached.Add(roster.Rows[row] is null ? null : new(NativeBattleGraphicsModelAddress.Encode(row), model.Flags0, transform?.Position28, transform?.Vertical34, model.Rotation12, model.Rotation14, model.Rotation16, transform?.Scale148, transform?.Scale154));
        }
        return cached.MoveToImmutable();
    }

    private void ValidateList(ImmutableArray<byte> actors)
    {
        if (actors.IsDefault || actors.Length > byte.MaxValue)
        {
            throw new InvalidDataException("Hosted cinematic actor list exceeds its authored byte count.");
        }
        foreach (byte actor in actors)
        {
            RequireBoundRow(actor);
        }
    }

    private void RequireBoundRow(int row)
    {
        if ((uint)row >= (uint)Roster.ActorCount || Roster.Rows[row] is null)
        {
            throw new InvalidDataException($"Cinematic actor row {row} has no graphics binding.");
        }
    }

    private static void ValidateCursor(int cursor)
    {
        if ((uint)cursor > NativeBattleContactAddress.MaximumRowCount)
        {
            throw new InvalidDataException("Hosted cinematic contact cursor leaves its maximum storage extent.");
        }
    }
}
