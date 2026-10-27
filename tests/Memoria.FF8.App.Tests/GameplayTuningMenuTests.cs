using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class GameplayTuningMenuTests
{
    [Fact]
    public void Debug_room_has_one_entry_and_three_compact_peer_pages()
    {
        MenuController menus = Create(out _);
        Assert.Single(menus.Entries, row => row.Id == "gameplay-rules");
        Open(menus);
        Assert.Equal(new[] { "DrawBonus", "ApMultiplier", "ExperienceMultiplier", "GuardianExperienceMultiplier", "previous", "next", "apply", "cancel" }, menus.Entries.Select(row => row.Id));
        menus.Execute(new("gameplay-rule-page", Value: "1"));
        Assert.Contains(menus.Entries, row => row.Id == "UnlimitedMagic" && row.Label.Contains("Infinite magic"));
        Assert.Contains(menus.Entries, row => row.Id == "MaximumDamage");
        menus.Execute(new("gameplay-rule-page", Value: "1"));
        Assert.Contains(menus.Entries, row => row.Id == "First");
        Assert.Contains(menus.Entries, row => row.Id == "UnrestrictedScanRotation");
        Assert.DoesNotContain(menus.Entries, row => row.Label.Contains("Save anywhere", StringComparison.OrdinalIgnoreCase));
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal("debug-room", menus.ScreenId);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("100000", 100000)]
    public void Keyboard_keypad_edit_reaches_damage_boundaries_and_applies_atomically(string input, int expected)
    {
        GameplayTuning saved = new();
        int writes = 0;
        MenuController menus = new();
        menus.ConnectGameplayTuning(() => saved, value =>
        {
            saved = value;
            writes++;
        });
        menus.Open("debug-room", true);
        Open(menus);
        menus.Execute(new("gameplay-rule-page", Value: "1"));
        Edit(menus, "MaximumDamage", input);
        Assert.Equal(9999, saved.MaximumDamage);
        Assert.Equal(0, writes);
        menus.Execute(new("gameplay-rule-apply"));
        Assert.Equal(expected, saved.MaximumDamage);
        Assert.Equal(1, writes);
    }

    [Theory]
    [InlineData("100001")]
    [InlineData("1.5")]
    [InlineData("")]
    public void Invalid_number_stays_in_editor_and_cancel_preserves_value(string input)
    {
        MenuController menus = Create(out var read);
        Open(menus);
        menus.Execute(new("gameplay-rule-page", Value: "1"));
        Edit(menus, "MaximumDamage", input);
        Assert.Equal("debug-gameplay-number", menus.ScreenId);
        Assert.NotEmpty(menus.Message);
        menus.Navigate(MenuInput.Cancel);
        menus.Execute(new("gameplay-rule-apply"));
        Assert.Equal(9999, read().MaximumDamage);
    }

    [Fact]
    public void Rare_item_preserves_exact_decimals_and_requires_total_before_any_apply()
    {
        MenuController menus = Create(out var read);
        Open(menus);
        menus.Execute(new("gameplay-rule-page", Value: "-1"));
        Edit(menus, "First", "0.390625");
        Assert.False(menus.Entries.Single(row => row.Id == "apply").Enabled);
        menus.Execute(new("gameplay-rule-apply"));
        Assert.Equal("debug-gameplay-rules", menus.ScreenId);
        Assert.Equal(50m, read().RareItem.First);
        Edit(menus, "Second", "99.609375");
        Edit(menus, "Third", "0");
        Assert.True(menus.Entries.Single(row => row.Id == "apply").Enabled);
        menus.Execute(new("gameplay-rule-apply"));
        Assert.Equal(0.390625m, read().RareItem.First);
        Assert.Equal(99.609375m, read().RareItem.Second);
    }

    [Fact]
    public void Keypad_and_cancel_work_without_text_input_and_discard_whole_draft()
    {
        MenuController menus = Create(out var read);
        Open(menus);
        menus.Execute(new("gameplay-rule-edit", "DrawBonus"));
        menus.Execute(new("gameplay-rule-number", Value: "Clear"));
        foreach (string key in new[] { "1", "0", "0", "Accept" })
        {
            menus.SelectEntry("debug-gameplay-number", key);
            menus.Navigate(MenuInput.Confirm);
        }
        Assert.Contains(menus.Entries, row => row.Id == "DrawBonus" && row.Label.Contains("100"));
        menus.Navigate(MenuInput.Cancel);
        Assert.Equal(0, read().DrawBonus);
        Open(menus);
        Assert.Contains(menus.Entries, row => row.Id == "DrawBonus" && row.Label.EndsWith("0"));
    }

    [Fact]
    public void Policy_shows_effective_values_and_locks_forced_fields()
    {
        GameplayTuning tuning = new() { MaximumDamage = 100000 };
        MenuController menus = new();
        menus.ConnectGameplayTuning(() => tuning, value => tuning = value, field => field == "MaximumDamage" ? "Required by an enabled mod." : null, () => "Current game. Battle changes take effect next battle.");
        menus.Open("debug-room", true);
        Open(menus);
        menus.Execute(new("gameplay-rule-page", Value: "1"));
        MenuEntry locked = menus.Entries.Single(row => row.Id == "MaximumDamage");
        Assert.False(locked.Enabled);
        Assert.Contains("100000", locked.Label);
        Assert.Contains("enabled mod", locked.Help);
        menus.Execute(new("gameplay-rule-edit", "MaximumDamage"));
        Assert.Equal("debug-gameplay-rules", menus.ScreenId);
    }

    [Fact]
    public void Apply_uses_connected_session_pipeline_and_displays_rejection_without_losing_draft()
    {
        MenuController menus = new();
        int attempts = 0;
        menus.ConnectGameplayTuning(() => new(), _ =>
        {
            attempts++;
            throw new InvalidOperationException("Battle change is pending.");
        });
        menus.Open("debug-room", true);
        Open(menus);
        Edit(menus, "ApMultiplier", "100");
        menus.Execute(new("gameplay-rule-apply"));
        Assert.Equal(1, attempts);
        Assert.Equal("debug-gameplay-rules", menus.ScreenId);
        Assert.Equal("Battle change is pending.", menus.Message);
        Assert.Contains(menus.Entries, row => row.Id == "ApMultiplier" && row.Label.Contains("100"));
    }

    [Fact]
    public void Keypad_has_enabled_named_controls_in_both_presentations()
    {
        MenuController menus = Create(out _);
        Open(menus);
        menus.Execute(new("gameplay-rule-edit", "DrawBonus"));
        foreach (MenuPresentation presentation in Enum.GetValues<MenuPresentation>())
        {
            menus.Presentation = presentation;
            UiView view = menus.BuildView();
            Assert.NotNull(view.FocusedElement);
            Assert.Contains(menus.Entries, row => row.Id == "Accept" && row.Enabled);
            Assert.False(menus.Entries.Single(row => row.Id == ".").Enabled);
        }
    }

    [Fact]
    public void Infinite_magic_display_replaces_limited_capacity_without_hiding_stock()
    {
        MenuMagic magic = new() { UnlimitedUses = true, Quantity = 42, RemainingUses = int.MaxValue, Capacity = int.MaxValue };
        Assert.Equal("∞  42", magic.StockPresentation);
        MenuController menus = new();
        menus.Open("new-game-options", true);
        Assert.Equal("Limited magic", menus.Entries.Single(row => row.Id == "Infinite magic").Label);
    }

    private static MenuController Create(out Func<GameplayTuning> read)
    {
        GameplayTuning saved = new();
        MenuController menus = new();
        menus.ConnectGameplayTuning(() => saved, value => saved = value);
        read = () => saved;
        menus.Open("debug-room", true);
        return menus;
    }

    private static void Open(MenuController menus)
    {
        menus.SelectEntry("debug-room", "gameplay-rules");
        menus.Navigate(MenuInput.Confirm);
        Assert.Equal("debug-gameplay-rules", menus.ScreenId);
    }

    private static void Edit(MenuController menus, string field, string input)
    {
        menus.Execute(new("gameplay-rule-edit", field));
        menus.Execute(new("gameplay-rule-number", Value: "Clear"));
        foreach (char character in input)
        {
            Assert.True(menus.EnterGameplayNumberCharacter(character));
        }
        menus.Execute(new("gameplay-rule-number", Value: "Accept"));
    }
}
