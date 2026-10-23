using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleGuardianForceLearningRenderTests
{
    [Theory]
    [InlineData(640, 448, MenuPresentation.Classic)]
    [InlineData(1280, 896, MenuPresentation.Classic)]
    [InlineData(640, 448, MenuPresentation.Vector)]
    [InlineData(1280, 896, MenuPresentation.Vector)]
    public void InstalledCompletedAbilityReviewRendersWithAndWithoutItsCursor(int width, int height, MenuPresentation presentation)
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original resources are required for GF learning menu rendering.");
        using GameAssets assets = new(directory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
        string preserved = JsonSerializer.Serialize(source);
        NativeGuardianForceAbility target = source.GuardianForceAbilities(0).First(ability => !ability.Learned && ability.Ability != 0 && ability.RequiredPoints > 0);
        NativeCharacterVitals completed = source.SelectGuardianForceLearning(0, target.Ability).AwardGuardianForceAbilityPoints(0, checked((ushort)(target.RequiredPoints - target.Progress)));
        Assert.True(completed.GuardianForces[0].LearnedBank.Has(target.Ability));
        string abilityId = target.Ability.ToString(CultureInfo.InvariantCulture);
        MenuController menu = new(() =>
        {
            MenuData data = LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = completed }, catalog: catalog);
            data.GuardianForces.RemoveAll(force => force.Id != "gf-0");
            return data;
        }) { Presentation = presentation };
        menu.OpenBattleLearning(new Dictionary<string, string> { ["gf-0"] = abilityId });
        Assert.Equal(abilityId, menu.SelectedEntry?.Id);
        Assert.False(menu.SelectedEntry!.Enabled);
        Draw("learned-cursor");

        menu.Navigate(MenuInput.Down);

        Assert.NotEqual(abilityId, menu.SelectedEntry?.Id);
        Draw("moved-cursor");
        Assert.Equal(preserved, JsonSerializer.Serialize(source));

        void Draw(string state)
        {
            UiView view = menu.BuildView();
            view.Resize(width, height);
            UiList list = Assert.IsType<UiList>(view.FocusedElement);
            int learnedIndex = menu.Entries.ToList().FindIndex(entry => entry.Id == abilityId);
            Assert.True(learnedIndex >= 0);
            Assert.Equal("Complete!", list.SecondaryItems[learnedIndex]);
            Assert.Equal(catalog.AbilityName(target.Ability), list.Items[learnedIndex]);
            Assert.All(Enumerable.Range(0, list.Items.Count), index =>
            {
                UiColor? color = list.ItemTextColor!(index);
                Assert.Equal(index == learnedIndex, color.HasValue);
                if (color is { } readable)
                {
                    Assert.InRange(readable.R, (byte)160, byte.MaxValue);
                    Assert.InRange(readable.G, (byte)160, byte.MaxValue);
                    Assert.InRange(readable.B, (byte)160, byte.MaxValue);
                }
            });
            using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
            using (BattlePreviewCanvas canvas = new(bitmap, directory))
            {
                canvas.BeginFrame(TimeSpan.Zero);
                view.Draw(canvas, menu.CreateTheme());
            }
            string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RESULT_UI_OUTPUT");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                bitmap.Save(Path.Combine(output, $"{presentation.ToString().ToLowerInvariant()}-gf-learning-{state}-{width}.png"), ImageFormat.Png);
            }
        }
    }
}
