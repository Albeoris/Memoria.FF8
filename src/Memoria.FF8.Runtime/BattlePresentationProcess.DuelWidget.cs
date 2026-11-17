namespace Memoria.FF8.Runtime;

internal sealed record NativeBattleDuelWidgetFacts(string ActorId, BattleNativeDuelState State, bool Automatic, IReadOnlyList<BattleNativeDuelChoice> Choices);
internal sealed record NativeBattleDuelWidgetUpdated(NativeBattleDuelWidgetFacts Facts, BattleNativeDuelState State);

public sealed partial class BattlePresentationProcess
{
    private void AdvanceNativeDuelWindow(Action<NativeBattleDuelWidgetUpdated> publish)
    {
        if (NativeGraphics is not { UiServices.Windows: { } windows } graphics || windows.Rows[6].Update0 != 0x4AF840)
        {
            return;
        }
        if (services?.ReadDuelWidget() is not { } facts)
        {
            throw new InvalidOperationException("Registered Duel window6 lost its current gameplay owner.");
        }
        NativeBattleUiServices ui = graphics.UiServices!;
        bool frame33 = ui.Frame33 ?? throw new InvalidOperationException("Duel window6 requires its current publication byte.");
        BattleNativeDuelState next = NativeBattleDuelRules.AdvanceWidget(facts.State with { PendingPulse = 0 }, frame33, facts.State.PendingPulse, facts.Automatic, facts.Choices, DrawDuelUiRandom);
        if (next.WidgetClosed && !facts.State.WidgetClosed)
        {
            NativeGraphics = SetNativeDuelWindow(graphics, false);
        }
        publish(new(facts, next));

        ushort DrawDuelUiRandom()
        {
            nativeBattleUiRandom = (nativeBattleUiRandom ?? throw new NotSupportedException("Automatic Duel requires its native UI random source.")).Draw(out ushort roll);
            return roll;
        }
    }

    private static NativeBattleGraphicsState SetNativeDuelWindow(NativeBattleGraphicsState graphics, bool registered)
    {
        NativeBattleUiServices ui = graphics.UiServices ?? throw new InvalidOperationException("Duel requires the current UI services.");
        NativeBattleUiWindows windows = ui.Windows ?? throw new InvalidOperationException("Duel requires the native window bank.");
        NativeBattleUiWindow window = windows.Rows[6];
        if (registered && window.Update0 is not (0 or 0x4AF840))
        {
            throw new InvalidOperationException("Duel cannot register over another window6 callback.");
        }
        window = window with
        {
            Update0 = registered ? 0x4AF840u : 0,
            Draw8 = registered ? 0x4B00E0u : 0,
            Context12 = 0,
            Current16 = registered ? (sbyte)3 : (sbyte)-1,
            Requested17 = registered ? (sbyte)3 : (sbyte)-1,
            Dirty18 = registered ? (byte)1 : (byte)0
        };
        ui = registered ? ui.SetFlags(0x800) : ui.SetFlags(0, 0x800);
        return graphics with { UiServices = ui with { Windows = windows with { Rows = windows.Rows.SetItem(6, window) } } };
    }
}
