using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record TripleTriadNativeAiWeights(int BoardBase, int CardPriorityScale, int RandomAmplitude, int SpecialMultiplier, int HeldCardMultiplier);
public sealed record TripleTriadNativeAiParameters(ImmutableArray<byte> Depths, ImmutableArray<TripleTriadNativeAiWeights> Profiles);
public sealed record TripleTriadNativeAiResult(TripleTriadMove Move, uint RandomState, int RandomDraws, int EvaluatedMoves, int Depth);

/// <summary>Native 53ADF0/53B170/53B430 search, without the presentation-frame yields.</summary>
public static class TripleTriadNativeAi
{
    public static bool SupportsParameters(TripleTriadNativeAiParameters parameters, byte intelligence, byte personality, int emptyCells)
    {
        return parameters is not null && parameters.Depths.Length == 73 && parameters.Profiles.Length == 8 && emptyCells is >= 1 and <= 9 && parameters.Depths[(intelligence & 7) * 9 + emptyCells] is >= 1 and <= 4 && parameters.Depths[(intelligence & 7) * 9 + emptyCells] <= emptyCells;
    }

    public static TripleTriadNativeAiResult ChooseMove(IReadOnlyList<TripleTriadCard> catalog, IReadOnlyList<byte> tradePriorities, TripleTriadMatch match, TripleTriadNativeAiParameters parameters, byte intelligence, byte personality, uint randomState, IReadOnlyList<int>? originalAiHand = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(tradePriorities);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(parameters);
        int emptyCells = match.Board.Count(card => card is null);
        if (!SupportsParameters(parameters, intelligence, personality, emptyCells) || catalog.Count != 110 || tradePriorities.Count != 110)
        {
            throw new NotSupportedException("Native Triple Triad AI requires all original cards and a supported original search profile.");
        }
        int depth = parameters.Depths[(intelligence & 7) * 9 + emptyCells];
        if (depth > emptyCells)
        {
            throw new InvalidDataException("Native Triple Triad search depth exceeds the remaining board cells.");
        }
        TripleTriadNativeAiWeights weights = parameters.Profiles[personality & 7];
        int[] values = tradePriorities.Select(priority => unchecked(priority * weights.CardPriorityScale) / 200 >> 12).ToArray();
        TripleTriadRandom random = new(randomState);
        int draws = 0;
        int evaluated = 0;
        int aiPlayer = match.CurrentPlayer;
        bool guessHiddenHand = !match.Rules.HasFlag(TripleTriadRules.Open) && (intelligence & 0x10) == 0;
        if (guessHiddenHand && match.Round > 0 && originalAiHand is null)
        {
            throw new NotSupportedException("Native hidden-hand search after Sudden Death requires the original pre-match AI hand.");
        }
        IReadOnlyList<int> initialAiHand = originalAiHand ?? match.InitialHands[aiPlayer];
        if (initialAiHand.Count != 5 || initialAiHand.Any(id => (uint)id >= 110))
        {
            throw new InvalidDataException("Native Triple Triad AI requires its five original pre-Sudden-Death card IDs.");
        }
        int Next()
        {
            random = random.Draw(out int value);
            draws++;
            return value;
        }
        TripleTriadMatch root = match.CreateSimulation();
        if (guessHiddenHand)
        {
            // 53AE70 replaces every opposite simulated slot, including used ones.
            // Guessed levels are bounded by the AI's corresponding original card.
            int?[][] guessed = match.Hands.Select(hand => hand.ToArray()).ToArray();
            for (int hand = 0; hand < 5; hand++)
            {
                int levels = initialAiHand[hand] / 11 + 1;
                guessed[1 - aiPlayer][hand] = Next() % levels * 11 + Next() % 11;
            }
            root = match.CreateSimulation(guessed);
        }
        SearchFrame[] frames = Enumerable.Range(0, depth).Select(_ => new SearchFrame()).ToArray();

        int Evaluate(TripleTriadMatch state)
        {
            int score = 0;
            foreach (TripleTriadPlacedCard? placed in state.Board)
            {
                if (placed is not null)
                {
                    int value = unchecked(weights.BoardBase + values[placed.CardId]);
                    score = unchecked(score + (placed.Owner == aiPlayer ? value : -value));
                }
            }
            foreach (int? card in state.Hands[aiPlayer])
            {
                if (card is int id)
                {
                    score = unchecked(score + (unchecked(values[id] * weights.HeldCardMultiplier) >> 12));
                }
            }
            if (weights.RandomAmplitude != 0)
            {
                score = unchecked(score + Next() % (weights.RandomAmplitude + 1));
            }
            evaluated++;
            return score;
        }

        (int Weighted, int Raw) Search(TripleTriadMatch state, int ply)
        {
            SearchFrame frame = frames[ply];
            bool maximizing = state.CurrentPlayer == aiPlayer;
            bool found = false;
            foreach (TripleTriadMove move in state.GetLegalMoves())
            {
                found = true;
                TripleTriadMatch candidate = state.CreateSimulation();
                TripleTriadMoveResult placed = candidate.PlayForNativeAi(move.HandIndex, move.CellIndex);
                int multiplier = maximizing && (placed.Same || placed.Plus) ? weights.SpecialMultiplier : 4096;
                int raw;
                int weighted;
                if (ply + 1 == depth)
                {
                    raw = Evaluate(candidate);
                    weighted = unchecked(raw * multiplier) >> 12;
                }
                else
                {
                    (int childWeighted, int childRaw) = Search(candidate, ply + 1);
                    raw = childRaw;
                    weighted = unchecked(childWeighted * multiplier) >> 12;
                }
                if (frame.Fresh || (maximizing ? weighted > frame.Weighted : raw < frame.Raw))
                {
                    frame.Fresh = false;
                    frame.Weighted = weighted;
                    frame.Raw = raw;
                    frame.Move = move;
                }
                // Preserve the original's two score channels and strict pruning
                // comparisons. A frame's bound survives its parent's siblings.
                if (frame.HasBound && (maximizing ? raw > frame.Bound : weighted < frame.Bound))
                {
                    break;
                }
            }
            if (!found)
            {
                throw new InvalidDataException("Native Triple Triad search reached a node without a legal move.");
            }
            frame.Fresh = true;
            frame.HasBound = true;
            frame.Bound = maximizing ? frame.Raw : frame.Weighted;
            return (frame.Weighted, frame.Raw);
        }

        Search(root, 0);
        return new TripleTriadNativeAiResult(frames[0].Move!, random.State, draws, evaluated, depth);
    }

    private sealed class SearchFrame
    {
        public bool Fresh { get; set; } = true;
        public bool HasBound { get; set; }
        public int Weighted { get; set; }
        public int Raw { get; set; }
        public int Bound { get; set; }
        public TripleTriadMove? Move { get; set; }
    }
}
