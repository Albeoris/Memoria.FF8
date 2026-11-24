using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class BattleLoadoutEditorTests
{
    [Fact]
    public void Back_collapses_weapons_then_publishes_all_characters_once()
    {
        int applies = 0;
        BattleLoadoutOverrides? live = null;
        MenuController menu = Create(draft =>
        {
            applies++;
            live = draft.Overrides;
        });
        Open(menu);

        Select(menu, "limit-Duel");
        menu.Navigate(MenuInput.PageRight);
        Select(menu, "limit-Desperado");
        menu.Navigate(MenuInput.PageLeft);
        Select(menu, "weapon-type-8");

        Assert.Contains(menu.Entries, entry => entry.Id == "weapon-instance-2" && entry.Help.Contains("Laguna", StringComparison.Ordinal));
        Assert.Null(live);

        menu.Navigate(MenuInput.Cancel);

        Assert.Equal("debug-loadouts", menu.ScreenId);
        Assert.Equal("weapon-type-8", menu.SelectedEntry!.Id);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id == "weapon-instance-2");
        Assert.DoesNotContain(menu.Entries, entry => entry.Id is "apply" or "cancel");
        Assert.Null(live);

        menu.Navigate(MenuInput.Cancel);

        Assert.Equal("debug-battle", menu.ScreenId);
        Assert.Equal(1, applies);
        Assert.Equal([BattleLimitFamily.Renzokuken, BattleLimitFamily.Duel], live!.Get(0)!.LimitFamilies);
        Assert.Empty(live.Get(8)!.LimitFamilies!);
    }

    [Fact]
    public void Weapon_types_expand_to_instances_and_base_grade_with_exchange_and_inventory_reuse()
    {
        BattleLoadoutOverrides? live = null;
        MenuController menu = Create(draft => live = draft.Overrides);
        Open(menu);
        Assert.Contains("supports", menu.Entries.Single(entry => entry.Id == "limit-Renzokuken").Help, StringComparison.Ordinal);

        Select(menu, "weapon-type-8");
        Select(menu, "weapon-instance-2");
        Select(menu, "yes");
        Assert.Contains("Requires Gunblade", menu.Entries.Single(entry => entry.Id == "limit-Renzokuken").Help, StringComparison.Ordinal);
        Assert.Contains(Elements(menu.BuildView().Root).OfType<UiLabel>(), label => label.Text == "Machine Gun");
        menu.Navigate(MenuInput.PageRight);
        Assert.Contains(Elements(menu.BuildView().Root).OfType<UiLabel>(), label => label.Text == "Revolver");
        Assert.Contains("Requires Machine gun", menu.Entries.Single(entry => entry.Id == "limit-Desperado").Help, StringComparison.Ordinal);
        menu.Navigate(MenuInput.PageLeft);
        Assert.Null(live);

        Select(menu, "weapon-type-8");
        Select(menu, "weapon-new-30");
        Select(menu, "weapon-type-8");
        Assert.Contains(menu.Entries, entry => entry.Id == "weapon-instance-2" && entry.Help.Contains("from inventory", StringComparison.Ordinal));
        Select(menu, "weapon-instance-2");
        menu.Navigate(MenuInput.Cancel);

        Assert.NotNull(live);
        BattleWeaponInventory inventory = Assert.IsType<BattleWeaponInventory>(live.WeaponInstances);
        Assert.Equal(new BattleWeaponInstance(2, 30, 0), inventory.GetEquipped(0));
        Assert.Equal(new BattleWeaponInstance(1, 0, 8), inventory.GetEquipped(8));
        Assert.Equal(new BattleWeaponInstance(3, 30, 0), inventory.Instances[3]);
    }

    [Fact]
    public void Weapon_rows_distinguish_equipped_instances_spares_and_base_weapons()
    {
        MenuController menu = Create(_ => throw new InvalidOperationException("Unexpected publication."));
        Open(menu);
        menu.Navigate(MenuInput.PageRight);
        Select(menu, "weapon-type-8");
        Select(menu, "weapon-new-30");
        Select(menu, "weapon-type-8");

        UiColor green = new(112, 232, 144);
        AssertRow("weapon-type-8", "Equipped", green);
        AssertRow("weapon-instance-3", "Equipped", green);
        AssertRow("weapon-instance-2", "Inventory", null);
        AssertRow("weapon-new-30", "Base", null);

        menu.Navigate(MenuInput.PageLeft);
        Select(menu, "weapon-type-8");
        AssertRow("weapon-type-8", "", null);
        AssertRow("weapon-instance-3", "", null, new UiImageSource(UiImageKind.CharacterPortrait, 8));
        AssertRow("weapon-instance-2", "Laguna’s inventory", null);

        void AssertRow(string id, string status, UiColor? color, UiImageSource? portrait = null)
        {
            menu.SelectEntry(menu.ScreenId, id);
            UiList list = Assert.IsType<UiList>(menu.BuildView().FocusedElement);
            int row = list.SelectedIndex;
            Assert.Equal(status, list.SecondaryItems[row]);
            Assert.Equal(color, list.ItemTextColor?.Invoke(row));
            Assert.Equal(portrait, list.TrailingImages[row]);
        }
    }

    [Fact]
    public void Limit_rows_show_the_source_character_and_color_assigned_families_by_weapon_support()
    {
        MenuController menu = Create(_ => throw new InvalidOperationException("Unexpected publication."));
        Open(menu);
        Select(menu, "limit-Duel");
        Select(menu, "limit-Shot");
        UiElement[] controls = Elements(menu.BuildView().Root).ToArray();

        foreach (BattleLimitFamily family in Enum.GetValues<BattleLimitFamily>())
        {
            string id = "limit-" + family;
            UiButton button = Assert.Single(controls.OfType<UiButton>(), element => element.Id == id);
            UiImage portrait = Assert.Single(Elements(button).OfType<UiImage>(), image => image.Id == id + "-portrait");
            Assert.Equal(new UiImageSource(UiImageKind.CharacterPortrait, BattleLimitFamilyRules.SourceCharacter(family)), portrait.Source);
            UiColor? expected = family switch
            {
                BattleLimitFamily.Renzokuken or BattleLimitFamily.Duel => new UiColor(112, 232, 144),
                BattleLimitFamily.Shot => new UiColor(232, 184, 96),
                _ => null
            };
            Assert.Equal(expected, Assert.Single(Elements(button).OfType<UiLabel>()).Color);
        }
    }

    [Fact]
    public void Exchange_preview_shows_each_characters_weapon_and_only_newly_unavailable_assigned_limits()
    {
        MenuController menu = Create(_ => throw new InvalidOperationException("Unexpected publication."));
        Open(menu);
        Select(menu, "limit-FireCross");
        Select(menu, "limit-Duel");
        Select(menu, "limit-Shot");
        menu.Navigate(MenuInput.PageRight);
        Select(menu, "limit-Duel");
        Select(menu, "limit-Shot");
        menu.Navigate(MenuInput.PageLeft);
        Select(menu, "weapon-type-8");
        Select(menu, "weapon-instance-2");

        Assert.Equal(new[] { "no", "yes" }, menu.Entries.Select(entry => entry.Id).Order());
        Assert.Equal("no", menu.SelectedEntry!.Id);
        UiElement confirm = Assert.Single(Elements(menu.BuildView().Root), element => element.Id == "loadout-swap-confirm");

        string squall = SwapCardText(confirm, 0);
        Assert.Contains("Squall", squall, StringComparison.Ordinal);
        Assert.Contains("Revolver", squall, StringComparison.Ordinal);
        Assert.Contains("Renzokuken", squall, StringComparison.Ordinal);
        Assert.Contains("Fire Cross / No Mercy", squall, StringComparison.Ordinal);
        Assert.DoesNotContain("Desperado", squall, StringComparison.Ordinal);
        Assert.DoesNotContain("Duel", squall, StringComparison.Ordinal);
        Assert.DoesNotContain("Shot", squall, StringComparison.Ordinal);

        string laguna = SwapCardText(confirm, 8);
        Assert.Contains("Laguna", laguna, StringComparison.Ordinal);
        Assert.Contains("Machine Gun", laguna, StringComparison.Ordinal);
        Assert.Contains("Desperado", laguna, StringComparison.Ordinal);
        Assert.DoesNotContain("Renzokuken", laguna, StringComparison.Ordinal);
        Assert.DoesNotContain("Duel", laguna, StringComparison.Ordinal);
        Assert.DoesNotContain("Shot", laguna, StringComparison.Ordinal);
    }

    [Fact]
    public void Trading_another_characters_inventory_weapon_compares_their_equipped_weapon()
    {
        MenuController menu = Create(_ => throw new InvalidOperationException("Unexpected publication."));
        Open(menu);
        menu.Navigate(MenuInput.PageRight);
        Select(menu, "limit-Renzokuken");
        Select(menu, "weapon-type-0");
        Select(menu, "weapon-new-0");
        menu.Navigate(MenuInput.PageLeft);
        Select(menu, "weapon-type-8");
        Select(menu, "weapon-instance-2");

        UiElement confirm = Assert.Single(Elements(menu.BuildView().Root), element => element.Id == "loadout-swap-confirm");
        Assert.Contains("Renzokuken", SwapCardText(confirm, 0), StringComparison.Ordinal);
        string laguna = SwapCardText(confirm, 8);
        Assert.Contains("Machine Gun", laguna, StringComparison.Ordinal);
        Assert.DoesNotContain("Desperado", laguna, StringComparison.Ordinal);
        Assert.DoesNotContain("Renzokuken", laguna, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(MenuInput.Confirm)]
    [InlineData(MenuInput.Cancel)]
    public void Declining_exchange_preserves_equipment_and_focus_on_the_selected_weapon(MenuInput decline)
    {
        int applies = 0;
        MenuController menu = Create(_ => applies++);
        Open(menu);
        Select(menu, "weapon-type-8");
        Select(menu, "weapon-instance-2");
        Assert.Equal("no", menu.SelectedEntry!.Id);
        if (decline == MenuInput.Cancel)
        {
            menu.Navigate(MenuInput.PageRight);
            menu.Navigate(MenuInput.Left);
            Assert.Equal("yes", menu.SelectedEntry!.Id);
        }

        menu.Navigate(decline);

        Assert.Equal("debug-loadouts", menu.ScreenId);
        UiView view = menu.BuildView();
        Assert.DoesNotContain(Elements(view.Root), element => element.Id == "loadout-swap-confirm");
        Assert.Equal("loadout-weapons", view.FocusedElement!.Id);
        Assert.Equal("weapon-instance-2", menu.SelectedEntry!.Id);
        Assert.Contains("Laguna", menu.SelectedEntry.Help, StringComparison.Ordinal);
        Assert.Contains(Elements(view.Root).OfType<UiLabel>(), label => label.Text == "Revolver");
        menu.Navigate(MenuInput.Cancel);
        menu.Navigate(MenuInput.Cancel);
        Assert.Equal("debug-battle", menu.ScreenId);
        Assert.Equal(0, applies);
    }

    [Fact]
    public void Pending_preparation_keeps_scene_open_and_blocks_edits_until_publication()
    {
        int applies = 0;
        int prepares = 0;
        TaskCompletionSource operation = new(TaskCreationOptions.RunContinuationsAsynchronously);
        MenuController menu = Create(_ => applies++, prepare: (_, characters, progress, _) =>
        {
            prepares++;
            Assert.Equal([0], characters);
            progress.Report((1, 1));
            return operation.Task;
        });
        Open(menu);
        Select(menu, "limit-Duel");
        menu.Navigate(MenuInput.Cancel);
        menu.Navigate(MenuInput.Cancel);
        menu.Execute(new("loadout-toggle", Value: nameof(BattleLimitFamily.Duel)));

        Assert.Equal("debug-loadouts", menu.ScreenId);
        Assert.Empty(menu.Entries);
        Assert.Contains(Elements(menu.BuildView().Root), element => element.Id == "loadout-apply-progress");
        Assert.Equal(0, applies);
        Assert.Equal(1, prepares);

        operation.SetResult();
        menu.BuildView();

        Assert.Equal("debug-battle", menu.ScreenId);
        Assert.Equal(1, applies);
    }

    [Fact]
    public void Unchanged_exit_skips_preparation_and_publication()
    {
        int applies = 0;
        int prepares = 0;
        MenuController menu = Create(_ => applies++, prepare: (_, _, _, _) =>
        {
            prepares++;
            return Task.CompletedTask;
        });
        Open(menu);
        menu.Navigate(MenuInput.Cancel);

        Assert.Equal("debug-battle", menu.ScreenId);
        Assert.Equal(0, prepares);
        Assert.Equal(0, applies);
    }

    [Theory]
    [InlineData("weapon-new-0", new int[] { })]
    [InlineData("weapon-new-30", new[] { 0 })]
    [InlineData("weapon-instance-2", new[] { 0, 8 })]
    public void Weapon_edits_prepare_only_characters_with_a_different_equipped_definition(string choice, int[] expected)
    {
        int applies = 0;
        List<int> prepared = [];
        MenuController menu = Create(_ => applies++, prepare: (draft, characters, _, _) =>
        {
            prepared.AddRange(characters);
            Assert.Equal(30, draft.ResolveWeapon(0));
            Assert.Equal(expected.Length == 2 ? 0 : 30, draft.ResolveWeapon(8));
            return Task.CompletedTask;
        });
        Open(menu);
        Select(menu, choice == "weapon-new-0" ? "weapon-type-0" : "weapon-type-8");
        Select(menu, choice);
        if (choice == "weapon-instance-2")
        {
            Select(menu, "yes");
        }
        menu.Navigate(MenuInput.Cancel);

        Assert.Equal("debug-battle", menu.ScreenId);
        Assert.Equal(1, applies);
        Assert.Equal(expected, prepared);
    }

    [Fact]
    public void Failed_preparation_keeps_the_draft_for_retry()
    {
        int applies = 0;
        bool blocked = true;
        MenuController menu = Create(draft =>
        {
            applies++;
            Assert.Contains(BattleLimitFamily.Duel, draft.ResolveFamilies(0));
        }, prepare: (_, _, _, _) => blocked ? Task.FromException(new InvalidOperationException("Animation unavailable.")) : Task.CompletedTask);
        Open(menu);
        Select(menu, "limit-Duel");

        menu.Navigate(MenuInput.Cancel);

        Assert.Equal("debug-loadouts", menu.ScreenId);
        Assert.Equal("Animation unavailable.", menu.Message);
        Assert.NotEmpty(menu.Entries);
        Assert.Equal(0, applies);

        blocked = false;
        menu.Navigate(MenuInput.Cancel);

        Assert.Equal("debug-battle", menu.ScreenId);
        Assert.Equal(1, applies);
    }

    [Fact]
    public void Dormant_FF8_editor_is_disabled_while_FF7_is_active_and_laboratory_stays_available()
    {
        bool ff7 = true;
        MenuController menu = Create(_ => throw new InvalidOperationException("Unexpected publication."), () => BattleLoadoutLabels.CurrentWorldUnavailableReason(ff7), () => !ff7);
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
        Select(menu, "limit-Duel");
        ff7 = true;
        menu.Navigate(MenuInput.Cancel);

        Assert.Equal("debug-loadouts", menu.ScreenId);
        Assert.Contains("FFVIII is dormant", menu.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Squall_preview_uses_opaque_surfaces_and_subpixel_silhouette_coverage(int weapon)
    {
        using BattleAssets assets = new(TestResources.GameDirectory);
        BattleLoadoutModelPreview preview = BattleLoadoutModelPreview.Prepare(assets, 0, 0, weapon);

        for (int frame = 0; frame < 3; frame++)
        {
            var image = preview.Render(frame / 30f);
            int[] alpha = new int[256];
            foreach (var pixel in image.Pixels)
            {
                alpha[pixel.A]++;
            }

            int filled = image.Pixels.Length - alpha[0];
            Assert.True(alpha[255] > filled * 0.9, $"Squall weapon {weapon}, frame {frame}: {alpha[255]} fully opaque pixels out of {filled} covered pixels.");
            Assert.True(alpha[64] + alpha[128] + alpha[191] > 0, "Expected partially covered pixels along the silhouette.");
            Assert.Equal(filled, alpha[64] + alpha[128] + alpha[191] + alpha[255]);
        }
    }

    [Fact]
    public void Render_editor_scene_when_output_is_requested()
    {
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_LOADOUT_MENU_OUTPUT");
        Assert.SkipUnless(!string.IsNullOrWhiteSpace(output), "Set MEMORIA_FF8_LOADOUT_MENU_OUTPUT for loadout editor previews.");
        string gameDirectory = TestResources.GameDirectory;
        using BattleAssets assets = new(gameDirectory);
        BattleLoadoutEditorWeapon[] weapons = assets.WeaponDefinitions.Select(weapon => new BattleLoadoutEditorWeapon(weapon.Id, weapon.Name, weapon.CharacterId, assets.ReadWeaponProfile(weapon.Id).Capabilities, weapon.Tier)).ToArray();
        BattleLoadoutModelPreview? preview = null;
        MenuController menu = Create(_ => { }, weapons: weapons, preview: (draft, character, _) =>
        {
            preview = BattleLoadoutModelPreview.Prepare(assets, character, 0, draft.ResolveWeapon(character));
            return Task.FromResult(preview);
        });
        Directory.CreateDirectory(output!);
        Open(menu);
        Select(menu, "limit-FireCross");
        Select(menu, "limit-Desperado");
        string[] editorControls = ["loadout-weapons", "loadout-character-model", "limit-Renzokuken-check"];
        Render("weapons-and-limits", editorControls);
        BattleLoadoutModelPreview rendered = Assert.IsType<BattleLoadoutModelPreview>(preview);
        Assert.True(rendered.CurrentFrame >= 0);
        var pose = rendered.Render(0).Pixels.ToArray();
        Assert.Contains(pose, pixel => pixel.A > 0);
        Assert.Contains(Enumerable.Range(1, 8), frame => !pose.AsSpan().SequenceEqual(rendered.Render(frame / 30f).Pixels));
        Select(menu, "weapon-type-8");
        Render("weapon-dropdown", editorControls);
        Select(menu, "weapon-instance-2");
        Render("weapon-exchange", "loadout-swap-confirm", "loadout-swap-character-0", "loadout-swap-character-8");

        void Render(string name, params string[] expectedControls)
        {
            using SkiaSharp.SKBitmap bitmap = new(1280, 720);
            using BattlePreviewCanvas canvas = new(bitmap, gameDirectory);
            UiView view = menu.BuildView();
            view.Resize(bitmap.Width, bitmap.Height);
            UiElement[] controls = Elements(view.Root).ToArray();
            foreach (string id in expectedControls)
            {
                Assert.Contains(controls, control => control.Id == id && !control.Bounds.IsEmpty);
            }
            view.Draw(canvas, menu.CreateTheme());
            bitmap.SavePng(Path.Combine(output!, name + ".png"));
        }
    }

    private static string SwapCardText(UiElement confirm, int characterId)
    {
        UiElement card = Assert.Single(Elements(confirm), element => element.Id == $"loadout-swap-character-{characterId}");
        return string.Join("\n", Elements(card).OfType<UiLabel>().Select(label => label.Text));
    }

    private static IEnumerable<UiElement> Elements(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement element in Elements(child))
            {
                yield return element;
            }
        }
    }

    private static MenuController Create(Action<BattleLoadoutDraft> apply, Func<string?>? boundary = null, Func<bool>? available = null, BattleLoadoutEditorWeapon[]? weapons = null, Func<BattleLoadoutDraft, int, CancellationToken, Task<BattleLoadoutModelPreview>>? preview = null, Func<BattleLoadoutDraft, IReadOnlyList<int>, IProgress<(int Completed, int Total)>, CancellationToken, Task>? prepare = null)
    {
        MenuData data = new();
        data.Characters.Add(new() { Id = "squall", NativeId = 0, Name = "Squall", InParty = true, Level = 17, CurrentHp = 890, MaximumHp = 920 });
        data.Characters.Add(new() { Id = "laguna", NativeId = 8, Name = "Laguna", Level = 16, CurrentHp = 740, MaximumHp = 860 });
        MenuController menu = new(() => data);
        weapons ??= [new(0, "Revolver", 0, BattleWeaponCapabilities.Gunblade, 0), new(30, "Machine Gun", 8, BattleWeaponCapabilities.MachineGun, 0)];
        menu.ConnectBattleLoadoutEditor(
            () => new(new(null, [new(0, 0, [BattleLimitFamily.Renzokuken]), new(8, 30, [BattleLimitFamily.Desperado])], initialWeapons: [0, 30]), [new(0, "Squall"), new(8, "Laguna")], weapons),
            apply, boundary ?? (() => null), available, preview, prepare);
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
