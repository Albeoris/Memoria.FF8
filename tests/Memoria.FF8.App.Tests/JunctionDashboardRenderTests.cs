using Memoria.FF8.GameUi;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionDashboardRenderTests
{
    [Theory]
    [InlineData(1280, 720, MenuPresentation.Classic, false)]
    [InlineData(1280, 720, MenuPresentation.Classic, true)]
    [InlineData(1280, 720, MenuPresentation.Vector, false)]
    [InlineData(1280, 720, MenuPresentation.Vector, true)]
    [InlineData(1280, 800, MenuPresentation.Classic, false)]
    [InlineData(1280, 800, MenuPresentation.Vector, false)]
    [InlineData(1920, 1080, MenuPresentation.Classic, false)]
    [InlineData(1920, 1080, MenuPresentation.Vector, false)]
    public void MaximumDashboardKeepsAllAssignmentsAndBothCompleteTablesVisible(int width, int height, MenuPresentation presentation, bool picker)
    {
        MenuData data = MaximumFixture();
        MenuController menu = new(() => data) { Presentation = presentation };
        menu.Open("junction-dashboard");
        menu.PrepareViewport(width, height);
        UiView view = menu.BuildView();
        if (picker)
        {
            UiButton slot = Assert.IsType<UiButton>(Descendants(view.Root).Single(element => element.Id == "slot-ST-Atk"));
            slot.Click!();
            view = menu.BuildView();
        }
        using SKBitmap bitmap = new(width, height);
        using BattlePreviewCanvas renderer = new(bitmap, Path.Combine(Path.GetTempPath(), "unconfigured-game-resources"));
        renderer.BeginFrame(TimeSpan.Zero);
        RecordingCanvas canvas = new(renderer);
        view.Resize(width, height);
        view.Draw(canvas, presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme());

        UiElement[] elements = Descendants(view.Root).ToArray();
        Assert.Equal(19, elements.Count(element => element.Id?.StartsWith("slot-", StringComparison.Ordinal) == true));
        Assert.Equal(8, elements.Count(element => element.Id?.StartsWith("elemental-readout-", StringComparison.Ordinal) == true));
        Assert.Equal(13, elements.Count(element => element.Id?.StartsWith("status-readout-", StringComparison.Ordinal) == true));
        foreach (UiElement assignment in elements.Where(element => element.Id?.StartsWith("slot-", StringComparison.Ordinal) == true || element.Id?.Contains("-readout-", StringComparison.Ordinal) == true))
        {
            Assert.True(assignment.Visible && !assignment.Bounds.IsEmpty, assignment.Id);
            Assert.True(assignment.Bounds.X >= 0 && assignment.Bounds.Y >= 0, assignment.Id);
            AssertContained(assignment.Bounds, view.Root.Bounds);
        }
        UiElement[] assignments = elements.Where(element => element.Id?.StartsWith("slot-", StringComparison.Ordinal) == true || element.Id?.Contains("-readout-", StringComparison.Ordinal) == true).ToArray();
        for (int first = 0; first < assignments.Length; first++)
        {
            for (int second = first + 1; second < assignments.Length; second++)
            {
                UiRect overlap = assignments[first].Bounds.Intersect(assignments[second].Bounds);
                Assert.True(overlap.Width <= 0.01f || overlap.Height <= 0.01f, $"Overlapping controls: {assignments[first].Id}, {assignments[second].Id}");
            }
        }
        HashSet<string> essentialNames = data.Magic.Select(spell => spell.Name).Concat(data.Characters[0].Abilities).Concat(data.Characters[0].JunctionEffects!.Elements.Select(effect => effect.Name)).Concat(data.Characters[0].JunctionEffects!.Statuses.Select(effect => effect.Name)).ToHashSet(StringComparer.Ordinal);
        foreach (var text in canvas.Texts.Where(text => essentialNames.Contains(text.Value) || text.Value.EndsWith('%')))
        {
            Assert.True(text.Measured.Width <= text.Bounds.Width + 1, $"Text width: {text.Value}, needs {text.Measured.Width}, has {text.Bounds.Width}");
            Assert.True(text.Measured.Height <= text.Bounds.Height + 1, $"Text height: {text.Value}, needs {text.Measured.Height}, has {text.Bounds.Height}");
        }
        UiImage[] guardianPortraits = elements.OfType<UiImage>().Where(image => image.Source?.Kind == UiImageKind.GuardianForcePortrait).ToArray();
        Assert.Equal(16, guardianPortraits.Length);
        for (int index = 0; index < guardianPortraits.Length; index++)
        {
            UiImage portrait = guardianPortraits[index];
            Assert.Equal(data.GuardianForces[index].NativeId, portrait.Source?.Id);
            Assert.Equal((byte)255, portrait.Source?.Opacity);
            Assert.InRange(Math.Abs(portrait.Bounds.Width - portrait.Bounds.Height), 0, 0.01f);
            Assert.InRange(Math.Abs(portrait.Bounds.Y - guardianPortraits[0].Bounds.Y), 0, 0.01f);
            AssertContained(portrait.Bounds, view.Root.Bounds);
        }
        Assert.Contains(canvas.Texts, text => text.Value == "150%");
        Assert.Contains(canvas.Texts, text => text.Value == "0%");
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            string prefix = Path.Combine(directory, $"junction-maximum-{presentation}-{width}x{height}{(picker ? "-picker" : "")}");
            bitmap.SavePng(prefix + ".png");
            File.WriteAllLines(prefix + ".txt", canvas.Texts.Select(text => $"{text.Bounds.X:0.0},{text.Bounds.Y:0.0},{text.Bounds.Width:0.0},{text.Bounds.Height:0.0}\t{text.Value}"));
        }
    }

    [Theory]
    [InlineData(640, 448, 100, MenuPresentation.Classic)]
    [InlineData(640, 448, 100, MenuPresentation.Vector)]
    [InlineData(640, 480, 100, MenuPresentation.Classic)]
    [InlineData(640, 480, 100, MenuPresentation.Vector)]
    [InlineData(1024, 768, 100, MenuPresentation.Classic)]
    [InlineData(1024, 768, 100, MenuPresentation.Vector)]
    [InlineData(1280, 720, 150, MenuPresentation.Classic)]
    [InlineData(1280, 720, 150, MenuPresentation.Vector)]
    public void AccessibleDashboardScrollsToEveryAssignmentWhileHeaderAndPickerStayAvailable(int width, int height, int textScale, MenuPresentation presentation)
    {
        MenuData data = MaximumFixture();
        data.Settings["Text scale"] = textScale + "%";
        MenuController menu = new(() => data) { Presentation = presentation };
        menu.Open("junction-dashboard");
        menu.PrepareViewport(width, height);
        UiView view = menu.BuildView();
        view.Resize(width, height);
        UiScrollPanel scroll = Assert.IsType<UiScrollPanel>(Descendants(view.Root).Single(element => element.Id == "junction-scroll"));
        Assert.True(scroll.MaximumOffset > 0);
        Assert.Equal(19, Descendants(scroll).Count(element => element.Id?.StartsWith("slot-", StringComparison.Ordinal) == true));
        Assert.Equal(21, Descendants(scroll).Count(element => element.Id?.Contains("-readout-", StringComparison.Ordinal) == true));
        UiElement portrait = Descendants(view.Root).Single(element => element.Id == "junction-character-portrait");
        UiRect headerBounds = portrait.Bounds;
        Capture("top");
        UiElement lowerAssignment = Descendants(scroll).Single(element => element.Id == "slot-ST-Def 4");
        Assert.True(view.Focus(lowerAssignment));
        Assert.True(scroll.Offset > 0);
        AssertContained(lowerAssignment.Bounds, scroll.Bounds);
        Assert.Equal(headerBounds, portrait.Bounds);
        Capture("scrolled");
        UiButton last = Assert.IsType<UiButton>(Descendants(scroll).Single(element => element.Id == "passive-3"));
        Assert.True(view.Focus(last));
        AssertContained(last.Bounds, scroll.Bounds);
        last.Click!();
        view = menu.BuildView();
        view.Resize(width, height);
        Assert.Contains(Descendants(view.Root), element => element.Id == "candidate-63");
        Assert.Equal("candidate-63", view.FocusedElement?.Id);
        Assert.DoesNotContain(Descendants(view.Root).OfType<UiButton>(), button => button.Id is "junction-back" or "junction-actions" or "junction-undo" or "junction-search" or "apply-junction-preview" or "cancel-junction-preview");
        Capture("picker");

        void Capture(string suffix)
        {
            using SKBitmap bitmap = new(width, height);
            using BattlePreviewCanvas renderer = new(bitmap, Path.Combine(Path.GetTempPath(), "unconfigured-game-resources"));
            renderer.BeginFrame(TimeSpan.Zero);
            RecordingCanvas canvas = new(renderer);
            view.Draw(canvas, presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme());
            string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
                string prefix = Path.Combine(directory, $"junction-accessible-{presentation}-{width}x{height}-text{textScale}-{suffix}");
                bitmap.SavePng(prefix + ".png");
                File.WriteAllLines(prefix + ".txt", canvas.Texts.Select(text => $"{text.Bounds.X:0.0},{text.Bounds.Y:0.0},{text.Bounds.Width:0.0},{text.Bounds.Height:0.0}\t{text.Value}"));
            }
        }
    }

    private static void AssertContained(UiRect inner, UiRect outer)
    {
        const float tolerance = 0.01f;
        Assert.True(inner.X >= outer.X - tolerance && inner.Y >= outer.Y - tolerance && inner.Right <= outer.Right + tolerance && inner.Bottom <= outer.Bottom + tolerance, $"Rectangle {inner} is outside {outer}.");
    }

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void RestingFocusDisclosesTheCompleteLongAssignmentName(MenuPresentation presentation)
    {
        const string longName = "Superposition protectrice extrêmement prolongée";
        MenuData data = MaximumFixture();
        data.Characters[0].Junctions["Str"] = longName;
        data.Magic.Single(spell => spell.Id == data.Characters[0].JunctionIds["Str"]).Name = longName;
        MenuController menu = new(() => data) { Presentation = presentation };
        menu.Open("junction-dashboard");
        menu.PrepareViewport(1280, 720);
        UiView view = menu.BuildView();
        UiElement strength = Descendants(view.Root).Single(element => element.Id == "slot-Str");
        Assert.True(view.Focus(strength));
        view = menu.BuildView();
        Assert.Contains(Descendants(view.Root).OfType<UiLabel>(), label => label.Text.Contains("Squall", StringComparison.Ordinal) && label.Text.Contains(longName, StringComparison.Ordinal));
        Assert.Equal(longName, data.Characters[0].Junctions["Str"]);
    }

    [Theory]
    [InlineData(MenuPresentation.Classic)]
    [InlineData(MenuPresentation.Vector)]
    public void PercentagePairsFitTheirActualRetainedReadoutColumns(MenuPresentation presentation)
    {
        MenuData data = MaximumFixture();
        MenuController menu = new(() => data) { Presentation = presentation };
        menu.Open("junction-dashboard");
        menu.PrepareViewport(1280, 720);
        UiView view = menu.BuildView();
        // These presentation samples exercise the same labels and font metrics as a projected diff.
        UiElement elemental = Descendants(view.Root).Single(element => element.Id == "elemental-readout-0");
        UiElement status = Descendants(view.Root).Single(element => element.Id == "status-readout-0");
        Assert.IsType<UiLabel>(elemental.Children[1]).Text = "100→200%";
        Assert.IsType<UiLabel>(status.Children[1]).Text = "0→100%";
        using SKBitmap bitmap = new(1280, 720);
        using BattlePreviewCanvas renderer = new(bitmap, Path.Combine(Path.GetTempPath(), "unconfigured-game-resources"));
        renderer.BeginFrame(TimeSpan.Zero);
        RecordingCanvas canvas = new(renderer);
        view.Draw(canvas, presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme());
        foreach (string pair in new[] { "100→200%", "0→100%" })
        {
            var text = Assert.Single(canvas.Texts, text => text.Value == pair);
            Assert.True(text.Measured.Width <= text.Bounds.Width + 1, $"{pair}: needs {text.Measured.Width}, has {text.Bounds.Width}");
            Assert.True(text.Measured.Height <= text.Bounds.Height + 1, $"{pair}: needs {text.Measured.Height}, has {text.Bounds.Height}");
        }
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
            bitmap.SavePng(Path.Combine(directory, $"junction-percentage-pairs-{presentation}-1280x720.png"));
        }
    }

    private static MenuData MaximumFixture()
    {
        MenuData data = new();
        MenuCharacter character = new() { Id = "character-0", NativeId = 0, Name = "Squall", Level = 100, CurrentHp = 9999, MaximumHp = 9999, AbilitySlots = 4 };
        character.Commands.AddRange(["Mug", "Magic", "Draw", "--"]);
        character.CommandIds.AddRange(["20", "22", "0"]);
        character.AvailableCommands.AddRange([new("20", "Magic", true), new("21", "GF", true), new("22", "Draw", true), new("23", "Item", true)]);
        character.Abilities.AddRange(["Auto-Protect", "Auto-Shell", "Auto-Haste", "Auto-Reflect"]);
        character.AbilityIds.AddRange(["60", "61", "62", "63"]);
        for (int index = 0; index < character.Abilities.Count; index++)
        {
            character.AvailableAbilities.Add(new(character.AbilityIds[index], character.Abilities[index], true));
        }
        string[] stats = ["HP", "Str", "Vit", "Mag", "Spr", "Spd", "Eva", "Hit", "Luck"];
        string[] slots = [.. stats, "Elem-Atk", "ST-Atk", "Elem-Def 1", "Elem-Def 2", "Elem-Def 3", "Elem-Def 4", "ST-Def 1", "ST-Def 2", "ST-Def 3", "ST-Def 4"];
        for (int index = 0; index < slots.Length; index++)
        {
            string id = (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            string name = index == 0 ? "Full-life" : "Spell " + id;
            character.AvailableJunctionSlots.Add(slots[index]);
            character.JunctionIds.Add(slots[index], id);
            character.Junctions.Add(slots[index], name);
            data.Magic.Add(new() { Id = id, Name = name, Quantity = 100, CharacterId = character.Id });
        }
        foreach (string stat in stats)
        {
            character.Stats.Add(stat, stat == "HP" ? 9999 : 255);
        }
        string[] elements = ["Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy"];
        string[] statuses = ["Death", "Poison", "Petrify", "Darkness", "Silence", "Berserk", "Zombie", "Sleep", "Slow", "Stop", "Curse", "Confuse", "Drain"];
        character.JunctionEffects = new(elements.Select((name, index) => new MenuJunctionPercentage(name, 0, index == 0 ? 150 : 0)).ToArray(), statuses.Select(name => new MenuJunctionPercentage(name, 0, 0)).ToArray());
        for (int index = 0; index < 16; index++)
        {
            string id = "gf-" + index;
            data.GuardianForces.Add(new() { Id = id, NativeId = index, Name = "Guardian " + index, Owner = new(character.Id, character.NativeId, character.Name) });
            character.GuardianForceIds.Add(id);
        }
        data.Characters.Add(character);
        return data;
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class RecordingCanvas(BattlePreviewCanvas renderer) : IUiCanvas
    {
        public List<(string Value, UiRect Bounds, UiSize Measured)> Texts { get; } = [];
        public void Fill(UiRect bounds, UiColor color) => renderer.Fill(bounds, color);
        public void Paper(UiRect bounds, UiColor color) => renderer.Paper(bounds, color);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color) => Text(text, fontSize, bounds, color, null, UiAlignment.Start, UiTextFlow.Auto);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? family) => Text(text, fontSize, bounds, color, family, UiAlignment.Start, UiTextFlow.Auto);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment vertical) => Text(text, fontSize, bounds, color, null, vertical, UiTextFlow.Auto);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment vertical, UiTextFlow flow) => Text(text, fontSize, bounds, color, null, vertical, flow);
        public void Text(string text, float fontSize, UiRect bounds, UiColor color, string? family, UiAlignment vertical, UiTextFlow flow)
        {
            Texts.Add((text, bounds, renderer.MeasureText(text, fontSize, 100000, family)));
            renderer.Text(text, fontSize, bounds, color, family, vertical, flow);
        }
        public UiSize MeasureText(string text, float fontSize, float maximumWidth) => renderer.MeasureText(text, fontSize, maximumWidth);
        public UiSize MeasureText(string text, float fontSize, float maximumWidth, string? family) => renderer.MeasureText(text, fontSize, maximumWidth, family);
        public void Image(UiImageSource source, UiRect bounds) => renderer.Fill(bounds, new UiColor(70, 100, 125, source.Opacity));
        public void MenuPointer(UiRect bounds) => renderer.Fill(bounds, new UiColor(240, 205, 80));
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness) => renderer.Line(x1, y1, x2, y2, color, thickness);
        public void PushClip(UiRect bounds) => renderer.PushClip(bounds);
        public void PopClip() => renderer.PopClip();
    }
}
