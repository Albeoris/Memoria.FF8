using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

/// <summary>The suspended card module's current deal, independent of the field VM wait record.</summary>
public sealed record NativeTripleTriadPlayState
{
    public bool[]? RewardParticipants { get; init; }
    public uint? RewardRandomState { get; init; }
    public int BlockedCellCount { get; init; }
    public bool UseModifiedSamePlus { get; init; }
    public int Width { get; init; } = 3;
    public int Height { get; init; } = 3;
    public int HandSize { get; init; } = 5;
    public int RedrawsUsed { get; init; }
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
        if (RedrawsUsed is < 0 or > 3 || Width is < 2 or > 5 || Height is < 2 or > 5 || BlockedCellCount < 0 || BlockedCellCount > Width * Height / 3 || HandSize < (Width * Height - BlockedCellCount + 1) / 2 || HandSize > 50 || Version != 1 || string.IsNullOrWhiteSpace(RequestIdentity) || OpponentHand is null || OpponentHand.Length != (RulesAccepted ? HandSize : 0) || OpponentHand.Any(id => id is < 0 or >= 110) || DealRandomState.HasValue != RulesAccepted || SelectedCards is null || SelectedCards.Length > HandSize || SelectedCards.Any(id => id is < 0 or >= 110) || (Match is not null) != (SelectedCards.Length == HandSize))
        {
            throw new InvalidDataException("Native Triple Triad requires its exact suspended request, deal and selected cards.");
        }
        if (Match is { } match && (match.BlockedCells is null || match.BlockedCells.Length != BlockedCellCount || match.UseModifiedSamePlus != UseModifiedSamePlus || match.Width != Width || match.Height != Height || match.InitialHands is not { Length: 2 } || match.InitialHands.Any(hand => hand is null || hand.Length != HandSize) || match.Hands is not { Length: 2 } || match.Hands.Any(hand => hand is null || hand.Length != HandSize) || match.Board is null || match.Board.Length != Width * Height || match.BoardElements is null || match.BoardElements.Length != Width * Height || match.InstanceOrder is null || match.InstanceOrder.Length != HandSize * 2))
        {
            throw new InvalidDataException("Native Triple Triad match arrays are incomplete.");
        }
        if ((RewardParticipants is null) != (RewardRandomState is null))
        {
            throw new InvalidDataException("A saved reward pool requires its exact sampling seed.");
        }
        if (RewardParticipants is { } participants)
        {
            if (Match is null || participants.Length != HandSize * 2 || Match.Board.Where((_, cell) => !Match.BlockedCells.Contains(cell)).Any(card => card is null))
            {
                throw new InvalidDataException("A reward pool requires a complete final board.");
            }
            int score = Match.Board.Count(card => card?.Owner == 0) + Match.Hands[0].Count(card => card.HasValue);
            if (Match.Rules.HasFlag(TripleTriadRules.SuddenDeath) && score == HandSize)
            {
                throw new InvalidDataException("A continuing Sudden Death round cannot publish rewards.");
            }
            TripleTriadPreparedRewardPool expected = TripleTriadPreparation.GenerateRewardPool(Match, RewardRandomState!.Value);
            if (!participants.SequenceEqual(expected.Participants) || RandomState != expected.RandomState)
            {
                throw new InvalidDataException("Saved reward participants disagree with their deterministic sampling.");
            }
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
            RewardParticipants = RewardParticipants?.ToArray(),
            OpponentHand = OpponentHand.ToArray(),
            SelectedCards = SelectedCards.ToArray(),
            Match = Match is null ? null : Match with { InitialHands = Match.InitialHands.Select(hand => hand.ToArray()).ToArray(), Hands = Match.Hands.Select(hand => hand.ToArray()).ToArray(), Board = Match.Board.ToArray(), BoardElements = Match.BoardElements.ToArray(), InstanceOrder = Match.InstanceOrder.ToArray(), BlockedCells = Match.BlockedCells.ToArray() }
        };
    }
}
