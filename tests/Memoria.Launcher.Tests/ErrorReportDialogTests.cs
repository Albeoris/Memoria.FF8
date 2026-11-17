using System.Diagnostics;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.LogicalTree;
using Avalonia.Themes.Fluent;
using Memoria.FF8.Platform;

namespace Memoria.Launcher.Tests;

[CollectionDefinition("Incident dialog UI", DisableParallelization = true)]
public sealed class IncidentDialogCollection : ICollectionFixture<LauncherHeadlessFixture>;

[Collection("Incident dialog UI")]
public sealed class ErrorReportDialogTests(LauncherHeadlessFixture fixture)
{
    [Fact]
    public async Task BrowserUpdatesDetailsAndBulkSelectionAndHandlesEmptyStorage()
    {
        await fixture.Session.Dispatch(() =>
        {
            Application.Current!.Styles.Add(new FluentTheme());
            ErrorIncident first = ErrorIncident.Capture(new InvalidOperationException("First incident detail"), "First subsystem");
            ErrorIncident second = ErrorIncident.Capture(new IOException("Second incident detail"), "Second subsystem");
            ErrorReportDialog dialog = new("Saved incidents", [first, second], second.Id);
            try
            {
                ListBox list = Assert.Single(dialog.GetLogicalDescendants().OfType<ListBox>());
                ListBoxItem[] rows = list.Items.Cast<ListBoxItem>().ToArray();
                Assert.Equal(2, rows.Length);
                Assert.Same(rows[1], list.SelectedItem);
                Assert.Contains(rows[0].GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == first.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                Assert.Contains(rows[1].GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == second.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));
                AssertDetails(dialog, "Second incident detail", "First incident detail");
                list.SelectedIndex = 0;
                AssertDetails(dialog, "First incident detail", "Second incident detail");
                CheckBox all = Assert.Single(dialog.GetLogicalDescendants().OfType<CheckBox>(), check => Equals(check.Content, "Select all"));
                CheckBox[] checks = rows.Select(row => Assert.Single(row.GetLogicalDescendants().OfType<CheckBox>())).ToArray();
                Button deleteSelected = Assert.Single(dialog.GetLogicalDescendants().OfType<Button>(), button => button.Content?.ToString()?.StartsWith("Delete selected", StringComparison.Ordinal) == true);
                Assert.False(deleteSelected.IsEnabled);
                checks[0].IsChecked = true;
                Assert.Null(all.IsChecked);
                Assert.True(deleteSelected.IsEnabled);
                Assert.Equal("Delete selected (1)", deleteSelected.Content);
                Toggle(all);
                Assert.All(checks, check => Assert.True(check.IsChecked));
                Assert.Equal("Delete selected (2)", deleteSelected.Content);
                Toggle(all);
                Assert.All(checks, check => Assert.False(check.IsChecked));
                Assert.False(deleteSelected.IsEnabled);
                Assert.Equal("Delete selected", deleteSelected.Content);
            }
            finally
            {
                dialog.Close();
            }
            ErrorReportDialog empty = new("Saved incidents", []);
            try
            {
                Assert.Empty(Assert.Single(empty.GetLogicalDescendants().OfType<ListBox>()).Items);
                Assert.Contains(empty.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "No saved incidents.");
                Assert.All(empty.GetLogicalDescendants().OfType<CheckBox>(), check => Assert.False(check.IsEnabled));
                Assert.All(empty.GetLogicalDescendants().OfType<Button>().Where(button => button.Content?.ToString() is "Report error" or "Delete incident" or "Delete selected"), button => Assert.False(button.IsEnabled));
            }
            finally
            {
                empty.Close();
            }
            foreach (int selectedIndex in new[] { 0, 1, 2 })
            {
                VerifyDeletion(selectedIndex, false);
            }
            VerifyDeletion(0, true);
        }, TestContext.Current.CancellationToken);
    }

    private static void Toggle(CheckBox check)
    {
        typeof(ToggleButton).GetMethod("Toggle", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)!.Invoke(check, null);
    }

    private static void VerifyDeletion(int selectedIndex, bool deleteAll)
    {
        string root = Path.Combine(Path.GetTempPath(), "memoria-dialog-tests-" + Guid.NewGuid().ToString("N"));
        ErrorIncident[] incidents = Enumerable.Range(0, 3).Select(index => ErrorIncident.Capture(new Exception($"Incident detail {index}"), $"Subsystem {index}")).ToArray();
        foreach (ErrorIncident incident in incidents)
        {
            incident.Persist(root);
        }
        ErrorReportDialog dialog = new("Saved incidents", incidents, incidents[selectedIndex].Id, root);
        try
        {
            dialog.Show();
            PumpUntil(() => ((Task)typeof(ErrorReportDialog).GetField("presentation", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(dialog)!).IsCompleted);
            ListBox list = Assert.Single(dialog.GetLogicalDescendants().OfType<ListBox>());
            if (deleteAll)
            {
                CheckBox all = Assert.Single(dialog.GetLogicalDescendants().OfType<CheckBox>(), check => Equals(check.Content, "Select all"));
                Toggle(all);
            }
            string? screenshot = Environment.GetEnvironmentVariable("MEMORIA_LAUNCHER_TEST_SCREENSHOT");
            if (screenshot is not null && selectedIndex == 0 && !deleteAll)
            {
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using Avalonia.Media.Imaging.WriteableBitmap? bitmap = dialog.CaptureRenderedFrame();
                Assert.NotNull(bitmap);
                bitmap.Save(screenshot);
            }
            Task deletion = (Task)typeof(ErrorReportDialog).GetMethod("DeleteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(dialog, [deleteAll])!;
            Window confirmation = Assert.Single(dialog.OwnedWindows);
            Button accept = Assert.Single(confirmation.GetLogicalDescendants().OfType<Button>(), button => Equals(button.Content, "Continue"));
            accept.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            PumpUntil(() => deletion.IsCompleted);
            deletion.GetAwaiter().GetResult();
            if (deleteAll)
            {
                Assert.Empty(list.Items);
                Assert.Empty(ErrorIncident.DiscoverAll(root));
                Assert.Contains(dialog.GetLogicalDescendants().OfType<TextBlock>(), text => text.Text == "No saved incidents.");
            }
            else
            {
                Assert.Equal(2, list.Items.Count);
                Assert.Equal(incidents.Where((_, index) => index != selectedIndex).Select(incident => incident.Id).Order(), ErrorIncident.DiscoverAll(root).Select(incident => incident.Id).Order());
                ErrorIncident expected = incidents[selectedIndex == 2 ? 1 : selectedIndex + 1];
                Assert.Equal(expected.Id, Assert.IsType<ErrorIncident>(Assert.IsType<ListBoxItem>(list.SelectedItem).Tag).Id);
                AssertDetails(dialog, expected.Exception.Message, incidents[selectedIndex].Exception.Message);
            }
        }
        finally
        {
            dialog.Close();
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static void PumpUntil(Func<bool> completed)
    {
        Stopwatch timer = Stopwatch.StartNew();
        while (!completed() && timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(5);
        }
        Assert.True(completed(), "The dialog operation did not complete.");
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertDetails(ErrorReportDialog dialog, string expected, string excluded)
    {
        TextBox details = Assert.Single(dialog.GetLogicalDescendants().OfType<TextBox>(), text => text.AcceptsReturn && text.MinHeight == 120);
        Assert.Contains(expected, details.Text);
        Assert.DoesNotContain(excluded, details.Text);
    }
}
