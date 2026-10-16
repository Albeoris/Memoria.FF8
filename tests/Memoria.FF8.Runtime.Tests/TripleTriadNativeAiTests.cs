using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class TripleTriadNativeAiTests
{
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
