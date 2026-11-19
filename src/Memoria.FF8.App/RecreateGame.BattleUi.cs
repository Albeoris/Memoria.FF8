using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed record BattleControlCommand(BattleInput? Input = null, BattleDefinition? Definition = null, BattleAtbMode? Mode = null, MenuInput? MenuInput = null, string? Policy = null, bool? RandomEncounters = null, BattleNativeControlInput? Native = null);

internal sealed partial class RecreateGame
{
    private BattleAssets? presentationBattleAssets;
    private GameAssets? practiceBattleGameAssets;
    private BattleDebugCatalog? presentationBattleCatalog;
    private BattleSession? debugBattle;
    private BattleHudController? battleHud;
    private object? authoringScanOwner;
    private long authoringScanLifetime;
    private readonly Dictionary<int, (uint Source, string Key, int Model)> authoringScanWindows = [];
    private readonly Dictionary<int, int> battleGuardianForceLearning = [];
    private BattleDebugController? battleDebug;
    private BattleScenePresentation? battleScene;
    private Texture2D? battleTexture;
    private bool previousBattleMouse;
    private bool slotsInputReleasePending;
    private bool SlotsModalBlocksGameplay => ActiveBattle is { } active && (active.PendingSlotsDeal is not null || ReferenceEquals(active, presentedBattle) && (battleHud?.CardSlotsOwnsInput == true || slotsInputReleasePending));
    private bool DuelOwnsInput => ActiveBattle is { } active && active.Combatants.Any(actor => active.GetNativeDuel(actor.Id) is { WidgetClosed: false });
    private bool BattleModalBlocksGameplay => SlotsModalBlocksGameplay || DuelOwnsInput;
    private BattleSession? presentedBattle;
    private BattleDebugPresets? battleDebugPresets;
    private BattleSession? ActiveBattle => debugBattle ?? world?.Battle;
    private BattleAssets PresentationBattleResources => presentationBattleAssets ??= new BattleAssets(options.GameDirectory, resourceHost?.Resources, modelOverrides: resourceHost);

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
                CloseBattleDebug();
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

    private bool UpdateBattle(TimeSpan elapsed, InputFrame input, TimeSpan presentationElapsed)
    {
        try
        {
            return UpdateBattleCore(elapsed, input, presentationElapsed);
        }
        catch (Exception exception) when (exception is Memoria.FF8.Events.EventScriptException or NotSupportedException or IOException or InvalidDataException or InvalidOperationException or ArgumentException)
        {
            fieldFaulted = true;
            CaptureErrorIncident(exception, "battle update");
            if (diagnosticDriver is not null)
            {
                diagnosticBattleFault = exception;
            }
            error = exception.Message;
            Console.Error.WriteLine(exception);
            return true;
        }
    }

    private bool UpdateBattleCore(TimeSpan elapsed, InputFrame input, TimeSpan presentationElapsed)
    {
        using IDisposable visibleArea = BeginBattleUpdateVisibleArea();
        if (fieldFaulted && debugBattle is null)
        {
            return false;
        }
        if (world?.HasPendingBattle == true && world.Battle is null)
        {
            BeginBattleEntry();
            return true;
        }
        BattleSession? active = ActiveBattle;
        if (active is null)
        {
            return false;
        }
        EnsureBattleHud(active);
        if (effectPreview is not null)
        {
            if (effectPreviewSubmitted)
            {
                AdvanceMeasuredBattlePresentation(active, elapsed);
                if (effectComponentSource is not null)
                {
                    world!.SynchronizeBattlePresentation();
                }
                presentationTiming.ObservePresentationSource(active, active.PreparedPresentation, presentationElapsed.Ticks > 0 ? elapsed.Ticks / (double)presentationElapsed.Ticks : 1);
                SynchronizeBattleAudio();
            }
            battlePresentationUpdated = true;
            return true;
        }
        if (DiagnosticPresentationHeld)
        {
            battlePresentationUpdated = true;
            return true;
        }
        bool duelOwned = DuelOwnsInput;
        bool slotsOwned = SlotsModalBlocksGameplay;
        bool confirmHeld = (keyboard.HeldFieldButtons & 0x40) != 0;
        bool mouseHeld = physicalInput.ReadMouse().LeftButton == ButtonState.Pressed;
        battleHud!.AdvanceSlotsPresentation(elapsed, confirmHeld, mouseHeld);
        if (slotsOwned && !battleHud.CardSlotsOwnsInput)
        {
            slotsInputReleasePending = confirmHeld || mouseHeld;
        }
        else if (!confirmHeld && !mouseHeld)
        {
            slotsInputReleasePending = false;
        }
        slotsOwned |= SlotsModalBlocksGameplay;
        bool skipInput = !slotsOwned && !duelOwned && keyboard.GuardianFormSkipRequested && active.CanSkipGuardianFormFinisher;
        bool boostInput = !slotsOwned && !duelOwned && keyboard.GuardianBoostRequested && active.NativeGuardianBoost is not null && battleHud.PresentationUi.GuardianBoostOwnsInput;
        if (boostInput)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.GuardianBoost));
        }
        if (skipInput)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.GuardianFormSkip));
        }
        if (!options.ControlExclusive || options.IsDiagnosticGraphicalHost)
        {
            active.PublishNativeEscapeButtons(slotsOwned || duelOwned ? (ushort)0 : keyboard.HeldControlButtons);
            active.PublishNativeScanMappedButtons(slotsOwned || duelOwned ? (ushort)0 : ReadDiagnosticScanMappedButtons(active, keyboard.HeldFieldButtons));
            active.PublishNativeGuardianBoostMappedButtons(slotsOwned || duelOwned ? (ushort)0 : keyboard.HeldFieldButtons);
        }
        AdvanceMeasuredBattlePresentation(active, elapsed);
        battlePresentationUpdated = true;
        presentationTiming.ObservePresentationSource(active, active.PreparedPresentation, presentationElapsed.Ticks > 0 ? elapsed.Ticks / (double)presentationElapsed.Ticks : 1);
        if (ReferenceEquals(active, world?.Battle))
        {
            world.SynchronizeBattlePresentation();
            if (world.Battle is null)
            {
                SynchronizeBattleAudio(active);
                SynchronizeBattleAudio();
                return true;
            }
        }
        battleHud!.AdvancePresentation(elapsed);
        ObserveScanAuthoringDialogues(active);
        if (ActiveBattle is null || battleHud is null)
        {
            return true;
        }
        if (!options.ControlExclusive || options.IsDiagnosticGraphicalHost)
        {
            battleHud.SetNativeCommandButtons(duelOwned ? (ushort)0 : keyboard.HeldFieldButtons);
            active.SubmitNativeDuelInput(ReadDiagnosticDuelButtons(active, slotsOwned ? (ushort)0 : keyboard.HeldFieldButtons));
        }
        ObserveDiagnosticDuelUi(active);
        SynchronizeBattleAudio();
        BattleCombatant? shotActor = active.Combatants.FirstOrDefault(actor => active.GetNativeShot(actor.Id) is not null);
        bool shotInput = !slotsOwned && !duelOwned && keyboard.GunbladeTriggerRequested && shotActor is not null;
        if (shotInput)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.ShotTrigger, ActorId: shotActor!.Id));
        }
        bool gunbladeInput = !slotsOwned && !duelOwned && !shotInput && keyboard.GunbladeTriggerRequested && active.CanTriggerGunblade;
        if (gunbladeInput)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.GunbladeTrigger));
        }
        if (!slotsOwned && !duelOwned && (keyboard.BattleInstantVictoryRequested || keyboard.BattleInstantDefeatRequested) && CanCompleteInstantOutcome)
        {
            CompleteInstantOutcome(keyboard.BattleInstantVictoryRequested);
        }
        else if (!slotsOwned && !duelOwned && keyboard.BattleAdvanceTurnRequested && active.Mode == BattleAtbMode.TurnBased && active.Outcome == BattleOutcome.Active)
        {
            battleHud!.AdvanceTurn();
        }
        else
        {
            if (!slotsOwned && !duelOwned && keyboard.GameMenuRequested)
            {
                battleHud!.CycleCommandActor();
            }
            if (!duelOwned && !slotsInputReleasePending && (!slotsOwned || battleHud!.CardSlotsOwnsInput))
            {
                NavigateBattleView(battleHud!.Navigate, input, slotsOwned || gunbladeInput || shotInput || boostInput || skipInput);
            }
            if (ActiveBattle is null || battleHud is null)
            {
                return true;
            }
        }
        if (!duelOwned && !slotsInputReleasePending && (!slotsOwned || battleHud!.CardSlotsOwnsInput))
        {
            PointerBattleView(battleHud!.BuildView());
        }
        if (duelOwned)
        {
            previousBattleMouse = mouseHeld;
        }
        if (ActiveBattle is null)
        {
            return true;
        }
        if (battleHud!.CardSlotsOwnsInput)
        {
            active.PublishNativeEscapeButtons(0);
            active.PublishNativeScanMappedButtons(0);
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
        if (keyboard.UiDetailRequested)
        {
            navigate(MenuInput.Alternate);
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
        if (options.IsDiagnosticGraphicalHost)
        {
            return;
        }
        MouseState mouse = physicalInput.ReadMouse();
        bool pressed = physicalInput.CanRead && mouse.LeftButton == ButtonState.Pressed;
        view.Resize(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height);
        view.Pointer(mouse.X, mouse.Y, pressed && !previousBattleMouse);
        previousBattleMouse = pressed;
    }

    private void EnsureBattleHud(BattleSession active, BattleScenePresentation? preparedScene = null)
    {
        if (presentedBattle != active)
        {
            DisposeBattleTextures();
            presentedBattle = active;
            battleScene = preparedScene ?? new BattleScenePresentation(PresentationBattleResources, active.ReadDefinition());
            battleGuardianForceLearning.Clear();
            MenuData? rewardNames = debugBattle is null ? ReadLiveMenuData() : null;
            battleHud = new BattleHudController(active, (actor, target) => ApplyBattleCommand(new BattleInput(BattleInputKind.Attack, ActorId: actor, TargetId: target)), OpenBattleDebug, () => ApplyBattleCommand(new BattleInput(BattleInputKind.AdvanceTurn)), CompleteBattleResult, input => ApplyBattleCommand(input), () => debugBattle is null && world?.IsDiagnosticBattle != true && world?.IsEffectComponentPreview != true ? world!.PreviewBattleRewards() : null, id => rewardNames?.GuardianForces.FirstOrDefault(guardian => guardian.Id == $"gf-{id}")?.Name ?? liveMenuCatalog?.GuardianForceName(id) ?? $"GF {id}", id => liveMenuCatalog?.ItemName(id) ?? $"Item {id}", id => liveMenuCatalog?.CardName(id) ?? $"Card {id}", world?.Events.Scene.Names);
            battleHud.ConfigureResults(PresentationBattleResources.ReadNativeGlobalBattleMessages(), id => liveMenuCatalog?.ItemDescription(id) ?? "", id => liveMenuCatalog?.AbilityName(id) ?? $"Ability {id}");
            if (resourceHost is not null)
            {
                battleHud.ConfigureLocalization(new LocalizationBattlePresentation(resourceHost.Resources));
                string occurrenceScope = Guid.NewGuid().ToString("N");
                battleHud.DialoguePresented += (binding, channel, id, rendered) => ObserveBoundAuthoringDialogue(binding, channel, occurrenceScope + ":" + id, rendered);
            }
            battleHud.ConfigureGuardianForceLearning(CreateBattleGuardianForceLearningMenu);
            battleHud.SoundRequested += cue => fieldAudio?.PlayUiSound(cue);
            battleHud.SlotsSoundRequested += cue => fieldAudio?.PlayTripleTriadSound(cue);
            battleHud.NativeButtonBindingHint = keyboard.NativeButtonBindingPrompt;
            slotsInputReleasePending = false;
        }
        battleHud!.OriginalCompatibleLayout = OriginalTextLayout;
        battleHud.ShowActorDialogue = !ReferenceEquals(diagnosticIdleBattle?.Battle, active);
        battleHud!.TextScale = uiPreferences.TextScale / 100f * uiPreferences.UiFontSize / 24f;
        battleHud.CursorMemory = uiPreferences.CursorMemory;
        battleHud.ShowEnemySpellNames = uiPreferences.ShowEnemySpellNames;
        battleHud.SlotsNavigationHint = string.Join(" / ", new[] { ControlAction.MoveUp, ControlAction.MoveDown, ControlAction.MoveLeft, ControlAction.MoveRight }.Select(keyboard.BindingPrompt));
        battleHud.SlotsConfirmHint = keyboard.BindingPrompt(ControlAction.Confirm);
        battleHud.GunbladeBindingHint = keyboard.BindingPrompt(ControlAction.RotateRight);
        battleHud.PhysicalOverrideBindingHint = keyboard.BindingPrompt(ControlAction.CameraLeft);
        battleHud.MagicConfirmBindingHint = keyboard.BindingPrompt(ControlAction.Card);
    }

    private void ObserveScanAuthoringDialogues(BattleSession active)
    {
        object? owner = active.Presentation?.Graphics.OriginalEffect?.Pools.FirstOrDefault();
        if (!ReferenceEquals(authoringScanOwner, owner))
        {
            authoringScanOwner = owner;
            authoringScanLifetime++;
            authoringScanWindows.Clear();
        }
        if (resourceHost is null || active.PreparedPresentation?.Scan is not { Actor: { } actor } scan || SlotsModalBlocksGameplay)
        {
            authoringScanWindows.Clear();
            return;
        }
        var resources = resourceHost.Resources;
        IReadOnlyList<NativeScanWindowText> windows = NativeScanWindowTextProjection.ProjectForViewport(scan.Text, scan.CreateExpansionServices(), new(0, 0, 320, 224), resources.Source.Language);
        windows = new LocalizationBattlePresentation(resources).Scan(scan, windows);
        HashSet<int> visible = [];
        foreach (NativeScanWindowText window in windows)
        {
            if (window.Binding is not { } binding || window.Lines.Count == 0 || window.Document.Text.Length == 0)
            {
                continue;
            }
            visible.Add(window.Index);
            var identity = (scan.Text.Windows.Row(window.Index).ReadDword(8), binding.Key, actor.ModelId);
            if (authoringScanWindows.TryGetValue(window.Index, out var previous) && previous == identity)
            {
                continue;
            }
            authoringScanWindows[window.Index] = identity;
            string occurrence = authoringScanLifetime.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + window.Index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + Guid.NewGuid().ToString("N");
            ObserveBoundAuthoringDialogue(binding, "Battle/Scan/" + window.Index.ToString(System.Globalization.CultureInfo.InvariantCulture), occurrence, window.Document.Text);
        }
        foreach (int index in authoringScanWindows.Keys.Where(index => !visible.Contains(index)).ToArray())
        {
            authoringScanWindows.Remove(index);
        }
    }

    private MenuController CreateBattleGuardianForceLearningMenu(BattleRewardSummary rewards)
    {
        NativeCharacterVitals projected = world!.PreviewBattleResultOwners().CharacterVitals;
        Dictionary<string, string> learned = rewards.GuardianForceGrowth.Where(growth => growth.LearnedAbilityIds.Length > 0).ToDictionary(growth => $"gf-{growth.Id}", growth => growth.LearnedAbilityIds[0].ToString(System.Globalization.CultureInfo.InvariantCulture));
        MenuData ReadLearningData()
        {
            MenuData data = LiveMenuData.Build(session.State with { CharacterVitals = projected }, world.Events.Scene.Names, liveMenuCatalog);
            data.GuardianForces.RemoveAll(force => !learned.ContainsKey(force.Id));
            data.Settings["Text scale"] = $"{uiPreferences.TextScale}%";
            return data;
        }
        void SelectLearning(MenuAction action)
        {
            if (action.Kind != "gf-learn" || !learned.ContainsKey(action.TargetId))
            {
                throw new InvalidOperationException("Victory learning accepts only a participating GF's next ability.");
            }
            int id = int.Parse(action.TargetId.AsSpan(3), System.Globalization.CultureInfo.InvariantCulture);
            int ability = int.Parse(action.SubjectId, System.Globalization.CultureInfo.InvariantCulture);
            projected = projected.SelectGuardianForceLearning(id, ability);
            battleGuardianForceLearning[id] = ability;
        }
        MenuController menu = new(ReadLearningData, SelectLearning) { Presentation = uiPreferences.MenuPresentation };
        menu.ConnectPreferences(ReadCurrentUiPreferences, ApplyLiveMenuAction);
        menu.SoundRequested += cue => fieldAudio?.PlayUiSound(cue);
        menu.OpenBattleLearning(learned);
        return menu;
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
            world!.ApplyBattleInput(new BattleInput(BattleInputKind.Return), battleGuardianForceLearning);
            SynchronizeBattleAudio();
            ClearBattlePresentation();
            fieldSeconds = 0;
            return;
        }
        OpenPlayerSaveMenu(PlayerSaveMenuMode.Load);
    }

    private int ApplyBattleCommand(BattleInput input)
    {
        lastReportBattleInput = input;
        if (SlotsModalBlocksGameplay && input.Kind != BattleInputKind.Tick)
        {
            throw new InvalidOperationException("The pending Slots card choice owns battle input.");
        }
        if (debugBattle is null)
        {
            BattleSession? active = ActiveBattle;
            int dispatched = world!.ApplyBattleInput(input);
            if (active is not null && !ReferenceEquals(active, ActiveBattle))
            {
                SynchronizeBattleAudio(active);
            }
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
                GameWorld.ExecuteBattleCommand(debugBattle, input);
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
            case BattleInputKind.GuardianFormActivate:
                debugBattle.RequestGuardianForm(input.ActorId!, input.ActionId);
                break;
            case BattleInputKind.GuardianFormAction:
                debugBattle.QueueGuardianFormAction(input.ActorId!, input.TargetId!, (BattleGuardianFormAction)input.ActionId);
                break;
            case BattleInputKind.GuardianFormSkip:
                debugBattle.SkipGuardianFormFinisher();
                break;
            case BattleInputKind.GuardianBoost:
                debugBattle.PressNativeGuardianBoost();
                break;
            case BattleInputKind.ShotTrigger:
                debugBattle.TriggerNativeShot(input.ActorId ?? throw new InvalidDataException("Shot trigger requires its process owner."));
                break;
            case BattleInputKind.ScanInput:
                debugBattle.QueueNativeScanInput(input.ScanButton ?? throw new InvalidDataException("Scan input requires ScanButton."));
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
        using GameAssets game = new(options.GameDirectory, resources: resourceHost?.Resources, modelOverrides: resourceHost);
        NativeCharacterVitals defaults = game.ReadNewGameCharacterVitals().WithGuestBattleRecords();
        return presentationBattleCatalog = new BattleDebugCatalog
        {
            Players = resources.PlayerCharacterIds.Select(id => InstalledBattlePractice.PreparePlayer(new BattleCombatantDefinition { Id = $"player-{id}", Name = LiveMenuData.CharacterName(session.State, checked((byte)id), events?.Scene.Names, liveMenuCatalog), IsPlayer = true, ModelId = id }, defaults, resources, initializeDefaults: true)).ToArray(),
            Enemies = resources.EnemyModelIds.Select(id => new BattleCombatantDefinition { Id = $"enemy-{id}", Name = resources.ReadEnemyName(id), ModelId = id, MaximumHp = 500, CurrentHp = 500, Speed = 80, Attack = 50, Defense = 10 }).ToArray(),
            Weapons = resources.WeaponDefinitions.Select(weapon => new BattleDebugEquipment(weapon.Id, weapon.Name, weapon.CharacterId)).ToArray(),
            LimitFamilies = Enum.GetValues<BattleLimitFamily>().Select(resources.ReadNativeLimitFamily).ToArray(),
            ResolveWeapon = (actor, weaponId) => InstalledBattlePractice.PreparePlayer(actor with { WeaponId = weaponId }, defaults, resources),
            Equipment = Enumerable.Range(0, 16).Select(id => new BattleDebugEquipment(id, liveMenuCatalog!.GuardianForceName(id) ?? $"GF {id}")).ToArray(),
            StageIds = resources.StageIds.ToArray(),
            PlayerCostumes = resources.PlayerCharacterIds.ToDictionary(id => id, id => Enumerable.Range(0, resources.PlayerModelNames(id).Count).ToArray())
        };
    }

    private MenuNavigationState? battleDebugReturnNavigation;
    private bool battleDebugReturnDemo;
    private bool battleDebugReturnFixture;

    private void CloseBattleDebug()
    {
        battleDebug = null;
        if (battleDebugReturnNavigation is { } saved && gameMenus is not null)
        {
            gameMenus.RestoreNavigation(saved);
            gameMenus.RestoreEditingContext(battleDebugReturnDemo, battleDebugReturnFixture);
        }
        battleDebugReturnNavigation = null;
    }

    private void OpenBattleDebug()
    {
        battleDebugReturnNavigation = gameMenus?.IsOpen == true ? gameMenus.CaptureNavigation() : null;
        battleDebugReturnDemo = gameMenus?.IsDemo == true;
        battleDebugReturnFixture = gameMenus?.IsFixtureContext == true;
        BattleDebugCatalog catalog = ReadBattleDebugCatalog();
        BattleDefinition? activeEncounter = world?.Battle?.ReadDefinition();
        BattleCombatantDefinition? lastEnemy = activeEncounter?.Combatants.FirstOrDefault(actor => !actor.IsPlayer && actor.InitialActive && !actor.InitialHidden && !actor.InitialUntargetable && !actor.InitialSuspended);
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
        BattleDefinition initial = BattleDebugDefaults.Create(currentParty, lastEnemy ?? catalog.Enemies.First(), stageId, world?.Battle, actions, items) with { LimitProgress = session.State.LimitProgress, NativeShotAvailability = session.State.ShotAvailability, NativeSavedBattleFlags = session.State.SavedBattleFlags?.Value, NativeSavedBattleFlagsKnownMask = session.State.SavedBattleFlags?.EffectiveKnownMask ?? 0 };
        battleDebugPresets ??= new BattleDebugPresets(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(options.SavePath))!, "battle-presets.json"));
        battleDebug = new BattleDebugController(initial, catalog, ReadCurrentUiPreferences().AtbMode, StartDebugBattle, CloseBattleDebug, battleDebugPresets);
        battleDebug.LocalizeText = LocalizeUiText;
        battleDebug.SoundRequested += sound => fieldAudio?.PlayUiSound(sound);
        gameMenus?.Close();
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
        if (session.State.Scene == SceneKind.Title)
        {
            nextPreferences.Save(UiPreferencesPath);
            uiPreferences = nextPreferences;
        }
    }

    private void StartDebugBattle(BattleDefinition definition, BattleAtbMode mode)
    {
        definition = definition with
        {
            LimitOncePerTurn = ReadCurrentUiPreferences().LimitOncePerTurn,
            SelphieCardSlots = ReadCurrentUiPreferences().SelphieCardSlots,
            DrawOncePerSpell = ReadCurrentUiPreferences().DrawOncePerSpell,
            Tuning = world?.ReadGameplayTuning() ?? (resourceHost?.GameplayPolicy ?? GameplayTuningPolicy.Empty).Apply(session.State.GameplayPreferences.Tuning),
            GuardianFormsEnabled = ReadCurrentUiPreferences().GuardianFormsEnabled,
            GuardianFormResources = ReadCurrentUiPreferences().GuardianFormsEnabled ? [PresentationBattleResources.ReadIfritGuardianFormResources()] : []
        };
        practiceBattleGameAssets ??= new GameAssets(options.GameDirectory, resources: resourceHost?.Resources, modelOverrides: resourceHost);
        BattleSession next = InstalledBattlePractice.Create(definition, mode, practiceBattleGameAssets, PresentationBattleResources);
        next.SetDifficulty(ReadCurrentUiPreferences().Difficulty);
        next.SetGunbladeMode(ReadCurrentUiPreferences().GunbladeMode);
        debugBattle = next;
        battleDebug = null;
        gameMenus?.Close();
        EnsureBattleHud(next);
        SynchronizeBattleAudio();
    }

    private void AdvanceMeasuredBattlePresentation(BattleSession active, TimeSpan elapsed)
    {
        if (options.ControlStep)
        {
            return;
        }
        using DiagnosticHostPhaseTiming.Scope presentationTimingScope = diagnosticHostPhaseTiming.Measure(DiagnosticHostPhase.BattlePresentationUpdate);
        using NativeBattlePhaseTiming.ObservationScope nativeTimingScope = NativeBattlePhaseTiming.Observe(diagnosticHostPhaseTiming.Enabled ? diagnosticHostPhaseTiming : null);
        active.AdvanceNativeGraphicsPresentation(elapsed);
    }

    private IDisposable BeginBattleUpdateVisibleArea()
    {
        PixelBounds sceneSize = ViewportLayout.WorldRenderTarget(GraphicsDevice.Viewport.Width, GraphicsDevice.Viewport.Height, options.WorldRenderLimit, false);
        PixelBounds projection = ViewportLayout.Fit(320, 240, sceneSize.Width, sceneSize.Height);
        return BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.FromViewport(sceneSize.Width, sceneSize.Height, projection.X, projection.Y, projection.Width, projection.Height));
    }

    private void PrepareBattleTexture(Rectangle bounds)
    {
        using DiagnosticHostPhaseTiming.Scope sceneTiming = diagnosticHostPhaseTiming.Measure(DiagnosticHostPhase.BattleSceneRender);
        BattleSession active = ActiveBattle!;
        EnsureBattleHud(active);
        PixelBounds size = ViewportLayout.WorldRenderTarget(bounds.Width, bounds.Height, options.WorldRenderLimit, false);
        if (battleGpu is null && (battleTexture?.Width != size.Width || battleTexture.Height != size.Height))
        {
            battleTexture?.Dispose();
            battleTexture = new Texture2D(GraphicsDevice, size.Width, size.Height);
        }
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        byte[] pixels;
        try
        {
            float renderPhase = ReadBattleRenderPhase(active, battleScene!);
            battleScene!.PilotDiagnosticsEnabled = diagnosticTelemetry?.VisualChangesEnabled == true;
            pixels = battleScene.Render(active, size.Width, size.Height, battleGpu, renderPhase);
            InvalidateTexturePresentation();
            texturePresentedBattle = active;
            if (diagnosticTelemetry?.VisualChangesEnabled == true && battleGpu is not null)
            {
                long frameOrdinal = diagnosticPresentedFrames + 1;
                diagnosticTelemetry.ObserveBattleImageRendered(frameOrdinal, battleScene.PilotSubmission);
                ObserveDiagnosticBattleImage(battleGpu.Target, frameOrdinal);
            }
        }
        catch
        {
            texturePresentedBattle = null;
            battleGpu?.Abort();
            throw;
        }
        double renderMilliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        started = System.Diagnostics.Stopwatch.GetTimestamp();
        if (battleGpu is null)
        {
            battleTexture!.SetData(pixels);
        }
        metrics.Render(renderMilliseconds, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
    }

    private float ReadBattleRenderPhase(BattleSession active, BattleScenePresentation scene)
    {
        scene.SmoothCompatibleMotion = uiPreferences.BattleFps == 60 && !options.ControlStep;
        return presentationTiming.SamplePresentationPhase(active, active.PreparedPresentation, CanAdvanceBattleRenderPhase, scene.SmoothCompatibleMotion);
    }

    private static object? ReadBattlePreparedFrameIdentity(NativeBattlePreparedPresentation? frame)
    {
        return frame is null ? null : new { frame.Version, frame.Callback, frame.Frame, Actors = frame.Actors.Select(actor => new { actor.Id, actor.WeaponId, HasLinkedTextures = actor.LinkedTextures is not null }).ToArray() };
    }

    private void DrawBattle(Rectangle bounds)
    {
        BattleSession active = ActiveBattle!;
        PixelBounds viewport = ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
        batch.Draw(battleGpu?.Target ?? battleTexture!, bounds, Color.White);
        battleScene!.DrawEffects(active, uiCanvas!, new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        UiView view = battleHud!.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        battleHud.TargetModelBounds = actorId => battleScene.GetActorBounds(actorId, new UiRect(bounds.X, bounds.Y, bounds.Width, bounds.Height));
        view.Draw(uiCanvas!, uiTheme);
        if (!SlotsModalBlocksGameplay && battleScene.PresentedNativeFrame is { Scan: { } scan } presentation)
        {
            IReadOnlyList<NativeScanWindowText> windows = NativeScanWindowTextProjection.ProjectForViewport(scan.Text, scan.CreateExpansionServices(), new(0, 0, 320, 224), (resourceHost ?? throw new InvalidOperationException("Native scan text requires its resource source.")).Resources.Source.Language);
            if (resourceHost is not null)
            {
                windows = new LocalizationBattlePresentation(resourceHost.Resources).Scan(scan, windows);
            }
            NativeScanWindowDrawing.Draw(uiCanvas!, uiTheme, windows, new UiRect(viewport.X, viewport.Y, viewport.Width, viewport.Height), BattlePresentationTiming.ElapsedForCallbacks(presentation.Callback).TotalSeconds, uiPreferences.TextScale / 100f * uiPreferences.UiFontSize / 24f);
        }
        if (!SlotsModalBlocksGameplay && active.NativeExamTimer is { DisplayEnabled: true } timer)
        {
            DrawTimerUi(bounds, timer);
        }
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
        return active is null ? null : new { IsDebug = debugBattle is not null, active.Mode, active.Frame, active.Outcome, active.Name, active.Combatants, active.ActiveAction, active.QueuedActions, active.CanQueueNativeScanInput, active.CanAdmitDebugOutcome, active.CanReturnToField, active.NativeEntryAdvantage, active.NativeRenzokukenTriggerWindowOpen, NativeControllers = active.Combatants.Where(actor => actor.IsPlayer).Select(actor => new { actor.Id, Shot = active.GetNativeShot(actor.Id)?.Phase.ToString(), Duel = active.GetNativeDuel(actor.Id)?.Phase.ToString(), Renzokuken = active.GetNativeRenzokuken(actor.Id) is null ? null : "Active" }).ToArray(), NativePresentation = active.Presentation is { } presentation ? new { presentation.EntryReady, presentation.HasActiveAction, NativeCallback = presentation.Graphics.Callback, Prepared = ReadBattlePreparedFrameIdentity(active.PreparedPresentation), Presented = ReadBattlePreparedFrameIdentity(battleScene?.PresentedNativeFrame), PresentedNextCallback = battleScene?.PresentedNextNativeCallback, PresentedInterpolationAlpha = battleScene?.PresentedInterpolationAlpha, Outcome = presentation.Graphics.NativeOutcome, Party = presentation.Graphics.ActorModels.Party.Entries.Select(entry => new { entry.State.ModelId4, entry.State.Phase6, entry.State.Sequence, entry.State.SequenceFrame, entry.State.ActiveAnimationBank, entry.State.EquipmentPresentationRole, DefaultSequence = entry.State.SequenceState?.Default2, Pose = entry.State.SequenceState?.Pose, PoseFrame = entry.State.SequenceState?.PoseFrame114, PoseLimit = entry.State.SequenceState?.PoseLimit115 }).ToArray(), DescriptorTasks = presentation.Graphics.Descriptors.Tasks.Where(task => task.Flags != 0).Select(task => new { task.Kind, task.Phase }).ToArray() } : null, RecentEvents = active.Events.TakeLast(12).ToArray(), BattlePolicy = ReadCurrentUiPreferences().BattlePolicy, DebugMenuOpen = battleDebug is not null, DiagnosticOwners = world?.IsDiagnosticBattle == true ? active.ObserveDiagnosticOwners() : null };
    }

    private object DispatchBattleControl(ControlRequest request)
    {
        if (SlotsModalBlocksGameplay && request.Command.ToLowerInvariant() is not ("battle-status" or "battle-input" or "battle-native-input"))
        {
            throw new InvalidOperationException("The pending Slots card choice owns battle input.");
        }
        BattleControlCommand command = request.Battle ?? new();
        UiPreferences currentPreferences = ReadCurrentUiPreferences();
        switch (request.Command.ToLowerInvariant())
        {
            case "battle-native-input":
                return DispatchNativeBattleControl(command.Native ?? throw new ArgumentException("Native controller input is required."));
            case "battle-start":
                StartDebugBattle(command.Definition ?? throw new ArgumentException("Battle Definition is required."), command.Mode ?? ReadCurrentUiPreferences().AtbMode);
                break;
            case "battle-input":
                if (world?.IsDiagnosticBattle == true && command.Input?.Kind is BattleInputKind.Tick or BattleInputKind.AdvanceTurn)
                {
                    throw new NotSupportedException("Diagnostic graphical commands must use normal real-time simulation and presentation pacing.");
                }
                ApplyBattleCommand(command.Input ?? throw new ArgumentException("Battle Input is required."));
                break;
            case "battle-settings":
                ApplyBattleDebugSettings(command.Mode ?? currentPreferences.AtbMode, command.Policy ?? currentPreferences.BattlePolicy, command.RandomEncounters);
                break;
            case "battle-debug-open":
                OpenBattleDebug();
                break;
            case "battle-debug-close":
                CloseBattleDebug();
                break;
            case "battle-debug-input":
                (battleDebug ?? throw new InvalidOperationException("Open Battle Debug first.")).Navigate(command.MenuInput ?? throw new ArgumentException("MenuInput is required."));
                break;
            case "battle-close":
                if (world?.IsDiagnosticBattle == true)
                {
                    world.EndDiagnosticBattle();
                    ClearBattlePresentation();
                    break;
                }
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
        return new { DiagnosticInitialization = world?.DiagnosticReceipt, Battle = BattleStatus(), DebugMenuOpen = battleDebug is not null, Definition = battleDebug?.Definition };
    }

    private void DisposeBattleTextures()
    {
        battleTexture?.Dispose();
        battleTexture = null;
        battleScene?.ClearNativeRawTextureCache();
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
        slotsInputReleasePending = false;
        DisposeBattleTextures();
    }

    private void DisposeBattlePresentation()
    {
        ClearBattlePresentation(synchronizeAudio: false);
        battleAudio = null;
        practiceBattleGameAssets?.Dispose();
        presentationBattleAssets?.Dispose();
    }
}
