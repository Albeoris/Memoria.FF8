using Bitmap = SkiaSharp.SKBitmap;
using Color = SkiaSharp.SKColor;

using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TripleTriadPresentationTests
{
    [Fact]
    public void CaptureRetainsOldOwnerUntilEdgeAndBlocksNextTurnWithoutChangingSave()
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 1, 1, 1, 1, TripleTriadElement.None)).ToArray();
        cards[1] = new(1, 9, 9, 9, 9, TripleTriadElement.None);
        TripleTriadMatch match = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9]);
        match.Play(0, 8);
        match.Play(0, 1);
        TripleTriadController controller = new(cards, Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1), [5, 6, 7, 8, 9], id => id.ToString());
        controller.RestoreMatch(match);
        controller.EnablePresentation();
        List<TripleTriadSound> sounds = [];
        controller.SoundRequested += sounds.Add;
        controller.Play(1, 4);
        Assert.True(controller.IsAnimating);
        Assert.False(controller.AdvanceOpponent());
        Assert.Equal(0, controller.Match!.Board[1]!.Owner);
        Assert.Equal(1, controller.PresentedCard(1, 0).Owner);
        Assert.Equal(new TripleTriadCapture(1, 4, 0), Assert.Single(controller.PresentedMove!.Captures));
        TripleTriadSnapshot saved = controller.Match.CaptureSnapshot();
        controller.AdvancePresentation(TimeSpan.FromSeconds(TripleTriadController.PlacementDuration + TripleTriadController.FlipDuration * 0.49));
        var edge = controller.PresentedCard(1, 0);
        Assert.Equal(1, edge.Owner);
        Assert.Equal(1, edge.Width);
        Assert.True(edge.Height < 0.04);
        controller.AdvancePresentation(TimeSpan.FromSeconds(TripleTriadController.FlipDuration * 0.02));
        Assert.Equal(0, controller.PresentedCard(1, 0).Owner);
        Assert.Equal(1, sounds.Count(sound => sound == TripleTriadSound.Flip));
        controller.AdvancePresentation(TimeSpan.FromSeconds(2));
        Assert.False(controller.IsAnimating);
        Assert.Equal(saved.Board, controller.Match.CaptureSnapshot().Board);
        Assert.True(controller.AdvanceOpponent());
        controller.RestoreMatch(TripleTriadMatch.Restore(cards, saved));
        Assert.False(controller.IsAnimating);
        Assert.Equal(saved.Board, controller.Match.Board);
    }

    [Fact]
    public void SameFinishesItsSimultaneousWaveBeforeComboStarts()
    {
        TripleTriadCard[] cards = Enumerable.Range(0, 110).Select(id => new TripleTriadCard(id, 5, 5, 5, 5, TripleTriadElement.None)).ToArray();
        cards[5] = new(5, 5, 9, 5, 5, TripleTriadElement.None);
        TripleTriadSnapshot snapshot = TripleTriadMatch.Create(cards, [0, 1, 2, 3, 4], [5, 6, 7, 8, 9], TripleTriadRules.Same).CaptureSnapshot();
        int[] positions = [3, 1, 8, 2];
        for (int turn = 0; turn < positions.Length; turn++)
        {
            int player = turn % 2;
            int slot = turn / 2;
            snapshot.Hands[player][slot] = null;
            snapshot.Board[positions[turn]] = new TripleTriadPlacedCard(player * 5 + slot, player, player, slot);
        }
        TripleTriadController controller = new(cards, Enumerable.Range(0, 5).ToDictionary(id => id, _ => 1), [5, 6, 7, 8, 9], id => id.ToString());
        controller.RestoreMatch(TripleTriadMatch.Restore(cards, snapshot));
        controller.EnablePresentation();
        controller.Play(2, 4);
        Assert.True(controller.PresentedMove!.Combo);
        controller.AdvancePresentation(TimeSpan.FromSeconds(TripleTriadController.PlacementDuration + 0.01));
        Assert.Equal("SAME", controller.CaptureAnnouncement);
        Assert.Equal(1, controller.PresentedCard(2, 0).Owner);
        controller.AdvancePresentation(TimeSpan.FromSeconds(TripleTriadController.FlipDuration + TripleTriadController.FlipSettleDuration));
        Assert.Equal("COMBO", controller.CaptureAnnouncement);
        Assert.Equal(0, controller.PresentedCard(1, 0).Owner);
        Assert.Equal(1, controller.PresentedCard(2, 0).Owner);
        Assert.True(controller.IsAnimating);
        controller.AdvancePresentation(TimeSpan.FromSeconds(2));
        Assert.False(controller.IsAnimating);
        controller.SelectHand(3);
        Assert.False(controller.ChoosingCell);
    }

    [Fact]
    public void NavigationRepeatKeepsQuickTapsAndRepeatsHeldDirectionWithoutCatchupBurst()
    {
        UiNavigationRepeat repeat = new();
        Assert.Equal(1, repeat.Update(1, 1, TimeSpan.Zero));
        Assert.Equal(0, repeat.Update(1, 0, TimeSpan.FromMilliseconds(200)));
        Assert.Equal(1, repeat.Update(1, 0, TimeSpan.FromMilliseconds(51)));
        Assert.Equal(0, repeat.Update(1, 0, TimeSpan.FromMilliseconds(40)));
        Assert.Equal(1, repeat.Update(1, 0, TimeSpan.FromMilliseconds(26)));
        Assert.Equal(0, repeat.Update(0, 0, TimeSpan.Zero));
        Assert.Equal(1, repeat.Update(1, 1, TimeSpan.Zero));
        Assert.Equal(-1, repeat.Update(-1, -1, TimeSpan.Zero));
        Assert.Equal(-1, repeat.Update(-1, 0, TimeSpan.FromSeconds(3)));
        Assert.Equal(0, repeat.Update(-1, 0, TimeSpan.Zero));
    }
    [Fact]
    public void OriginalArtworkRendersSelectionPlacementAndBothSidesOfCapture()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Original resources required.");
        TripleTriadCatalog catalog = TripleTriadCatalog.Load(directory);
        TripleTriadController controller = new(catalog.Cards, Enumerable.Range(0, 110).ToDictionary(id => id, _ => 1), [0, 1, 2, 3, 4], catalog.Name);
        string output = Environment.GetEnvironmentVariable("MEMORIA_FF8_TRIAD_UI_OUTPUT_DIRECTORY") ?? Path.Combine(Path.GetTempPath(), "ff8-triad-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(output);
        Render("selection");
        TripleTriadMatch match = TripleTriadMatch.Create(catalog.Cards, [109, 84, 82, 83, 90], [0, 1, 2, 3, 4], TripleTriadRules.Open);
        match.Play(1, 8);
        match.Play(0, 1);
        controller.RestoreMatch(match);
        Render("board");
        controller.EnablePresentation();
        controller.Play(0, 4);
        Assert.Contains(1, controller.PresentedMove!.CapturedCells);
        controller.AdvancePresentation(TimeSpan.FromSeconds(TripleTriadController.PlacementDuration / 2));
        Render("placement");
        controller.AdvancePresentation(TimeSpan.FromSeconds(TripleTriadController.PlacementDuration / 2 + TripleTriadController.FlipDuration * 0.35));
        Render("flip-before");
        controller.AdvancePresentation(TimeSpan.FromSeconds(TripleTriadController.FlipDuration * 0.30));
        Render("flip-after");
        controller.AdvancePresentation(TimeSpan.FromSeconds(2));
        Render("captured");
        Console.WriteLine($"Triple Triad UI original-resource renders: {output}");

        void Render(string name)
        {
            using Bitmap bitmap = new(960, 672, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Unpremul);
            using BattlePreviewCanvas canvas = new(bitmap, directory);
            UiView view = controller.BuildView();
            view.Resize(960, 672);
            view.Draw(canvas, new UiTheme());
            bitmap.SavePng(Path.Combine(output, name + ".png"));
        }
    }

}
