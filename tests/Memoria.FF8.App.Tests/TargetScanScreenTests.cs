using System.Collections.Immutable;
using System.Globalization;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class TargetScanScreenTests
{
    [Fact]
    public void Standard_scan_rows_show_signed_resistance_and_highlight_only_elemental_weakness()
    {
        NativeBattlePreparedScanActor actor = new(16, "Target", false, false);
        NativeBattlePreparedScan prepared = new(null!, null!, null!, ImmutableDictionary<int, NativeBattlePreparedScanExtra>.Empty, ImmutableDictionary<byte, ImmutableArray<byte>>.Empty, null, null, actor);
        byte[] status = Enumerable.Repeat((byte)100, 40).ToArray();
        status[6] = 90;
        status[24] = 200;
        NativeBattleScanTargetData facts = new(32, 7, 320, 800, 11, 22, 33, 44, 55, 66, [700, 850, 800, 800, 800, 800, 900, 1000], [.. status]);
        TargetScanData scan = Ff8TargetScan.Create(prepared, facts, actor.Name, "", "en", (key, original) => original);

        Assert.Equal(new[] { -100, 50, 0, 0, 0, 0, 100, 200 }, scan.Elements.Select(row => row.Percent));
        Assert.Equal(new TargetScanResistance("Eject", TargetScanScreen.HolyIcon, 100), scan.Statuses[^1]);
        Assert.Equal(-10, scan.Statuses.Single(row => row.Name == "Zombie").Percent);

        RecordingCanvas canvas = new();
        TargetScanScreen.Draw(canvas, new VectorUiTheme(), new(new UiRect(0, 0, 960, 672)), 1, scan, null);
        Assert.Equal(new[] { "Str", "Vit", "Spd" }, StatLabels(591));
        Assert.Equal(new[] { "Mag", "Spr", "Eva" }, StatLabels(623));
        UiColor weaknessColor = new(255, 220, 96);
        var highlighted = canvas.Texts.Where(entry => entry.Color == weaknessColor).Select(entry => entry.Value).ToArray();
        Assert.Equal(new[] { "Fire", (-1m).ToString("P0", CultureInfo.CurrentCulture) }, highlighted);
        Assert.Contains(new UiImageSource(UiImageKind.Icon, TargetScanScreen.HolyIcon), canvas.Images);

        string[] StatLabels(float y) => canvas.Texts.Where(row => row.Bounds.Y == y && char.IsLetter(row.Value[0])).Select(row => row.Value).ToArray();
    }

    [Fact]
    public void Installed_scan_uses_the_allocated_enemy_description_and_standard_element_labels()
    {
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_GAME_DIR");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(directory), "Select the original FFVIII installation.");
        using GameAssets game = new(directory!);
        using IResourceSource source = GameDataSourceFactory.Open(directory!);
        NativeBattlePreparedScanActor actor = new(88, "Edea", false, false);
        NativeBattlePreparedScan prepared = new(null!, null!, null!, ImmutableDictionary<int, NativeBattlePreparedScanExtra>.Empty, ImmutableDictionary<byte, ImmutableArray<byte>>.Empty, game.ReadNativeScanTextResources(), null, actor);
        NativeBattleScanTargetData facts = new(104, 7, 320, 800, 11, 22, 33, 44, 55, 66, [800, 800, 800, 800, 800, 800, 800, 800], []);
        var (name, description) = new LocalizationBattlePresentation(new ResourceService(source)).TargetScanText(prepared, actor, facts.DescriptionIndex);
        TargetScanData scan = Ff8TargetScan.Create(prepared, facts, name, description, "en", (key, original) => original);
        Assert.Equal("Edea", scan.Name);
        Assert.Contains("sorceress", scan.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(new[] { "Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy" }, scan.Elements.Select(row => row.Name));
    }

    private sealed class RecordingCanvas : IUiCanvas
    {
        public List<(string Value, UiRect Bounds, UiColor Color)> Texts { get; } = [];
        public List<UiImageSource> Images { get; } = [];

        public UiSize MeasureText(string text, float fontSize, float maximumWidth) => new(text.Length * fontSize / 2, fontSize);

        public void Text(string text, float fontSize, UiRect bounds, UiColor color)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Texts.Add((text, bounds, color));
            }
        }

        public void Text(string text, float fontSize, UiRect bounds, UiColor color, UiAlignment verticalAlignment) => Text(text, fontSize, bounds, color);
        public void Image(UiImageSource source, UiRect bounds) => Images.Add(source);
        public void Fill(UiRect bounds, UiColor color) { }
        public void Line(float x1, float y1, float x2, float y2, UiColor color, float thickness) { }
        public void MenuPointer(UiRect bounds) { }
        public void PushClip(UiRect bounds) { }
        public void PopClip() { }
    }
}
