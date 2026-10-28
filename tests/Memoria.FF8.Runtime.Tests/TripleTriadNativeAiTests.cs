using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadNativeAiTests
{
    [Fact]
    public void NativeSearchCountsOnlyPlayableEmptyCellsAndNeverChoosesABlock()
    {
        TripleTriadCard[] cards = Cards();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1], [2, 3], TripleTriadRules.Open, width: 2, height: 3, blockedCells: [0, 5]);
        TripleTriadNativeAiParameters parameters = Parameters() with { Depths = Parameters().Depths.SetItem(4, 2).SetItem(6, 3) };
        TripleTriadNativeAiResult result = TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, parameters, 0, 0, 123);
        Assert.Equal(2, result.Depth);
        Assert.False(match.IsBlocked(result.Move.CellIndex));
        Assert.All(match.Board, card => Assert.Null(card));
        Assert.Equal(new[] { 0, 5 }, match.BlockedCells);
    }

    [Fact]
    public void SearchUsesModifiedSpecialRanksWithoutMutatingItsInput()
    {
        TripleTriadCard[] cards = Cards();
        cards[0] = new TripleTriadCard(0, 8, 7, 8, 8, TripleTriadElement.Fire);
        cards[1] = new TripleTriadCard(1, 5, 5, 5, 5, TripleTriadElement.Fire);
        cards[5] = new TripleTriadCard(5, 9, 9, 5, 9, TripleTriadElement.Fire);
        TripleTriadElement[] elements = new TripleTriadElement[9];
        elements[3] = TripleTriadElement.Ice;
        elements[1] = elements[4] = TripleTriadElement.Fire;
        TripleTriadSnapshot position = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], TripleTriadRules.Plus | TripleTriadRules.Elemental | TripleTriadRules.Open, boardElements: elements).CaptureSnapshot();
        position.Hands[0][0] = position.Hands[1][0] = null;
        position.Board[3] = new TripleTriadPlacedCard(0, 1, 0, 0);
        position.Board[1] = new TripleTriadPlacedCard(5, 1, 1, 0);
        TripleTriadMatch original = TripleTriadMatch.Restore(cards, position);
        TripleTriadMatch modified = TripleTriadMatch.Restore(cards, position with { UseModifiedSamePlus = true });
        string before = JsonSerializer.Serialize(modified.CaptureSnapshot());
        TripleTriadNativeAiResult originalResult = TripleTriadNativeAi.ChooseMove(cards, new byte[110], original, Parameters(), 0, 0, 123);
        TripleTriadNativeAiResult modifiedResult = TripleTriadNativeAi.ChooseMove(cards, new byte[110], modified, Parameters(), 0, 0, 123);
        Assert.Equal(new TripleTriadMove(1, 0), originalResult.Move);
        Assert.Equal(new TripleTriadMove(1, 4), modifiedResult.Move);
        Assert.True(modified.PreviewMove(modifiedResult.Move.HandIndex, modifiedResult.Move.CellIndex).Plus);
        Assert.Equal(before, JsonSerializer.Serialize(modified.CaptureSnapshot()));
        Assert.Equal(0, modifiedResult.RandomDraws);
    }

    [Theory]
    [InlineData(2, 2, 3)]
    [InlineData(5, 2, 5)]
    [InlineData(5, 5, 13)]
    public void OptionalBoardsUseBoundedNativeSearchAndGuessEveryHandSlot(int width, int height, int handSize)
    {
        TripleTriadCard[] cards = Cards();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, Enumerable.Range(0, handSize).ToArray(), Enumerable.Range(handSize, handSize).ToArray(), width: width, height: height);
        TripleTriadNativeAiParameters parameters = Parameters() with { Depths = Enumerable.Range(0, 73).Select(index => (byte)Math.Clamp(index % 9, 1, 4)).ToImmutableArray() };
        // Native profiles encode nine empty cells at each row's next boundary.
        parameters = parameters with { Depths = parameters.Depths.SetItem(Math.Min(width * height, 9), 4) };
        TripleTriadNativeAiResult result = TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, parameters, 0, 0, 123);
        Assert.Contains(result.Move, match.GetLegalMoves());
        Assert.InRange(result.Depth, 1, 4);
        Assert.InRange(result.EvaluatedMoves, 1, 20000);
        Assert.Equal(handSize * 2, result.RandomDraws);
        Assert.All(match.Board, card => Assert.Null(card));
    }

    [Fact]
    public void EqualScoresKeepFirstHandAndRowMajorCellWithoutMutatingTheMatch()
    {
        TripleTriadCard[] cards = Cards();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], TripleTriadRules.Open);
        string before = JsonSerializer.Serialize(match.CaptureSnapshot());
        TripleTriadNativeAiResult result = TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, Parameters(), 0, 0, 123);
        Assert.Equal(new TripleTriadMove(0, 0), result.Move);
        Assert.Equal(45, result.EvaluatedMoves);
        Assert.Equal(0, result.RandomDraws);
        Assert.Equal(123u, result.RandomState);
        Assert.Equal(before, JsonSerializer.Serialize(match.CaptureSnapshot()));
    }

    [Fact]
    public void HiddenHandConsumesTenGuessesUnlessOpenOrKnowledgeBitIsSet()
    {
        TripleTriadCard[] cards = Cards();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]);
        TripleTriadNativeAiResult guessed = TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, Parameters(), 0, 0, 123);
        TripleTriadRandom expected = new(123);
        for (int index = 0; index < 10; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(10, guessed.RandomDraws);
        Assert.Equal(expected.State, guessed.RandomState);
        Assert.Equal(0, TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, Parameters(), 0x10, 0, 123).RandomDraws);
    }

    [Fact]
    public void NoisyProfileConsumesOneNativeRandomDrawPerEvaluatedLeaf()
    {
        TripleTriadCard[] cards = Cards();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], TripleTriadRules.Open);
        TripleTriadNativeAiParameters settings = Parameters();
        settings = settings with { Profiles = settings.Profiles.SetItem(3, new TripleTriadNativeAiWeights(4096, 0, 4096, 4096, 4096)) };
        TripleTriadNativeAiResult result = TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, settings, 0, 3, 123);
        Assert.Equal(45, result.RandomDraws);
        Assert.Equal(result.RandomDraws, result.EvaluatedMoves);
        TripleTriadRandom expected = new(123);
        for (int index = 0; index < 45; index++)
        {
            expected = expected.Draw(out _);
        }
        Assert.Equal(expected.State, result.RandomState);
    }

    [Fact]
    public void UnsupportedDepthFailsBeforeRngOrMatchMutation()
    {
        TripleTriadNativeAiParameters settings = Parameters() with { Depths = Enumerable.Repeat((byte)5, 73).ToImmutableArray() };
        Assert.False(TripleTriadNativeAi.SupportsParameters(settings, 0, 0, 9));
        TripleTriadCard[] cards = Cards();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]);
        Assert.Throws<NotSupportedException>(() => TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, settings, 0, 0, 123));
        Assert.DoesNotContain(match.Board, card => card is not null);
    }

    [Fact]
    public void HiddenSuddenDeathSearchRequiresTheOriginalPreMatchHand()
    {
        TripleTriadCard[] cards = Cards();
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], TripleTriadRules.SuddenDeath);
        while (match.Outcome == TripleTriadOutcome.InProgress)
        {
            TripleTriadMove move = match.GetLegalMoves()[0];
            match.Play(move.HandIndex, move.CellIndex);
        }
        match.RestartSuddenDeath(1);
        Assert.Throws<NotSupportedException>(() => TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, Parameters(), 0, 0, 123));
        TripleTriadNativeAiResult result = TripleTriadNativeAi.ChooseMove(cards, new byte[110], match, Parameters(), 0, 0, 123, [5, 6, 7, 8, 9]);
        Assert.Equal(10, result.RandomDraws);
    }

    private static TripleTriadCard[] Cards()
    {
        return Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
    }

    private static TripleTriadNativeAiParameters Parameters()
    {
        return new TripleTriadNativeAiParameters(Enumerable.Repeat((byte)1, 73).ToImmutableArray(), Enumerable.Repeat(new TripleTriadNativeAiWeights(4096, 0, 0, 4096, 4096), 8).ToImmutableArray());
    }
}
