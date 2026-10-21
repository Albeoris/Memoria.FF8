using System.Text.Json;
using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;

namespace Memoria.FF8.App.Tests;

public sealed class NativeBattleReadyMenuTests
{
    [Fact]
    public void Raw_select_input_hides_the_registered_menu_and_JSON_restore_releases_it_on_the_same_callback()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets);
        battle.Tick();
        BattleHudController hud = Hud(battle);
        string actor = hud.SelectedActor!.Id;
        Assert.False(hud.CommandMenuHidden);
        battle.PublishNativeScanMappedButtons(0x100);
        for (int callback = 0; callback < 16 && !hud.CommandMenuHidden; callback++)
        {
            battle.Tick();
        }
        Assert.True(hud.CommandMenuHidden);
        Assert.Equal((ushort?)0x100, battle.NativeGraphics!.UiServices!.HeldButtons16);
        hud.Navigate(MenuInput.Confirm);
        hud.CycleCommandActor();
        Assert.False(hud.SelectingTarget);
        Assert.Equal(actor, hud.SelectedActor!.Id);

        string saved = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved)!);
        Assert.True(Hud(restored).CommandMenuHidden);
        battle.PublishNativeScanMappedButtons(0);
        restored.PublishNativeScanMappedButtons(0);
        for (int callback = 0; callback < 16 && hud.CommandMenuHidden; callback++)
        {
            battle.Tick();
            restored.Tick();
            Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        }
        Assert.False(hud.CommandMenuHidden);
        Assert.False(Hud(restored).CommandMenuHidden);
        Assert.Equal(actor, hud.SelectedActor!.Id);
        hud.Navigate(MenuInput.Confirm);
        Assert.True(hud.SelectingTarget);
    }

    [Fact]
    public void Native_Limit_selection_waits_for_UI_registration_and_survives_HUD_recreation()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets, limits: true);
        string before = JsonSerializer.Serialize(battle.Capture());
        Assert.False(battle.HasNativeLimitSelection("party-0"));
        Assert.Throws<InvalidOperationException>(() => battle.SelectPlayerCommandActor("party-0"));
        Assert.Throws<InvalidOperationException>(() => battle.QueueNativePlayerCommand("party-0", "enemy", 5, 0));
        Assert.Equal(before, JsonSerializer.Serialize(battle.Capture()));

        battle.Tick();

        Assert.True(battle.HasNativeLimitSelection("party-0"));
        string selected = JsonSerializer.Serialize(battle.Capture());
        Hud(battle).BuildView();
        Assert.Equal(selected, JsonSerializer.Serialize(battle.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(selected)!);
        Hud(restored).BuildView();
        Assert.Equal(selected, JsonSerializer.Serialize(restored.Capture()));
    }

    [Fact]
    public void Raw_hud_waits_for_ready_registration_and_switches_in_the_registered_order()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets);
        BattleHudController hud = Hud(battle);
        Assert.Null(hud.SelectedActor);
        Assert.False(battle.CanChoosePlayerCommand("party-0"));
        battle.Tick();
        Assert.Equal("party-0", hud.SelectedActor!.Id);
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        NativeBattleUiServices ui = graphics.UiServices!;
        (NativeBattleUiCommands commands, uint mask, _) = ui.Commands.Enqueue(0, 18).Enqueue(0, 17).Dispatch(ui.ReadyMask);
        battle = BattleSession.Restore(battle.Capture() with { NativeGraphics = graphics with { UiServices = ui with { Commands = commands, ReadyMask = mask } } });
        hud = Hud(battle);
        Assert.Equal(new[] { "party-3", "party-4", "party-0" }, battle.ReadyPlayerCommandOrder.Select(actor => actor.Id));
        Assert.Equal("party-3", hud.SelectedActor!.Id);
        hud.SelectParty("party-0");
        Assert.Equal((sbyte)0, battle.NativeGraphics!.UiServices!.Commands.SelectedActor);
        hud.CycleCommandActor();
        Assert.Equal("party-3", hud.SelectedActor!.Id);
        hud.Navigate(MenuInput.PageLeft);
        Assert.Equal("party-0", hud.SelectedActor!.Id);
        string saved = JsonSerializer.Serialize(battle.Capture());
        hud.BuildView();
        Assert.Equal(saved, JsonSerializer.Serialize(battle.Capture()));
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(saved)!);
        Assert.Equal("party-0", Hud(restored).SelectedActor!.Id);
    }

    [Fact]
    public void Logical_formula_status_does_not_invalidate_a_registered_menu_before_UI18_publication()
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets);
        battle.Tick();
        BattleSnapshot saved = battle.Capture();
        BattleCombatantSnapshot[] actors = saved.Combatants.ToArray();
        actors[0] = actors[0] with { CurrentHp = 0, Atb = 0, NativeAtb = actors[0].NativeAtb! with { Current = 0 }, NativeStatus = actors[0].NativeStatus! with { Saved = 1 } };
        battle = BattleSession.Restore(saved with { Combatants = actors });
        Assert.True(battle.CanChoosePlayerCommand("party-0"));
        Assert.Equal("party-0", Hud(battle).SelectedActor!.Id);
        NativeBattleGraphicsState graphics = battle.NativeGraphics!;
        NativeBattleUiServices ui = graphics.UiServices!;
        battle = BattleSession.Restore(battle.Capture() with { NativeGraphics = graphics with { UiServices = ui with { Commands = ui.Commands.Enqueue(0, 18) } } });
        battle.Tick();
        Assert.False(battle.CanChoosePlayerCommand("party-0"));
        Assert.Equal("party-3", Hud(battle).SelectedActor!.Id);
    }

    [Theory]
    [InlineData((ushort)4, true)]
    [InlineData((ushort)1, false)]
    public void Contact_publication_uses_the_physical_queue_before_removing_ready_and_queued_commands(ushort savedStatus, bool retainsQueuedCommand)
    {
        using GameAssets game = new(TestResources.GameDirectory);
        using BattleAssets assets = new(game.GameDirectory);
        BattleSession battle = Create(game, assets);
        battle.Tick();
        battle.QueueAttack("party-0", "enemy");
        Assert.Equal(1, battle.NativeCommandBanks.CountActor(0));
        BattleSnapshot snapshot = battle.Capture();
        NativeBattleUiServices ui = snapshot.NativeGraphics!.UiServices!;
        (NativeBattleUiCommands commands, uint readyMask, _) = ui.Commands.Dispatch(ui.ReadyMask);
        (commands, readyMask, _) = commands.Enqueue(0, 17).Dispatch(readyMask);
        snapshot = snapshot with { NativeGraphics = snapshot.NativeGraphics with { UiServices = ui with { Commands = commands, ReadyMask = readyMask } } };
        BattleCombatantSnapshot[] actors = snapshot.Combatants.ToArray();
        actors[0] = actors[0] with { CurrentHp = savedStatus == 1 ? 0 : actors[0].CurrentHp, NativeStatus = actors[0].NativeStatus! with { Saved = savedStatus, Presentation = new(0, 0) } };
        NativeBattleContactPacket packet = new(new(0, 0, 1, 4, savedStatus, 0, 0), new(255, 0, 0, 0, 0, 0, 0));
        battle = BattleSession.Restore(snapshot with { Combatants = actors, NativeClock = snapshot.NativeClock! with { ActionPause = 1 }, NativeGraphics = snapshot.NativeGraphics! with { PendingContacts = [packet] } });
        string before = JsonSerializer.Serialize(battle.Capture());
        BattleSession restored = BattleSession.Restore(JsonSerializer.Deserialize<BattleSnapshot>(before)!);
        battle.Tick();
        restored.Tick();
        Assert.Equal(JsonSerializer.Serialize(battle.Capture()), JsonSerializer.Serialize(restored.Capture()));
        Assert.Equal(retainsQueuedCommand ? 1 : 0, battle.NativeCommandBanks.CountActor(0));
        Assert.Equal(retainsQueuedCommand, (battle.NativeGraphics!.UiServices!.ReadyMask & 1) != 0);
        Assert.Equal((ushort)0, battle.NativePartyStatusMirrors.Saved[0]);
        Assert.Equal(savedStatus, battle.Capture().Combatants[0].NativeStatus!.Presentation.Saved);
    }

    private static BattleHudController Hud(BattleSession battle) => new(battle, battle.QueueAttack, () => { }, () => battle.AdvanceTurn());

    private static BattleSession Create(GameAssets game, BattleAssets assets, bool limits = false)
    {
        BattleEncounter encounter = assets.LoadEncounter(94);
        BattleCombatantDefinition[] actors = new[] { 0, 3, 4 }.Select(character => new BattleCombatantDefinition { Id = $"party-{character}", Name = $"Character{character}", IsPlayer = true, ModelId = character, WeaponId = assets.WeaponDefinitions.First(weapon => weapon.CharacterId == character).Id, InitialAtb = BattleSession.AtbMaximum, Speed = 1, NativeDeathCount = 0 }).Append(new() { Id = "enemy", Name = "Ifrit", ModelId = 61, Speed = 1, SceneSlot = encounter.Enemies.Single(enemy => enemy.ModelId == 61).Slot }).ToArray();
        if (limits)
        {
            actors[0] = actors[0] with { NativeLimit = assets.ReadNativeLimitDefinition(0) };
        }
        NativeBattleGraphicsDefinition graphics = assets.ReadNativeGraphicsDefinition(actors, game.ReadNativeBattleGraphicsRules(), encounter);
        BattleDefinition definition = new() { EncounterId = 94, StageId = encounter.StageId, NativeRetainedMemory = game.ReadColdProcessNativeBattleMemory(), NativeEntryWaveFile = assets.ReadNativeBattleFile(0xA5), NativeGlobalSoundProcess = NativeGlobalSoundProcess.FromBufferlessColdProcess(), NativeFlags = 0x80, NativeEntry = new(0, 0), NativeGlobalRandom = new(0), NativeGraphics = graphics, NativeClockRules = new(10), NativeBattleSpeed = 2, NativeSavedOptions = 1, NativeCommandSequences = game.ReadNativeBattleCommandSequences(), NativeStageSource = assets.LoadStage(encounter.StageId).NativeSource, NativeStageCameraBank = assets.LoadStage(encounter.StageId).NativeCameraBank, NativeStageCameraSelectors = [encounter.PrimaryCamera, encounter.AlternativeCamera], NativeStageCameraControl = assets.LoadStage(encounter.StageId).NativeCameraControl, Combatants = actors };
        definition = definition with { NativeLimitsDisabled = false, NativeCommandNames = assets.ReadNativeCommandNames(), NativeInputButtonMap = game.ReadNewGameInputConfiguration().ButtonMap };
        definition = NativeBattleComponentInputs.PrepareMusic(definition, game.GameDirectory, 0, 5, -1);
        BattleSession battle = new(definition, BattleAtbMode.TurnBased);
        for (int callback = 0; callback < 200 && !battle.NativeEntryReady; callback++)
        {
            battle.Tick();
        }
        Assert.True(battle.NativeEntryReady);
        return battle;
    }
}
