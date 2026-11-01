using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeTutorialBindingsTests
{
    [Fact]
    public void CopiedCustomNamesRemainPrivateAndRestoreExactly()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        Dictionary<int, string> names = new() { [0] = "Leon", [1] = "Punch", [3] = "Custom guest", [4] = "Angel" };
        NativeTutorialPlayer player = new(catalog, menus, 5, names: names);
        Assert.Equal("Leon", player.Menu.Character!.Name);
        names[0] = "Changed outside lesson";
        player.Menu.InvalidateLiveData();
        Assert.Equal("Leon", player.Menu.Character.Name);
        Assert.True(player.Apply(new PlayerAction(PlayerActionKind.Wait, Frames: 48)));
        JsonElement snapshot = player.Capture();
        NativeTutorialPlayer restored = new(catalog, menus, 5, snapshot, new Dictionary<int, string> { [0] = "Different current name" });
        Assert.True(JsonElement.DeepEquals(snapshot, restored.Capture()));
        Assert.Equal("Leon", restored.Menu.Character!.Name);
        Assert.Equal("Changed outside lesson", names[0]);
    }

    [Fact]
    public void PreparedOriginalGfLearningInputKeepsItsSemanticTargetWhenRowsReorder()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeTutorialPlayer player = new(catalog, LiveMenuCatalog.ReadInstalled(assets), 4);
        bool reached = false;
        for (int tick = 0; tick < 10000 && !player.IsComplete; tick++)
        {
            JsonElement state = player.Capture();
            Assert.Equal((byte)83, player.Party.GuardianForces[1].Learning);
            if (state.GetProperty("ProgramCounter").GetInt32() == 213 && state.GetProperty("InputPrepared").GetBoolean())
            {
                reached = true;
                break;
            }
            player.Apply(new PlayerAction(player.WaitingForInput ? PlayerActionKind.Confirm : PlayerActionKind.Wait));
        }
        Assert.True(reached);
        Assert.Equal("10", player.Menu.SelectedEntry!.Id);
        MenuScreenDefinition original = player.Menu.Definitions.Single(screen => screen.Id == "gf-learn");
        player.Menu.Register(new MenuScreenDefinition { Id = original.Id, Title = original.Title, Help = original.Help, Layout = original.Layout, Columns = original.Columns, MainCommand = original.MainCommand, Details = original.Details, BuildContent = original.BuildContent, Entries = controller => original.Entries(controller).Reverse().ToArray() });
        Assert.True(player.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Equal("10", player.Menu.SelectedEntry!.Id);
        Assert.Equal((byte)83, player.Party.GuardianForces[1].Learning);
        Assert.True(player.Apply(new PlayerAction(PlayerActionKind.Wait)));
        Assert.Equal((byte)10, player.Party.GuardianForces[1].Learning);
    }
    [Fact]
    public void DashboardMagicCursorKeepsCurrentStatsAndShowsPreviewUntilApply()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeTutorialPlayer player = new(catalog, LiveMenuCatalog.ReadInstalled(assets), 1);
        AdvanceUntilOperation(player, 87);
        NativeCharacterVitals before = player.Party;
        JsonElement original = JsonSerializer.SerializeToElement(before);
        int fire = before.Records[0].MagicInventory[0].SpellId;
        int blizzard = before.Records[0].MagicInventory[1].SpellId;
        int thunder = before.Records[0].MagicInventory[2].SpellId;
        Assert.Equal((1, 7, 4), (fire, blizzard, thunder));
        AdvanceUntilOperation(player, 101);
        int blizzardStrength = catalog.ReadStatistics(before.SetJunctionMagic(0, 1, blizzard), 0).Strength;
        Assert.Equal($"candidate-magic-0-{blizzard}", player.Menu.SelectedEntry!.Id);
        Assert.Equal(catalog.ReadStatistics(before, 0).Strength, player.Menu.Character!.Stats["Str"]);
        AssertStatPreview(player, "Str", player.Menu.Character.Stats["Str"], blizzardStrength);
        Assert.NotEqual(catalog.ReadStatistics(before, 0).Strength, blizzardStrength);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(player.Party)));
        AdvanceUntilOperation(player, 103);
        Assert.Equal($"candidate-magic-0-{thunder}", player.Menu.SelectedEntry!.Id);
        AssertStatPreview(player, "Str", player.Menu.Character!.Stats["Str"], catalog.ReadStatistics(before.SetJunctionMagic(0, 1, thunder), 0).Strength);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(player.Party)));
        AdvanceUntilOperation(player, 115);
        Assert.Equal(fire, player.Party.Records[0].Junction.MagicJunctions[1]);
        MenuJunctionPlan expected = new MenuJunctionPlanner(new GameState { Scene = SceneKind.Field, CharacterVitals = before }, catalog: LiveMenuCatalog.ReadInstalled(assets)).Plan(new("junction-magic", $"magic-0-{fire}", "character-0", "Str"));
        Assert.Null(expected.UnavailableReason);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(expected.Result), JsonSerializer.SerializeToElement(player.Party)));
        Assert.Equal(before.Records.Skip(1), player.Party.Records.Skip(1));
    }

    [Fact]
    public void DashboardCureElementPreviewKeepsCommittedAssignmentUntilCancel()
    {
        string directory = TestResources.GameDirectory;
        Assert.SkipUnless(Directory.Exists(directory), "Installed original tutorial resources are required.");
        using GameAssets assets = new(directory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 2);
        AdvanceUntilOperation(player, 100);
        NativeCharacterVitals before = player.Party;
        JsonElement original = JsonSerializer.SerializeToElement(before);
        int thunder = before.Records[0].MagicInventory[2].SpellId;
        int cure = before.Records[0].MagicInventory[3].SpellId;
        Assert.Equal("Cure", menus.MagicName(cure));
        Assert.Equal(thunder, before.Records[0].Junction.MagicJunctions[9]);
        Assert.Equal((byte)0, catalog.ReadStatRules(before, 0).MagicRecords[cure][0x20]);
        AdvanceUntilOperation(player, 102);
        Assert.Equal($"candidate-magic-0-{cure}", player.Menu.SelectedEntry!.Id);
        Assert.Equal(menus.MagicName(thunder), player.Menu.Character!.Junctions["Elem-Atk"]);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(player.Party)));
        NativeTutorialPlayer restored = new(catalog, menus, 2, player.Capture());
        Assert.Equal(menus.MagicName(thunder), restored.Menu.Character!.Junctions["Elem-Atk"]);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(restored.Party)));
        AdvanceUntilOperation(player, 111);
        Assert.Equal("junction-dashboard", player.Menu.ScreenId);
        Assert.True(JsonElement.DeepEquals(original, JsonSerializer.SerializeToElement(player.Party)));
    }

    private static void AssertStatPreview(NativeTutorialPlayer player, string slot, int current, int next)
    {
        UiView view = player.Menu.BuildView();
        view.Resize(1280, 720);
        IEnumerable<UiElement> Descendants(UiElement parent) => parent.Children.SelectMany(child => new[] { child }.Concat(Descendants(child)));
        UiButton row = Assert.Single(Descendants(view.Root).OfType<UiButton>(), button => button.Id == "slot-" + slot);
        Assert.Contains(Descendants(row).OfType<UiLabel>(), label => label.Text == (current == next ? current.ToString() : $"{current}→{next}"));
    }

    [Fact]
    public void PendingDashboardPreviewRestoresItsDestinationCandidateAndCurrentValues()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 1);
        AdvanceUntilOperation(player, 101);
        JsonElement snapshot = player.Capture();
        NativeTutorialPlayer restored = new(catalog, menus, 1, snapshot);
        Assert.True(JsonElement.DeepEquals(snapshot, restored.Capture()));
        Assert.Equal("junction-dashboard", restored.Menu.ScreenId);
        Assert.Equal("slot-Str", restored.Menu.CaptureJunctionDashboard().Origin);
        Assert.Equal("magic-0-7", restored.Menu.CaptureJunctionDashboard().Candidate);
        int current = catalog.ReadStatistics(restored.Party, 0).Strength;
        int preview = catalog.ReadStatistics(restored.Party.SetJunctionMagic(0, 1, 7), 0).Strength;
        Assert.Equal(current, restored.Menu.Character!.Stats["Str"]);
        AssertStatPreview(restored, "Str", current, preview);
        AdvanceUntilOperation(restored, 115);
        Assert.Equal(1, restored.Party.Records[0].Junction.MagicJunctions[1]);
    }

    [Fact]
    public void StatLessonDemonstratesReviewedStatOnlyOptimizerOnTheDashboard()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 1);
        AdvanceUntilOperation(player, 169);
        NativeCharacterVitals before = player.Party;
        MenuJunctionPlan expected = new MenuJunctionPlanner(new GameState { Scene = SceneKind.Field, CharacterVitals = before }, catalog: menus).Optimize("character-0", "Attack", new HashSet<string>());
        Assert.Null(expected.UnavailableReason);
        Assert.True(expected.HasChanges);
        AdvanceUntilOperation(player, 209);
        Assert.Equal("junction-dashboard", player.Menu.ScreenId);
        Assert.Equal(expected.Result.Records[0].Junction, player.Party.Records[0].Junction);
        Assert.Equal(before.Records[0].Junction.MagicJunctions.Skip(9), player.Party.Records[0].Junction.MagicJunctions.Skip(9));
        Assert.Equal(before.Records[0].Junction.Commands, player.Party.Records[0].Junction.Commands);
        Assert.Equal(before.Records[0].Junction.Abilities, player.Party.Records[0].Junction.Abilities);
        Assert.Equal(before.Records[0].Junction.GuardianForces, player.Party.Records[0].Junction.GuardianForces);
    }

    [Fact]
    public void EarlierJunctionSnapshotAdaptsOnlyItsIsolatedCommandsAndNavigation()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 1);
        AdvanceUntilOperation(player, 101);
        NativeTutorialSnapshot current = player.Capture().Deserialize<NativeTutorialSnapshot>()!;
        NativeCharacterVitals legacyParty = current.Party.DeepCopy();
        uint rawCommands = catalog.CreateIsolatedParty(1).Records[0].Junction.Commands;
        Assert.Equal(0x00040603u, rawCommands);
        legacyParty.Records[0] = legacyParty.Records[0] with { Junction = legacyParty.Records[0].Junction with { Commands = rawCommands } };
        MenuNavigationState navigation = current.Navigation with { ScreenId = "junction-stock", History = ["junction", "junction-options", "junction-magic"], JunctionSlot = "Str", MagicId = "magic-0-7", JunctionDashboard = null };
        NativeTutorialSnapshot legacy = current with { Version = 1, Party = legacyParty, Navigation = navigation };
        JsonElement original = JsonSerializer.SerializeToElement(legacy);
        string unchanged = original.GetRawText();
        GameSession live = new(new GameState { CharacterVitals = assets.ReadNewGameCharacterVitals() });
        JsonElement liveBefore = JsonSerializer.SerializeToElement(live.State);
        NativeTutorialPlayer adapted = new(catalog, menus, 1, original);
        Assert.Equal(unchanged, original.GetRawText());
        Assert.Equal(rawCommands, legacyParty.Records[0].Junction.Commands);
        Assert.True(JsonElement.DeepEquals(liveBefore, JsonSerializer.SerializeToElement(live.State)));
        Assert.Equal(2, adapted.Capture().GetProperty("Version").GetInt32());
        Assert.Equal(current.Party.Records[0].Junction.Commands, adapted.Party.Records[0].Junction.Commands);
        Assert.Equal(current.Party.Records[0].Junction.MagicJunctions, adapted.Party.Records[0].Junction.MagicJunctions);
        Assert.Equal("junction-dashboard", adapted.Menu.ScreenId);
        Assert.Equal("slot-Str", adapted.Menu.CaptureJunctionDashboard().Origin);
        Assert.Equal("magic-0-7", adapted.Menu.CaptureJunctionDashboard().Candidate);
        Assert.Equal(catalog.ReadStatistics(adapted.Party, 0).Strength, adapted.Menu.Character!.Stats["Str"]);
        NativeTutorialPlayer roundtrip = new(catalog, menus, 1, adapted.Capture());
        Assert.True(JsonElement.DeepEquals(adapted.Capture(), roundtrip.Capture()));
        AdvanceUntilOperation(adapted, 115);
        Assert.Equal(1, adapted.Party.Records[0].Junction.MagicJunctions[1]);
    }

    [Fact]
    public void EarlierJunctionSnapshotRejectsUnknownNavigationAndUnmappedCommands()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 1);
        NativeTutorialSnapshot current = player.Capture().Deserialize<NativeTutorialSnapshot>()!;
        NativeTutorialSnapshot unsupportedNavigation = current with { Version = 1, Navigation = current.Navigation with { ScreenId = "battle", JunctionDashboard = null } };
        Assert.Throws<InvalidDataException>(() => new NativeTutorialPlayer(catalog, menus, 1, JsonSerializer.SerializeToElement(unsupportedNavigation)));
        NativeCharacterVitals unmapped = current.Party.DeepCopy();
        unmapped.Records[0] = unmapped.Records[0] with { Junction = unmapped.Records[0].Junction with { Commands = (unmapped.Records[0].Junction.Commands & 0xFFFFFF00) | 5 } };
        NativeTutorialSnapshot unsupportedCommand = current with { Version = 1, Party = unmapped, Navigation = current.Navigation with { ScreenId = "junction", JunctionDashboard = null } };
        Assert.Throws<NotSupportedException>(() => new NativeTutorialPlayer(catalog, menus, 1, JsonSerializer.SerializeToElement(unsupportedCommand)));
    }

    [Fact]
    public void GuardianLessonUsesAcquiredPortraitTargetsAndPreservesItsInputBoundaries()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        NativeCharacterVitals party = catalog.CreateIsolatedParty(0);
        IReadOnlyDictionary<int, NativeTutorialMenuStep> steps = NativeTutorialJunctionPlan.Create(0, catalog.Get(0), party);
        Assert.DoesNotContain(steps.Values, step => step.EntryId == "manage-gfs" || step.DashboardPreparation?.Contains("manage-gfs") == true);
        Assert.Equal("gf-portrait-gf-0", steps[58].EntryId);
        Assert.Equal("gf-portrait-gf-1", steps[122].EntryId);
        Assert.Empty(steps[122].DashboardPreparation!);
        Assert.Equal(NativeTutorialPress.ApplyDashboard, steps[136].Press);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void EarlierGuardianDashboardControlRestoresToAnAcquiredPortraitWithoutChangingItsCandidate(int version)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 0);
        AdvanceUntilOperation(player, 122);
        NativeTutorialSnapshot current = player.Capture().Deserialize<NativeTutorialSnapshot>()!;
        JunctionDashboardNavigationState dashboard = current.Navigation.JunctionDashboard! with { Origin = "manage-gfs", Focus = "manage-gfs" };
        NativeTutorialSnapshot earlier = current with { Version = version, Navigation = current.Navigation with { JunctionDashboard = dashboard } };
        JsonElement source = JsonSerializer.SerializeToElement(earlier);
        string original = source.GetRawText();
        NativeTutorialPlayer restored = new(catalog, menus, 0, source);
        string portrait = "gf-portrait-" + dashboard.Candidate;
        Assert.Equal(original, source.GetRawText());
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(current.Party), JsonSerializer.SerializeToElement(restored.Party)));
        Assert.Equal("gf-1", restored.Menu.CaptureJunctionDashboard().Candidate);
        Assert.Equal(portrait, restored.Menu.CaptureJunctionDashboard().Origin);
        Assert.Equal(portrait, restored.Menu.CaptureJunctionDashboard().Focus);
        Assert.DoesNotContain(restored.Menu.Entries, entry => entry.Id == "manage-gfs");
        NativeTutorialPlayer roundtrip = new(catalog, menus, 0, restored.Capture());
        Assert.True(JsonElement.DeepEquals(restored.Capture(), roundtrip.Capture()));
        AdvanceUntilOperation(restored, 136);
        Assert.Contains(0, restored.Party.Records[catalog.Get(0).Descriptor.CharacterId].Junction.GuardianForceAssignments);
    }

    [Fact]
    public void EarlierGuardianPickerReconstructsItsPortraitAndPreviewWithoutApplying()
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeTutorialCatalog catalog = assets.ReadNativeTutorialCatalog();
        LiveMenuCatalog menus = LiveMenuCatalog.ReadInstalled(assets);
        NativeTutorialPlayer player = new(catalog, menus, 0);
        AdvanceUntilOperation(player, 122);
        NativeTutorialSnapshot current = player.Capture().Deserialize<NativeTutorialSnapshot>()!;
        NativeTutorialSnapshot earlier = current with { Version = 1, Navigation = current.Navigation with { ScreenId = "junction-gf", GuardianForceId = "gf-1", JunctionDashboard = null } };
        JsonElement source = JsonSerializer.SerializeToElement(earlier);
        NativeTutorialPlayer restored = new(catalog, menus, 0, source);
        Assert.Equal("gf-portrait-gf-1", restored.Menu.CaptureJunctionDashboard().Origin);
        Assert.Equal("gf-1", restored.Menu.CaptureJunctionDashboard().Candidate);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(current.Party), JsonSerializer.SerializeToElement(restored.Party)));
        Assert.DoesNotContain(restored.Menu.Entries, entry => entry.Id == "manage-gfs");
    }

    private static void AdvanceUntilOperation(NativeTutorialPlayer player, int operation)
    {
        bool reached = false;
        void Observe(int pc, ushort word)
        {
            reached |= pc == operation;
        }
        player.OperationExecuted += Observe;
        try
        {
            for (int tick = 0; tick < 10000 && !reached && !player.IsComplete; tick++)
            {
                Assert.True(player.Apply(new PlayerAction(player.WaitingForInput ? PlayerActionKind.Confirm : PlayerActionKind.Wait)));
            }
        }
        finally
        {
            player.OperationExecuted -= Observe;
        }
        Assert.True(reached, $"Original tutorial operation{operation} was not reached.");
    }

}
