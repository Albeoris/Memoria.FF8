using System.Drawing;
using System.Drawing.Imaging;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionEffectsPresentationTests
{
    [Theory]
    [InlineData(MenuPresentation.Vector, "junction-element", "Elem", 8)]
    [InlineData(MenuPresentation.Vector, "junction-status", "ST", 13)]
    [InlineData(MenuPresentation.Classic, "junction-element", "Elem", 8)]
    [InlineData(MenuPresentation.Classic, "junction-status", "ST", 13)]
    public void EffectsRemainVisibleWhileStockSelectionPreviewsBothAttackAndDefense(MenuPresentation presentation, string screen, string prefix, int count)
    {
        MenuController menu = new() { Presentation = presentation };
        menu.Open(screen, demo: true);
        MenuCharacter character = menu.Character!;
        MenuJunctionPercentage[] elements = new[] { "Fire", "Ice", "Thunder", "Earth", "Poison", "Wind", "Water", "Holy" }.Select(name => new MenuJunctionPercentage(name, 0, 100)).ToArray();
        MenuJunctionPercentage[] statuses = new[] { "Death", "Poison", "Petrify", "Darkness", "Silence", "Berserk", "Zombie", "Sleep", "Slow", "Stop", "Curse", "Confuse", "Drain" }.Select(name => new MenuJunctionPercentage(name, 0, 100)).ToArray();
        character.JunctionEffects = new(elements, statuses);
        int requests = 0;
        character.JunctionEffectsPreview = (slot, spell) =>
        {
            requests++;
            Assert.Equal(prefix + "-Atk", slot);
            return new(elements.Select(value => value with { Attack = 50, Defense = 0 }).ToArray(), statuses.Select(value => value with { Attack = 50, Defense = 0 }).ToArray());
        };
        AssertValues(preview: false);
        Assert.Equal(0, requests);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("junction-stock", menu.ScreenId);
        AssertValues(preview: true);
        Assert.True(requests > 0);
        menu.Navigate(MenuInput.Down);
        int before = requests;
        AssertValues(preview: true);
        Assert.True(requests > before);
        Assert.Equal(0, character.JunctionEffects.Elements[0].Attack);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal(screen, menu.ScreenId);
        AssertValues(preview: false);

        void AssertValues(bool preview)
        {
            foreach ((int width, int height) in new[] { (640, 448), (1280, 720), (2560, 1080) })
            {
                UiView view = menu.BuildView();
                view.Resize(width, height);
                UiElement[] all = Descendants(view.Root).ToArray();
                UiLabel[] values = all.OfType<UiLabel>().Where(label => label.Id?.StartsWith("junction-" + prefix + "-", StringComparison.Ordinal) == true).ToArray();
                Assert.Equal(count * 2, values.Length);
                Assert.All(values, label =>
                {
                    Assert.True(label.Bounds.Width > 0 && label.Bounds.Height > 0);
                    Assert.Equal(preview, label.Text.Contains('→'));
                    if (preview)
                    {
                        Assert.Equal(label.Id!.Contains("-attack-", StringComparison.Ordinal) ? new UiColor(120, 190, 255) : new UiColor(255, 120, 120), label.Color);
                    }
                });
                Assert.DoesNotContain(all.OfType<UiLabel>(), label => label.Id == "junction-value-Str");
                UiList stock = Assert.Single(all.OfType<UiList>(), list => preview ? list.Id == "commands" : list.Id?.StartsWith("magic-preview-", StringComparison.Ordinal) == true);
                Assert.True(stock.Bounds.Bottom <= values.Min(label => label.Bounds.Y));
                string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_MENU_UI_OUTPUT");
                if (!string.IsNullOrEmpty(output) && !string.IsNullOrEmpty(LaunchOptions.ConfiguredGameDirectory))
                {
                    Directory.CreateDirectory(output);
                    using Bitmap bitmap = new(width, height);
                    using BattlePreviewCanvas canvas = new(bitmap);
                    view.Draw(canvas, menu.CreateTheme());
                    bitmap.Save(Path.Combine(output, $"{presentation}-{prefix}-{preview}-{width}.png"), ImageFormat.Png);
                }
            }
        }
    }

    private static IEnumerable<UiElement> Descendants(UiElement element)
    {
        yield return element;
        foreach (UiElement child in element.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
