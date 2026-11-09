using Memoria.FF8.Platform;
using Memoria.FF8.Ui;

namespace Memoria.FF8.Tests;

public sealed class ErrorReportPresenterTests
{
    [Theory]
    [InlineData("FFVIII field")]
    [InlineData("FFVII field")]
    public void SharedReportViewRetainsActionsAndCopiesCompleteNativeFault(string subsystem)
    {
        string? copied = null;
        using ErrorReportPresenter presenter = new(value => copied = value);
        presenter.Reset("Native fault: entity5 priority1 PC0447 opcodeF1");
        presenter.Incident = ErrorIncident.Capture(new InvalidDataException("Original native failure"), subsystem);
        UiView view = presenter.View("Game stopped");
        UiButton[] buttons = Descendants(view.Root).OfType<UiButton>().ToArray();
        Assert.Equal(new[] { "Report error", "Details", "Copy full description" }, buttons.Select(button => button.Text));
        Assert.Equal(960, view.DesignWidth);
        Assert.Equal(672, view.DesignHeight);
        buttons[2].Click!();
        Assert.Equal("Native fault: entity5 priority1 PC0447 opcodeF1", copied);
        buttons[1].Click!();
        Assert.Contains(Descendants(presenter.View("Game stopped").Root), element => element is UiScrollPanel);
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text.Contains("will be public", StringComparison.Ordinal));
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement nested in Descendants(child))
            {
                yield return nested;
            }
        }
    }
}
