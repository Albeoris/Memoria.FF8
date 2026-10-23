using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The suspended card module's current deal, independent of the field VM wait record.</summary>
public sealed record NativeTripleTriadPlayState
{
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string RequestIdentity { get; init; }
    [JsonRequired]
    public required uint InitialRandomState { get; init; }
    [JsonRequired]
    public required uint RandomState { get; init; }
    [JsonRequired]
    public required uint? DealRandomState { get; init; }
    [JsonRequired]
    public required int[] OpponentHand { get; init; }
    [JsonRequired]
    public required int[] SelectedCards { get; init; }
    [JsonRequired]
    public required bool RulesAccepted { get; init; }
    /// <summary>Identifies rematches, which reuse the accepted rules without repeating their prompt.</summary>
    public bool IsReplay { get; init; }
    [JsonRequired]
    public required TripleTriadPromptState Prompt { get; init; }
    [JsonRequired]
    public required TripleTriadSnapshot? Match { get; init; }

    public void Validate()
    {
        Prompt.Validate();
        if (Prompt.Phase == TripleTriadPromptPhase.Cancelled || Prompt.Phase == TripleTriadPromptPhase.FadingIn && Prompt.RandomState != InitialRandomState)
        {
            throw new InvalidDataException("An active card module cannot contain a returned quit prompt or random draws during its initial fade.");
        }
        if (Version != 1 || string.IsNullOrWhiteSpace(RequestIdentity) || OpponentHand is null || OpponentHand.Length != (RulesAccepted ? 5 : 0) || OpponentHand.Any(id => id is < 0 or >= 110) || DealRandomState.HasValue != RulesAccepted || SelectedCards is null || SelectedCards.Length > 5 || SelectedCards.Any(id => id is < 0 or >= 110) || (Match is not null) != (SelectedCards.Length == 5))
        {
            throw new InvalidDataException("Native Triple Triad requires its exact suspended request, deal and selected cards.");
        }
        if (Match is { } match && (match.InitialHands is not { Length: 2 } || match.InitialHands.Any(hand => hand is not { Length: 5 }) || match.Hands is not { Length: 2 } || match.Hands.Any(hand => hand is not { Length: 5 }) || match.Board is not { Length: 9 } || match.BoardElements is not { Length: 9 } || match.InstanceOrder is not { Length: 10 }))
        {
            throw new InvalidDataException("Native Triple Triad match arrays are incomplete.");
        }
        if (!RulesAccepted && SelectedCards.Length != 0)
        {
            throw new InvalidDataException("Card selection cannot precede the native rules prompt.");
        }
        if (RulesAccepted != (Prompt.Phase == TripleTriadPromptPhase.Accepted) || !RulesAccepted && RandomState != Prompt.RandomState || RulesAccepted && DealRandomState != Prompt.RandomState)
        {
            throw new InvalidDataException("Native Triple Triad prompt and deal random ownership disagree.");
        }
    }

    public NativeTripleTriadPlayState DeepCopy()
    {
        Validate();
        return this with
        {
            OpponentHand = OpponentHand.ToArray(),
            SelectedCards = SelectedCards.ToArray(),
            Match = Match is null ? null : Match with { InitialHands = Match.InitialHands.Select(hand => hand.ToArray()).ToArray(), Hands = Match.Hands.Select(hand => hand.ToArray()).ToArray(), Board = Match.Board.ToArray(), BoardElements = Match.BoardElements.ToArray(), InstanceOrder = Match.InstanceOrder.ToArray() }
        };
    }
}
