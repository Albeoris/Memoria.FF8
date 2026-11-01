using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8.App.Tests;

public sealed class JunctionDashboardInteractionTests
{
    [Theory]
    [InlineData(false, 1280, 720, 100)]
    [InlineData(true, 1280, 720, 100)]
    [InlineData(false, 640, 448, 100)]
    [InlineData(true, 640, 448, 100)]
    [InlineData(false, 1280, 720, 150)]
    [InlineData(true, 1280, 720, 150)]
    public void GuardianCapabilitiesUseOneMeasuredColumnFlowWithChangedAbilitiesFirst(bool removing, int width, int height, int textScale)
    {
        using Fixture fixture = new();
        fixture.Vitals = fixture.Vitals.AcquireGuardianForce(1);
        foreach (int ability in Enumerable.Range(39, 19))
        {
            fixture.Vitals = fixture.Vitals.TeachGuardianForceAbility(1, ability);
        }
        if (removing)
        {
            fixture.Vitals = fixture.Vitals.ToggleGuardianForceJunction(0, 1);
        }
        fixture.Refresh();
        fixture.Data.Settings["Text scale"] = textScale + "%";
        fixture.Menu.PrepareViewport(width, height);
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Menu.SelectEntry("junction-dashboard", "gf-portrait-gf-1");
        UiView view = fixture.Menu.BuildView();
        UiColumnPicker columns = Assert.Single(Descendants(view.Root).OfType<UiColumnPicker>());
        Assert.Equal("gf-capability-columns", columns.Id);
        Assert.Empty(Descendants(columns).OfType<UiList>());
        Assert.True(columns.Children.Count > columns.RowCount);
        UiButton[] rows = columns.Children.Cast<UiButton>().ToArray();
        string prefix = removing ? "− " : "+ ";
        int changed = rows.TakeWhile(row => row.Children.OfType<UiLabel>().First().Text.StartsWith(prefix, StringComparison.Ordinal)).Count();
        Assert.Equal(19, changed);
        Assert.All(rows.Skip(changed), row => Assert.False(row.Children.OfType<UiLabel>().First().Text.StartsWith(prefix, StringComparison.Ordinal)));
        foreach (UiButton row in rows)
        {
            UiImage icon = Assert.Single(row.Children.OfType<UiImage>());
            Assert.Equal(NativeAbilityMenuMetadata.Icon(row.Id!.StartsWith("gf-capability-command-", StringComparison.Ordinal) ? 20 : 39), icon.Source);
        }
        UiButton[] visible = rows.Where(row => row.Visible).ToArray();
        Assert.All(visible, row => Assert.True(row.Bounds.Right <= columns.Bounds.Right));
        Assert.Equal(16 * textScale / 100f, rows[columns.RowCount].Bounds.X - rows[0].Bounds.Right, 3);
        Assert.True(view.Focus(rows[0]));
        Assert.True(view.Key(UiKey.Right));
        Assert.Same(rows[columns.RowCount], view.FocusedElement);
        UiRect dock = columns.Bounds;
        fixture.Menu.HandleJunctionWheel((int)(dock.X + dock.Width / 2), (int)(dock.Y + dock.Height / 2), -1);
        Assert.True(columns.FirstVisibleColumn > 0);
        fixture.Menu.HandleJunctionWheel((int)(dock.X + dock.Width / 2), (int)(dock.Y + dock.Height / 2), 1);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            foreach (MenuPresentation presentation in new[] { MenuPresentation.Classic, MenuPresentation.Vector })
            {
                fixture.Menu.Presentation = presentation;
                using SKBitmap bitmap = new(width, height);
                using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
                canvas.BeginFrame(TimeSpan.Zero);
                fixture.Menu.BuildView().Draw(canvas, presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme());
                bitmap.SavePng(Path.Combine(directory, $"guardian-capabilities-{(removing ? "remove" : "add")}-{presentation}-{width}x{height}-text{textScale}.png"));
            }
        }
    }

    [Theory]
    [InlineData("Elem-Atk", 9)]
    [InlineData("ST-Atk", 10)]
    [InlineData("Elem-Def 1", 11)]
    [InlineData("ST-Def 1", 15)]
    public void Effect_picker_only_lists_stock_with_the_selected_native_junction_mask(string slot, int index)
    {
        using Fixture fixture = new();
        fixture.Hit("slot-" + slot);
        BattleNativePlayerStatRules rules = fixture.Catalog.ReadStatRules(fixture.Vitals, 0);
        Assert.Contains(fixture.Menu.Entries, entry => entry.Id == "candidate-none");
        string[] candidates = fixture.Menu.Entries.Select(entry => entry.Id).Where(id => id.StartsWith("candidate-magic-0-", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(candidates);
        foreach (string candidate in candidates)
        {
            int spell = int.Parse(candidate.AsSpan("candidate-magic-0-".Length));
            Assert.True(NativeJunctionMagicEligibility.HasContribution(rules.MagicRecords[spell].AsSpan(), index));
        }
        Assert.Equal(0, fixture.Actions);
    }

    [Fact]
    public void ClickingACandidateCommitsImmediatelyWithoutSearchOrConfirmationButtons()
    {
        using Fixture fixture = new();
        fixture.Hit("slot-Str");
        Assert.DoesNotContain(fixture.Menu.Entries, entry => entry.Id is "junction-search" or "junction-sort" or "apply-junction-preview" or "cancel-junction-preview");
        fixture.Hit("candidate-magic-0-2");
        Assert.Equal(1, fixture.Actions);
        Assert.Equal(2, fixture.Vitals.Records[0].Junction.MagicJunctions[1]);
        fixture.Menu.Navigate(MenuInput.Alternate);
        Assert.Contains(fixture.Menu.Entries, entry => entry.Id == "optimize" && entry.Enabled);
    }

    [Theory]
    [InlineData("fill-commands")]
    [InlineData("clear-magic")]
    [InlineData("remove-junctions")]
    [InlineData("clear-party-magic")]
    public void CompoundReviewRestoresItsExactOperationWithoutApplyingIt(string control)
    {
        using Fixture fixture = new();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Menu.Navigate(MenuInput.Alternate);
        fixture.Hit(control);
        MenuNavigationState saved = fixture.Menu.CaptureNavigation();
        Assert.Equal("junction-review", fixture.Menu.BuildView().FocusedElement?.Id);
        fixture.Menu.Navigate(MenuInput.Cancel);
        fixture.Menu.RestoreNavigation(saved);
        Assert.Equal("junction-review", fixture.Menu.BuildView().FocusedElement?.Id);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);
        fixture.Apply();
    }

    [Fact]
    public void ForcedCloseDiscardsCandidatePreviewAndClosedControlsCannotDispatch()
    {
        using Fixture fixture = new();
        string before = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Hit("slot-Str");
        fixture.Preview("candidate-magic-0-2");
        fixture.Menu.Close();
        Assert.Throws<InvalidOperationException>(() => fixture.Menu.SelectEntry("junction-dashboard", "slot-Str"));
        fixture.Hit("candidate-magic-0-2");
        Assert.Equal(0, fixture.Actions);
        Assert.Equal(before, JsonSerializer.Serialize(fixture.Vitals));
    }

    [Fact]
    public void BasicActionModeIsEditableAndUndoPreservesCastingHistory()
    {
        using Fixture fixture = new(smart: true);
        string history = JsonSerializer.Serialize(fixture.Vitals.MagicSystem.PersonalFieldMasks);
        fixture.Hit("basic-action");
        fixture.Hit("smart-mode");
        Assert.Equal(MagicAttackMode.SmartMagic, fixture.Vitals.MagicSystem.AttackModes[0]);
        fixture.Menu.Navigate(MenuInput.Alternate);
        Assert.Contains(fixture.Menu.Entries, entry => entry.Id == "junction-undo" && entry.Enabled);
        fixture.Hit("junction-undo");
        Assert.Equal(MagicAttackMode.PhysicalOnly, fixture.Vitals.MagicSystem.AttackModes[0]);
        Assert.Equal(history, JsonSerializer.Serialize(fixture.Vitals.MagicSystem.PersonalFieldMasks));
    }

    [Fact]
    public void CandidatePagingAndWarmPreviewsKeepSourceAndCastingHistoryUnchangedWithoutSearch()
    {
        using Fixture fixture = new(shared: true);
        fixture.Vitals = fixture.Vitals with { MagicSystem = fixture.Vitals.MagicSystem with { RecordZeroExtension = Enumerable.Range(33, 24).Select(spell => new NativeMagicStock((byte)spell, 100)).ToArray() } };
        fixture.Refresh();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Hit("slot-Str");
        fixture.Menu.SelectEntry("junction-dashboard", "candidate-magic-0-56");
        double cold = fixture.Menu.JunctionPreviewMilliseconds;
        fixture.Menu.SelectEntry("junction-dashboard", "candidate-magic-0-1");
        fixture.Menu.SelectEntry("junction-dashboard", "candidate-magic-0-56");
        double warm = fixture.Menu.JunctionPreviewMilliseconds;
        Assert.DoesNotContain(fixture.Menu.Entries, entry => entry.Id is "junction-search" or "junction-sort");
        Assert.Contains(fixture.Menu.Entries, entry => entry.Id == "candidate-magic-0-56");
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);
        string? output = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_LATENCY_OUTPUT");
        if (!string.IsNullOrWhiteSpace(output))
        {
            File.WriteAllText(output, $"Candidate 56 preview: first {cold:F3} ms; cached {warm:F3} ms. Original resources, shared 56-spell synthetic bank. Timings include planning, not render presentation.\n");
        }
    }

    [Fact]
    public void ManualMoveCommitsOnceAndUndoRestoresAssignmentsWithoutHealing()
    {
        using Fixture fixture = new();
        NativeCharacterVitals before = fixture.Vitals.DeepCopy();
        fixture.Hit("slot-Str");
        fixture.Preview("candidate-magic-0-1");
        Assert.Equal(before.Records[0].Junction, fixture.Vitals.Records[0].Junction);
        fixture.Apply();
        Assert.Equal(0, fixture.Vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.Equal(1, fixture.Vitals.Records[0].Junction.MagicJunctions[1]);
        NativeHpWord clamped = fixture.Vitals.Records[0].HitPoints;
        Assert.True(clamped.Maximum < before.Records[0].HitPoints.Maximum);
        fixture.Undo();
        Assert.Equal(2, fixture.Actions);
        Assert.Equal(before.Records[0].Junction, fixture.Vitals.Records[0].Junction);
        Assert.Equal(clamped, fixture.Vitals.Records[0].HitPoints);
        Assert.Equal(JsonSerializer.Serialize(before.MagicSystem), JsonSerializer.Serialize(fixture.Vitals.MagicSystem));
        Assert.Equal(before.Records[0].MagicInventory, fixture.Vitals.Records[0].MagicInventory);
        fixture.Menu.Navigate(MenuInput.Alternate);
        Assert.False(fixture.Menu.Entries.Single(entry => entry.Id == "junction-undo").Enabled);
    }

    [Fact]
    public void CancelAfterCandidateNavigationLeavesAllNestedOwnersUnchanged()
    {
        using Fixture fixture = new();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Hit("slot-Str");
        fixture.Preview("candidate-magic-0-1");
        fixture.Preview("candidate-magic-0-2");
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Equal("junction-dashboard", fixture.Menu.ScreenId);
        Assert.Equal(0, fixture.Actions);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
    }

    [Fact]
    public void CardAlternateOpensActionsAndPinsWithoutChangingGameplay()
    {
        using Fixture fixture = new();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Menu.SelectEntry("junction-dashboard", "slot-Str");
        fixture.Menu.Navigate(MenuInput.Alternate);
        foreach (string control in new[] { "optimize", "fill-commands", "pin-stat", "clear-magic", "remove-junctions", "clear-party-magic" })
        {
            Assert.Contains(fixture.Menu.Entries, entry => entry.Id == control && entry.Enabled);
        }
        fixture.Hit("pin-stat");
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.Equal(0, fixture.Actions);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelClosesTheDashboardWithoutQuestionsAndPreservesCurrentAssignments(bool changed)
    {
        using Fixture fixture = new();
        if (changed)
        {
            fixture.Hit("slot-Str");
            fixture.Preview("candidate-magic-0-2");
            fixture.Apply();
        }
        string current = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.False(fixture.Menu.IsOpen);
        Assert.Equal(current, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(changed ? 1 : 0, fixture.Actions);
        fixture.Menu.Open("junction");
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.False(fixture.Menu.IsOpen);
        Assert.Equal(current, JsonSerializer.Serialize(fixture.Vitals));
    }

    [Fact]
    public void SharedDepletedSpellRemainsJunctionableAndApplyUndoNeverRefillCasts()
    {
        using Fixture fixture = new(shared: true);
        fixture.Vitals.MagicSystem.SharedFieldMasks[1] = 15;
        fixture.Refresh();
        Assert.Equal(0, fixture.Data.Magic.Single(magic => magic.Id == "magic-0-1").RemainingUses);
        string history = JsonSerializer.Serialize(fixture.Vitals.MagicSystem);
        fixture.Hit("slot-Str");
        fixture.Preview("candidate-magic-0-1");
        fixture.Apply();
        fixture.Undo();
        Assert.Equal(2, fixture.Actions);
        Assert.Equal(history, JsonSerializer.Serialize(fixture.Vitals.MagicSystem));
        Assert.Equal(0, fixture.Data.Magic.Single(magic => magic.Id == "magic-0-1").RemainingUses);
        Assert.Equal(100, fixture.Vitals.GetMagicCount(0, 1));
    }

    [Fact]
    public void ActionsSnapshotPreservesTheLastMagicDestinationIndependentlyOfItsOrigin()
    {
        using Fixture fixture = new();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Hit("slot-Str");
        fixture.Menu.Navigate(MenuInput.Cancel);
        fixture.Menu.Open("junction");
        fixture.Menu.Navigate(MenuInput.Alternate);
        MenuNavigationState saved = fixture.Menu.CaptureNavigation();
        Assert.Equal("Str", saved.JunctionSlot);
        Assert.Equal("slot-HP", saved.JunctionDashboard!.Origin);
        fixture.Menu.RestoreNavigation(saved);
        Assert.Equal(JsonSerializer.Serialize(saved), JsonSerializer.Serialize(fixture.Menu.CaptureNavigation()));
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);
    }

    [Fact]
    public void CancelFromGuardianHoverClosesImmediatelyWithoutChangingAssignments()
    {
        using Fixture fixture = new();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Menu.SelectEntry("junction-dashboard", "gf-portrait-gf-0");
        Assert.Equal("gf", fixture.Menu.CaptureJunctionDashboard().Editor);
        fixture.Menu.Navigate(MenuInput.Cancel);
        Assert.False(fixture.Menu.IsOpen);
        Assert.Equal(0, fixture.Actions);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
    }

    [Fact]
    public void AttachingTheFirstGuardianKeepsFocusOnItsPortraitWithoutOpeningCommandSelection()
    {
        using Fixture fixture = new();
        fixture.Vitals = fixture.Vitals.ToggleGuardianForceJunction(0, 0);
        fixture.Refresh();
        Assert.Empty(fixture.Menu.Character!.AvailableCommands);
        for (int operation = 0; operation < 2; operation++)
        {
            fixture.Hit("gf-portrait-gf-0");
            UiView view = fixture.Menu.BuildView();
            Assert.Equal("gf-portrait-gf-0", view.FocusedElement?.Id);
            Assert.NotEqual("command", fixture.Menu.CaptureJunctionDashboard().Editor);
            Assert.DoesNotContain(fixture.Menu.Entries, entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal));
            Assert.All(fixture.Menu.Character!.CommandIds, id => Assert.Equal("0", id));
            Assert.Equal(operation + 1, fixture.Actions);
        }
        Assert.DoesNotContain(0, fixture.Vitals.Records[0].Junction.GuardianForceAssignments);
    }

    [Fact]
    public void OwnGuardianRemovalCommitsImmediatelyDespiteCapabilityAndHpLoss()
    {
        using Fixture fixture = new();
        string inventory = JsonSerializer.Serialize(fixture.Vitals.Records.Select(record => record.MagicInventory));
        fixture.Hit("gf-portrait-gf-0");
        Assert.Equal(1, fixture.Actions);
        Assert.DoesNotContain(0, fixture.Vitals.Records[0].Junction.GuardianForceAssignments);
        Assert.Equal(0, fixture.Vitals.Records[0].Junction.MagicJunctions[0]);
        Assert.DoesNotContain(fixture.Menu.Entries, entry => entry.Id is "junction-transfer-yes" or "junction-transfer-no");
        Assert.Equal(inventory, JsonSerializer.Serialize(fixture.Vitals.Records.Select(record => record.MagicInventory)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardianHoverShowsEveryGainedOrLostJunctionSlotWithoutApplyingTheProposal(bool attached)
    {
        using Fixture fixture = new();
        if (!attached)
        {
            fixture.Vitals = fixture.Vitals.ToggleGuardianForceJunction(0, 0);
            fixture.Refresh();
        }
        string original = JsonSerializer.Serialize(fixture.Vitals);
        UiView view = fixture.Menu.BuildView();
        UiButton portrait = Assert.IsType<UiButton>(Descendants(view.Root).Single(element => element.Id == "gf-portrait-gf-0"));
        portrait.Hover!();
        view = fixture.Menu.BuildView();
        for (int index = 0; index < 19; index++)
        {
            string slot = LiveMenuData.JunctionSlotName(index);
            Assert.Equal(attached, fixture.Vitals.JunctionMagicSlotAvailable(0, index));
            UiElement row = Descendants(view.Root).Single(element => element.Id == "slot-" + slot);
            UiLabel availability = Assert.IsType<UiLabel>(Descendants(row).Single(element => element.Id == "junction-slot-availability-" + slot));
            Assert.Equal(attached ? "− Locked" : "+ Available", availability.Text);
            Assert.Equal(attached ? new UiColor(245, 90, 90) : new UiColor(110, 220, 135), availability.Color);
            Assert.Equal(attached, Descendants(row).Any(element => element.Id == "junction-lock-" + slot));
        }
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);

        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            using SKBitmap bitmap = new(1280, 720);
            using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
            canvas.BeginFrame(TimeSpan.Zero);
            view.Draw(canvas, new VectorUiTheme());
            bitmap.SavePng(Path.Combine(directory, $"guardian-slots-{(attached ? "remove" : "add")}.png"));
        }

        UiElement statRow = Descendants(view.Root).Single(element => element.Id == "slot-Str");
        Assert.True(view.Focus(statRow));
        Assert.DoesNotContain(Descendants(fixture.Menu.BuildView().Root), element => element.Id?.StartsWith("junction-slot-availability-", StringComparison.Ordinal) == true);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);

        fixture.Hit("gf-portrait-gf-0");
        Assert.Equal(1, fixture.Actions);
        for (int index = 0; index < 19; index++)
        {
            Assert.Equal(!attached, fixture.Vitals.JunctionMagicSlotAvailable(0, index));
        }
    }

    [Fact]
    public void GuardianRemovalPreviewKeepsSlotsProvidedByAnotherAttachedGuardianAvailable()
    {
        using Fixture fixture = new();
        fixture.Vitals = fixture.Vitals.AcquireGuardianForce(1).TeachGuardianForceAbility(1, 1).ToggleGuardianForceJunction(0, 1);
        fixture.Refresh();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Preview("gf-portrait-gf-0");
        UiElement[] elements = Descendants(fixture.Menu.BuildView().Root).ToArray();
        Assert.DoesNotContain(elements, element => element.Id is "junction-slot-availability-HP" or "junction-lock-HP");
        Assert.Contains(elements, element => element.Id == "junction-slot-availability-Str");
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);
    }

    [Fact]
    public void ForeignGuardianTransferHasOneModalAndCancelPreservesEveryOwner()
    {
        using Fixture fixture = new();
        fixture.Vitals = fixture.Vitals.ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(7, 0).SetJunctionMagic(7, 0, 1);
        fixture.Refresh();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        string selected = fixture.Menu.Character!.Id;
        fixture.Hit("gf-portrait-gf-0");
        Assert.Equal("junction-transfer-no", fixture.Menu.BuildView().FocusedElement?.Id);
        UiView view = fixture.Menu.BuildView();
        view.Resize(1280, 720);
        UiElement overlay = Descendants(view.Root).Single(element => element.Id == "junction-transfer-modal");
        UiElement panel = Assert.Single(overlay.Children);
        Assert.Equal(overlay.Bounds.X + overlay.Bounds.Width / 2, panel.Bounds.X + panel.Bounds.Width / 2, 3);
        Assert.Equal(overlay.Bounds.Y + overlay.Bounds.Height / 2, panel.Bounds.Y + panel.Bounds.Height / 2, 3);
        Assert.Equal(0, fixture.Actions);
        Assert.Contains(fixture.Menu.Entries, entry => entry.Id == "junction-transfer-yes" && entry.Enabled);
        Assert.All(fixture.Menu.Entries.Where(entry => entry.Enabled), entry => Assert.True(entry.Id is "junction-transfer-yes" or "junction-transfer-no" || entry.Id.StartsWith("transfer-loss-", StringComparison.Ordinal), entry.Id));
        fixture.Menu.Navigate(MenuInput.PageRight);
        Assert.Equal(selected, fixture.Menu.Character!.Id);
        fixture.Hit("junction-transfer-no");
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.DoesNotContain(fixture.Menu.Entries, entry => entry.Id is "junction-transfer-yes" or "junction-transfer-no");
    }

    [Theory]
    [InlineData(1280, 720, 100)]
    [InlineData(640, 448, 100)]
    [InlineData(1280, 720, 150)]
    public void TransferShowsOnlyUncompensatedDonorLossesInCompactColumns(int width, int height, int textScale)
    {
        using Fixture fixture = new(shared: true);
        fixture.Vitals = fixture.Vitals.ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(7, 0);
        foreach (int ability in Enumerable.Range(39, 19))
        {
            fixture.Vitals = fixture.Vitals.TeachGuardianForceAbility(0, ability);
        }
        fixture.Vitals = fixture.Vitals.AcquireGuardianForce(1).TeachGuardianForceAbility(1, 39).ToggleGuardianForceJunction(7, 1);
        fixture.Refresh();
        fixture.Data.Settings["Text scale"] = textScale + "%";
        fixture.Menu.PrepareViewport(width, height);
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Hit("gf-portrait-gf-0");
        UiView view = fixture.Menu.BuildView();
        UiElement modal = Descendants(view.Root).Single(element => element.Id == "junction-transfer-modal");
        UiElement[] elements = Descendants(modal).ToArray();
        Assert.Equal(new UiImageSource(UiImageKind.CharacterPortrait, 7), elements.OfType<UiImage>().Single(image => image.Id == "junction-transfer-donor").Source);
        Assert.Equal(new UiImageSource(UiImageKind.GuardianForcePortrait, 0), elements.OfType<UiImage>().Single(image => image.Id == "junction-transfer-guardian").Source);
        Assert.Equal(new UiImageSource(UiImageKind.CharacterPortrait, 0), elements.OfType<UiImage>().Single(image => image.Id == "junction-transfer-recipient").Source);
        Assert.DoesNotContain(elements, element => element.Id is "impact-previous" or "impact-next" or "junction-review");
        Assert.DoesNotContain(elements.OfType<UiLabel>(), label => label.Text.Contains("Caster:", StringComparison.Ordinal) || label.Text.Contains("Review consequences", StringComparison.Ordinal));
        UiColumnPicker columns = Assert.Single(elements.OfType<UiColumnPicker>());
        Assert.Equal("gf-transfer-loss-columns", columns.Id);
        Assert.DoesNotContain(columns.Children, row => row.Id == "transfer-loss-gf-capability-passive-39");
        Assert.Contains(columns.Children, row => row.Id == "transfer-loss-gf-capability-passive-40");
        Assert.All(columns.Children, row =>
        {
            UiLabel label = row.Children.OfType<UiLabel>().First();
            Assert.StartsWith("− ", label.Text);
            Assert.Equal(new UiColor(245, 90, 90), label.Color);
            Assert.Single(row.Children.OfType<UiImage>());
        });
        Assert.True(columns.Children.Count > columns.RowCount);
        Assert.Equal(23 * textScale / 100f, columns.Children[1].Bounds.Y - columns.Children[0].Bounds.Y, 3);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            foreach (MenuPresentation presentation in new[] { MenuPresentation.Classic, MenuPresentation.Vector })
            {
                fixture.Menu.Presentation = presentation;
                using SKBitmap bitmap = new(width, height);
                using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
                canvas.BeginFrame(TimeSpan.Zero);
                fixture.Menu.BuildView().Draw(canvas, presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme());
                bitmap.SavePng(Path.Combine(directory, $"guardian-transfer-{presentation}-{width}x{height}-text{textScale}.png"));
            }
        }
    }

    [Theory]
    [InlineData(false, 1280, 720, 100)]
    [InlineData(true, 1280, 720, 100)]
    [InlineData(false, 640, 448, 100)]
    [InlineData(true, 640, 448, 100)]
    [InlineData(false, 1280, 720, 150)]
    [InlineData(true, 1280, 720, 150)]
    public void InstalledAbilityPickerShowsNativeIconsAndSelectedDescription(bool passive, int width, int height, int textScale)
    {
        using Fixture fixture = new();
        foreach (int ability in Enumerable.Range(40, 18))
        {
            fixture.Vitals = fixture.Vitals.TeachGuardianForceAbility(0, ability);
        }
        fixture.Refresh();
        fixture.Data.Settings["Text scale"] = textScale + "%";
        fixture.Menu.PrepareViewport(width, height);
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Hit(passive ? "passive-0" : "command-0");
        IReadOnlyList<MenuAbilityChoice> choices = passive ? fixture.Menu.Character!.AvailableAbilities : fixture.Menu.Character!.AvailableCommands;
        MenuAbilityChoice selected = choices.First(choice => choice.Id != "0");
        fixture.Preview("candidate-" + selected.Id);
        UiView view = fixture.Menu.BuildView();
        UiColumnPicker columns = Assert.Single(Descendants(view.Root).OfType<UiColumnPicker>());
        Assert.Equal(selected.Description, Descendants(view.Root).OfType<UiLabel>().Single(label => label.Id == "junction-candidate-description").Text);
        foreach (MenuAbilityChoice choice in choices)
        {
            UiElement row = columns.Children.Single(row => row.Id == "candidate-" + choice.Id);
            Assert.Equal(choice.Icon, Assert.Single(row.Children.OfType<UiImage>()).Source);
            Assert.Equal(choice.Name, row.Children.OfType<UiLabel>().First().Text);
        }
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);
        string? directory = Environment.GetEnvironmentVariable("MEMORIA_FF8_JUNCTION_RENDER_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            foreach (MenuPresentation presentation in new[] { MenuPresentation.Classic, MenuPresentation.Vector })
            {
                fixture.Menu.Presentation = presentation;
                using SKBitmap bitmap = new(width, height);
                using BattlePreviewCanvas canvas = new(bitmap, TestResources.GameDirectory);
                canvas.BeginFrame(TimeSpan.Zero);
                fixture.Menu.BuildView().Draw(canvas, presentation == MenuPresentation.Vector ? new VectorUiTheme() : new UiTheme());
                bitmap.SavePng(Path.Combine(directory, $"ability-picker-{(passive ? "passive" : "command")}-{presentation}-{width}x{height}-text{textScale}.png"));
            }
        }
    }

    [Fact]
    public void CommandFocusPreviewsAndConfirmCommitsExactlyOnce()
    {
        using Fixture fixture = new();
        string original = JsonSerializer.Serialize(fixture.Vitals);
        fixture.Hit("command-0");
        fixture.Preview("candidate-20");
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Vitals));
        Assert.Equal(0, fixture.Actions);
        fixture.Menu.Navigate(MenuInput.Confirm);
        Assert.Equal(1, fixture.Actions);
        Assert.Equal(20, (byte)fixture.Vitals.Records[0].Junction.Commands);
        Assert.DoesNotContain(fixture.Menu.Entries, entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal));
    }

    [Fact]
    public void TransferringGfReviewsAndUpdatesTheHiddenDonorAsOneOperation()
    {
        using Fixture fixture = new();
        fixture.Vitals = fixture.Vitals.ToggleGuardianForceJunction(0, 0).ToggleGuardianForceJunction(7, 0).SetJunctionMagic(7, 0, 1).SetJunctionAbility(7, 0, 21, true);
        fixture.Vitals.Records[7] = fixture.Vitals.Records[7] with { MemberFlags = 0 };
        fixture.Refresh();
        Assert.DoesNotContain(fixture.Data.Characters, actor => actor.Id == "character-7");
        fixture.Hit("gf-portrait-gf-0");
        Assert.Equal("junction-transfer-no", fixture.Menu.BuildView().FocusedElement?.Id);
        Assert.Equal(0, fixture.Actions);
        fixture.Hit("junction-transfer-yes");
        Assert.Equal("gf-portrait-gf-0", fixture.Menu.BuildView().FocusedElement?.Id);
        Assert.DoesNotContain(fixture.Menu.Entries, entry => entry.Id.StartsWith("candidate-", StringComparison.Ordinal));
        Assert.Contains(0, fixture.Vitals.Records[0].Junction.GuardianForceAssignments);
        Assert.DoesNotContain(0, fixture.Vitals.Records[7].Junction.GuardianForceAssignments);
        Assert.Equal(0u, fixture.Vitals.Records[7].Junction.Commands);
        Assert.Equal(0, fixture.Vitals.Records[7].Junction.MagicJunctions[0]);
        fixture.Undo();
        Assert.Contains(0, fixture.Vitals.Records[7].Junction.GuardianForceAssignments);
        Assert.Equal(21u, fixture.Vitals.Records[7].Junction.Commands);
        Assert.Equal(1, fixture.Vitals.Records[7].Junction.MagicJunctions[0]);
    }

    [Fact]
    public void EquippedMugUsesTheOriginalPassiveFlagsForItsBasicActionName()
    {
        using Fixture fixture = new();
        int mug = Enumerable.Range(58, 20).First(ability =>
        {
            NativeCharacterVitals candidate = fixture.Vitals.DeepCopy();
            candidate.Records[0] = candidate.Records[0] with { Junction = candidate.Records[0].Junction with { Abilities = (uint)ability } };
            return (fixture.Catalog.ReadStatRules(candidate, 0).NativeStatusPassiveFlags!.Value & 1) != 0;
        });
        fixture.Vitals = fixture.Vitals.TeachGuardianForceAbility(0, mug).SetJunctionAbility(0, 0, mug, false);
        fixture.Refresh();
        Assert.Equal(fixture.Catalog.CommandName(12), fixture.Data.Characters.Single(actor => actor.Id == "character-0").Commands[0]);
        fixture.Vitals = fixture.Vitals.SetJunctionAbility(0, 0, 0, false);
        fixture.Refresh();
        Assert.Equal(fixture.Catalog.CommandName(1), fixture.Data.Characters.Single(actor => actor.Id == "character-0").Commands[0]);
    }

    private static IEnumerable<UiElement> Descendants(UiElement root)
    {
        yield return root;
        foreach (UiElement child in root.Children)
        {
            foreach (UiElement descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly GameAssets assets;
        private readonly GameplayPreferences preferences;
        public LiveMenuCatalog Catalog { get; }
        public NativeCharacterVitals Vitals { get; set; }
        public MenuData Data { get; private set; }
        public MenuController Menu { get; }
        public int Actions { get; private set; }

        public Fixture(bool shared = false, bool smart = false)
        {
            Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Original menu/kernel resources must be explicitly configured.");
            assets = new GameAssets(TestResources.GameDirectory);
            Catalog = LiveMenuCatalog.ReadInstalled(assets);
            Vitals = assets.ReadNewGameCharacterVitals().AcquireGuardianForce(0);
            foreach (int ability in Enumerable.Range(1, 24))
            {
                Vitals = Vitals.TeachGuardianForceAbility(0, ability);
            }
            Vitals = Vitals.ToggleGuardianForceJunction(0, 0);
            for (int row = 0; row < 8; row++)
            {
                Vitals.Records[row] = Vitals.Records[row] with { MagicInventory = Enumerable.Range(1, 32).Select(spell => new NativeMagicStock((byte)spell, 100)).ToArray() };
            }
            preferences = new GameplayPreferences { SharedMagicStock = shared, InfiniteMagic = true, SmartAttack = smart };
            Vitals = MagicModeCoordinator.Preview(Vitals, preferences).Vitals.SetJunctionMagic(0, 0, 1);
            Vitals = Vitals.WithSavedHitPoints(0, Vitals.GetMagicMaximumHitPoints(0));
            Data = Build();
            Menu = new MenuController(() => Data, action =>
            {
                if (action.Kind == "attack-mode")
                {
                    GameSession session = new(new GameState { Scene = SceneKind.Field, CharacterVitals = Vitals, GameplayPreferences = preferences });
                    session.SetAttackMode(int.Parse(action.TargetId.AsSpan(10)), Enum.Parse<MagicAttackMode>(action.Value));
                    Vitals = session.State.CharacterVitals!;
                }
                else
                {
                    Vitals = NativeJunctionMenuInput.Apply(Vitals, NativeJunctionMenuProjection.PlayerAction(action));
                }
                Actions++;
                Data = Build();
            });
            Menu.Open("junction");
            Assert.Equal("junction-dashboard", Menu.ScreenId);
        }

        private MenuData Build() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = Vitals, GameplayPreferences = preferences }, catalog: Catalog);

        public void Refresh()
        {
            Data = Build();
            Menu.InvalidateLiveData();
        }

        public void Hit(string control) => Menu.Execute(new MenuAction("junction-ui", control));

        public void Preview(string control) => Menu.SelectEntry("junction-dashboard", control);

        public void Undo()
        {
            Menu.Navigate(MenuInput.Alternate);
            Hit("junction-undo");
        }

        public void Apply()
        {
            int before = Actions;
            Menu.Navigate(MenuInput.Confirm);
            Assert.Equal(before + 1, Actions);
        }

        public void Dispose() => assets.Dispose();
    }
}
