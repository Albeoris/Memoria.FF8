using Memoria.FF8.Assets;
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
        Assert.Equal(nameof(BattleLimitFamily.Duel), menu.SelectedEntry!.Id);
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
    public void Dormant_FF8_editor_is_disabled_while_FF7_is_active_and_laboratory_stays_available()
    {
        bool ff7 = true;
        BattleLoadoutOverrides? live = null;
        MenuController menu = Create(() => live, draft => live = draft.Overrides, () => BattleLoadoutLabels.CurrentWorldUnavailableReason(ff7), () => !ff7);
        menu.BattleDebugRequested += () => { };
        menu.Open("debug-battle");
        MenuEntry liveEditor = menu.Entries.Single(entry => entry.Id == "weapons-limits");
        Assert.False(liveEditor.Enabled);
        Assert.Contains("FFVIII is dormant", liveEditor.Help, StringComparison.Ordinal);
        Assert.True(menu.Entries.Single(entry => entry.Id == "battle-laboratory").Enabled);
        menu.Execute(new("loadout-open"));
        Assert.Contains("FFVIII is dormant", menu.Message, StringComparison.Ordinal);
        ff7 = false;
        Open(menu);
        menu.Execute(new("loadout-character", Value: "0"));
        menu.Execute(new("loadout-remove-all"));
        menu.Navigate(MenuInput.Cancel);
        ff7 = true;
        menu.Execute(new("loadout-apply"));
        Assert.Null(live);
        Assert.Contains("FFVIII is dormant", menu.Message, StringComparison.Ordinal);
        menu.Execute(new("loadout-cancel"));
        Assert.Equal("debug-battle", menu.ScreenId);
    }

    [Fact]
    public void Render_editor_pages_when_output_is_requested()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_MENU_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_LOADOUT_MENU_OUTPUT for loadout editor previews.");
        string gameDirectory = TestResources.GameDirectory;
        using BattleAssets assets = new(gameDirectory);
        BattleLoadoutEditorWeapon[] weapons = assets.WeaponDefinitions.Select(weapon => new BattleLoadoutEditorWeapon(weapon.Id, weapon.Name, LiveMenuData.CharacterName(new(), checked((byte)weapon.CharacterId), null, null), "Fighting style: " + BattleLoadoutLabels.Style(weapon.CharacterId), BattleLoadoutLabels.Style(weapon.CharacterId))).ToArray();
        MenuController menu = Create(() => null, _ => { }, weapons: weapons);
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
        Assert.Equal(weapons.Length, menu.Entries.Count);
        HashSet<string> visited = [];
        for (int index = 0; index < weapons.Length; index++)
        {
            visited.Add(menu.SelectedEntry!.Id);
            if (index + 1 < weapons.Length)
            {
                menu.Navigate(MenuInput.Down);
            }
        }
        Assert.Equal(weapons.Select(weapon => "weapon-" + weapon.WeaponId).Order(), visited.Order());
        Render("weapons-scrolled");
        Assert.Contains(menu.CurrentScreen.Details(menu), line => line == "Weapon: Revolver");

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

    private static MenuController Create(Func<BattleLoadoutOverrides?> read, Action<BattleLoadoutDraft> apply, Func<string?>? boundary = null, Func<bool>? available = null, BattleLoadoutEditorWeapon[]? weapons = null)
    {
        MenuController menu = new(() => MenuDemoData.Create());
        menu.ConnectBattleLoadoutEditor(() => new(new(read(), [new(0, 0, [BattleLimitFamily.Renzokuken]), new(8, 33, [BattleLimitFamily.Desperado])]), [new(0, "Squall", "Squall"), new(8, "Laguna", "Laguna")], weapons ?? [new(0, "Revolver", "Squall", "Gunblade", "Gunblade"), new(33, "Machine Gun", "Laguna", "Machine gun", "Machine gun")]), apply, boundary ?? (() => null), (draft, character) => ["Weapon: " + (draft.ResolveWeapon(character) == 0 ? "Revolver" : "Machine Gun"), string.Join(", ", draft.ResolveFamilies(character))], available: available, familyStatus: (_, _, family) => BattleLoadoutLabels.CanonicalFacts(new() { LimitProgress = new(new string('0', 64), 3, 1, 0x11, 0, [0, 0, 0, 0, 0, 0, 0, 0]), SavedBattleFlags = new(NativeInputConfiguration.OriginalInitHash, 0) }, family));
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
