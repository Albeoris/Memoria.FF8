using Memoria.FF8.Platform;
using Memoria.FF8.Ui;

namespace Memoria.FF8.Tests;

public sealed class ErrorReportPresenterTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Fatal_exit_writes_an_archive_without_a_report_button_and_preserves_capture_failure(bool collectionFails)
    {
        string directory = Path.Combine(Path.GetTempPath(), "memoria-fatal-report-" + Guid.NewGuid().ToString("N"));
        try
        {
            ErrorIncident original = ErrorIncident.Capture(new InvalidOperationException("Original failure"), "game host");
            ErrorIncident complete = original.WithSaveUnavailableReason("No supported save boundary.");
            ErrorIncident? prepared = null;
            using ErrorReportPresenter presenter = new(_ => throw new InvalidOperationException("Clipboard must not be used during shutdown."), incident =>
            {
                prepared = incident;
                return ErrorReport.PrepareAsync(incident, directory);
            });
            presenter.Incident = original;
            TaskCompletionSource<ErrorIncident> evidence = new(TaskCreationOptions.RunContinuationsAsynchronously);
            presenter.Evidence = evidence.Task;
            Task<string?> pending = presenter.SaveForExitAsync();
            Assert.False(pending.IsCompleted);
            if (collectionFails)
            {
                evidence.SetException(new IOException("Collection interrupted."));
            }
            else
            {
                evidence.SetResult(complete);
            }
            string? archive = await pending;
            Assert.NotNull(archive);
            Assert.True(File.Exists(archive));
            Assert.NotNull(prepared);
            Assert.Equal(original.Id, prepared.Id);
            Assert.Equal("Original failure", prepared.Exception.Message);
            Assert.Equal(collectionFails ? "Save collection failed during shutdown: Collection interrupted." : complete.SaveUnavailableReason, prepared.SaveUnavailableReason);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
            if (Directory.Exists(directory + ".pending"))
            {
                Directory.Delete(directory + ".pending", recursive: true);
            }
        }
    }

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
