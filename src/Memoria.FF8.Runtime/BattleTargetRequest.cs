using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public enum BattleTargetSide
{
    Allies,
    Enemies,
    Both
}

public enum BattleTargetEligibility
{
    SavedStatus,
    DrawnMagic,
    CompelledAction,
    DeathCallback
}

public enum BattleTargetReplacement
{
    None,
    UseCurrentBattlePolicy
}

/// <summary>Immutable selection and eligibility for one queued cast or action.</summary>
public sealed record BattleTargetRequest
{
    public BattleTargetRequest(BattleTargetSelection selection, BattleTargetEligibility eligibility, bool allowsKnockedOutAllies, BattleTargetReplacement replacement, BattleActorReference? deathCallbackSelf = null)
    {
        ArgumentNullException.ThrowIfNull(selection);
        if (!Enum.IsDefined(eligibility) || !Enum.IsDefined(replacement) || (eligibility == BattleTargetEligibility.DeathCallback) != (deathCallbackSelf is not null))
        {
            throw new ArgumentException("Battle target eligibility must identify any death-callback self exception explicitly.");
        }
        Selection = selection;
        Eligibility = eligibility;
        AllowsKnockedOutAllies = allowsKnockedOutAllies;
        Replacement = replacement;
        DeathCallbackSelf = deathCallbackSelf;
    }

    public BattleTargetSelection Selection { get; }
    public BattleTargetEligibility Eligibility { get; }
    public bool AllowsKnockedOutAllies { get; }
    public BattleTargetReplacement Replacement { get; }
    public BattleActorReference? DeathCallbackSelf { get; }

    public static BattleTargetRequest None { get; } = new(new BattleTargetSelection.None(), BattleTargetEligibility.SavedStatus, false, BattleTargetReplacement.None);
}

public abstract record BattleTargetSelection
{
    private protected BattleTargetSelection()
    {
    }

    public sealed record None : BattleTargetSelection;

    public sealed record Explicit : BattleTargetSelection
    {
        public Explicit(IEnumerable<BattleActorReference> actors)
        {
            ArgumentNullException.ThrowIfNull(actors);
            Actors = actors.ToImmutableArray();
            if (Actors.IsEmpty || Actors.Any(actor => actor is null) || Actors.Select(actor => actor.ActorId).Distinct(StringComparer.Ordinal).Count() != Actors.Length)
            {
                throw new ArgumentException("A battle target list requires distinct canonical actors in order.", nameof(actors));
            }
        }

        public ImmutableArray<BattleActorReference> Actors { get; }
    }

    public sealed record SideGroup : BattleTargetSelection
    {
        public SideGroup(BattleTargetSide side)
        {
            if (!Enum.IsDefined(side))
            {
                throw new ArgumentOutOfRangeException(nameof(side));
            }
            Side = side;
        }

        public BattleTargetSide Side { get; }
    }

    public sealed record RandomOnSide : BattleTargetSelection
    {
        public RandomOnSide(BattleSide side) : this(side, samplesOriginalSlots: false)
        {
        }

        internal RandomOnSide(BattleSide side, bool samplesOriginalSlots)
        {
            if (!Enum.IsDefined(side))
            {
                throw new ArgumentOutOfRangeException(nameof(side));
            }
            Side = side;
            SamplesOriginalSlots = samplesOriginalSlots;
        }

        public BattleSide Side { get; }
        internal bool SamplesOriginalSlots { get; }
    }
}

/// <summary>The first target operand recorded when an AI command was queued.</summary>
public abstract record BattlePreviousTarget
{
    private protected BattlePreviousTarget()
    {
    }

    public sealed record Actor : BattlePreviousTarget
    {
        public Actor(BattleActorReference reference)
        {
            ArgumentNullException.ThrowIfNull(reference);
            Reference = reference;
        }

        public BattleActorReference Reference { get; }
    }
}
