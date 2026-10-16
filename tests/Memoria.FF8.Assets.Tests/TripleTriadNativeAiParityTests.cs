using Memoria.FF8.Runtime;

namespace Memoria.FF8.Assets.Tests;

public sealed class TripleTriadNativeAiParityTests
{
    [Theory]
    [InlineData(0, 0, 0, 12345u, 0, 6, 1)]
    [InlineData(1, 3, 1, 12346u, 0, 22, 2)]
    [InlineData(2, 0, 4, 12347u, 0, 17, 2)]
    [InlineData(3, 1, 4, 578465516u, 16, 16, 3)]
    [InlineData(4, 1, 0, 12349u, 0, 708, 3)]
    [InlineData(5, 1, 3, 12350u, 0, 725, 3)]
    [InlineData(6, 0, 6, 12351u, 0, 2811, 4)]
    [InlineData(7, 1, 4, 12352u, 0, 2924, 4)]
    [InlineData(8, 0, 1, 12353u, 0, 15, 1)]
    [InlineData(9, 1, 3, 12354u, 0, 15, 1)]
    [InlineData(10, 0, 4, 12355u, 0, 161, 2)]
    [InlineData(11, 1, 2, 945944693u, 513, 513, 3)]
    [InlineData(12, 1, 0, 1319423635u, 10, 1335, 3)]
    [InlineData(13, 1, 0, 2623320028u, 10, 1153, 3)]
    [InlineData(14, 2, 8, 3927216421u, 10, 7782, 4)]
    [InlineData(15, 0, 6, 936145518u, 10, 8881, 4)]
    [InlineData(16, 0, 1, 2240041911u, 10, 15, 1)]
    [InlineData(17, 3, 1, 3543938304u, 10, 15, 1)]
    [InlineData(18, 0, 4, 552867401u, 10, 257, 2)]
    [InlineData(19, 1, 3, 3389236225u, 637, 627, 3)]
    [InlineData(20, 0, 2, 12365u, 0, 687, 3)]
    [InlineData(21, 1, 2, 12366u, 0, 506, 3)]
    [InlineData(22, 1, 2, 12367u, 0, 2349, 4)]
    [InlineData(23, 2, 1, 12368u, 0, 2587, 4)]
    [InlineData(24, 0, 1, 12369u, 0, 15, 1)]
    [InlineData(25, 2, 6, 12370u, 0, 15, 1)]
    [InlineData(26, 0, 3, 12371u, 0, 116, 2)]
    [InlineData(27, 1, 7, 868068244u, 448, 448, 3)]
    [InlineData(28, 0, 1, 12373u, 0, 678, 3)]
    [InlineData(29, 1, 1, 12374u, 0, 682, 3)]
    [InlineData(30, 1, 5, 12375u, 0, 2337, 4)]
    [InlineData(31, 1, 1, 12376u, 0, 2217, 4)]
    [InlineData(32, 1, 6, 12377u, 0, 15, 1)]
    [InlineData(33, 2, 7, 12378u, 0, 15, 1)]
    [InlineData(34, 2, 5, 12379u, 0, 153, 2)]
    [InlineData(35, 4, 7, 1052150493u, 465, 465, 3)]
    [InlineData(36, 1, 2, 12381u, 0, 589, 3)]
    [InlineData(37, 1, 6, 12382u, 0, 811, 3)]
    [InlineData(38, 1, 1, 12383u, 0, 2652, 4)]
    [InlineData(39, 1, 7, 12384u, 0, 2799, 4)]
    public void SearchMatchesOriginalX86Instructions(int index, int handIndex, int cellIndex, uint expectedRandomState, int randomDraws, int leaves, int depth)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(File.Exists(Path.Combine(directory, "FF8_EN.exe")) && File.Exists(Path.Combine(directory, "Data", "lang-en", "menu.fs")), "Original English card resources are required.");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        Random random = new(987 + index);
        int[] first = Enumerable.Range(0, 5).Select(_ => random.Next(110)).ToArray();
        int[] second = Enumerable.Range(0, 5).Select(_ => random.Next(110)).ToArray();
        TripleTriadRules rules = index < 12 ? TripleTriadRules.Open : index < 20 ? TripleTriadRules.None : TripleTriadRules.Open | TripleTriadRules.Same | TripleTriadRules.Plus | TripleTriadRules.SameWall | TripleTriadRules.Elemental;
        TripleTriadElement[] elements = Enumerable.Range(0, 9).Select(_ => (TripleTriadElement)random.Next(9)).ToArray();
        TripleTriadMatch match = TripleTriadMatch.Create(catalog.Cards, first, second, rules, index % 2, index < 20 ? null : elements);
        int placed = index < 4 ? 6 : 4;
        for (int moveIndex = 0; moveIndex < placed; moveIndex++)
        {
            IReadOnlyList<TripleTriadMove> legal = match.GetLegalMoves();
            TripleTriadMove move = legal[random.Next(legal.Count)];
            match.Play(move.HandIndex, move.CellIndex);
        }
        TripleTriadNativeAiResult result = TripleTriadNativeAi.ChooseMove(catalog.Cards, catalog.TradePriorities, match, catalog.AiParameters, (byte)(index % 8), (byte)(index % 8), (uint)(12345 + index));
        Assert.Equal(new TripleTriadMove(handIndex, cellIndex), result.Move);
        Assert.Equal(expectedRandomState, result.RandomState);
        Assert.Equal(randomDraws, result.RandomDraws);
        Assert.Equal(leaves, result.EvaluatedMoves);
        Assert.Equal(depth, result.Depth);
    }
}
