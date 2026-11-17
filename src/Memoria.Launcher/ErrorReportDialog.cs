using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Memoria.FF8.Platform;
using Memoria.FF8.Runtime;

namespace Memoria.Launcher;

internal sealed class ErrorReportDialog : Window
{
    private sealed class SelectAllCheckBox : CheckBox
    {
        protected override Type StyleKeyOverride => typeof(CheckBox);

        protected override void Toggle()
        {
            IsChecked = IsChecked != true;
        }
    }

    private ErrorIncident? incident;
    private readonly string? reportsDirectory;
    private readonly List<ErrorIncident> incidents;
    private readonly ListBox incidentList = new();
    private readonly SelectAllCheckBox selectAll = new() { Content = "Select all" };
    private readonly Dictionary<string, CheckBox> selections = [];
    private readonly Button deleteSelected = new() { Content = "Delete selected", IsEnabled = false };
    private readonly StackPanel incidentContent = new() { Spacing = 14 };
    private readonly TextBox details = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 120, MaxHeight = 320 };
    private readonly TextBlock count = new();
    private readonly Dictionary<string, PreparedErrorReport> reports = [];
    private bool updatingSelection;
    private readonly TextBlock progress = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBox location = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox url = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap };
    private readonly TextBox summary = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly Button prepare = new() { Content = "Report error" };
    private readonly Button delete = new() { Content = "Delete incident" };
    private readonly StackPanel recovery = new() { Spacing = 8 };
    private readonly Expander recoverySection = new() { Header = "Prepared report and recovery actions", IsExpanded = true, IsVisible = false };
    private Task presentation = Task.CompletedTask;
    private PreparedErrorReport? report;
    private bool preparing;
    private DateTimeOffset lastOpen;

    public ErrorReportDialog(string message, IReadOnlyList<ErrorIncident> incidents, string? initialIncidentId = null, string? reportsDirectory = null)
    {
        this.incidents = incidents.ToList();
        this.reportsDirectory = reportsDirectory;
        Title = "Memoria - Error reports";
        Width = 960;
        Height = 700;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        LauncherWindow.StyleIncidentDialog(this);

        Grid shell = new() { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(24) };
        StackPanel heading = new() { Spacing = 8, Margin = new Thickness(0, 0, 0, 20) };
        heading.Children.Add(new TextBlock { Text = "Error reports", FontSize = 24, FontWeight = FontWeight.SemiBold });
        heading.Children.Add(new TextBlock { Text = ReportPrivacy.Text(message), TextWrapping = TextWrapping.Wrap });
        shell.Children.Add(heading);

        Grid columns = new() { ColumnDefinitions = new ColumnDefinitions("270,18,*") };
        Grid.SetRow(columns, 1);
        shell.Children.Add(columns);
        Grid browser = new() { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto"), Margin = new Thickness(14) };
        browser.Children.Add(count);
        selectAll.Margin = new Thickness(0, 10, 0, 10);
        Grid.SetRow(selectAll, 1);
        browser.Children.Add(selectAll);
        incidentList.Background = Brushes.Transparent;
        incidentList.BorderThickness = new Thickness(0);
        Grid.SetRow(incidentList, 2);
        browser.Children.Add(incidentList);
        deleteSelected.Margin = new Thickness(0, 12, 0, 0);
        Grid.SetRow(deleteSelected, 3);
        browser.Children.Add(deleteSelected);
        columns.Children.Add(LauncherWindow.IncidentCard(browser));

        ScrollViewer.SetHorizontalScrollBarVisibility(summary, ScrollBarVisibility.Disabled);
        incidentContent.SizeChanged += (_, _) => summary.MaxWidth = Math.Max(0, incidentContent.Bounds.Width);
        incidentContent.Children.Add(summary);
        AddCopy(incidentContent, "Copy incident path", () => incident!.GetStoragePath(reportsDirectory));
        WrapPanel actions = new() { Orientation = Orientation.Horizontal };
        prepare.Margin = new Thickness(0, 0, 12, 8);
        delete.Margin = new Thickness(0, 0, 0, 8);
        actions.Children.Add(prepare);
        actions.Children.Add(delete);
        incidentContent.Children.Add(actions);
        recovery.Children.Add(new TextBlock { Text = ErrorReport.PublicInstructions, TextWrapping = TextWrapping.Wrap });
        recovery.Children.Add(location);
        AddAction(recovery, "Open GitHub again", OpenGitHub);
        AddAction(recovery, "Show ZIP", () => DisplayDesktopResult(ReportDesktop.RevealFile(report!.ArchivePath)));
        AddCopy(recovery, "Copy full description", () => report!.Description);
        AddCopy(recovery, "Copy report path", () => report!.ArchivePath);
        recovery.Children.Add(url);
        AddCopy(recovery, "Copy issue URL", () => report!.IssueUrl);
        recoverySection.Content = recovery;
        incidentContent.Children.Add(recoverySection);
        StackPanel detailPanel = new() { Spacing = 8 };
        detailPanel.Children.Add(details);
        AddCopy(detailPanel, "Copy error details", () => ReportPrivacy.Text(incident!.ExceptionText));
        incidentContent.Children.Add(new Expander { Header = "Details", IsExpanded = true, Content = detailPanel, HorizontalAlignment = HorizontalAlignment.Stretch });
        ScrollViewer detailScroll = new() { Content = incidentContent, Padding = new Thickness(18), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Border detailCard = LauncherWindow.IncidentCard(detailScroll);
        Grid.SetColumn(detailCard, 2);
        columns.Children.Add(detailCard);

        Grid footer = new() { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 16, 0, 0) };
        footer.Children.Add(progress);
        Button close = new() { Content = "Close", IsCancel = true, MinWidth = 90, Margin = new Thickness(16, 0, 0, 0) };
        close.Click += (_, _) => Close();
        Grid.SetColumn(close, 1);
        footer.Children.Add(close);
        Grid.SetRow(footer, 2);
        shell.Children.Add(footer);
        Content = shell;

        incidentList.SelectionChanged += (_, _) => SelectIncident();
        selectAll.IsCheckedChanged += (_, _) =>
        {
            if (updatingSelection)
            {
                return;
            }
            bool value = selectAll.IsChecked != false;
            updatingSelection = true;
            foreach (CheckBox selection in selections.Values)
            {
                selection.IsChecked = value;
            }
            updatingSelection = false;
            RefreshSelection();
        };
        prepare.Click += async (_, _) => await PrepareAsync();
        delete.Click += async (_, _) => await DeleteAsync(false);
        deleteSelected.Click += async (_, _) => await DeleteAsync(true);
        Closing += (_, args) => args.Cancel = preparing;
        Closed += (_, _) =>
        {
            foreach (PreparedErrorReport prepared in reports.Values)
            {
                prepared.Dispose();
            }
        };
        RefreshList(initialIncidentId, 0);
        Opened += (_, _) =>
        {
            FitToScreen();
            presentation = MarkPresentedAsync();
        };
    }

    private void FitToScreen()
    {
        if (Screens.ScreenFromWindow(this) is not { } screen)
        {
            return;
        }
        double availableWidth = Math.Max(320, screen.WorkingArea.Width / screen.Scaling - 32);
        double availableHeight = Math.Max(240, screen.WorkingArea.Height / screen.Scaling - 64);
        MinWidth = Math.Min(MinWidth, availableWidth);
        MinHeight = Math.Min(MinHeight, availableHeight);
        Width = Math.Min(Width, availableWidth);
        Height = Math.Min(Height, availableHeight);
        Position = new PixelPoint(screen.WorkingArea.X + (int)Math.Max(0, (screen.WorkingArea.Width - Width * screen.Scaling) / 2), screen.WorkingArea.Y + (int)Math.Max(0, (screen.WorkingArea.Height - (Height + 32) * screen.Scaling) / 2));
    }

    private void RefreshList(string? selectedId, int fallbackIndex)
    {
        selections.Clear();
        List<ListBoxItem> rows = [];
        foreach (ErrorIncident entry in incidents)
        {
            CheckBox selection = new() { VerticalAlignment = VerticalAlignment.Center };
            selections.Add(entry.Id, selection);
            selection.IsCheckedChanged += (_, _) => RefreshSelection();
            StackPanel label = new() { Spacing = 4, Margin = new Thickness(8, 4) };
            label.Children.Add(new TextBlock { Text = entry.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"), FontWeight = FontWeight.SemiBold });
            label.Children.Add(new TextBlock { Text = ReportPrivacy.Text(entry.Subsystem), Opacity = 0.75, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid row = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            row.Children.Add(selection);
            Grid.SetColumn(label, 1);
            row.Children.Add(label);
            rows.Add(new ListBoxItem { Content = row, Tag = entry, HorizontalContentAlignment = HorizontalAlignment.Stretch });
        }
        incidentList.ItemsSource = rows;
        count.Text = $"Incidents ({incidents.Count})";
        int index = incidents.FindIndex(entry => entry.Id == selectedId);
        incidentList.SelectedIndex = index >= 0 ? index : Math.Min(fallbackIndex, incidents.Count - 1);
        SelectIncident();
        RefreshSelection();
    }

    private void SelectIncident()
    {
        incident = (incidentList.SelectedItem as ListBoxItem)?.Tag as ErrorIncident;
        report = incident is not null ? reports.GetValueOrDefault(incident.Id) : null;
        incidentContent.IsVisible = incident is not null;
        if (incident is not null)
        {
            RefreshSummary();
            string saveDetails = incident.Save is null ? "Save unavailable: " + incident.SaveUnavailableReason : $"Save: {incident.Save.Kind}; {incident.Save.CapturedUtc:O}; {incident.Save.Validation}";
            details.Text = ReportPrivacy.Text(incident.ExceptionText + Environment.NewLine + saveDetails);
            if (report is not null)
            {
                location.Text = $"{report.ArchivePath}{Environment.NewLine}{report.Size:N0} bytes";
                url.Text = report.IssueUrl;
            }
        }
        recoverySection.IsVisible = report is not null;
        progress.Text = incidents.Count == 0 ? "No saved incidents." : "";
        prepare.IsEnabled = incident is not null && !preparing;
        delete.IsEnabled = incident is not null && !preparing;
    }

    private void RefreshSelection()
    {
        if (updatingSelection)
        {
            return;
        }
        int selected = selections.Values.Count(selection => selection.IsChecked == true);
        updatingSelection = true;
        selectAll.IsChecked = selected == 0 ? false : selected == incidents.Count ? true : null;
        updatingSelection = false;
        selectAll.IsEnabled = incidents.Count > 0 && !preparing;
        deleteSelected.Content = selected == 0 ? "Delete selected" : $"Delete selected ({selected})";
        deleteSelected.IsEnabled = selected > 0 && !preparing;
    }

    private void SetPreparing(bool value)
    {
        preparing = value;
        incidentList.IsEnabled = !value;
        prepare.IsEnabled = !value && incident is not null;
        delete.IsEnabled = !value && incident is not null;
        RefreshSelection();
    }

    private void RefreshSummary()
    {
        if (incident is null)
        {
            return;
        }
        summary.Text = $"Incident time: {incident.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz}{Environment.NewLine}" + ReportPrivacy.Text($"{incident.Subsystem}: {incident.Exception.Type}: {incident.Exception.Message}") + $"{Environment.NewLine}Incident file: {incident.GetStoragePath(reportsDirectory)}";
    }

    private async Task MarkPresentedAsync()
    {
        try
        {
            foreach (ErrorIncident entry in incidents.ToArray())
            {
                await Task.Run(() => entry.MarkPresented(reportsDirectory));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            progress.Text = "Could not record that this report was shown: " + exception.Message;
        }
    }

    private async Task DeleteAsync(bool selected)
    {
        if (preparing || incident is null)
        {
            return;
        }
        ErrorIncident[] targets = selected ? incidents.Where(entry => selections[entry.Id].IsChecked == true).ToArray() : [incident];
        if (targets.Length == 0)
        {
            return;
        }
        SetPreparing(true);
        int index = incidentList.SelectedIndex;
        string selectedId = incident.Id;
        List<string> failures = [];
        try
        {
            if (!await LauncherDialogs.ConfirmAsync(this, "Delete incidents", $"Delete {targets.Length} local incident(s) and their prepared ZIPs? This cannot be undone. Ordinary saves and original logs will be kept. Nothing will be sent to GitHub."))
            {
                return;
            }
            await presentation;
            foreach (ErrorIncident target in targets)
            {
                try
                {
                    if (reports.Remove(target.Id, out PreparedErrorReport? prepared))
                    {
                        prepared.Dispose();
                    }
                    await Task.Run(() => target.Delete(reportsDirectory));
                    incidents.Remove(target);
                }
                catch (Exception exception)
                {
                    failures.Add(exception.Message);
                }
            }
            RefreshList(selectedId, index);
            if (failures.Count > 0)
            {
                progress.Text = "Could not delete some incidents: " + string.Join(Environment.NewLine, failures);
            }
        }
        finally
        {
            SetPreparing(false);
        }
    }

    private async Task PrepareAsync()
    {
        if (preparing || incident is null)
        {
            return;
        }
        if (report is not null)
        {
            OpenGitHub();
            return;
        }
        SetPreparing(true);
        progress.Text = "Preparing…";
        try
        {
            await presentation;
            report = await Memoria.FF8.GameErrorReport.PrepareAsync(incident, reportsDirectory);
            reports[incident.Id] = report;
            RefreshSummary();
            location.Text = $"{report.ArchivePath}{Environment.NewLine}{new FileInfo(report.ArchivePath).Length:N0} bytes";
            url.Text = report.IssueUrl;
            recoverySection.IsVisible = true;
            progress.Text = "Ready";
            string? revealFailure = ReportDesktop.RevealFile(report.ArchivePath);
            OpenGitHub();
            if (revealFailure is not null)
            {
                progress.Text += Environment.NewLine + revealFailure;
            }
        }
        catch (Exception exception)
        {
            progress.Text = "Preparation failed: " + exception.Message;
        }
        finally
        {
            SetPreparing(false);
        }
    }

    private void OpenGitHub()
    {
        if (report is null || DateTimeOffset.UtcNow - lastOpen < TimeSpan.FromSeconds(1))
        {
            return;
        }
        lastOpen = DateTimeOffset.UtcNow;
        string? failure = ReportDesktop.OpenUrl(report.IssueUrl);
        progress.Text = failure is null ? "GitHub form opened. Attach the ZIP in your browser." : "Ready. " + failure;
    }

    private void DisplayDesktopResult(string? failure)
    {
        progress.Text = failure ?? "Ready";
    }

    private static void AddAction(StackPanel panel, string label, Action action)
    {
        Button button = new() { Content = label };
        button.Click += (_, _) => action();
        panel.Children.Add(button);
    }

    private void AddCopy(StackPanel panel, string label, Func<string> value)
    {
        Button button = new() { Content = label };
        button.Click += async (_, _) =>
        {
            try
            {
                if (Clipboard is null)
                {
                    progress.Text = "Clipboard unavailable. Select and copy the text in Details or the prepared report fields.";
                    return;
                }
                await Clipboard.SetTextAsync(value());
            }
            catch (Exception exception)
            {
                progress.Text = "Could not copy: " + exception.Message;
            }
        };
        panel.Children.Add(button);
    }
}
