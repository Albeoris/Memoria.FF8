using System.Text.Json;
using System.Buffers.Binary;
using Albeoris.Games.FF8.Jsm;
using Memoria.FF8.Assets;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativePartySwitchTests
{
    [Fact]
    public void LiveSwitchCarriesBothSelectedRowsAndRefreshesMembership()
    {
        NativeCharacterVitals party = CreateParty();
        string original = JsonSerializer.Serialize(party);
        NativeCharacterVitals source = party;
        List<PlayerAction> inputs = [];
        MenuController controller = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = party, WorldControls = NativeWorldControlState.CreateNewGame() with { Menu209 = 2 } }), action =>
        {
            PlayerAction input = NativePartySwitchMenuProjection.PlayerAction(action);
            inputs.Add(input);
            PlayerAction restoredInput = JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(input))!;
            Assert.Equal(input, restoredInput);
            party = NativePartySwitchInput.Apply(party, restoredInput, 2);
        });
        controller.Open("switch-party");
        controller.SelectEntry("switch-party", "character-1");
        controller.Navigate(MenuInput.Confirm);
        controller.SelectEntry("switch-party-target", "character-3");
        controller.Navigate(MenuInput.Confirm);

        Assert.Equal(new PlayerAction(PlayerActionKind.SwitchPartyMembers, 1, Quantity: 3), Assert.Single(inputs));
        Assert.Equal(new byte[] { 0, 3, 2 }, party.CombatParty);
        Assert.Equal("switch-party", controller.ScreenId);
        Assert.DoesNotContain("Unsupported", controller.Message);
        Assert.True(controller.Data.Characters.Single(character => character.Id == "character-3").InParty);
        Assert.False(controller.Data.Characters.Single(character => character.Id == "character-1").InParty);
        Assert.Equal(original, JsonSerializer.Serialize(source));
        Assert.Equal(source.Records.Select(record => record.Junction), party.Records.Select(record => record.Junction));
        NativeCharacterVitals restored = JsonSerializer.Deserialize<NativeCharacterVitals>(JsonSerializer.Serialize(party))!;
        restored.Validate();
        Assert.Equal(party.CombatParty, restored.CombatParty);
    }

    [Fact]
    public void NativeLocksPreventCrossPaneReplacementButPermitPartyReordering()
    {
        NativeCharacterVitals source = CreateParty(locked: 0);
        Assert.False(NativePartySwitchInput.CanExchange(source, 0, 3, 2));
        Assert.Throws<InvalidOperationException>(() => NativePartySwitchInput.Apply(source, new PlayerAction(PlayerActionKind.SwitchPartyMembers, 0, Quantity: 3), 2));
        NativeCharacterVitals result = NativePartySwitchInput.Apply(source, new PlayerAction(PlayerActionKind.SwitchPartyMembers, 0, Quantity: 2), 2);
        Assert.Equal(new byte[] { 2, 1, 0 }, result.CombatParty);
        Assert.Equal(source.Records[0].MemberFlags, result.Records[0].MemberFlags);
    }

    [Fact]
    public void ExpandedSwitchAdmitsALivingFourthAllyAndPreservesNativeOrder()
    {
        NativeCharacterVitals party = CreateParty();
        for (int row = 0; row < 3; row++)
        {
            party.Records[row] = party.Records[row] with { Status = 1, HitPoints = new(0, 0) };
        }
        PlayerAction add = new(PlayerActionKind.SwitchPartyMembers, 255, Quantity: 3);
        Assert.True(NativePartySwitchInput.CanExchange(party, 255, 3, 2, capacity: 4));
        NativeCharacterVitals selected = NativePartySwitchInput.Apply(party, add, 2, capacity: 4);
        Assert.Equal(new byte[] { 0, 1, 2 }, selected.CombatParty);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, selected.GetBattlePartyRows(4));
        Assert.False(NativePartySwitchInput.CanExchange(selected, 3, 255, 2, capacity: 4));
    }

    [Fact]
    public void ExpandedSwitchAllowsTheLastLivingMemberToBeAnEligibleGuest()
    {
        NativeCharacterVitals party = CreateParty().WithExtendedRoster(true);
        party = party.WithBattleParty([0, 10], 4);
        party.Records[0] = party.Records[0] with { Status = 1, HitPoints = new(0, 0) };
        PlayerAction remove = new(PlayerActionKind.SwitchPartyMembers, 0, Quantity: 255);
        NativeCharacterVitals selected = NativePartySwitchInput.Apply(party, remove, 2, capacity: 4);
        Assert.Equal(new byte[] { 10, 255, 255 }, selected.CombatParty);
        Assert.Equal(new byte[] { 10 }, selected.GetBattlePartyRows(4));
        Assert.False(NativePartySwitchInput.CanExchange(selected, 10, 255, 2, capacity: 4));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(11)]
    public void ExpandedMenuUsesStableCharacterRowsForPointerAndMembershipEdits(int capacity)
    {
        NativeCharacterVitals party = CreateParty(locked: 0).WithExtendedRoster(true);
        party.Records[4] = party.Records[4] with { MemberFlags = 1 };
        party.Records[5] = party.Records[5] with { MemberFlags = 1 };
        byte[] selected = [.. Enumerable.Range(0, capacity - 1).Select(row => (byte)row)];
        party = party.WithBattleParty(selected, capacity);
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame() with { Menu209 = 2 };
        MenuController menu = new(() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = party, WorldControls = controls, GameplayPreferences = new() { BattleAllyCount = capacity } }), action =>
        {
            party = NativePartySwitchInput.Apply(party, NativePartySwitchMenuProjection.PlayerAction(action), 2, capacity);
        });
        menu.Open("switch-party");
        Assert.Equal(capacity, menu.Data.PartyCapacity);
        Assert.Equal(selected.Select(row => $"character-{row}"), menu.Data.Characters.Where(character => character.InParty).Select(character => character.Id));
        Assert.False(NativePartySwitchInput.CanExchange(party, 0, 255, 2, capacity));
        UiView view = menu.BuildView();
        UiButton add = Assert.Single(Buttons(view.Root), button => button.Id == MenuController.EmptySwitchSlotId);
        Assert.Equal("Add to party", add.Text);
        view.Pointer(add.Bounds.X + add.Bounds.Width / 2, add.Bounds.Y + add.Bounds.Height / 2, true);
        Assert.Equal("switch-party-target", menu.ScreenId);
        menu.SelectEntry("switch-party-target", $"character-{capacity - 1}");
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(capacity, party.GetBattlePartyRows(capacity).Length);
        Assert.Equal((byte)(capacity - 1), party.GetBattlePartyRows(capacity)[^1]);

        view = menu.BuildView();
        UiButton last = Assert.Single(Buttons(view.Root), button => button.Id == $"character-{capacity - 1}");
        Assert.True(last.Bounds.Height >= 30);
        view.Pointer(last.Bounds.X + last.Bounds.Width / 2, last.Bounds.Y + last.Bounds.Height / 2, true);
        Assert.Equal("switch-party-target", menu.ScreenId);
        Assert.Equal("Remove from party", Assert.Single(menu.Entries, entry => entry.Id == MenuController.EmptySwitchSlotId).Label);
        menu.SelectEntry("switch-party-target", MenuController.EmptySwitchSlotId);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal(selected, party.GetBattlePartyRows(capacity));

        static IEnumerable<UiButton> Buttons(UiElement element)
        {
            if (element is UiButton button)
            {
                yield return button;
            }
            foreach (UiButton child in element.Children.SelectMany(Buttons))
            {
                yield return child;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorldSwitchAddsFourthAllyFromAnOpenExchangeAndKeepsSelectionThroughNormalSave(bool pointer)
    {
        string directory = TestResources.GameDirectory;
        using GameAssets assets = new(directory);
        GameSession session = new(GameplaySaveSessionLoader.Restore(GameplaySaveSessionLoaderTests.Document(assets, true), directory, sharedAssets: assets));
        using GameWorld world = new(session, directory, sharedAssets: assets);
        world.EnsureField();

        NativeCharacterVitals original = session.State.CharacterVitals!.DeepCopy();
        byte[] primary = [5, 0, 4];
        for (int row = 0; row < 6; row++)
        {
            original.Records[row] = original.Records[row] with { MemberFlags = (ushort)(row == 0 ? 7 : 1) };
        }
        NativeCharacterVitals initial = (original with { CombatParty = primary }).Rest();
        if (pointer)
        {
            initial = initial.WithBattleParty([.. primary, 1, 2], 5).WithBattleParty(primary, 4);
        }
        session.SetCharacterVitals(initial);
        session.SetWorldControls(session.WorldControls! with { Menu209 = 2 });
        MenuController menu = new(() =>
        {
            MenuData data = LiveMenuData.Build(session.State);
            data.Settings["Battle roster size"] = world.EffectiveBattleAllyCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            data.SupportedActions.Add("config:Battle roster size");
            return data;
        }, action =>
        {
            if (action.Kind == "config")
            {
                Assert.Equal("Battle roster size", action.SubjectId);
                UiPreferences preferences = new UiPreferences().WithSetting(action.SubjectId, action.Value);
                world.ConfigureGameplayPreferences(session.State.GameplayPreferences with { BattleAllyCount = preferences.BattleAllyCount });
                return;
            }
            PlayerActionResult result = world.Apply(NativePartySwitchMenuProjection.PlayerAction(action));
            Assert.True(result.Accepted, result.Detail);
        });
        menu.Open("config-combat");
        menu.SelectEntry("config-combat", "Battle roster size");
        menu.Navigate(MenuInput.Right);
        Assert.Equal(4, session.State.GameplayPreferences.BattleAllyCount);

        menu.Open("switch");
        menu.Navigate(MenuInput.Confirm);
        menu.Navigate(MenuInput.Confirm);
        Assert.Equal("switch-party-target", menu.ScreenId);
        Assert.Equal(4, menu.Data.PartyCapacity);
        Assert.Equal(primary, session.State.CharacterVitals!.GetBattlePartyRows(4));
        if (pointer)
        {
            Click(MenuController.SwitchAddModeId);
            Click("character-1");
        }
        else
        {
            for (int step = 0; step < primary.Length; step++)
            {
                menu.Navigate(MenuInput.Down);
            }
            Assert.Equal(MenuController.SwitchAddModeId, menu.SelectedEntry!.Id);
            menu.Navigate(MenuInput.Confirm);
            Assert.Equal("character-1", menu.SelectedEntry!.Id);
            menu.Navigate(MenuInput.Confirm);
        }

        byte[] selected = [.. primary, 1];
        Assert.Equal(primary, session.State.CharacterVitals!.CombatParty);
        Assert.Equal(selected, session.State.CharacterVitals.GetBattlePartyRows(4));
        Assert.Equal(selected.Select(row => $"character-{row}"), menu.Data.Characters.Where(character => character.InParty).Select(character => character.Id));
        string savePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.ff8");
        try
        {
            _ = world.WriteGameplaySave(savePath);
            GameState restored = PlayerStateLoader.Load(savePath, directory, sharedAssets: assets);
            Assert.Equal(SceneKind.WorldMap, restored.Scene);
            Assert.Equal(session.State.Variables, restored.Variables);
            Assert.Equal(session.State.Economy!.Items, restored.Economy!.Items);
            Assert.Equal(4, restored.GameplayPreferences.BattleAllyCount);
            Assert.Equal(primary, restored.CharacterVitals!.CombatParty);
            Assert.Equal(selected, restored.CharacterVitals.GetBattlePartyRows(4));
            Assert.Equal(pointer ? [.. selected, 2] : selected, restored.CharacterVitals.GetBattlePartyRows(5));
            using GameWorld resumed = new(new GameSession(restored), directory, sharedAssets: assets);
            resumed.EnsureField();
            BattleDefinition battle = resumed.CreateEncounterBattle(87, nativeSeed: 4);
            Assert.True(battle.UsesOriginalEnemySlots);
            Assert.Equal(selected.Select(row => (int?)row), battle.Combatants.Where(actor => actor.IsPlayer).Select(actor => actor.MagicRecordId));
        }
        finally
        {
            File.Delete(savePath);
        }

        void Click(string id)
        {
            UiView view = menu.BuildView();
            UiButton button = Assert.Single(Descendants(view.Root).OfType<UiButton>(), element => element.Id == id);
            Assert.True(button.Enabled && button.Bounds.Width > 0 && button.Bounds.Height > 0);
            view.Pointer(button.Bounds.X + button.Bounds.Width / 2, button.Bounds.Y + button.Bounds.Height / 2, true);
        }

        static IEnumerable<UiElement> Descendants(UiElement root)
        {
            yield return root;
            foreach (UiElement child in root.Children.SelectMany(Descendants))
            {
                yield return child;
            }
        }
    }

    [Fact]
    public void StorySquadMenuExplainsItsThreeSlotsAndPreservesTheAdditionalSelection()
    {
        NativeCharacterVitals party = CreateParty().WithBattleParty([0, 1, 2, 3], 4);
        GameState state = new()
        {
            Scene = SceneKind.Field,
            CharacterVitals = party,
            WorldControls = NativeWorldControlState.CreateNewGame() with { Menu209 = 2 },
            EventMemory = NativeEventMemory.Empty.Write(256, 2, NativeScalarValue.Exact(850)),
            GameplayPreferences = new() { BattleAllyCount = 4 }
        };
        MenuController menu = new(() => LiveMenuData.Build(state));
        menu.Open("switch-party");
        Assert.Equal(3, menu.Data.PartyCapacity);
        Assert.Contains("story squad", menu.SelectedEntry!.Help);
        Assert.DoesNotContain(menu.Entries, entry => entry.Id is MenuController.SwitchAddModeId || entry.Id == MenuController.EmptySwitchSlotId);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, party.GetBattlePartyRows(4));
    }

    [Fact]
    public void FieldPublicationPreservesForcedFieldCellsAndUsesSelectedMenuOrder()
    {
        NativeCharacterVitals source = CreateParty(forced: 0);
        NativeCharacterVitals result = NativePartySwitchInput.Apply(source, new PlayerAction(PlayerActionKind.SwitchPartyMembers, 1, Quantity: 3), 2);
        Assert.Equal(new byte[] { 3, 0, 2 }, result.PreparePartyMenuFieldSlots([1, 0, 2], result.CombatParty));
    }

    [Fact]
    public void MissingEntryPermissionDisablesMemberTabAndTargets()
    {
        NativeCharacterVitals source = CreateParty();
        MenuData data = LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = source, WorldControls = NativeWorldControlState.CreateNewGame() });
        MenuController controller = new(() => data);
        controller.Open("switch");
        Assert.False(controller.Entries.Single(entry => entry.Id == "switch-party").Enabled);
        Assert.DoesNotContain("party-switch", data.SupportedActions);
        Assert.False(NativePartySwitchInput.CanExchange(source, 1, 3, 0));
        Assert.False(NativePartySwitchInput.CanExchange(source, 1, 7, 2));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(11, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 11)]
    public void MalformedPhysicalRowsFailBeforeMutation(int source, int target)
    {
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine events = new(session);
        GameState before = session.State;
        Assert.Throws<ArgumentException>(() => events.Apply(new PlayerAction(PlayerActionKind.SwitchPartyMembers, source, Quantity: target)));
        Assert.Same(before, session.State);
        Assert.Empty(events.ActionLog);
    }

    [Fact]
    public void IncompleteAndEntirelyIncapacitatedPartiesCannotBePublished()
    {
        NativeCharacterVitals source = CreateParty();
        Assert.False(NativePartySwitchInput.CanExchange(source with { CombatParty = [0, 1, 255] }, 1, 3, 2));
        NativeCharacterVital[] records = source.Records.Select(record => record with { Status = (ushort)(record.Status | 1) }).ToArray();
        Assert.False(NativePartySwitchInput.CanExchange(source with { Records = records }, 1, 3, 2));
    }

    [Fact]
    public void ExtendedRosterSwitchProjectsEveryReserveAndCanFillAnEmptySlot()
    {
        NativeCharacterVitals party = CreateParty(locked: 0).WithExtendedRoster(true);
        party.Records[4] = party.Records[4] with { MemberFlags = 1 };
        party.Records[5] = party.Records[5] with { MemberFlags = 1 };
        party = party with { CombatParty = [0, 255, 255] };
        NativeWorldControlState controls = NativeWorldControlState.CreateNewGame() with { Menu209 = 2 };
        MenuData ReadData() => LiveMenuData.Build(new GameState { Scene = SceneKind.Field, CharacterVitals = party, WorldControls = controls });
        List<PlayerAction> actions = [];
        MenuController controller = new(ReadData, action =>
        {
            PlayerAction input = NativePartySwitchMenuProjection.PlayerAction(action);
            actions.Add(input);
            party = NativePartySwitchInput.Apply(party, input, controls.Menu209);
        });

        controller.Open("switch-party");
        Assert.Equal(11, controller.Data.Characters.Count);
        Assert.DoesNotContain(controller.Data.Characters, character => character.Id == MenuController.EmptySwitchSlotId);
        controller.SelectEntry("switch-party", MenuController.EmptySwitchSlotId);
        controller.Navigate(MenuInput.Confirm);
        controller.SelectEntry("switch-party-target", "character-10");
        UiElement root = controller.BuildView().Root;
        UiButton ward = Assert.Single(Descendants(root).OfType<UiButton>(), button => button.Id == "character-10");
        Assert.True(ward.Bounds.Width > 0 && ward.Bounds.Height > 0);
        controller.Navigate(MenuInput.Confirm);
        Assert.Equal(new PlayerAction(PlayerActionKind.SwitchPartyMembers, 255, Quantity: 10), Assert.Single(actions));
        Assert.Equal(new byte[] { 0, 10, 255 }, party.CombatParty);
        Assert.True(controller.Data.Characters.Single(character => character.Id == "character-10").InParty);

        static IEnumerable<UiElement> Descendants(UiElement element)
        {
            yield return element;
            foreach (UiElement child in element.Children.SelectMany(Descendants))
            {
                yield return child;
            }
        }
    }

    [Fact]
    public void MalformedTransportRejectsBeforeOpeningAnActionJournal()
    {
        PlayerAction valid = new(PlayerActionKind.SwitchPartyMembers, 0, Quantity: 1);
        PlayerAction[] malformed = [valid with { Quantity = null }, valid with { Text = "1" }, valid with { Frames = 2 }, valid with { Horizontal = 1 }, valid with { Vertical = 1 }, valid with { CameraHorizontal = 1 }, valid with { WorldButtons = 1 }, valid with { WaitWhilePlayable = true }];
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        EventEngine events = new(session);
        GameState before = session.State;
        foreach (PlayerAction input in malformed)
        {
            Assert.Throws<ArgumentException>(() => events.Apply(input));
            Assert.Same(before, session.State);
            Assert.Empty(events.ActionLog);
        }
    }

    [Fact]
    public void NativeNormalSwitchPublishesItsResultAndRestoresBeforeReturn()
    {
        byte[] program = NormalMenuProgram();
        (GameSession session, EventEngine engine) = OpenNormalMenu(CreateParty(), program);
        PlayerAction action = new(PlayerActionKind.SwitchPartyMembers, 1, Quantity: 3);
        PlayerActionResult result = engine.Apply(JsonSerializer.Deserialize<PlayerAction>(JsonSerializer.Serialize(action))!);
        Assert.True(result.Accepted, result.Detail ?? $"menu entry {engine.PartyMenu?.NativeEntrySelection}; party {string.Join(",", session.CharacterVitals!.CombatParty)}");
        Assert.Equal(1, result.FramesAdvanced);
        Assert.Equal((ushort)1, engine.PartyMenu!.NativeReturnResult);
        Assert.Equal(new byte[] { 0, 3, 2 }, session.CharacterVitals!.CombatParty);
        Assert.Equal(new[] { 0, 3, 2 }, engine.Scene.Party);
        Assert.Equal(session.CharacterVitals.CombatParty, engine.PartyMenu.TemporaryCombatParty);
        JsonElement checkpoint = engine.Capture();
        GameSession resumedSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        EventEngine resumed = new(resumedSession);
        resumed.LoadField(74, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resumed.Restore(checkpoint);
        Assert.True(JsonElement.DeepEquals(checkpoint, resumed.Capture()));
        Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
        Assert.True(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()));
        Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Cancel)).Accepted);
        Assert.Equal(NativePartyMenuPhase.Closing, engine.PartyMenu!.Phase);
        Assert.Equal((ushort)1, engine.PartyMenu.NativeReturnResult);
        Assert.True(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()));
        for (int callback = 0; engine.PartyMenu!.Phase != NativePartyMenuPhase.FieldResumeInitialization; callback++)
        {
            Assert.True(callback < 5);
            Assert.True(engine.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(resumed.Apply(new PlayerAction(PlayerActionKind.Wait)).Accepted);
            Assert.True(JsonElement.DeepEquals(engine.Capture(), resumed.Capture()));
        }
        Assert.Equal(new[] { 255, 255, 255 }, engine.Scene.FieldPartyNativeActorIds);
        Assert.NotNull(engine.Scene.PartyBindingMemory);
        Assert.Equal(new[] { 0, 3, 2 }, engine.Scene.Party);
        Assert.Null(engine.LastFault);
    }

    [Fact]
    public void NormalMenuPublishesAdditionalMembersWithoutChangingFieldBindings()
    {
        byte[] program = NormalMenuProgram();
        (GameSession session, EventEngine engine) = OpenNormalMenu(CreateParty(), program);
        engine.ReadBattlePartyCapacity = () => 4;

        PlayerActionResult result = engine.Apply(new PlayerAction(PlayerActionKind.SwitchPartyMembers, 255, Quantity: 3));
        Assert.True(result.Accepted, result.Detail);
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, session.CharacterVitals!.GetBattlePartyRows(4));
        Assert.Equal(new byte[] { 0, 1, 2 }, engine.PartyMenu!.TemporaryCombatParty);
        Assert.Equal(new[] { 0, 1, 2 }, engine.Scene.Party);
        Assert.Equal((ushort)0, engine.PartyMenu.NativeReturnResult);

        JsonElement checkpoint = engine.Capture();
        GameSession resumedSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        EventEngine resumed = new(resumedSession);
        resumed.LoadField(74, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resumed.Restore(checkpoint);
        Assert.True(JsonElement.DeepEquals(checkpoint, resumed.Capture()));
        Assert.Equal(new byte[] { 0, 1, 2, 3 }, resumedSession.CharacterVitals!.GetBattlePartyRows(4));
    }

    [Fact]
    public void ExtendedNormalSwitchKeepsNativeFieldBindingsOnCheckpointRestore()
    {
        byte[] program = NormalMenuProgram();
        (GameSession session, EventEngine engine) = OpenNormalMenu(CreateParty(), program);
        engine.ConfigureExtendedRoster(true);

        PlayerActionResult result = engine.Apply(new PlayerAction(PlayerActionKind.SwitchPartyMembers, 1, Quantity: 10));
        Assert.True(result.Accepted, result.Detail);
        Assert.Equal(new byte[] { 0, 10, 2 }, session.CharacterVitals!.CombatParty);
        Assert.Equal(new[] { 0, 1, 2 }, engine.Scene.Party);
        Assert.Equal((ushort)0, engine.PartyMenu!.NativeReturnResult);

        JsonElement checkpoint = engine.Capture();
        GameSession resumedSession = new(JsonSerializer.Deserialize<GameState>(JsonSerializer.Serialize(session.State))!);
        EventEngine resumed = new(resumedSession);
        resumed.LoadField(74, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        resumed.Restore(checkpoint);
        Assert.True(JsonElement.DeepEquals(checkpoint, resumed.Capture()));
        Assert.Equal(new byte[] { 0, 10, 2 }, resumedSession.CharacterVitals!.CombatParty);
        Assert.Equal(new[] { 0, 1, 2 }, resumed.Scene.Party);
    }

    private static (GameSession Session, EventEngine Engine) OpenNormalMenu(NativeCharacterVitals vitals, byte[] program)
    {
        using GameAssets assets = new(TestResources.GameDirectory);
        GameSession session = GameSession.CreateColdProcess();
        session.StartNewGame();
        session.InitializeNewGameCharacterVitals(vitals);
        session.InitializeNewGameEconomy(assets.ReadNewGameEconomy());
        session.InitializeNewGameShotAvailability(assets.ReadNewGameShotAvailability());
        session.InitializeNewGameInputConfiguration(assets.ReadNewGameInputConfiguration());
        session.SetNativeEventOwners(session.WorldControls! with { Menu209 = 2 });
        EventEngine engine = new(session);
        engine.LoadField(74, program, [], nativeRuntimeProfile: NativeFieldRuntimeProfile.FF8En);
        session.SetNativeEventOwners(session.WorldControls! with { Menu209 = 2 });
        engine.Scene.Party.Clear();
        engine.Scene.Party.AddRange([0, 1, 2]);
        for (int callback = 0; engine.PartyMenu?.Phase != NativePartyMenuPhase.Active; callback++)
        {
            Assert.True(callback < 8, engine.LastFault?.ToString());
            engine.Step();
        }
        return (session, engine);
    }

    private static byte[] NormalMenuProgram()
    {
        // Declared command component with authentic installed menu owners; this is not a gameplay save.
        (Jsm.Opcode Opcode, int Argument)[] operations = [(Jsm.Opcode.LBL, 0), (Jsm.Opcode.RET, 8), (Jsm.Opcode.LBL, 1), (Jsm.Opcode.MENUNORMAL, 0), (Jsm.Opcode.HALT, 0), (Jsm.Opcode.RET, 8)];
        byte[] bytes = new byte[16 + operations.Length * 4];
        bytes[2] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6), 16);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), (ushort)operations.Length);
        for (int index = 0; index < operations.Length; index++)
        {
            (Jsm.Opcode opcode, int argument) = operations[index];
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(16 + index * 4), argument == 0 ? (int)opcode : (int)opcode << 24 | argument);
        }
        return bytes;
    }

    private static NativeCharacterVitals CreateParty(int? locked = null, int? forced = null)
    {
        Assert.SkipUnless(Directory.Exists(TestResources.GameDirectory), "Installed character and kernel resources are required.");
        using GameAssets assets = new(TestResources.GameDirectory);
        NativeCharacterVitals source = assets.ReadNewGameCharacterVitals();
        NativeCharacterVital[] records = source.Records.Select((record, row) => record with { CharacterId = (byte)row, MemberFlags = (ushort)(row < 4 ? 1 | (locked == row ? 2 : 0) | (forced == row ? 4 : 0) : 0) }).ToArray();
        return (source with { Records = records, CombatParty = [0, 1, 2], DreamEnabled = false }).RebuildParty();
    }
}
