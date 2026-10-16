using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed record BattleControlCommand(BattleInput? Input = null, BattleDefinition? Definition = null, BattleAtbMode? Mode = null, MenuInput? MenuInput = null, string? Policy = null, bool? RandomEncounters = null);

internal sealed partial class RecreateGame
{
    private BattleAssets? presentationBattleAssets;
    private BattleDebugCatalog? presentationBattleCatalog;
    private BattleSession? debugBattle;
    private BattleHudController? battleHud;
    private BattleDebugController? battleDebug;
    private BattleScenePresentation? battleScene;
    private Texture2D? battleTexture;
    private double battleSeconds;
    private bool previousBattleMouse;
    private BattleSession? presentedBattle;
    private BattleDebugPresets? battleDebugPresets;
    private BattleSession? ActiveBattle => debugBattle ?? world?.Battle;
    private BattleAssets PresentationBattleResources => presentationBattleAssets ??= new BattleAssets(options.GameDirectory);

    private bool UpdateBattleDebugUi(InputFrame input)
    {
        if (keyboard.BattleDebugRequested)
        {
            if (battleDebug is null)
            {
                OpenBattleDebug();
            }
            else
            {
                battleDebug = null;
            }
        }
        if (battleDebug is null)
        {
            return false;
        }
        NavigateBattleView(battleDebug.Navigate, input);
        if (battleDebug is not null)
        {
            PointerBattleView(battleDebug.BuildView());
        }
        return true;
    }

    private bool UpdateBattle(TimeSpan elapsed, InputFrame input)
    {
        try
        {
            return UpdateBattleCore(elapsed, input);
        }
        catch (Exception exception) when (exception is Memoria.FF8.Events.EventScriptException or NotSupportedException or InvalidDataException or InvalidOperationException)
        {
            fieldFaulted = true;
            error = exception.Message;
            Console.Error.WriteLine(exception);
            return true;
        }
    }

    private bool UpdateBattleCore(TimeSpan elapsed, InputFrame input)
    {
        if (fieldFaulted && debugBattle is null)
        {
            return false;
        }
        if (world?.HasPendingBattle == true && world.Battle is null)
        {
            world.ApplyBattleInput(new BattleInput(BattleInputKind.StartEncounter, Mode: uiPreferences.AtbMode));
        }
        BattleSession? active = ActiveBattle;
        if (active is null)
        {
            return false;
        }
        EnsureBattleHud(active);
        battleScene!.AdvancePresentation(elapsed);
        battleHud!.AdvancePresentation(elapsed);
        active.AdvanceNativeShotPresentation(elapsed);
        active.AdvanceNativeDuelPresentation(elapsed, keyboard.HeldFieldButtons);
        active.AdvanceNativeRenzokukenPresentation(elapsed);
        SynchronizeBattleAudio();
        BattleCombatant? shotActor = active.Combatants.FirstOrDefault(actor => active.GetNativeShot(actor.Id) is not null);
        bool shotInput = keyboard.GunbladeTriggerRequested && shotActor is not null;
        if (shotInput)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.ShotTrigger, ActorId: shotActor!.Id));
        }
        bool gunbladeInput = !shotInput && keyboard.GunbladeTriggerRequested && active.CanTriggerGunblade;
        if (gunbladeInput)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.GunbladeTrigger));
        }
        if (keyboard.BattleInstantVictoryRequested && active.Outcome == BattleOutcome.Active)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.InstantVictory));
        }
        else if (keyboard.BattleAdvanceTurnRequested && active.Mode == BattleAtbMode.TurnBased && active.Outcome == BattleOutcome.Active)
        {
            battleHud!.AdvanceTurn();
        }
        else
        {
            NavigateBattleView(battleHud!.Navigate, input, gunbladeInput || shotInput);
            if (ActiveBattle is null || battleHud is null)
            {
                return true;
            }
        }
        PointerBattleView(battleHud!.BuildView());
        if (ActiveBattle is null)
        {
            return true;
        }
        if (!options.ControlStep && active.Outcome == BattleOutcome.Active)
        {
            battleSeconds += elapsed.TotalSeconds;
            int frames = Math.Min(30000, (int)(battleSeconds * BattleSession.FramesPerSecond));
            if (frames > 0)
            {
                battleSeconds -= frames / (double)BattleSession.FramesPerSecond;
                ApplyBattleCommand(new BattleInput(BattleInputKind.Tick, frames));
            }
        }
        return true;
    }

    private void NavigateBattleView(Action<MenuInput> navigate, InputFrame input, bool skipCharacterPage = false)
    {
        if (input.MenuVertical != 0)
        {
            navigate(input.MenuVertical < 0 ? MenuInput.Up : MenuInput.Down);
        }
        if (input.MenuHorizontal != 0)
        {
            navigate(input.MenuHorizontal < 0 ? MenuInput.Left : MenuInput.Right);
        }
        if (input.MenuPage != 0)
        {
            navigate(input.MenuPage < 0 ? MenuInput.PageUp : MenuInput.PageDown);
        }
        if (!skipCharacterPage && keyboard.UiCharacterPage != 0)
        {
            navigate(keyboard.UiCharacterPage < 0 ? MenuInput.PageLeft : MenuInput.PageRight);
        }
        if (input.Confirm || keyboard.UiConfirmRequested)
        {
            navigate(MenuInput.Confirm);
        }
        if (input.Cancel || keyboard.UiCancelRequested)
        {
            navigate(MenuInput.Cancel);
        }
    }

    private void PointerBattleView(UiView view)
    {
        MouseState mouse = Mouse.GetState();
        bool pressed = IsActive && mouse.LeftButton == ButtonState.Pressed;
        view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        view.Pointer(mouse.X, mouse.Y, pressed && !previousBattleMouse);
        previousBattleMouse = pressed;
    }

    private void EnsureBattleHud(BattleSession active)
    {
        if (presentedBattle != active)
        {
            if (active.GunbladeMode != uiPreferences.GunbladeMode)
            {
                ApplyBattleCommand(new BattleInput(BattleInputKind.SetGunbladeMode, GunbladeMode: uiPreferences.GunbladeMode));
            }
            DisposeBattleTextures();
            presentedBattle = active;
            battleScene = new BattleScenePresentation(PresentationBattleResources, active.Capture().Definition, debugBattle is null);
            MenuData? rewardNames = debugBattle is null ? ReadLiveMenuData() : null;
            battleHud = new BattleHudController(active, (actor, target) => ApplyBattleCommand(new BattleInput(BattleInputKind.Attack, ActorId: actor, TargetId: target)), OpenBattleDebug, () => ApplyBattleCommand(new BattleInput(BattleInputKind.AdvanceTurn)), CompleteBattleResult, input => ApplyBattleCommand(input), () => debugBattle is null ? world!.PreviewBattleRewards() : null, id => rewardNames?.GuardianForces.FirstOrDefault(guardian => guardian.Id == $"gf-{id}")?.Name ?? liveMenuCatalog?.GuardianForceName(id) ?? $"GF {id}", id => liveMenuCatalog?.ItemName(id) ?? $"Item {id}", id => liveMenuCatalog?.CardName(id) ?? $"Card {id}", world?.Events.Scene.Names);
            battleHud.SoundRequested += cue => fieldAudio?.PlayUiSound(cue);
            battleHud.NativeButtonBindingHint = keyboard.NativeButtonBindingHint;
            battleSeconds = 0;
        }
        battleHud!.TextScale = uiPreferences.TextScale / 100f * uiPreferences.UiFontSize / 24f;
        battleHud.GunbladeBindingHint = keyboard.BindingHint(ControlAction.RotateRight);
    }

    private void CompleteBattleResult()
    {
        if (debugBattle is not null)
        {
            ClearBattlePresentation();
            return;
        }
        if (ActiveBattle?.CanReturnToField == true)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.Return));
            ClearBattlePresentation();
            fieldSeconds = 0;
            return;
        }
        OpenPlayerSaveMenu(PlayerSaveMenuMode.Load);
    }

    private int ApplyBattleCommand(BattleInput input)
    {
        if (debugBattle is null)
        {
            int dispatched = world!.ApplyBattleInput(input);
            SynchronizeBattleAudio();
            return dispatched;
        }
        input.Validate();
        long before = debugBattle.Frame;
        switch (input.Kind)
        {
            case BattleInputKind.Tick:
                for (int frame = 0; frame < input.Frames; frame++)
                {
                    debugBattle.Tick();
                }
                break;
            case BattleInputKind.Attack:
                debugBattle.QueueAttack(input.ActorId!, input.TargetId!);
                break;
            case BattleInputKind.Action:
                debugBattle.QueueAction(input.ActorId!, input.TargetId!, input.Ability, input.ActionId, input.DrawCast, input.MagicTargets.IsEmpty ? null : input.MagicTargets);
                break;
            case BattleInputKind.AdvanceTurn:
                debugBattle.AdvanceTurn();
                break;
            case BattleInputKind.SetMode:
                debugBattle.SetMode(input.Mode);
                break;
            case BattleInputKind.SetGunbladeMode:
                debugBattle.SetGunbladeMode(input.GunbladeMode);
                break;
            case BattleInputKind.GunbladeTrigger:
                debugBattle.TriggerGunblade();
                break;
            case BattleInputKind.ShotTrigger:
                debugBattle.TriggerNativeShot(input.ActorId ?? throw new InvalidDataException("Shot trigger requires its process owner."));
                break;
            case BattleInputKind.InstantVictory:
                debugBattle.InstantVictory();
                break;
            case BattleInputKind.InstantDefeat:
                debugBattle.InstantDefeat();
                break;
            default:
                throw new InvalidOperationException("This command requires a game-owned encounter.");
        }
        SynchronizeBattleAudio();
        return checked((int)(debugBattle.Frame - before));
    }

    private BattleDebugCatalog ReadBattleDebugCatalog()
    {
        if (presentationBattleCatalog is not null)
        {
            return presentationBattleCatalog with { Players = presentationBattleCatalog.Players.Select(actor => actor with { Name = LiveMenuData.CharacterName(session.State, checked((byte)actor.ModelId), events?.Scene.Names, liveMenuCatalog) }).ToArray() };
        }
        _ = ReadLiveMenuData();
        BattleAssets resources = PresentationBattleResources;
        return presentationBattleCatalog = new BattleDebugCatalog
        {
            Players = resources.PlayerCharacterIds.Select(id => new BattleCombatantDefinition { Id = $"player-{id}", Name = LiveMenuData.CharacterName(session.State, checked((byte)id), events?.Scene.Names, liveMenuCatalog), IsPlayer = true, ModelId = id, MaximumHp = 1000, CurrentHp = 1000, Speed = 100, Attack = 80, Defense = 20 }).ToArray(),
            Enemies = [BattleDebugDefaults.Dummy, .. resources.EnemyModelIds.Select(id => new BattleCombatantDefinition { Id = $"enemy-{id}", Name = resources.ReadEnemyName(id), ModelId = id, MaximumHp = 500, CurrentHp = 500, Speed = 80, Attack = 50, Defense = 10 })],
            Weapons = resources.WeaponDefinitions.Select(weapon => new BattleDebugEquipment(weapon.Id, weapon.Name, weapon.CharacterId)).ToArray(),
            Equipment = Enumerable.Range(0, 16).Select(id => new BattleDebugEquipment(id, liveMenuCatalog!.GuardianForceName(id) ?? $"GF {id}")).ToArray(),
            StageIds = resources.StageIds.ToArray(),
            PlayerCostumes = resources.PlayerCharacterIds.ToDictionary(id => id, id => Enumerable.Range(0, resources.PlayerModelNames(id).Count).ToArray())
        };
    }

    private void OpenBattleDebug()
    {
        gameMenus?.Close();
        BattleDebugCatalog catalog = ReadBattleDebugCatalog();
        BattleDefinition? activeEncounter = world?.Battle?.Capture().Definition;
        BattleCombatantDefinition? lastEnemy = activeEncounter?.Combatants.FirstOrDefault(actor => !actor.IsPlayer);
        int stageId = activeEncounter?.StageId ?? catalog.StageIds.First();
        if (lastEnemy is null && events?.Scene.NativeBattleCompletions?.LastOrDefault(completion => !completion.Suppressed) is { } completion)
        {
            BattleEncounter encounter = PresentationBattleResources.LoadEncounter(completion.Encounter);
            BattleEnemyPlacement enemy = encounter.Enemies.First(placement => !placement.Hidden && !placement.Untargetable);
            lastEnemy = catalog.Enemies.Single(actor => actor.ModelId == enemy.ModelId);
            stageId = encounter.StageId;
        }
        BattleCombatantDefinition[] currentParty;
        NativeCharacterVitals currentVitals;
        NativeEconomy currentEconomy;
        if (session.State.Scene == SceneKind.Title)
        {
            using GameAssets assets = new(options.GameDirectory);
            currentParty = BattlePartyProjection.CreateNewGame(assets, liveMenuCatalog!, PresentationBattleResources);
            currentVitals = assets.ReadNewGameCharacterVitals();
            currentEconomy = assets.ReadNewGameEconomy();
        }
        else
        {
            currentParty = world?.CreateBattleParty() ?? throw new NotSupportedException("Battle laboratory requires the current game-owned party.");
            currentVitals = session.State.CharacterVitals ?? throw new NotSupportedException("Battle laboratory requires the current native GF owner.");
            currentEconomy = session.State.Economy ?? throw new NotSupportedException("Battle laboratory requires the current native item owner.");
        }
        BattleActionDefinition[] actions = BattlePartyProjection.CreateActions(currentVitals, PresentationBattleResources.ActionDefinitions);
        BattleStock[] items = currentEconomy.Items.Where(stock => stock.ItemId > 0 && stock.Count > 0).Select(stock => new BattleStock(stock.ItemId, stock.Count)).ToArray();
        BattleDefinition initial = BattleDebugDefaults.Create(currentParty, lastEnemy, stageId, world?.Battle, actions, items);
        battleDebugPresets ??= new BattleDebugPresets(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.SavePath))!, "battle-presets.json"));
        battleDebug = new BattleDebugController(initial, catalog, uiPreferences.AtbMode, StartDebugBattle, () => battleDebug = null, battleDebugPresets);
        battleDebug.SoundRequested += sound => fieldAudio?.PlayUiSound(sound);
        SynchronizeUiMoviePause();
    }

    private void ApplyBattleDebugSettings(BattleAtbMode mode, string policy, bool? randomEncounters)
    {
        UiPreferences nextPreferences = ReadCurrentUiPreferences().WithSetting("Battle policy", policy) with { AtbMode = mode };
        ApplyCurrentBattleSettings(nextPreferences, "Battle policy");
        if (randomEncounters is bool enabled)
        {
            nextPreferences = nextPreferences with { RandomEncounters = enabled };
            ApplyCurrentBattleSettings(nextPreferences, "Random encounters");
        }
        ApplyCurrentBattleSettings(nextPreferences, "Battle ATB mode");
        nextPreferences.Save(UiPreferencesPath);
        uiPreferences = nextPreferences;
    }

    private void StartDebugBattle(BattleDefinition definition, BattleAtbMode mode)
    {
        BattleSession next = new(definition, mode);
        _ = PresentationBattleResources.LoadStage(definition.StageId ?? throw new ArgumentException("A debug battle needs a stage ID."));
        _ = new BattleScenePresentation(PresentationBattleResources, definition, false);
        debugBattle = next;
        battleDebug = null;
        gameMenus?.Close();
        EnsureBattleHud(next);
        SynchronizeBattleAudio();
    }

    private void DrawBattle(Rectangle bounds)
    {
        BattleSession active = ActiveBattle!;
        EnsureBattleHud(active);
        PixelBounds size = ViewportLayout.WorldRenderTarget(bounds.Width, bounds.Height, options.WorldRenderLimit, false);
        if (battleTexture?.Width != size.Width || battleTexture.Height != size.Height)
        {
            battleTexture?.Dispose();
            battleTexture = new Texture2D(GraphicsDevice, size.Width, size.Height);
        }
        battleTexture!.SetData(battleScene!.Render(active, size.Width, size.Height));
        PixelBounds viewport = ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
        batch.Draw(battleTexture, new Rectangle(viewport.X, viewport.Y, viewport.Width, viewport.Height), Color.White);
        battleScene.DrawEffects(active, uiCanvas!, new UiRect(viewport.X, viewport.Y, viewport.Width, viewport.Height));
        UiView view = battleHud!.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(uiCanvas!, uiTheme);
    }

    private void DrawBattleDebugUi(Rectangle bounds)
    {
        if (battleDebug is not null)
        {
            batch.Draw(pixel, bounds, new Color(0, 0, 0, 225));
            UiView view = battleDebug.BuildView();
            view.Resize(bounds.Width, bounds.Height);
            view.Draw(uiCanvas!, uiTheme);
        }
    }

    private object? BattleStatus()
    {
        BattleSession? active = ActiveBattle;
        return active is null ? null : new { IsDebug = debugBattle is not null, active.Mode, active.Frame, active.Outcome, active.Name, active.Combatants, active.ActiveAction, active.QueuedActions, RecentEvents = active.Events.TakeLast(12).ToArray(), uiPreferences.BattlePolicy, DebugMenuOpen = battleDebug is not null };
    }

    private object DispatchBattleControl(ControlRequest request)
    {
        BattleControlCommand command = request.Battle ?? new();
        UiPreferences currentPreferences = ReadCurrentUiPreferences();
        switch (request.Command.ToLowerInvariant())
        {
            case "battle-start":
                StartDebugBattle(command.Definition ?? throw new ArgumentException("Battle Definition is required."), command.Mode ?? uiPreferences.AtbMode);
                break;
            case "battle-input":
                ApplyBattleCommand(command.Input ?? throw new ArgumentException("Battle Input is required."));
                break;
            case "battle-settings":
                ApplyBattleDebugSettings(command.Mode ?? currentPreferences.AtbMode, command.Policy ?? currentPreferences.BattlePolicy, command.RandomEncounters);
                break;
            case "battle-debug-open":
                OpenBattleDebug();
                break;
            case "battle-debug-close":
                battleDebug = null;
                break;
            case "battle-debug-input":
                (battleDebug ?? throw new InvalidOperationException("Open Battle Debug first.")).Navigate(command.MenuInput ?? throw new ArgumentException("MenuInput is required."));
                break;
            case "battle-close":
                if (debugBattle is null)
                {
                    throw new InvalidOperationException("Only an isolated debug scenario can be closed without its game-owned outcome.");
                }
                ClearBattlePresentation();
                break;
            case "battle-status":
                break;
            default:
                throw new ArgumentException("Battle commands: battle-start, battle-input, battle-status, battle-debug-open/close/input, battle-close.");
        }
        return new { Battle = BattleStatus(), DebugMenuOpen = battleDebug is not null, Definition = battleDebug?.Definition };
    }

    private void DisposeBattleTextures()
    {
        battleTexture?.Dispose();
        battleTexture = null;
        battleScene = null;
    }

    private void ClearBattlePresentation(bool synchronizeAudio = true)
    {
        debugBattle = null;
        if (synchronizeAudio)
        {
            SynchronizeBattleAudio();
        }
        battleDebug = null;
        battleHud = null;
        presentedBattle = null;
        battleSeconds = 0;
        DisposeBattleTextures();
    }

    private void DisposeBattlePresentation()
    {
        ClearBattlePresentation(synchronizeAudio: false);
        battleAudio = null;
        presentationBattleAssets?.Dispose();
    }
}
