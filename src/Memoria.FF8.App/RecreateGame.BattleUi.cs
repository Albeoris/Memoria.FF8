using Memoria.FF8.Assets;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NVector3 = System.Numerics.Vector3;

namespace Memoria.FF8;

internal sealed record BattleControlCommand(BattleInput? Input = null, BattleDefinition? Definition = null, BattleAtbMode? Mode = null, MenuInput? MenuInput = null);

internal sealed partial class RecreateGame
{
    private BattleAssets? presentationBattleAssets;
    private BattleSession? debugBattle;
    private BattleHudController? battleHud;
    private BattleDebugController? battleDebug;
    private BattleRenderer? battleRenderer;
    private Texture2D? battleTexture;
    private readonly Dictionary<string, Texture2D> battleHeads = [];
    private double battleSeconds;
    private bool previousBattleMouse;
    private BattleSession? presentedBattle;
    private BattleDefinition? debugBattleDefinition;
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
        if (world?.Events.PendingInteractiveBattle is not null && world.Battle is null)
        {
            world.ApplyBattleInput(new BattleInput(BattleInputKind.StartEncounter, Mode: uiPreferences.AtbMode));
        }
        BattleSession? active = ActiveBattle;
        if (active is null)
        {
            return false;
        }
        EnsureBattleHud(active);
        if (keyboard.BattleInstantVictoryRequested)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.InstantVictory));
        }
        else if (keyboard.BattleAdvanceTurnRequested && active.Mode == BattleAtbMode.TurnBased && active.Outcome == BattleOutcome.Active)
        {
            battleHud!.AdvanceTurn();
        }
        else if (active.Outcome == BattleOutcome.Active)
        {
            NavigateBattleView(battleHud!.Navigate, input);
        }
        else if (input.Confirm || keyboard.UiConfirmRequested)
        {
            if (debugBattle is not null)
            {
                ClearBattlePresentation();
                return true;
            }
            if (active.Outcome == BattleOutcome.Victory)
            {
                ApplyBattleCommand(new BattleInput(BattleInputKind.Return));
                ClearBattlePresentation();
                fieldSeconds = 0;
                return true;
            }
            ShowSaveNotice("Defeat. Load a save with F9/F10 or open Debug Battle with F4.");
        }
        PointerBattleView(battleHud!.BuildView());
        if (!options.ControlStep && active.Outcome == BattleOutcome.Active)
        {
            battleSeconds += elapsed.TotalSeconds;
            int frames = Math.Min(30000, (int)(battleSeconds * BattleSession.FramesPerSecond));
            if (frames > 0)
            {
                battleSeconds -= frames / (double)BattleSession.FramesPerSecond;
                if (active.Mode == BattleAtbMode.Normal || active.ActiveAction is not null || active.QueuedActions.Count > 0)
                {
                    ApplyBattleCommand(new BattleInput(BattleInputKind.Tick, frames));
                }
            }
        }
        return true;
    }

    private void NavigateBattleView(Action<MenuInput> navigate, InputFrame input)
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
            DisposeBattleTextures();
            presentedBattle = active;
            battleHud = new BattleHudController(active, (actor, target) => ApplyBattleCommand(new BattleInput(BattleInputKind.Attack, ActorId: actor, TargetId: target)), OpenBattleDebug, () => ApplyBattleCommand(new BattleInput(BattleInputKind.AdvanceTurn)));
            battleHud.HeadRenderer = DrawBattleHead;
            battleSeconds = 0;
        }
        battleHud!.ShowInitiative = uiPreferences.ShowInitiative;
        battleHud.TextScale = uiPreferences.TextScale / 100f;
    }

    private int ApplyBattleCommand(BattleInput input)
    {
        if (debugBattle is null)
        {
            return world!.ApplyBattleInput(input);
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
            case BattleInputKind.AdvanceTurn:
                debugBattle.AdvanceTurn();
                break;
            case BattleInputKind.SetMode:
                debugBattle.SetMode(input.Mode);
                break;
            case BattleInputKind.InstantVictory:
                debugBattle.InstantVictory();
                break;
            default:
                throw new InvalidOperationException("This command requires a game-owned encounter.");
        }
        return checked((int)(debugBattle.Frame - before));
    }

    private BattleDebugCatalog ReadBattleDebugCatalog()
    {
        _ = ReadLiveMenuData();
        BattleAssets resources = PresentationBattleResources;
        return new BattleDebugCatalog
        {
            Players = resources.PlayerCharacterIds.Select(id => new BattleCombatantDefinition { Id = $"player-{id}", Name = liveMenuCatalog!.CharacterName(id) ?? $"Character {id}", IsPlayer = true, ModelId = id, MaximumHp = 1000, CurrentHp = 1000, Speed = 100, Attack = 80, Defense = 20 }).ToArray(),
            Enemies = resources.EnemyModelIds.Select(id => new BattleCombatantDefinition { Id = $"enemy-{id}", Name = $"Enemy {id:000}", ModelId = id, MaximumHp = 500, CurrentHp = 500, Speed = 80, Attack = 50, Defense = 10 }).ToArray(),
            Weapons = liveMenuCatalog!.Weapons.Select((weapon, id) => new BattleDebugEquipment(id, weapon.Name ?? $"Weapon {id}")).ToArray(),
            Equipment = Enumerable.Range(0, 16).Select(id => new BattleDebugEquipment(id, liveMenuCatalog.GuardianForceName(id) ?? $"GF {id}")).ToArray(),
            StageIds = resources.StageIds.ToArray()
        };
    }

    private void OpenBattleDebug()
    {
        gameMenus?.Close();
        BattleDebugCatalog catalog = ReadBattleDebugCatalog();
        BattleDefinition initial = ActiveBattle?.Capture().Definition ?? debugBattleDefinition ?? new BattleDefinition { StageId = catalog.StageIds.First(), Combatants = [catalog.Players.First() with { Id = "party-0" }, catalog.Enemies.First() with { Id = "enemy-0" }] };
        battleDebug = new BattleDebugController(initial, catalog, uiPreferences.AtbMode, uiPreferences.BattlePolicy, uiPreferences.ShowInitiative, StartDebugBattle, ApplyBattleDebugSettings, () => battleDebug = null);
        SynchronizeUiMoviePause();
    }

    private void ApplyBattleDebugSettings(BattleAtbMode mode, string policy, bool initiative)
    {
        uiPreferences = uiPreferences with { AtbMode = mode, BattlePolicy = policy, ShowInitiative = initiative };
        uiPreferences.Save(UiPreferencesPath);
        if (ActiveBattle is not null && ActiveBattle.Mode != mode)
        {
            ApplyBattleCommand(new BattleInput(BattleInputKind.SetMode, Mode: mode));
        }
    }

    private void StartDebugBattle(BattleDefinition definition, BattleAtbMode mode)
    {
        BattleSession next = new(definition, mode);
        _ = PresentationBattleResources.LoadStage(definition.StageId ?? throw new ArgumentException("A debug battle needs a stage ID."));
        foreach (BattleCombatant actor in next.Combatants)
        {
            _ = ReadBattleModel(actor);
        }
        debugBattleDefinition = BattleSession.CloneDefinition(definition);
        debugBattle = next;
        battleDebug = null;
        gameMenus?.Close();
        EnsureBattleHud(next);
    }

    private BattleModel ReadBattleModel(BattleCombatant actor)
    {
        return actor.IsPlayer ? PresentationBattleResources.LoadPlayer(actor.ModelId, actor.Costume) : PresentationBattleResources.LoadEnemy(actor.ModelId);
    }

    private void DrawBattle(Rectangle bounds)
    {
        BattleSession active = ActiveBattle!;
        EnsureBattleHud(active);
        PixelBounds size = ViewportLayout.WorldRenderTarget(bounds.Width, bounds.Height, options.WorldRenderLimit, false);
        if (battleRenderer?.Width != size.Width || battleRenderer.Height != size.Height)
        {
            battleRenderer = new BattleRenderer(size.Width, size.Height);
            battleTexture?.Dispose();
            battleTexture = new Texture2D(GraphicsDevice, size.Width, size.Height);
        }
        BattleDefinition definition = active.Capture().Definition;
        BattleStage stage = PresentationBattleResources.LoadStage(definition.StageId!.Value);
        BattleEncounter? encounter = debugBattle is null && definition.EncounterId is int id ? PresentationBattleResources.LoadEncounter(id) : null;
        List<BattleActorDraw> actors = [];
        List<BattleCombatant> party = active.Combatants.Where(actor => actor.IsPlayer).ToList();
        List<BattleCombatant> enemies = active.Combatants.Where(actor => !actor.IsPlayer).ToList();
        Dictionary<string, NVector3> places = [];
        for (int index = 0; index < party.Count; index++)
        {
            places[party[index].Id] = new NVector3(-12, 0, (index - (party.Count - 1) / 2f) * 7);
        }
        for (int index = 0; index < enemies.Count; index++)
        {
            places[enemies[index].Id] = encounter?.Enemies[index].Position ?? new NVector3(12 + index / 4 * 7, 0, (index % 4 - (Math.Min(4, enemies.Count) - 1) / 2f) * 7);
        }
        foreach (BattleCombatant actor in active.Combatants)
        {
            NVector3 offset = NVector3.Zero;
            if (active.ActiveAction is BattleActionPresentation action && action.ActorId == actor.Id)
            {
                float progress = action.Phase == BattleActionPhase.Windup ? 1 - action.RemainingFrames / (float)BattleSession.AttackWindupFrames : action.RemainingFrames / (float)BattleSession.AttackRecoveryFrames;
                offset = (places[action.TargetId] - places[actor.Id]) * Math.Clamp(progress, 0, 1) * 0.7f;
            }
            BattleModel model = ReadBattleModel(actor);
            actors.Add(new BattleActorDraw(model, places[actor.Id], actor.IsPlayer ? MathF.PI / 2 : -MathF.PI / 2, Frame: active.Frame / 2f, AttackOffset: offset, Opacity: actor.IsAlive ? 1 : 0.25f));
        }
        battleTexture!.SetData(battleRenderer!.Render(stage, actors));
        PixelBounds viewport = ViewportLayout.Fit(320, 240, bounds.Width, bounds.Height);
        batch.Draw(battleTexture, new Rectangle(viewport.X, viewport.Y, viewport.Width, viewport.Height), Color.White);
        UiView view = battleHud!.BuildView();
        view.Resize(bounds.Width, bounds.Height);
        view.Draw(uiCanvas!, uiTheme);
    }

    private void DrawBattleHead(BattleCombatant actor, IUiCanvas canvas, UiRect bounds)
    {
        string key = $"{actor.IsPlayer}:{actor.ModelId}:{actor.Costume}";
        if (!battleHeads.TryGetValue(key, out Texture2D? head))
        {
            var image = (battleRenderer ??= new BattleRenderer(640, 448)).RenderPortrait(ReadBattleModel(actor));
            head = new Texture2D(GraphicsDevice, image.Width, image.Height);
            head.SetData(image.Pixels.Select(color => new Color(color.R, color.G, color.B, color.A)).ToArray());
            battleHeads.Add(key, head);
        }
        batch.Draw(head, new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height), actor.IsPlayer ? Color.White : new Color(255, 210, 210));
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
        return active is null ? null : new { IsDebug = debugBattle is not null, active.Mode, active.Frame, active.Outcome, active.Name, active.Combatants, active.ActiveAction, active.QueuedActions, RecentEvents = active.Events.TakeLast(12).ToArray(), uiPreferences.ShowInitiative, uiPreferences.BattlePolicy, DebugMenuOpen = battleDebug is not null };
    }

    private object DispatchBattleControl(ControlRequest request)
    {
        BattleControlCommand command = request.Battle ?? new();
        switch (request.Command.ToLowerInvariant())
        {
            case "battle-start":
                StartDebugBattle(command.Definition ?? throw new ArgumentException("Battle Definition is required."), command.Mode ?? uiPreferences.AtbMode);
                break;
            case "battle-input":
                ApplyBattleCommand(command.Input ?? throw new ArgumentException("Battle Input is required."));
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
        foreach (Texture2D head in battleHeads.Values)
        {
            head.Dispose();
        }
        battleHeads.Clear();
        battleTexture?.Dispose();
        battleTexture = null;
        battleRenderer = null;
    }

    private void ClearBattlePresentation()
    {
        debugBattle = null;
        battleDebug = null;
        battleHud = null;
        presentedBattle = null;
        battleSeconds = 0;
        DisposeBattleTextures();
    }

    private void DisposeBattlePresentation()
    {
        ClearBattlePresentation();
        presentationBattleAssets?.Dispose();
    }
}
