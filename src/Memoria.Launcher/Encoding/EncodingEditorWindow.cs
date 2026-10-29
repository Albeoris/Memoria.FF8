using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Memoria.FF8.Mods.EncodingProfiles;
using Memoria.FF8.Resources.Text;

namespace Memoria.Launcher.EncodingProfiles;

/// <summary>Small desktop editor; only explicit Apply/Save/Export commands change profile data.</summary>
internal sealed class EncodingEditorWindow : Window
{
    private sealed class Document(NativeFontProfile profile, string? path = null)
    {
        public NativeFontProfile Profile = profile;
        public string? Path = path;
        public bool Enabled = true;
        public bool Dirty;
        public override string ToString() => (Enabled ? "✓ " : "") + Profile.DisplayName + (Dirty ? " *" : "");
    }

    private readonly List<Document> documents = [];
    private readonly ComboBox profiles = new() { MinWidth = 240 };
    private readonly CheckBox enabled = new() { Content = "Enabled for export" };
    private readonly TextBox displayName = new() { Watermark = "Display name", MinWidth = 180 };
    private readonly TextBox status = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 68 };
    private readonly TextBox search = new() { Watermark = "Search character or image index" };
    private readonly ListBox glyphs = new() { MinHeight = 180 };
    private readonly TextBox character = new() { Watermark = "Unicode text element" };
    private readonly TextBox imageIndex = new() { Watermark = "Image index (decimal)" };
    private readonly TextBox atlasId = new() { Watermark = "Atlas ID" };
    private readonly TextBox rectangle = new() { Watermark = "x,y,width,height" };
    private readonly TextBox metrics = new() { Watermark = "advance,bearingX,bearingY" };
    private readonly TextBox field = new() { Watermark = "Optional field override" };
    private readonly TextBox atlasesJson = new() { AcceptsReturn = true, MinHeight = 140, TextWrapping = TextWrapping.NoWrap };
    private readonly TextBox assignmentsJson = new() { AcceptsReturn = true, MinHeight = 140, TextWrapping = TextWrapping.NoWrap };
    private readonly TextBox domainsJson = new() { AcceptsReturn = true, MinHeight = 85, TextWrapping = TextWrapping.NoWrap };
    private readonly TextBox lineHeight = new() { Watermark = "Line height" };
    private readonly ListBox codec = new() { MinHeight = 180 };
    private readonly TextBox codecText = new() { Watermark = "Decoded text" };
    private readonly TextBox codecHex = new() { Watermark = "Native bytes, hexadecimal" };
    private readonly CheckBox canonical = new() { Content = "Canonical Unicode reverse mapping", IsChecked = true };
    private readonly TextBox edition = new() { Text = "classic", Watermark = "Edition" };
    private readonly TextBox resource = new() { Text = "field", Watermark = "Resource family" };
    private readonly TextBox dictionary = new() { Text = "en", Watermark = "Dictionary language" };
    private readonly TextBox sample = new() { Text = "Sample text", AcceptsReturn = true, MinHeight = 70 };
    private readonly NativeFontPreview preview = new();
    private readonly NativeFontPreview atlasPreview = new();
    private bool selecting;
    private bool confirmedClose;
    private Document? Current => profiles.SelectedItem as Document;

    public EncodingEditorWindow()
    {
        Title = "Encoding Editor";
        Width = 1080;
        Height = 850;
        MinWidth = 780;
        MinHeight = 620;
        StackPanel shell = new() { Margin = new Thickness(16), Spacing = 10 };
        shell.Children.Add(Row(Button("New", New), Button("Duplicate", Duplicate), Button("Open / import…", () => _ = OpenAsync()), Button("Save / export profile…", () => _ = SaveAsync()), Button("Export enabled to mod…", () => _ = ExportAsync())));
        shell.Children.Add(Row(profiles, enabled, displayName, Button("Rename", Rename)));
        TabControl tabs = new();
        StackPanel glyphTab = new() { Spacing = 8 };
        glyphTab.Children.Add(new TextBlock { Text = "Unicode/text element → image index → atlas rectangle and metrics. Image indices are independent of native bytes." });
        glyphTab.Children.Add(search);
        glyphTab.Children.Add(glyphs);
        glyphTab.Children.Add(Row(character, imageIndex, atlasId, field));
        glyphTab.Children.Add(Row(rectangle, metrics, Button("Apply glyph / image", ApplyGlyph), Button("Remove character", RemoveGlyph)));
        glyphTab.Children.Add(new TextBlock { Text = "Click an outlined atlas image to select its mapping. Pink marks the selected image." });
        glyphTab.Children.Add(new ScrollViewer { Content = atlasPreview, MaxHeight = 280, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
        StackPanel codecTab = new() { Spacing = 8 };
        codecTab.Children.Add(new TextBlock { Text = "Separate native byte mappings. Reserved control mappings are protected. Import a reviewed profile for another native grammar." });
        codecTab.Children.Add(codec);
        codecTab.Children.Add(Row(codecText, codecHex, canonical));
        codecTab.Children.Add(Button("Apply printable mapping", ApplyCodec));
        StackPanel dependenciesTab = new() { Spacing = 8 };
        dependenciesTab.Children.Add(new TextBlock { Text = "Atlas declarations (JSON array): id, relative path, dimensions, optional SHA-256 and palette." });
        dependenciesTab.Children.Add(atlasesJson);
        dependenciesTab.Children.Add(Button("Apply atlas declarations", ApplyAtlases));
        dependenciesTab.Children.Add(new TextBlock { Text = "Supported native domains (JSON array): edition, resourceFamily, optional field and dictionaryLanguage. Empty means render-only." });
        dependenciesTab.Children.Add(domainsJson);
        dependenciesTab.Children.Add(Row(lineHeight, Button("Apply native domains / line height", ApplyDomains)));
        dependenciesTab.Children.Add(new TextBlock { Text = "Dataset assignments (JSON array): role Source/Reference/Target, dataset, stable profileId, and explicit context." });
        dependenciesTab.Children.Add(assignmentsJson);
        dependenciesTab.Children.Add(Button("Apply assignments", ApplyAssignments));
        StackPanel sampleTab = new() { Spacing = 8 };
        sampleTab.Children.Add(Row(edition, resource, dictionary));
        sampleTab.Children.Add(sample);
        sampleTab.Children.Add(Button("Validate + preview", Preview));
        sampleTab.Children.Add(new TextBlock { Text = "Uses shared game glyph placement and metrics. First page shown at 2×. Name/dictionary substitutions must be expanded before bitmap layout." });
        sampleTab.Children.Add(new ScrollViewer { Content = preview, MaxHeight = 300 });
        tabs.ItemsSource = new TabItem[] { new() { Header = "Glyph images", Content = glyphTab }, new() { Header = "Native bytes", Content = codecTab }, new() { Header = "Dependencies / roles", Content = dependenciesTab }, new() { Header = "Sample", Content = sampleTab } };
        shell.Children.Add(tabs);
        shell.Children.Add(status);
        Content = new ScrollViewer { Content = shell };
        profiles.SelectionChanged += (_, _) => Select();
        enabled.IsCheckedChanged += (_, _) =>
        {
            if (!selecting && Current is Document current)
            {
                current.Enabled = enabled.IsChecked == true;
                RefreshProfiles(current);
            }
        };
        search.TextChanged += (_, _) => RefreshGlyphs();
        glyphs.SelectionChanged += (_, _) => SelectGlyph();
        codec.SelectionChanged += (_, _) =>
        {
            if (codec.SelectedItem is CodecRow row)
            {
                codecText.Text = row.Mapping.Text;
                codecHex.Text = row.Mapping.Hex;
                canonical.IsChecked = row.Mapping.Canonical;
                codecText.IsReadOnly = row.Mapping.Control;
                codecHex.IsReadOnly = row.Mapping.Control;
                canonical.IsEnabled = !row.Mapping.Control;
            }
        };
        atlasPreview.SelectImage = index =>
        {
            if (Current is not Document current)
            {
                return;
            }
            NativeGlyphImage image = current.Profile.Images.First(image => image.Index == index);
            NativeGlyphMapping? mapping = current.Profile.Glyphs.FirstOrDefault(mapping => mapping.ImageIndex == index);
            FillGlyph(mapping, image);
        };
        Closed += (_, _) =>
        {
            preview.Dispose();
            atlasPreview.Dispose();
        };
        Closing += async (_, args) =>
        {
            if (!confirmedClose && documents.Any(document => document.Dirty))
            {
                args.Cancel = true;
                if (await LauncherDialogs.ConfirmAsync(this, "Unsaved encoding profiles", "Close and discard unsaved profile changes? Save each changed profile first to keep it."))
                {
                    confirmedClose = true;
                    Close();
                }
            }
        };
        New();
    }

    private static StackPanel Row(params Control[] controls)
    {
        StackPanel panel = new() { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (Control control in controls)
        {
            if (control is TextBox box)
            {
                box.MinWidth = 140;
            }
            panel.Children.Add(control);
        }
        return panel;
    }

    private Button Button(string text, Action action)
    {
        Button button = new() { Content = text };
        button.Click += (_, _) => Guard(action);
        return button;
    }

    private void Guard(Action action)
    {
        try
        {
            action();
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidOperationException or FormatException or NotSupportedException or OverflowException)
        {
            status.Text = exception.Message;
        }
    }

    private void New()
    {
        Document document = new(new NativeFontProfile()) { Dirty = true };
        documents.Add(document);
        RefreshProfiles(document);
    }

    private void Duplicate()
    {
        if (Current is Document current)
        {
            Document copy = new(current.Profile with { Id = Guid.NewGuid().ToString("D"), DisplayName = current.Profile.DisplayName + " copy" }, current.Path) { Dirty = true };
            documents.Add(copy);
            RefreshProfiles(copy);
        }
    }

    private void Rename()
    {
        if (Current is Document current && !string.IsNullOrWhiteSpace(displayName.Text))
        {
            SetProfile(current.Profile with { DisplayName = displayName.Text });
        }
    }

    private void RefreshProfiles(Document selected)
    {
        selecting = true;
        profiles.ItemsSource = documents.ToArray();
        profiles.SelectedItem = selected;
        selecting = false;
        Select();
    }

    private void Select()
    {
        if (Current is not Document current)
        {
            return;
        }
        selecting = true;
        enabled.IsChecked = current.Enabled;
        displayName.Text = current.Profile.DisplayName;
        atlasesJson.Text = JsonSerializer.Serialize(current.Profile.Atlases, NativeFontProfileIO.JsonOptions);
        assignmentsJson.Text = JsonSerializer.Serialize(current.Profile.Assignments, NativeFontProfileIO.JsonOptions);
        domainsJson.Text = JsonSerializer.Serialize(current.Profile.Domains, NativeFontProfileIO.JsonOptions);
        lineHeight.Text = current.Profile.LineHeight.ToString(CultureInfo.InvariantCulture);
        codec.ItemsSource = current.Profile.Codec.Select(mapping => new CodecRow(mapping)).ToArray();
        selecting = false;
        RefreshGlyphs();
        status.Text = $"Profile: {current.Profile.Id}\n{current.Path ?? "Unsaved profile"}. Select exactly one profile for each operation; enabled profiles remain separate namespaces.";
    }

    private sealed record GlyphRow(NativeGlyphMapping Mapping)
    {
        public override string ToString() => $"{Mapping.Text} → image {Mapping.ImageIndex}" + (Mapping.Field is null ? "" : "  field=" + Mapping.Field);
    }
    private sealed record CodecRow(NativeCodecMapping Mapping)
    {
        public override string ToString() => $"{Mapping.Hex} ↔ {Mapping.Text}" + (Mapping.Canonical ? " [canonical]" : " [alias]") + (Mapping.Control ? " [protected]" : "");
    }

    private void RefreshGlyphs()
    {
        string query = search.Text ?? "";
        glyphs.ItemsSource = Current?.Profile.Glyphs.Where(mapping => mapping.Text.Contains(query, StringComparison.OrdinalIgnoreCase) || mapping.ImageIndex.ToString(CultureInfo.InvariantCulture).Contains(query, StringComparison.Ordinal)).Select(mapping => new GlyphRow(mapping)).ToArray();
    }

    private void SelectGlyph()
    {
        if (Current is Document current && glyphs.SelectedItem is GlyphRow row)
        {
            FillGlyph(row.Mapping, current.Profile.Images.First(image => image.Index == row.Mapping.ImageIndex));
        }
    }

    private void FillGlyph(NativeGlyphMapping? mapping, NativeGlyphImage image)
    {
        character.Text = mapping?.Text;
        field.Text = mapping?.Field;
        imageIndex.Text = image.Index.ToString(CultureInfo.InvariantCulture);
        atlasId.Text = image.AtlasId;
        rectangle.Text = $"{image.X},{image.Y},{image.Width},{image.Height}";
        metrics.Text = string.Join(",", new[] { image.Advance, image.BearingX, image.BearingY }.Select(number => number.ToString(CultureInfo.InvariantCulture)));
        Guard(() =>
        {
            if (Current?.Path is string path)
            {
                atlasPreview.Load(Current.Profile, path);
                atlasPreview.ShowAtlas(image.AtlasId, image.Index);
            }
        });
    }

    private void SetProfile(NativeFontProfile profile)
    {
        IReadOnlyList<NativeTextDiagnostic> diagnostics = NativeFontProfileValidation.Validate(profile);
        if (diagnostics.Count != 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, diagnostics.Select(issue => issue.Code + ": " + issue.Message)));
        }
        if (Current is Document current)
        {
            current.Profile = profile;
            current.Dirty = true;
            RefreshProfiles(current);
        }
    }

    private void ApplyGlyph()
    {
        if (Current is not Document current)
        {
            return;
        }
        int index = int.Parse(imageIndex.Text ?? "", CultureInfo.InvariantCulture);
        int[] rect = (rectangle.Text ?? "").Split(',').Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        float[] metric = (metrics.Text ?? "").Split(',').Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        if (rect.Length != 4 || metric.Length != 3)
        {
            throw new ArgumentException("Rectangle requires four integers; metrics require advance,bearingX,bearingY.");
        }
        NativeGlyphImage image = new(index, atlasId.Text ?? "", rect[0], rect[1], rect[2], rect[3], metric[0], metric[1], metric[2]);
        string? fieldName = string.IsNullOrWhiteSpace(field.Text) ? null : field.Text;
        NativeGlyphMapping mapping = new(character.Text ?? "", index, fieldName);
        SetProfile(current.Profile with { Images = current.Profile.Images.Where(value => value.Index != index).Append(image).ToArray(), Glyphs = current.Profile.Glyphs.Where(value => value.Text != mapping.Text || value.Field != mapping.Field).Append(mapping).ToArray() });
    }

    private void RemoveGlyph()
    {
        if (Current is Document current && glyphs.SelectedItem is GlyphRow row)
        {
            SetProfile(current.Profile with { Glyphs = current.Profile.Glyphs.Where(mapping => mapping != row.Mapping).ToArray() });
        }
    }

    private void ApplyCodec()
    {
        if (Current is not Document current)
        {
            return;
        }
        string hex = (codecHex.Text ?? "").ToUpperInvariant();
        if (current.Profile.Codec.Any(mapping => mapping.Control && mapping.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Reserved control slots are protected in the table editor.");
        }
        NativeCodecMapping updated = new(hex, codecText.Text ?? "", canonical.IsChecked == true, Field: string.IsNullOrWhiteSpace(field.Text) ? null : field.Text);
        var entries = current.Profile.Codec.Where(mapping => mapping.Hex != updated.Hex || mapping.Field != updated.Field).Select(mapping => updated.Canonical && mapping.Text == updated.Text && mapping.Field == updated.Field ? mapping with { Canonical = false } : mapping).Append(updated).ToArray();
        SetProfile(current.Profile with { Codec = entries });
    }

    private void ApplyAtlases()
    {
        if (Current is Document current)
        {
            SetProfile(current.Profile with { Atlases = JsonSerializer.Deserialize<NativeFontAtlas[]>(atlasesJson.Text ?? "[]", NativeFontProfileIO.JsonOptions) ?? [] });
        }
    }

    private void ApplyAssignments()
    {
        if (Current is Document current)
        {
            SetProfile(current.Profile with { Assignments = JsonSerializer.Deserialize<NativeProfileAssignment[]>(assignmentsJson.Text ?? "[]", NativeFontProfileIO.JsonOptions) ?? [] });
        }
    }

    private void ApplyDomains()
    {
        if (Current is Document current)
        {
            SetProfile(current.Profile with { Domains = JsonSerializer.Deserialize<NativeTextDomain[]>(domainsJson.Text ?? "[]", NativeFontProfileIO.JsonOptions) ?? [], LineHeight = float.Parse(lineHeight.Text ?? "", CultureInfo.InvariantCulture) });
        }
    }

    private async Task OpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Open / import profile.json", AllowMultiple = true });
        Guard(() =>
        {
            foreach (IStorageFile file in files)
            {
                string path = file.TryGetLocalPath() ?? throw new IOException("Choose a local profile file.");
                Document document = new(NativeFontProfileIO.Load(path), path);
                documents.Add(document);
                RefreshProfiles(document);
            }
        });
    }

    private async Task SaveAsync()
    {
        if (Current is not Document current)
        {
            return;
        }
        IStorageFile? file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "Save profile; keep relative dependencies beside it", SuggestedFileName = "profile.json", DefaultExtension = "json" });
        if (file is null)
        {
            return;
        }
        Guard(() =>
        {
            string path = file.TryGetLocalPath() ?? throw new IOException("Choose a local profile file.");
            NativeFontProfileIO.Save(path, current.Profile);
            current.Path = path;
            current.Dirty = false;
            RefreshProfiles(current);
            status.Text = "Profile saved. " + string.Join(Environment.NewLine, NativeFontProfileIO.ValidateDependencies(path, current.Profile).Select(issue => issue.Message));
        });
    }

    private async Task ExportAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose mod root (new Encoding/Name directories will be created)", AllowMultiple = false });
        if (folders.Count == 0)
        {
            return;
        }
        Guard(() =>
        {
            Document[] selected = documents.Where(document => document.Enabled).ToArray();
            if (selected.Any(document => document.Path is null || document.Dirty))
            {
                throw new InvalidOperationException("Save all enabled profiles before exporting their dependency closure.");
            }
            string root = folders[0].TryGetLocalPath() ?? throw new IOException("Choose a local mod directory.");
            EncodingProfilePackage.Export(selected.Select(document => new EncodingProfileExport(document.Path!, document.Profile.DisplayName)), root);
            status.Text = $"Exported {selected.Length} enabled profiles with dependencies. Native-resource table validation does not establish original-game deployment.";
        });
    }

    private void Preview()
    {
        if (Current is not Document current || current.Path is null)
        {
            throw new InvalidOperationException("Save a profile and configure its atlas paths before previewing.");
        }
        NativeTextContext context = new(edition.Text ?? "", resource.Text ?? "", string.IsNullOrWhiteSpace(field.Text) ? null : field.Text, dictionary.Text);
        NativeFontLayoutResult result = NativeFontLayout.Layout(current.Profile, sample.Text ?? "", context, current.Profile.LogicalWidth, current.Profile.LogicalHeight);
        preview.Load(current.Profile, current.Path);
        preview.ShowSample(result);
        var diagnostics = NativeFontProfileIO.ValidateDependencies(current.Path, current.Profile).Concat(result.Diagnostics).Concat(NativeStrictCodec.Encode(current.Profile, sample.Text ?? "", context).Diagnostics).ToArray();
        status.Text = $"Native-style sample: {result.Width}×{result.Height}, {result.Pages} page(s).\n" + (diagnostics.Length == 0 ? "Mapping and strict native table validation passed. Original-game deployment is unverified." : string.Join(Environment.NewLine, diagnostics.Select(issue => $"{issue.Code} at {issue.Position}: {issue.Message}")));
    }
}
