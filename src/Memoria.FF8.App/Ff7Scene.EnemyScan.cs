using Memoria.FF7.Runtime;
using Memoria.FF8.GameUi;
using Memoria.FF8.Rendering;
using Memoria.FF8.Resources.Localization;
using Memoria.FF8.Runtime;
using Memoria.FF8.Ui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector3 = System.Numerics.Vector3;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7TargetScan? displayedEnemyScan;
    private Ff7TargetScan? dismissedEnemyScan;
    private Ff7TargetScanModel? enemyScanModel;
    private double enemyScanYaw;
    private double enemyScanPitch;
    private double enemyScanIdleDelay;
    private GpuTriangleRenderer? enemyScanRenderer;
    private Func<string, string, string>? enemyScanLocalize;

    internal void ConfigureEnemyScanLocalization(Func<string, string, string> localize)
    {
        enemyScanLocalize = localize;
    }

    private bool UpdateEnemyScan(Ff7BattleSession battle, double seconds)
    {
        Ff7TargetScan? latest = mixedCombat?.Guests.Select(guest => guest.LastScan).Append(battle.LastSense).OfType<Ff7TargetScan>().MaxBy(scan => scan.Tick) ?? battle.LastSense;
        if (autoPlay is { Running: true, Paused: false })
        {
            dismissedEnemyScan = latest;
            displayedEnemyScan = null;
            enemyScanModel = null;
            return false;
        }
        if (displayedEnemyScan is null && latest is not null && !ReferenceEquals(latest, dismissedEnemyScan))
        {
            displayedEnemyScan = latest;
            Ff7BattleActorState actor = battle.Actors.Single(actor => actor.Actor.Slot == latest.Slot);
            enemyScanModel = new(BattleRenderer(battle).CaptureActorGeometry(BattleActorVisual(battle, actor) with { Position = Vector3.Zero, FacingRadians = 0 }));
            enemyScanYaw = 0;
            enemyScanPitch = 0;
            enemyScanIdleDelay = 0;
            sharedInput.SuppressHeldUiInput();
            return true;
        }
        if (displayedEnemyScan is null)
        {
            return false;
        }
        if (semanticInput.Confirm || semanticInput.Cancel)
        {
            dismissedEnemyScan = displayedEnemyScan;
            displayedEnemyScan = null;
            enemyScanModel = null;
            sharedInput.SuppressHeldUiInput();
            return true;
        }
        ushort buttons = sharedInput.HeldFieldButtons;
        int horizontal = ((buttons & 0x2000) != 0 ? 1 : 0) - ((buttons & 0x8000) != 0 ? 1 : 0);
        int vertical = ((buttons & 0x4000) != 0 ? 1 : 0) - ((buttons & 0x1000) != 0 ? 1 : 0);
        double radiansPerUnit = Math.Min(seconds, 0.25) * BattlePresentationTiming.CallbacksPerSecond * Math.Tau / NativeBattleScanViewer.RotationRevolution;
        if (horizontal != 0 || vertical != 0)
        {
            enemyScanYaw = (enemyScanYaw + horizontal * NativeBattleScanViewer.DigitalRotationStep * radiansPerUnit) % Math.Tau;
            enemyScanPitch = (enemyScanPitch + vertical * NativeBattleScanViewer.DigitalRotationStep * radiansPerUnit) % Math.Tau;
            enemyScanIdleDelay = NativeBattleScanViewer.ManualRotationTimeout / BattlePresentationTiming.CallbacksPerSecond;
        }
        else if (enemyScanIdleDelay > 0)
        {
            enemyScanIdleDelay -= seconds;
        }
        else
        {
            enemyScanYaw = (enemyScanYaw + NativeBattleScanViewer.AutoRotationStep * radiansPerUnit) % Math.Tau;
        }
        return true;
    }

    private void RenderEnemyScanModel()
    {
        if (enemyScanModel is { IsVisible: true } model)
        {
            model.Render(enemyScanRenderer ??= new(device), (float)enemyScanYaw, (float)enemyScanPitch);
        }
    }

    private bool DrawEnemyScan(SpriteBatch batch, IUiCanvas canvas, UiTheme theme, BattleHudLayout layout, float textScale)
    {
        if (displayedEnemyScan is not { } scan)
        {
            return false;
        }

        canvas.Fill(layout.Bounds, new(0, 0, 0));
        var elements = new List<TargetScanResistance>(9);
        int[] nativeOrder = [0, 1, 2, 3, 4, 7, 6, 8];
        for (int index = 0; index < nativeOrder.Length; index++)
        {
            elements.Add(TargetScanScreen.Element(index, scan.ElementResistances[nativeOrder[index]], enemyScanLocalize));
        }
        elements.Add(new(SystemTextCatalog.Translate(enemyScanLocalize, "battle.scan.gravity", "Gravity"), null, scan.ElementResistances[5]));

        List<TargetScanResistance> statuses = [];
        for (int index = 0; index < TargetScanScreen.Statuses.Length; index++)
        {
            int id = (int)TargetScanScreen.Statuses[index];
            uint mask = id < 16 ? Ff7GuestBattleStatuses.ToHostStatuses((ushort)(1 << id), 0) : Ff7GuestBattleStatuses.ToHostStatuses(0, 1u << (id - 16));
            if (mask != 0)
            {
                statuses.Add(TargetScanScreen.Status(index, (scan.StatusImmunities & mask) != 0 ? 100 : 0, enemyScanLocalize));
            }
        }

        TargetScanData data = new(scan.Title, scan.Name, scan.Description, scan.Level, scan.Hp, scan.MaximumHp, scan.Strength, scan.Vitality, scan.Speed, scan.Magic, scan.Spirit, scan.Evasion, elements, statuses);
        TargetScanScreen.Draw(canvas, theme, layout, textScale, data, enemyScanLocalize, DrawModel);
        return true;

        void DrawModel(UiRect bounds)
        {
            if (enemyScanRenderer is not null && enemyScanModel is { IsVisible: true })
            {
                batch.Draw(enemyScanRenderer.Target, new Rectangle((int)bounds.X, (int)bounds.Y, (int)bounds.Width, (int)bounds.Height), Color.White);
            }
        }
    }
}
