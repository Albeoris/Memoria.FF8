using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleLoadoutEditorTests
{
    [Fact]
    public void Cancel_discards_staged_changes_and_apply_publishes_exactly_once()
    {
        int applies = 0;
        BattleLoadoutOverrides? live = null;
        MenuController menu = Create(() => live, draft =>
        {
            applies++;
            live = draft.Overrides;
        });
        Open(menu);
        menu.Execute(new("loadout-character", Value: "0"));
        menu.Execute(new("loadout-remove-all"));
        Assert.Null(live);
        menu.Navigate(MenuInput.Cancel);
        Select(menu, "cancel");
        Assert.Equal(0, applies);
        Open(menu);
        menu.Execute(new("loadout-character", Value: "0"));
        menu.Execute(new("loadout-remove-all"));
        menu.Navigate(MenuInput.Cancel);
        Select(menu, "apply");
        Assert.Equal(1, applies);
        Assert.Empty(live!.Get(0)!.LimitFamilies!);
        Assert.Contains("Save the game", menu.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Keyboard_family_catalog_supports_three_families_reorder_and_atomic_exchange_review()
    {
        BattleLoadoutOverrides? live = null;
        MenuController menu = Create(() => live, draft => live = draft.Overrides);
        Open(menu);
        menu.Execute(new("loadout-character", Value: "0"));
        Select(menu, "families");
        Select(menu, nameof(BattleLimitFamily.Duel));
        Select(menu, "toggle");
        menu.Navigate(MenuInput.Cancel);
        Select(menu, nameof(BattleLimitFamily.Combine));
        Select(menu, "toggle");
        Select(menu, "up");
        menu.Navigate(MenuInput.Cancel);
        menu.Navigate(MenuInput.Cancel);
        menu.Execute(new("loadout-exchange", Value: "weapons"));
        Assert.False(menu.Entries.Single(entry => entry.Id == "exchange-0").Enabled);
        Select(menu, "exchange-8");
        menu.Navigate(MenuInput.Cancel);
        Assert.Contains(menu.CurrentScreen.Details(menu), line => line.Contains("Changed: Squall, Laguna", StringComparison.Ordinal));
        Select(menu, "apply");
        Assert.Equal(33, live!.Get(0)!.WeaponId);
        Assert.Equal(0, live.Get(8)!.WeaponId);
        Assert.Equal([BattleLimitFamily.Renzokuken, BattleLimitFamily.Combine, BattleLimitFamily.Duel], live.Get(0)!.LimitFamilies!);
    }

    [Fact]
    public void Boundary_disables_apply_without_discarding_the_draft_and_reset_is_staged()
    {
        BattleLoadoutOverrides? live = null;
        bool blocked = true;
        MenuController menu = Create(() => live, draft => live = draft.Overrides, () => blocked ? "Battle is active." : null);
        Open(menu);
        menu.Execute(new("loadout-character", Value: "0"));
        menu.Execute(new("loadout-remove-all"));
        menu.Execute(new("loadout-reset"));
        menu.Navigate(MenuInput.Cancel);
        Assert.False(menu.Entries.Single(entry => entry.Id == "apply").Enabled);
        Assert.Null(live);
        blocked = false;
        Select(menu, "apply");
        Assert.NotNull(live);
        Assert.Empty(live.Characters);
    }

    [Fact]
    public void Render_editor_pages_when_output_is_requested()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_MENU_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_LOADOUT_MENU_OUTPUT for loadout editor previews.");
        string gameDirectory = TestResources.GameDirectory;
        MenuController menu = Create(() => null, _ => { });
        Directory.CreateDirectory(output!);
        Open(menu);
        Render("roster");
        menu.Execute(new("loadout-character", Value: "0"));
        Render("character");
        Select(menu, "families");
        Render("families");
        Select(menu, nameof(BattleLimitFamily.Duel));
        Render("family-actions");
        menu.Navigate(MenuInput.Cancel);
        menu.Navigate(MenuInput.Cancel);
        Select(menu, "weapon");
        Render("weapons");

        void Render(string name)
        {
            using SkiaSharp.SKBitmap bitmap = new(1280, 720);
            using BattlePreviewCanvas canvas = new(bitmap, gameDirectory);
            UiView view = menu.BuildView();
            view.Resize(bitmap.Width, bitmap.Height);
            view.Draw(canvas, menu.CreateTheme());
            bitmap.SavePng(Path.Combine(output!, name + ".png"));
        }
    }

    private static MenuController Create(Func<BattleLoadoutOverrides?> read, Action<BattleLoadoutDraft> apply, Func<string?>? boundary = null)
    {
        MenuController menu = new(() => MenuDemoData.Create());
        menu.ConnectBattleLoadoutEditor(() => new(new(read(), [new(0, 0, [BattleLimitFamily.Renzokuken]), new(8, 33, [BattleLimitFamily.Desperado])]), [new(0, "Squall", "Squall"), new(8, "Laguna", "Laguna")], [new(0, "Revolver", "Squall", "Gunblade"), new(33, "Machine Gun", "Laguna", "MachineGun")]), apply, boundary ?? (() => null), (draft, character) => ["Weapon: " + draft.ResolveWeapon(character), string.Join(", ", draft.ResolveFamilies(character))]);
        return menu;
    }

    private static void Open(MenuController menu)
    {
        menu.Open("debug-battle");
        Select(menu, "weapons-limits");
    }

    private static void Select(MenuController menu, string entry)
    {
        menu.SelectEntry(menu.ScreenId, entry);
        menu.Navigate(MenuInput.Confirm);
    }
}
