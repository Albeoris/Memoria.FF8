using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Memoria.FF8.Runtime;

public enum TripleTriadElement
{
    None,
    Fire,
    Ice,
    Thunder,
    Earth,
    Poison,
    Wind,
    Water,
    Holy
}

[Flags]
public enum TripleTriadRules
{
    None = 0,
    Open = 1,
    Same = 2,
    Plus = 4,
    SameWall = 8,
    Elemental = 16,
    SuddenDeath = 32,
    Random = 64
}

public enum TripleTriadOutcome
{
    InProgress,
    PlayerWin,
    OpponentWin,
    Draw
}

public sealed record TripleTriadCard(int Id, int Top, int Right, int Bottom, int Left, TripleTriadElement Element);
public sealed record TripleTriadPlacedCard([property: JsonRequired] int CardId, [property: JsonRequired] int Owner, [property: JsonRequired] int SourcePlayer, [property: JsonRequired] int SourceHandIndex);
public sealed record TripleTriadMove(int HandIndex, int CellIndex);
public enum TripleTriadCaptureKind
{
    Normal,
    Same,
    Plus,
    Combo
}

public sealed record TripleTriadCapture(int CellIndex, int SourceCellIndex, int Wave)
{
    public TripleTriadCaptureKind Kind { get; init; }
}
/// <summary>A rank comparison which participates in a triggered special rule. Wall entries have opposing rank 10; border walls have cell -1 and blocked walls retain their cell index.</summary>
public sealed record TripleTriadRuleParticipant(int Direction, int CellIndex, int OwnRank, int AdjacentRank, bool IsWall);
public sealed record TripleTriadMoveResult(int CardId, int Player, int HandIndex, int CellIndex, IReadOnlyList<int> CapturedCells, bool Same, bool Plus, bool Combo, TripleTriadOutcome Outcome)
{
    public IReadOnlyList<TripleTriadCapture> Captures { get; init; } = [];
    public IReadOnlyList<TripleTriadRuleParticipant> SameParticipants { get; init; } = [];
    public IReadOnlyList<TripleTriadRuleParticipant> PlusParticipants { get; init; } = [];
}

/// <summary>A standalone state, independent of field saves and native card inventory.</summary>
public sealed record TripleTriadSnapshot
{
    public int Width { get; init; } = 3;
    public int Height { get; init; } = 3;
    public bool UseModifiedSamePlus { get; init; }
    public int[] BlockedCells { get; init; } = [];
    [JsonRequired]
    public required int Version { get; init; }
    [JsonRequired]
    public required string CatalogHash { get; init; }
    [JsonRequired]
    public required TripleTriadRules Rules { get; init; }
    [JsonRequired]
    public required int StartingPlayer { get; init; }
    [JsonRequired]
    public required int CurrentPlayer { get; init; }
    [JsonRequired]
    public required int Round { get; init; }
    [JsonRequired]
    public required int[][] InitialHands { get; init; }
    [JsonRequired]
    public required int[] InstanceOrder { get; init; }
    [JsonRequired]
    public required int?[][] Hands { get; init; }
    [JsonRequired]
    public required TripleTriadPlacedCard?[] Board { get; init; }
    [JsonRequired]
    public required TripleTriadElement[] BoardElements { get; init; }
}

/// <summary>Pure rectangular match mechanics; card selection, randomization and inventory settlement belong to the caller.</summary>
public sealed class TripleTriadMatch
{
    private const TripleTriadRules SupportedRules = TripleTriadRules.Open | TripleTriadRules.Same | TripleTriadRules.Plus | TripleTriadRules.SameWall | TripleTriadRules.Elemental | TripleTriadRules.SuddenDeath | TripleTriadRules.Random;
    private readonly TripleTriadCard[] cards;
    private readonly string catalogHash;
    private int[][] initialHands;
    private int[] instanceOrder;
    private int?[][] hands;
    private TripleTriadPlacedCard?[] board;
    private TripleTriadElement[] boardElements;
    private readonly bool[] blocked;

    private TripleTriadMatch(TripleTriadCard[] cards, string catalogHash, TripleTriadSnapshot snapshot)
    {
        this.cards = cards;
        this.catalogHash = catalogHash;
        Width = snapshot.Width;
        Height = snapshot.Height;
        UseModifiedSamePlus = snapshot.UseModifiedSamePlus;
        Rules = snapshot.Rules;
        StartingPlayer = snapshot.StartingPlayer;
        CurrentPlayer = snapshot.CurrentPlayer;
        Round = snapshot.Round;
        initialHands = snapshot.InitialHands.Select(hand => hand.ToArray()).ToArray();
        instanceOrder = snapshot.InstanceOrder.ToArray();
        hands = snapshot.Hands.Select(hand => hand.ToArray()).ToArray();
        board = snapshot.Board.ToArray();
        blocked = new bool[board.Length];
        foreach (int cell in snapshot.BlockedCells)
        {
            blocked[cell] = true;
        }
        boardElements = snapshot.BoardElements.ToArray();
    }

    public int Width { get; }
    public int Height { get; }
    public int HandSize => initialHands[0].Length;
    public int PlayableCellCount => blocked.Count(value => !value);
    public IReadOnlyList<int> BlockedCells => Array.AsReadOnly(Enumerable.Range(0, blocked.Length).Where(cell => blocked[cell]).ToArray());
    public bool IsBlocked(int cellIndex) => (uint)cellIndex < blocked.Length ? blocked[cellIndex] : throw new ArgumentOutOfRangeException(nameof(cellIndex));
    public bool UseModifiedSamePlus { get; }
    public TripleTriadRules Rules { get; }
    public int StartingPlayer { get; private set; }
    public int CurrentPlayer { get; private set; }
    public int Round { get; private set; }
    public IReadOnlyList<IReadOnlyList<int>> InitialHands => initialHands.Select(hand => (IReadOnlyList<int>)Array.AsReadOnly(hand.ToArray())).ToArray();
    public IReadOnlyList<IReadOnlyList<int?>> Hands => hands.Select(hand => (IReadOnlyList<int?>)Array.AsReadOnly(hand.ToArray())).ToArray();
    public IReadOnlyList<TripleTriadPlacedCard?> Board => Array.AsReadOnly(board.ToArray());
    public IReadOnlyList<TripleTriadElement> BoardElements => Array.AsReadOnly(boardElements.ToArray());
    /// <summary>Final/current owners by original player hand slots, then original opponent hand slots, across every Sudden Death round.</summary>
    public IReadOnlyList<int> OriginalInstanceFinalOwners => Array.AsReadOnly(CurrentInstances().Select(card => card.Owner).ToArray());
    public IReadOnlyList<bool> OriginalInstancePlayed => Array.AsReadOnly(instanceOrder.Select(source => board.Any(card => card?.SourcePlayer == source / HandSize && card.SourceHandIndex == source % HandSize)).ToArray());
    public int OriginalInstanceIndex(int sourcePlayer, int handIndex)
    {
        ValidatePlayer(sourcePlayer);
        if ((uint)handIndex >= HandSize)
        {
            throw new ArgumentOutOfRangeException(nameof(handIndex));
        }
        return Array.IndexOf(instanceOrder, sourcePlayer * HandSize + handIndex);
    }

    public int PlayerScore => Score(0);
    public int OpponentScore => Score(1);
    public TripleTriadOutcome Outcome => board.Where((card, cell) => !blocked[cell]).Any(card => card is null) ? TripleTriadOutcome.InProgress : PlayerScore > OpponentScore ? TripleTriadOutcome.PlayerWin : PlayerScore < OpponentScore ? TripleTriadOutcome.OpponentWin : TripleTriadOutcome.Draw;
    public bool CanRestartSuddenDeath => Rules.HasFlag(TripleTriadRules.SuddenDeath) && Outcome == TripleTriadOutcome.Draw;

    public static TripleTriadMatch Create(IReadOnlyList<TripleTriadCard> definitions, IReadOnlyList<int> playerHand, IReadOnlyList<int> opponentHand, TripleTriadRules rules = TripleTriadRules.None, int startingPlayer = 0, IReadOnlyList<TripleTriadElement>? boardElements = null, int width = 3, int height = 3, bool useModifiedSamePlus = false, IReadOnlyList<int>? blockedCells = null)
    {
        ArgumentNullException.ThrowIfNull(playerHand);
        ArgumentNullException.ThrowIfNull(opponentHand);
        if (width is < 2 or > 5 || height is < 2 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Board dimensions must be between two and five.");
        }
        TripleTriadCard[] catalog = ValidateCatalog(definitions);
        TripleTriadSnapshot snapshot = new()
        {
            Version = 1,
            Width = width,
            Height = height,
            UseModifiedSamePlus = useModifiedSamePlus,
            BlockedCells = blockedCells?.ToArray() ?? [],
            CatalogHash = HashCatalog(catalog),
            Rules = rules,
            StartingPlayer = startingPlayer,
            CurrentPlayer = startingPlayer,
            Round = 0,
            InitialHands = [playerHand.ToArray(), opponentHand.ToArray()],
            InstanceOrder = Enumerable.Range(0, playerHand.Count + opponentHand.Count).ToArray(),
            Hands = [playerHand.Select(id => (int?)id).ToArray(), opponentHand.Select(id => (int?)id).ToArray()],
            Board = new TripleTriadPlacedCard?[width * height],
            BoardElements = boardElements?.ToArray() ?? new TripleTriadElement[width * height]
        };
        return Restore(catalog, snapshot);
    }

    public static TripleTriadMatch Restore(IReadOnlyList<TripleTriadCard> definitions, TripleTriadSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        TripleTriadCard[] catalog = ValidateCatalog(definitions);
        string hash = HashCatalog(catalog);
        ValidateSnapshot(snapshot, hash);
        return new TripleTriadMatch(catalog, hash, snapshot);
    }

    public TripleTriadSnapshot CaptureSnapshot() => new()
    {
        Version = 1,
        Width = Width,
        Height = Height,
        UseModifiedSamePlus = UseModifiedSamePlus,
        BlockedCells = BlockedCells.ToArray(),
        CatalogHash = catalogHash,
        Rules = Rules,
        StartingPlayer = StartingPlayer,
        CurrentPlayer = CurrentPlayer,
        Round = Round,
        InitialHands = initialHands.Select(hand => hand.ToArray()).ToArray(),
        InstanceOrder = instanceOrder.ToArray(),
        Hands = hands.Select(hand => hand.ToArray()).ToArray(),
        Board = board.ToArray(),
        BoardElements = boardElements.ToArray()
    };

    /// <summary>Transient AI search state; guessed hands have no save provenance and must never be restored as gameplay snapshots.</summary>
    internal TripleTriadMatch CreateSimulation(int?[][]? replacementHands = null)
    {
        if (replacementHands is not null && (replacementHands.Length != 2 || replacementHands.Any(hand => hand is null || hand.Length != HandSize || hand.Any(id => id.HasValue && (uint)id.Value >= 110))))
        {
            throw new ArgumentException("Simulation hands require two arrays matching the hand size of valid card IDs.", nameof(replacementHands));
        }
        TripleTriadSnapshot snapshot = CaptureSnapshot();
        return new TripleTriadMatch(cards, catalogHash, snapshot with { Hands = replacementHands ?? snapshot.Hands });
    }

    public IReadOnlyList<int?> GetVisibleHand(int viewer, int owner)
    {
        ValidatePlayer(viewer);
        ValidatePlayer(owner);
        return Array.AsReadOnly(hands[owner].Select(id => viewer == owner || Rules.HasFlag(TripleTriadRules.Open) ? id : null).ToArray());
    }

    public IReadOnlyList<TripleTriadMove> GetLegalMoves()
    {
        List<TripleTriadMove> moves = [];
        if (Outcome != TripleTriadOutcome.InProgress)
        {
            return moves;
        }
        for (int hand = 0; hand < HandSize; hand++)
        {
            if (hands[CurrentPlayer][hand] is null)
            {
                continue;
            }
            for (int cell = 0; cell < board.Length; cell++)
            {
                if (!blocked[cell] && board[cell] is null)
                {
                    moves.Add(new TripleTriadMove(hand, cell));
                }
            }
        }
        return moves;
    }

    /// <summary>Deterministic greedy opponent using only its own hand and the public board. Ties use hand index, then cell index.</summary>
    public TripleTriadMove ChooseMove()
    {
        TripleTriadMove? best = null;
        int bestValue = int.MinValue;
        foreach (TripleTriadMove move in GetLegalMoves())
        {
            TripleTriadMatch candidate = new(cards, catalogHash, CaptureSnapshot());
            candidate.Play(move.HandIndex, move.CellIndex);
            int value = candidate.Score(CurrentPlayer) * 100;
            TripleTriadCard card = cards[hands[CurrentPlayer][move.HandIndex]!.Value];
            for (int direction = 0; direction < 4; direction++)
            {
                int neighbor = Neighbor(move.CellIndex, direction);
                if (neighbor >= 0 && !blocked[neighbor] && candidate.board[neighbor] is null)
                {
                    value += candidate.EffectiveRank(card, move.CellIndex, direction);
                }
            }
            if (value > bestValue)
            {
                bestValue = value;
                best = move;
            }
        }
        return best ?? throw new InvalidOperationException("A completed match has no legal move.");
    }

    /// <summary>Resolves a prospective move without changing the match, including every capture wave.</summary>
    public TripleTriadMoveResult PreviewMove(int handIndex, int cellIndex) => CreateSimulation().Play(handIndex, cellIndex);

    public int GetElementModifier(int cardId, int cellIndex)
    {
        if ((uint)cardId >= cards.Length || (uint)cellIndex >= board.Length)
        {
            throw new ArgumentOutOfRangeException();
        }
        TripleTriadElement element = boardElements[cellIndex];
        return Rules.HasFlag(TripleTriadRules.Elemental) && element != TripleTriadElement.None ? cards[cardId].Element == element ? 1 : -1 : 0;
    }

    public int GetDisplayElementModifier(int cardId, int cellIndex) => UseModifiedSamePlus || (Rules & (TripleTriadRules.Same | TripleTriadRules.Plus)) == 0 ? GetElementModifier(cardId, cellIndex) : 0;

    private int SpecialRank(TripleTriadCard card, int cellIndex, int direction) => UseModifiedSamePlus ? EffectiveRank(card, cellIndex, direction) : Rank(card, direction);

    public int GetEffectiveRank(int cardId, int cellIndex, int direction) => Rank(cards[cardId], direction) + GetElementModifier(cardId, cellIndex);

    public TripleTriadMoveResult Play(int handIndex, int cellIndex) => PlayCore(handIndex, cellIndex, true);

    /// <summary>53B170 omits the game's 53AD70 source promotion between search capture passes.</summary>
    internal TripleTriadMoveResult PlayForNativeAi(int handIndex, int cellIndex) => PlayCore(handIndex, cellIndex, false);

    private TripleTriadMoveResult PlayCore(int handIndex, int cellIndex, bool propagateCombo)
    {
        if (Outcome != TripleTriadOutcome.InProgress)
        {
            throw new InvalidOperationException("The round has ended.");
        }
        if ((uint)handIndex >= HandSize || (uint)cellIndex >= board.Length || blocked[cellIndex] || hands[CurrentPlayer][handIndex] is not int cardId || board[cellIndex] is not null)
        {
            throw new ArgumentException("A move requires an unplayed hand slot and an empty board cell.");
        }
        int player = CurrentPlayer;
        TripleTriadCard card = cards[cardId];
        List<int> same = [];
        List<TripleTriadRuleParticipant> comparisons = [];
        List<TripleTriadRuleParticipant> sameParticipants = [];
        List<int> sameCaptures = [];
        Dictionary<int, List<int>> sums = [];
        int sameWalls = 0;
        int selectedSum = 0;
        int greatestMultiplicity = 0;
        // Native C75B90 / C74D00 direction order is top, bottom, left, right.
        foreach (int direction in new[] { 0, 2, 3, 1 })
        {
            int neighbor = Neighbor(cellIndex, direction);
            if (neighbor < 0 || blocked[neighbor])
            {
                if (Rules.HasFlag(TripleTriadRules.SameWall) && SpecialRank(card, cellIndex, direction) == 10)
                {
                    sameWalls++;
                    sameParticipants.Add(new TripleTriadRuleParticipant(direction, neighbor, 10, 10, true));
                }
                continue;
            }
            if (board[neighbor] is not TripleTriadPlacedCard adjacent)
            {
                continue;
            }
            int ownRank = SpecialRank(card, cellIndex, direction);
            int adjacentRank = SpecialRank(cards[adjacent.CardId], neighbor, Opposite(direction));
            TripleTriadRuleParticipant comparison = new(direction, neighbor, ownRank, adjacentRank, false);
            comparisons.Add(comparison);
            if (ownRank == adjacentRank)
            {
                sameParticipants.Add(comparison);
                same.Add(neighbor);
            }
            if (ownRank >= adjacentRank)
            {
                sameCaptures.Add(neighbor);
            }
            int sum = ownRank + adjacentRank;
            if (!sums.TryGetValue(sum, out List<int>? group))
            {
                group = [];
                sums.Add(sum, group);
            }
            group.Add(neighbor);
            if (group.Count > greatestMultiplicity)
            {
                greatestMultiplicity = group.Count;
                selectedSum = sum;
            }
        }
        hands[player][handIndex] = null;
        board[cellIndex] = new TripleTriadPlacedCard(cardId, player, player, handIndex);
        List<int> captured = [];
        Dictionary<int, TripleTriadCaptureKind> captureKinds = [];
        // The native second pass uses >= for every adjacent enemy after the two-match threshold.
        bool sameTriggered = Rules.HasFlag(TripleTriadRules.Same) && same.Count + sameWalls >= 2 && sameCaptures.Any(cell => board[cell]!.Owner != player);
        if (sameTriggered)
        {
            foreach (int cell in sameCaptures)
            {
                if (Capture(cell, player, captured))
                {
                    captureKinds[cell] = TripleTriadCaptureKind.Same;
                }
            }
        }
        bool plusTriggered = false;
        if (Rules.HasFlag(TripleTriadRules.Plus))
        {
            // 53AB30 keeps the first sum to reach the greatest multiplicity; tied groups are not combined.
            List<int>? selected = greatestMultiplicity >= 2 ? sums[selectedSum] : null;
            if (selected is not null)
            {
                foreach (int cell in selected)
                {
                    if (Capture(cell, player, captured))
                    {
                        plusTriggered = true;
                        captureKinds[cell] = TripleTriadCaptureKind.Plus;
                    }
                }
            }
        }
        CaptureWeakerNeighbors(cellIndex, player, captured, null);
        // 53AD70 promotes every flipped card to the next wave when 53ACF0 returned a Same/Plus effect.
        List<TripleTriadCapture> captures = captured.Select(cell => new TripleTriadCapture(cell, cellIndex, 0) { Kind = captureKinds.GetValueOrDefault(cell) }).ToList();
        Queue<int> combo = propagateCombo && (sameTriggered || plusTriggered) ? new Queue<int>(captured) : new Queue<int>();
        bool comboTriggered = false;
        while (combo.TryDequeue(out int source))
        {
            int previousCount = captured.Count;
            CaptureWeakerNeighbors(source, player, captured, combo);
            comboTriggered |= captured.Count != previousCount;
            int wave = captures.Single(capture => capture.CellIndex == source).Wave + 1;
            captures.AddRange(captured.Skip(previousCount).Select(cell => new TripleTriadCapture(cell, source, wave) { Kind = TripleTriadCaptureKind.Combo }));
        }
        CurrentPlayer = 1 - player;
        return new TripleTriadMoveResult(cardId, player, handIndex, cellIndex, captured.AsReadOnly(), sameTriggered, plusTriggered, comboTriggered, Outcome)
        {
            Captures = captures.AsReadOnly(),
            SameParticipants = sameTriggered ? sameParticipants.AsReadOnly() : [],
            PlusParticipants = plusTriggered ? comparisons.Where(comparison => comparison.OwnRank + comparison.AdjacentRank == selectedSum).ToArray() : []
        };
    }

    /// <summary>Completes the board deterministically for a debug result while preserving every card instance and the unplayed hand.</summary>
    public void CompleteInstantly(bool victory)
    {
        if (Outcome != TripleTriadOutcome.InProgress)
        {
            throw new InvalidOperationException("The round has ended.");
        }
        while (Outcome == TripleTriadOutcome.InProgress)
        {
            int handIndex = Array.FindIndex(hands[CurrentPlayer], card => card.HasValue);
            int cellIndex = Enumerable.Range(0, board.Length).First(cell => !blocked[cell] && board[cell] is null);
            Play(handIndex, cellIndex);
        }
        int winner = victory ? 0 : 1;
        for (int cell = 0; cell < board.Length; cell++)
        {
            if (board[cell] is { } placed)
            {
                board[cell] = placed with { Owner = winner };
            }
        }
    }

    public void RestartSuddenDeath(int startingPlayer, IReadOnlyList<TripleTriadElement>? boardElements = null)
    {
        if (!CanRestartSuddenDeath)
        {
            throw new InvalidOperationException("Sudden Death requires a completed drawn round with the rule enabled.");
        }
        ValidatePlayer(startingPlayer);
        int nextRound = checked(Round + 1);
        TripleTriadElement[] elements = boardElements?.ToArray() ?? this.boardElements.ToArray();
        ValidateElements(elements, Rules, board.Length);
        // Native 535D00 scans the original instances, retaining final owner as the next source side.
        TripleTriadPlacedCard[] instances = CurrentInstances();
        // Native array order is side0 (opponent), then side1 (player); public identity order remains player, then opponent.
        int[] nativeOrder = Enumerable.Range(HandSize, HandSize).Concat(Enumerable.Range(0, HandSize)).ToArray();
        int[][] redistributed = Enumerable.Range(0, 2).Select(player => nativeOrder.Select(index => instances[index]).Where(card => card.Owner == player).Select(card => card.CardId).ToArray()).ToArray();
        int[] ownerSlots = new int[2];
        foreach (int index in nativeOrder)
        {
            instanceOrder[index] = instances[index].Owner * HandSize + ownerSlots[instances[index].Owner]++;
        }
        initialHands = redistributed;
        hands = redistributed.Select(hand => hand.Select(id => (int?)id).ToArray()).ToArray();
        board = new TripleTriadPlacedCard?[Width * Height];
        this.boardElements = elements;
        StartingPlayer = startingPlayer;
        CurrentPlayer = startingPlayer;
        Round = nextRound;
    }

    private int Score(int player) => board.Count(card => card?.Owner == player) + hands[player].Count(id => id.HasValue);

    private TripleTriadPlacedCard[] CurrentInstances() => instanceOrder.Select(source => board.FirstOrDefault(card => card?.SourcePlayer == source / HandSize && card.SourceHandIndex == source % HandSize) ?? new TripleTriadPlacedCard(initialHands[source / HandSize][source % HandSize], source / HandSize, source / HandSize, source % HandSize)).ToArray();

    private bool Capture(int cell, int player, List<int> captured)
    {
        TripleTriadPlacedCard target = board[cell]!;
        if (target.Owner == player)
        {
            return false;
        }
        board[cell] = target with { Owner = player };
        captured.Add(cell);
        return true;
    }

    private void CaptureWeakerNeighbors(int source, int player, List<int> captured, Queue<int>? combo)
    {
        for (int direction = 0; direction < 4; direction++)
        {
            int neighbor = Neighbor(source, direction);
            if (neighbor < 0 || board[neighbor] is not TripleTriadPlacedCard adjacent || adjacent.Owner == player)
            {
                continue;
            }
            TripleTriadCard attacker = cards[board[source]!.CardId];
            if (EffectiveRank(attacker, source, direction) > EffectiveRank(cards[adjacent.CardId], neighbor, Opposite(direction)) && Capture(neighbor, player, captured))
            {
                combo?.Enqueue(neighbor);
            }
        }
    }

    private int EffectiveRank(TripleTriadCard card, int cell, int direction)
    {
        TripleTriadElement element = boardElements[cell];
        int modifier = Rules.HasFlag(TripleTriadRules.Elemental) && element != TripleTriadElement.None ? card.Element == element ? 1 : -1 : 0;
        return Rank(card, direction) + modifier;
    }

    private static int Rank(TripleTriadCard card, int direction) => direction switch { 0 => card.Top, 1 => card.Right, 2 => card.Bottom, 3 => card.Left, _ => throw new ArgumentOutOfRangeException(nameof(direction)) };
    private static int Opposite(int direction) => (direction + 2) % 4;
    private int Neighbor(int cell, int direction) => direction switch { 0 => cell >= Width ? cell - Width : -1, 1 => cell % Width < Width - 1 ? cell + 1 : -1, 2 => cell < Width * (Height - 1) ? cell + Width : -1, 3 => cell % Width > 0 ? cell - 1 : -1, _ => throw new ArgumentOutOfRangeException(nameof(direction)) };

    private static TripleTriadCard[] ValidateCatalog(IReadOnlyList<TripleTriadCard> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        if (definitions.Count != 110 || definitions.Any(card => card is null) || definitions.Select(card => card.Id).Distinct().Count() != 110 || definitions.Any(card => (uint)card.Id >= 110 || card.Top is < 1 or > 10 || card.Right is < 1 or > 10 || card.Bottom is < 1 or > 10 || card.Left is < 1 or > 10 || !Enum.IsDefined(card.Element)))
        {
            throw new InvalidDataException("A catalog requires exactly the 110 unique card IDs and printed ranks from 1 through A (10).");
        }
        return definitions.OrderBy(card => card.Id).ToArray();
    }

    private static string HashCatalog(TripleTriadCard[] definitions) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join(";", definitions.Select(card => $"{card.Id},{card.Top},{card.Right},{card.Bottom},{card.Left},{(int)card.Element}")))));

    private static void ValidatePlayer(int player)
    {
        if ((uint)player > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(player));
        }
    }

    private static void ValidateElements(TripleTriadElement[] elements, TripleTriadRules rules, int cellCount)
    {
        if (elements is null || elements.Length != cellCount || elements.Any(element => !Enum.IsDefined(element)) || !rules.HasFlag(TripleTriadRules.Elemental) && elements.Any(element => element != TripleTriadElement.None))
        {
            throw new InvalidDataException("Board elements require one defined entry per cell and the Elemental rule when nonempty.");
        }
    }

    private static void ValidateSnapshot(TripleTriadSnapshot snapshot, string hash)
    {
        int handSize = snapshot.InitialHands is { Length: 2 } && snapshot.InitialHands[0] is { } firstHand ? firstHand.Length : 0;
        int cells = snapshot.Width * snapshot.Height;
        int instanceCount = handSize * 2;
        if (snapshot.BlockedCells is null || snapshot.BlockedCells.Length > cells / 3 || snapshot.BlockedCells.Distinct().Count() != snapshot.BlockedCells.Length || snapshot.BlockedCells.Any(cell => (uint)cell >= cells))
        {
            throw new InvalidDataException("Blocked cells must be unique board indices and cover at most one third of the board.");
        }
        int playableCells = cells - snapshot.BlockedCells.Length;
        if (snapshot.Width is < 2 or > 5 || snapshot.Height is < 2 or > 5 || handSize < (playableCells + 1) / 2 || snapshot.Version != 1 || snapshot.CatalogHash != hash || (snapshot.Rules & ~SupportedRules) != 0 || (uint)snapshot.StartingPlayer > 1 || (uint)snapshot.CurrentPlayer > 1 || snapshot.Round < 0 || snapshot.Round > 0 && !snapshot.Rules.HasFlag(TripleTriadRules.SuddenDeath) || snapshot.InitialHands is not { Length: 2 } || snapshot.Hands is not { Length: 2 } || snapshot.InitialHands.Any(hand => hand is null || hand.Length != handSize || hand.Any(id => (uint)id >= 110)) || snapshot.Hands.Any(hand => hand is null || hand.Length != handSize) || snapshot.Board is null || snapshot.Board.Length != cells || snapshot.BlockedCells.Any(cell => snapshot.Board[cell] is not null) || snapshot.InstanceOrder is null || snapshot.InstanceOrder.Length != instanceCount || snapshot.InstanceOrder.Distinct().Count() != instanceCount || snapshot.InstanceOrder.Any(source => (uint)source >= instanceCount))
        {
            throw new InvalidDataException("Malformed or incompatible Triple Triad snapshot.");
        }
        ValidateElements(snapshot.BoardElements, snapshot.Rules, cells);
        int[] instanceSlots = new int[2];
        foreach (int index in Enumerable.Range(handSize, handSize).Concat(Enumerable.Range(0, handSize)))
        {
            int source = snapshot.InstanceOrder[index];
            if (source % handSize != instanceSlots[source / handSize]++ || snapshot.Round == 0 && source != index)
            {
                throw new InvalidDataException("Original-instance identities do not follow their original or redistributed hand order.");
            }
        }
        HashSet<int> sources = [];
        int[] placedByPlayer = new int[2];
        foreach (TripleTriadPlacedCard? card in snapshot.Board)
        {
            if (card is null)
            {
                continue;
            }
            if ((uint)card.Owner > 1 || (uint)card.SourcePlayer > 1 || (uint)card.SourceHandIndex >= handSize || (uint)card.CardId >= 110 || snapshot.InitialHands[card.SourcePlayer][card.SourceHandIndex] != card.CardId || snapshot.Hands[card.SourcePlayer][card.SourceHandIndex] is not null || !sources.Add(card.SourcePlayer * handSize + card.SourceHandIndex))
            {
                throw new InvalidDataException("A board card has an invalid owner, source slot or duplicate identity.");
            }
            placedByPlayer[card.SourcePlayer]++;
        }
        for (int player = 0; player < 2; player++)
        {
            for (int slot = 0; slot < handSize; slot++)
            {
                if (!sources.Contains(player * handSize + slot) && snapshot.Hands[player][slot] != snapshot.InitialHands[player][slot])
                {
                    throw new InvalidDataException("Every card must occur exactly once in its hand or on the board.");
                }
            }
        }
        int placed = sources.Count;
        if (placedByPlayer[snapshot.StartingPlayer] != (placed + 1) / 2 || placedByPlayer[1 - snapshot.StartingPlayer] != placed / 2 || snapshot.CurrentPlayer != (placed % 2 == 0 ? snapshot.StartingPlayer : 1 - snapshot.StartingPlayer))
        {
            throw new InvalidDataException("Snapshot turns do not agree with the cards placed by each player.");
        }
    }
}
