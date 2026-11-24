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
        Assert.Contains("Requires Gunblade", menu.Entries.Single(entry => entry.Id == "limit-Renzokuken").Help, StringComparison.Ordinal);
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
        Render("weapons-and-limits");
        BattleLoadoutModelPreview rendered = Assert.IsType<BattleLoadoutModelPreview>(preview);
        Assert.True(rendered.CurrentFrame >= 0);
        var pose = rendered.Render(0).Pixels.ToArray();
        Assert.Contains(pose, pixel => pixel.A > 0);
        Assert.Contains(Enumerable.Range(1, 8), frame => !pose.AsSpan().SequenceEqual(rendered.Render(frame / 30f).Pixels));
        Select(menu, "weapon-type-8");
        Render("weapon-dropdown");

        void Render(string name)
        {
            using SkiaSharp.SKBitmap bitmap = new(1280, 720);
            using BattlePreviewCanvas canvas = new(bitmap, gameDirectory);
            UiView view = menu.BuildView();
            view.Resize(bitmap.Width, bitmap.Height);
            UiElement[] controls = Elements(view.Root).ToArray();
            Assert.Contains(controls, control => control.Id == "loadout-weapons" && !control.Bounds.IsEmpty);
            Assert.Contains(controls, control => control.Id == "loadout-character-model" && !control.Bounds.IsEmpty);
            Assert.Contains(controls, control => control.Id == "limit-Renzokuken-check" && !control.Bounds.IsEmpty);
            view.Draw(canvas, menu.CreateTheme());
            bitmap.SavePng(Path.Combine(output!, name + ".png"));
        }
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
