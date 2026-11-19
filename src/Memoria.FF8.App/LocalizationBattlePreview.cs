using System.Runtime.InteropServices;
using Albeoris.Games.FF8.Imaging;
using Memoria.FF8.Events;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using SkiaSharp;

namespace Memoria.FF8;

internal sealed class LocalizationBattlePreview : IDisposable
{
    private const int Width = 640;
    private const int Height = 480;
    private static readonly UiRect viewport = new(0, 0, Width, Height);
    private static readonly TimeSpan fieldTick = TimeSpan.FromSeconds(1d / EventEngine.SimulationFps);
    private readonly GameWorld world;
    private readonly BattleScenePresentation scene;
    private readonly SKBitmap bitmap;
    private readonly BattlePreviewCanvas canvas;
    private readonly UiTheme theme = new();
    private TimeSpan presentationTime;

    private LocalizationBattlePreview(GameWorld world, BattleSession session, BattleScenePresentation scene)
    {
        this.world = world;
        Session = session;
        this.scene = scene;
        var resources = world.ValidationAssets.Resources;
        Lazy<LiveMenuCatalog> menu = new(() => LiveMenuCatalog.ReadInstalled(world.ValidationAssets));
        Hud = new(session, (actor, target) => world.ApplyBattleInput(new(BattleInputKind.Attack, ActorId: actor, TargetId: target)), () => { }, () => world.ApplyBattleInput(new(BattleInputKind.AdvanceTurn)),
            () =>
            {
                if (session.CanReturnToField)
                {
                    world.ApplyBattleInput(new(BattleInputKind.Return));
                }
            },
            input => world.ApplyBattleInput(input), world.PreviewBattleRewards, id => menu.Value.GuardianForceName(id) ?? $"GF {id}", id => menu.Value.ItemName(id) ?? $"Item {id}", id => menu.Value.CardName(id) ?? $"Card {id}", world.Events.Scene.Names);
        Hud.ConfigureLocalization(new LocalizationBattlePresentation(resources));
        Hud.ConfigureResults(world.BattleResources.ReadNativeGlobalBattleMessages(), id => menu.Value.ItemDescription(id) ?? "", id => menu.Value.AbilityName(id) ?? $"Ability {id}");
        Hud.TargetModelBounds = actor => scene.GetActorBounds(actor, viewport);
        bitmap = new(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        try
        {
            canvas = new(bitmap, world.ValidationAssets.GameDirectory, resources);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    internal BattleSession Session { get; }
    internal BattleHudController Hud { get; }

    internal static bool AreSynchronized(LocalizationBattlePreview original, LocalizationBattlePreview localized, bool originalActive, bool localizedActive)
    {
        BattleSession first = original.Session;
        BattleSession second = localized.Session;
        return originalActive == localizedActive && first.Frame == second.Frame && first.NativeHostCallbacks == second.NativeHostCallbacks && first.Outcome == second.Outcome && first.NativeEntryReady == second.NativeEntryReady && original.Hud.SelectedCommand == localized.Hud.SelectedCommand && original.Hud.SelectingAction == localized.Hud.SelectingAction && original.Hud.SelectingTarget == localized.Hud.SelectingTarget && original.Hud.ResultPage == localized.Hud.ResultPage;
    }

    internal static LocalizationBattlePreview Start(GameWorld world, GameState state, byte seed)
    {
        int formation = state.PendingBattleEncounter?.Formation ?? throw new InvalidOperationException("Preview battle entry requires a pending original encounter.");
        ushort flags = world.CaptureBattleEntryFlags();
        _ = world.CreateBattleEntryTransition();
        NativeBattleMusicEntryPreparation music = world.BeginBattleEntryMusic(formation, flags);
        GameWorld.PreparedBattleEntry prepared = world.PrepareCommittedBattleEntry(formation, flags, seed, state.GameplayPreferences.AtbMode, music);
        BattleScenePresentation scene = new(world.BattleResources, prepared.Battle.ReadDefinition());
        world.PublishPreparedBattleEntry(prepared);
        return new(world, prepared.Battle, scene);
    }

    internal void Advance(PlayerAction action)
    {
        if (action.Kind == PlayerActionKind.Wait)
        {
            TimeSpan elapsed = fieldTick * action.Frames;
            using IDisposable visibleArea = BattleSceneVisibleArea.Begin(BattleSceneVisibleArea.FromViewport(Width, Height, 0, 0, Width, Height));
            Hud.AdvanceSlotsPresentation(elapsed, confirmHeld: false, mouseHeld: false);
            Session.AdvanceNativeGraphicsPresentation(elapsed);
            world.SynchronizeBattlePresentation();
            if (world.Battle is not null)
            {
                Hud.AdvancePresentation(elapsed);
            }
            presentationTime += elapsed;
            return;
        }
        if (ReadMenuInput(action) is MenuInput input)
        {
            Hud.Navigate(input);
        }
    }

    private static MenuInput? ReadMenuInput(PlayerAction action)
    {
        return action.Kind switch
        {
            PlayerActionKind.Confirm => MenuInput.Confirm,
            PlayerActionKind.Cancel => MenuInput.Cancel,
            PlayerActionKind.Move when Math.Abs(action.Horizontal) > Math.Abs(action.Vertical) => action.Horizontal < 0 ? MenuInput.Left : MenuInput.Right,
            PlayerActionKind.Move when action.Vertical != 0 => action.Vertical < 0 ? MenuInput.Up : MenuInput.Down,
            _ => null
        };
    }

    internal PixelImage Render()
    {
        canvas.BeginFrame(presentationTime);
        canvas.DrawRgba(scene.Render(Session, Width, Height), Width, Height, viewport);
        scene.DrawEffects(Session, canvas, viewport);
        UiView view = Hud.BuildView();
        view.Resize(Width, Height);
        view.Draw(canvas, theme);
        return new(Width, Height, MemoryMarshal.Cast<byte, Rgba32>(PortableRaster.Rgba(bitmap)).ToArray());
    }

    public void Dispose()
    {
        canvas.Dispose();
        bitmap.Dispose();
    }
}
