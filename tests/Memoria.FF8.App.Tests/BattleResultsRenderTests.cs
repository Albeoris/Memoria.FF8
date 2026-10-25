using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleResultsRenderTests
{
    [Fact]
    public void InstalledResultTextAndArtworkRenderEveryPageAtSmallAndLargeViewports()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        using BattleAssets battles = new(assets.GameDirectory);
        LiveMenuCatalog catalog = LiveMenuCatalog.ReadInstalled(assets);
        BattleDefinition definition = new()
        {
            Combatants = [new() { Id = "party-0", Name = "Squall", IsPlayer = true }, new() { Id = "enemy-0", Name = "Enemy" }]
        };
        BattleRewardSummary rewards = new(new Dictionary<byte, ushort> { [0] = 123 }, new Dictionary<byte, ushort>(), new Dictionary<int, ushort> { [0] = 3 }, [new BattleStock(1, 2)], [0])
        {
            CharacterGrowth = [new(0, 6500, 6623, 7, 7, 500, 377)],
            GuardianForceGrowth = Enumerable.Range(0, 16).Select(id => new BattleGuardianForceRewardPresentation(id, 1, (byte)(id % 3 == 0 ? 2 : 1), id == 0 ? [20, 21, 22] : id == 3 ? [] : [20 + id])).ToArray()
        };
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_RESULT_UI_OUTPUT");
        if (output is not null)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "messages.json"), JsonSerializer.Serialize(battles.ReadNativeGlobalBattleMessages().Select((text, id) => new { id, text }).Where(entry => new[] { 6, 21, 22, 23, 24, 28, 29, 30, 31, 48, 49, 109, 110, 111, 121, 126, 127 }.Contains(entry.id)), new JsonSerializerOptions { WriteIndented = true }));
        }
        foreach ((int width, int height) in new[] { (640, 448), (1280, 896) })
        {
            BattleSession session = new(definition, BattleAtbMode.Normal);
            int completions = 0;
            BattleHudController controller = new(session, session.QueueAttack, () => { }, () => { }, () => completions++, previewRewards: () => rewards, guardianForceName: id => catalog.GuardianForceName(id)!, itemName: id => catalog.ItemName(id)!, cardName: id => catalog.CardName(id)!);
            controller.ConfigureResults(battles.ReadNativeGlobalBattleMessages(), id => catalog.ItemDescription(id) ?? "", id => catalog.AbilityName(id) ?? "");
            session.InstantVictory();
            Draw("experience");
            controller.ConfirmResult();
            controller.AdvancePresentation(TimeSpan.FromSeconds(1));
            controller.ConfirmResult();
            controller.ConfirmResult();
            Assert.Equal(BattleResultPage.Items, controller.ResultPage);
            Draw("item");
            controller.ConfirmResult();
            Assert.DoesNotContain("{Dialog", controller.ResultText(6, "", subject: "Potion"));
            Draw("received");
            controller.ConfirmResult();
            Assert.DoesNotContain("{Dialog", controller.ResultItemName(1));
            Draw("card");
            controller.ConfirmResult();
            controller.ConfirmResult();
            Assert.Equal(BattleResultPage.Items, controller.ResultPage);
            controller.AdvancePresentation(TimeSpan.FromSeconds(32.0 / BattleSession.FramesPerSecond + 0.000001));
            Assert.Equal(BattleResultPage.GuardianForce, controller.ResultPage);
            Assert.DoesNotContain("{Dialog", controller.ResultText(109, "", 3));
            Assert.Contains("3", controller.ResultText(109, "", 3));
            Assert.Equal(16, controller.ResultGuardianForceRows.Count);
            Draw("guardian-summary-first");
            Assert.Equal(0, completions);
            controller.ConfirmResult();
            Assert.Equal(BattleResultPage.GuardianForce, controller.ResultPage);
            Assert.Equal(2, controller.ResultGuardianForceRows.Count);
            Draw("guardian-summary-overflow");
            controller.ConfirmResult();
            Assert.Equal(1, completions);

            BattleRewardSummary overflow = rewards with { CharacterExperience = new Dictionary<byte, ushort>(), LootOffers = [new(false, 1, 5, false)] };
            controller = new BattleHudController(session, session.QueueAttack, () => { }, () => { }, previewRewards: () => overflow, itemName: id => catalog.ItemName(id)!);
            controller.ConfigureResults(battles.ReadNativeGlobalBattleMessages(), id => catalog.ItemDescription(id) ?? "");
            controller.AdvancePresentation(TimeSpan.FromSeconds(16.0 / BattleSession.FramesPerSecond + 0.000001));
            controller.ConfirmResult();
            controller.ConfirmResult();
            controller.AdvancePresentation(TimeSpan.FromSeconds(16.0 / BattleSession.FramesPerSecond + 0.000001));
            controller.ConfirmResult();
            Assert.Contains("Potion", controller.ResultText(24, "", subject: "Potion"));
            Draw("overflow");

            void Draw(string name)
            {
                if (controller.ResultOpacity < 1)
                {
                    controller.AdvancePresentation(TimeSpan.FromSeconds((1 - controller.ResultOpacity) * 16 / BattleSession.FramesPerSecond + 0.000001));
                }
                foreach (UiTheme theme in new UiTheme[] { new UiTheme(), new VectorUiTheme() })
                {
                    using Bitmap bitmap = new(width, height, PixelFormat.Format32bppArgb);
                    using (BattlePreviewCanvas canvas = new(bitmap, assets.GameDirectory))
                    {
                        UiView view = controller.BuildView();
                        view.Resize(width, height);
                        view.Draw(canvas, theme);
                    }
                    Assert.Equal(Color.FromArgb(255, 0, 0, 0).ToArgb(), bitmap.GetPixel(0, 0).ToArgb());
                    if (output is not null)
                    {
                        bitmap.Save(Path.Combine(output, $"{(theme is VectorUiTheme ? "vector" : "classic")}-{name}-{width}.png"), ImageFormat.Png);
                    }
                }
            }
        }
    }
}
