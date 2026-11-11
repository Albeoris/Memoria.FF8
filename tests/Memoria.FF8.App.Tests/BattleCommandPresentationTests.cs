using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Assets.Localization;
using Memoria.FF8.GameUi;
using Memoria.FF8.Resources;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.App.Tests;

public sealed class BattleCommandPresentationTests
{
    [Theory]
    [InlineData(1, "Attack")]
    [InlineData(12, "Mug")]
    public void Smart_physical_caption_uses_its_command_identity_without_changing_the_preview(byte command, string name)
    {
        BattleSession battle = CreateBattle([new(command, name, 0x40, 0)]);
        using InMemoryResourceSource source = new(new("test", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        List<BattleInput> submitted = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: submitted.Add);
        hud.ConfigureLocalization(new(resources));
        hud.Attack();
        BattleSmartAttackPreview preview = hud.TargetActionPreview!;
        Assert.Equal(name, hud.TargetActionCaption);
        string key = LocalizationSourceExporter.IndexedKey("en", "kernel_battlecommands_name", command);
        resources.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [key] = new("Localized " + name, "test", "test") })).Commit();
        Assert.Equal("Localized " + name, hud.TargetActionCaption);
        Assert.Equal(preview, hud.TargetActionPreview);
        hud.Attack();
        Assert.Equal(preview, Assert.Single(submitted).SmartAttackPreview);
        Assert.Contains("Localized " + name, hud.Message);
    }

    [Fact]
    public void Cursor_memory_distinguishes_the_second_native_command_after_dispatch_and_actor_reselection()
    {
        BattleSession battle = CreateBattle([new(1, "Attack", 0x40, 0), new(33, "LV Down", 0x40, 0), new(2, "Magic", 0x40, 0), new(34, "LV Up", 0x40, 0)], secondActor: true);
        List<BattleInput> submitted = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: submitted.Add) { CursorMemory = true };
        hud.Choose(3);
        hud.Choose(0);
        hud.Attack();
        Assert.Equal(34 << 16, Assert.Single(submitted).ActionId);
        hud.SelectParty("second");
        hud.SelectParty("party");
        Assert.Equal(3, hud.ChoiceIndex);
        Assert.Equal("LV Up", hud.ChoiceLabels[hud.ChoiceIndex]);
        hud.Navigate(MenuInput.Confirm);
        Assert.Equal((byte)34, Assert.Single(hud.ActionChoices).NativeAction!.Command);
    }

    [Fact]
    public void Installed_command_catalog_preserves_names_numeric_localization_and_level_command_selection()
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed battle resources are required.");
        using BattleAssets assets = new(TestResources.GameDirectory);
        using InMemoryResourceSource source = new(new("test", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        LocalizationBattlePresentation presentation = new(resources);
        byte[] commands = [1, 2, 3, 4, 6, 7, 12, .. Enumerable.Range(23, 12).Select(command => (byte)command), 38];
        foreach (byte command in commands)
        {
            BattleNativeCommandBinding binding = assets.ReadNativeCommand(command);
            BattleSession battle = CreateBattle([binding], assets.NativeActions.ToArray());
            BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
            hud.ConfigureLocalization(presentation);
            string before = JsonSerializer.Serialize(battle.Capture());
            Assert.Equal(binding.Name, Assert.Single(hud.ChoiceLabels));
            string key = LocalizationSourceExporter.IndexedKey("en", "kernel_battlecommands_name", command);
            resources.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [key] = new("命令 " + command, "test", "test") })).Commit();
            Assert.Equal("命令 " + command, Assert.Single(hud.ChoiceLabels));
            resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
            Assert.Equal(binding.Name, Assert.Single(hud.ChoiceLabels));
            Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
        }
        BattleNativeCommandBinding down = assets.ReadNativeCommand(33);
        BattleNativeCommandBinding up = assets.ReadNativeCommand(34);
        BattleSession combined = CreateBattle([assets.ReadNativeCommand(1), down, assets.ReadNativeCommand(2), up], assets.NativeActions.ToArray());
        BattleHudController combinedHud = new(combined, (_, _) => { }, () => { }, () => { });
        Assert.Equal(new[] { assets.ReadNativeCommand(1).Name, down.Name, assets.ReadNativeCommand(2).Name, up.Name }, combinedHud.ChoiceLabels);
        foreach (int row in new[] { 1, 3 })
        {
            combinedHud.Choose(row);
            BattleActionDefinition action = Assert.Single(combinedHud.ActionChoices);
            BattleNativeCommandBinding expected = row == 1 ? down : up;
            Assert.Equal(expected.Command, action.NativeAction!.Command);
            Assert.Equal((ushort)expected.ActionId, action.NativeAction.ActionId);
            combinedHud.Navigate(MenuInput.Cancel);
        }
    }

    [Theory]
    [InlineData(1, 33)]
    [InlineData(3, 34)]
    public void Native_command_rows_retain_slot_order_and_dispatch_only_the_selected_command(int row, byte command)
    {
        BattleSession battle = CreateBattle([new(1, "Attack", 0x40, 0), new(33, "LV Down", 0x40, 0), new(2, "Magic", 0x40, 0), new(34, "LV Up", 0x40, 0)]);
        List<BattleInput> submitted = [];
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { }, execute: submitted.Add);

        Assert.Equal(new[] { "Attack", "LV Down", "Magic", "LV Up" }, hud.ChoiceLabels);
        Assert.Equal(new[] { BattleAbility.Attack, BattleAbility.NativeCommand, BattleAbility.Magic, BattleAbility.NativeCommand }, hud.Commands);
        hud.Choose(row);

        BattleActionDefinition action = Assert.Single(hud.ActionChoices);
        Assert.Equal(command, action.NativeAction!.Command);
        Assert.DoesNotContain("NativeCommand", hud.Message);
        hud.Choose(0);
        Assert.True(hud.SelectingTarget);
        hud.Attack();

        BattleInput input = Assert.Single(submitted);
        Assert.Equal(BattleAbility.NativeCommand, input.Ability);
        Assert.Equal(command << 16, input.ActionId);
        Assert.Equal("enemy", input.TargetId);
    }

    [Fact]
    public void Restored_collapsed_abilities_recover_distinct_rows_without_mutating_saved_state()
    {
        BattleSession original = CreateBattle([new(12, "Mug", 0x40, 0), new(33, "LV Down", 0x40, 0), new(2, "Magic", 0x40, 0), new(34, "LV Up", 0x40, 0)]);
        BattleSession battle = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(JsonSerializer.Serialize(original.Capture()))!);
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });

        Assert.Equal(new[] { "Mug", "LV Down", "Magic", "LV Up" }, hud.ChoiceLabels);
        Assert.Equal(new[] { 12, 33, 2, 34 }, BattleCommandPresentation.Read(battle.Combatants[0]).Select(command => command.Command));
        hud.BuildView().Draw(new BattleUiTests.TestCanvas(), new Memoria.FF8.Ui.UiTheme());

        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    [Theory]
    [InlineData(1, "Attack")]
    [InlineData(2, "Magic")]
    [InlineData(3, "GF")]
    [InlineData(4, "Item")]
    [InlineData(6, "Draw")]
    [InlineData(12, "Mug")]
    [InlineData(33, "LV Down")]
    [InlineData(34, "LV Up")]
    public void Command_names_follow_numeric_localization_identity_and_hot_reload_with_native_fallback(byte command, string name)
    {
        using InMemoryResourceSource source = new(new("test", GameDataEdition.Classic, [], "en"));
        ResourceService resources = new(source);
        LocalizationBattlePresentation presentation = new(resources);
        BattleSession battle = CreateBattle([new(command, name, 0x40, 0)]);
        BattleHudController hud = new(battle, (_, _) => { }, () => { }, () => { });
        hud.ConfigureLocalization(presentation);
        string before = JsonSerializer.Serialize(battle.Capture());
        string key = LocalizationSourceExporter.IndexedKey("en", "kernel_battlecommands_name", command);
        Assert.Equal(name, Assert.Single(hud.ChoiceLabels));

        resources.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [key] = new("å‘½ä»¤ " + command, "test", "test") })).Commit();
        Assert.Equal("å‘½ä»¤ " + command, Assert.Single(hud.ChoiceLabels));
        resources.Prepare(null, localization: new(new Dictionary<string, LocalizationValue> { [key] = new("ÐšÐ¾Ð¼Ð°Ð½Ð´Ð° " + command, "test", "test") })).Commit();
        Assert.Equal("ÐšÐ¾Ð¼Ð°Ð½Ð´Ð° " + command, Assert.Single(hud.ChoiceLabels));
        resources.Prepare(null, localization: LocalizationCatalog.Empty).Commit();
        Assert.Equal(name, Assert.Single(hud.ChoiceLabels));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));
    }

    private static BattleSession CreateBattle(BattleNativeCommandBinding[] commands, BattleNativeAction[]? nativeActions = null, bool secondActor = false)
    {
        return new(new BattleDefinition
        {
            Combatants =
            [
                new() { Id = "party", Name = "Party", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum, Abilities = commands.Select(command => BattleCommandPresentation.AbilityFor(command.Command)).Distinct().ToArray(), NativeCommands = [.. commands], NativeCommandSlots = [.. commands.Select(command => command.Command).Concat(new byte[4]).Take(4)] },
                .. (secondActor ? new BattleCombatantDefinition[] { new() { Id = "second", Name = "Second", IsPlayer = true, InitialAtb = BattleSession.AtbMaximum } } : []),
                new() { Id = "enemy", Name = "Enemy" }
            ],
            NativeActions = nativeActions ?? [NativeAction(33, "LV Down"), NativeAction(34, "LV Up")]
        }, BattleAtbMode.TurnBased);
    }

    private static BattleNativeAction NativeAction(byte command, string name)
    {
        return new(command, 0, 10, command)
        {
            Name = name,
            MagicId = 0,
            AttackType = 0,
            Power = 0,
            HitCount = 1,
            TargetFlags = 0x40,
            AttackFlags = 0,
            DamagePresentation = 0,
            Element = 0,
            ElementPercent = null,
            StatusAttack = 0,
            Statuses0 = 0,
            Statuses1 = 0,
            Hit = null,
            Critical = null,
            PowerModifier = null,
            LevelModifier = null,
            RawRecord = [],
            SupplementalRecord = []
        };
    }
}
