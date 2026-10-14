using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;

namespace Memoria.FF8;

internal sealed class KeyboardInput
{
    private KeyboardState previous;

    public bool SpeedToggleRequested { get; private set; }
    public bool DebugRoomRequested { get; private set; }
    public bool GameMenuRequested { get; private set; }
    public bool UiTabRequested { get; private set; }
    public int UiCharacterPage { get; private set; }
    public bool UiDetailRequested { get; private set; }
    public bool UiConfirmRequested { get; private set; }
    public bool UiCancelRequested { get; private set; }
    public bool BattleDebugRequested { get; private set; }
    public bool BattleInstantVictoryRequested { get; private set; }
    public bool BattleAdvanceTurnRequested { get; private set; }

    public ushort HeldFieldButtons { get; private set; }

    public InputFrame Read(bool active)
    {
        return Read(Keyboard.GetState(), active);
    }

    internal InputFrame Read(KeyboardState current, bool active)
    {
        bool Pressed(Keys key)
        {
            return active && current.IsKeyDown(key) && previous.IsKeyUp(key);
        }
        SpeedToggleRequested = Pressed(Keys.F1);
        DebugRoomRequested = Pressed(Keys.F2);
        BattleDebugRequested = Pressed(Keys.F4);
        BattleInstantVictoryRequested = Pressed(Keys.F3);
        BattleAdvanceTurnRequested = Pressed(Keys.Space);
        GameMenuRequested = Pressed(Keys.V);
        UiTabRequested = Pressed(Keys.Tab);
        UiCharacterPage = (Pressed(Keys.H) ? 1 : 0) - (Pressed(Keys.G) ? 1 : 0);
        UiDetailRequested = Pressed(Keys.S);
        UiConfirmRequested = Pressed(Keys.X);
        UiCancelRequested = Pressed(Keys.C);
        bool confirmPressed = active && (current.IsKeyDown(Keys.Enter) || current.IsKeyDown(Keys.Space)) && !previous.IsKeyDown(Keys.Enter) && !previous.IsKeyDown(Keys.Space);
        HeldFieldButtons = active ? (ushort)((current.IsKeyDown(Keys.Up) ? 0x1000 : 0) | (current.IsKeyDown(Keys.Right) ? 0x2000 : 0) | (current.IsKeyDown(Keys.Down) ? 0x4000 : 0) | (current.IsKeyDown(Keys.Left) ? 0x8000 : 0) | (current.IsKeyDown(Keys.Enter) || current.IsKeyDown(Keys.Space) ? 0x40 : 0) | (current.IsKeyDown(Keys.Escape) ? 0x10 : 0) | (current.IsKeyDown(Keys.C) ? 0x20 : 0) | (current.IsKeyDown(Keys.S) ? 0x80 : 0) | (current.IsKeyDown(Keys.Q) ? 0x04 : 0)) : (ushort)0;
        var input = new InputFrame(active ? (current.IsKeyDown(Keys.Right) ? 1 : 0) - (current.IsKeyDown(Keys.Left) ? 1 : 0) : 0, active ? (current.IsKeyDown(Keys.Down) ? 1 : 0) - (current.IsKeyDown(Keys.Up) ? 1 : 0) : 0, confirmPressed, Pressed(Keys.Escape), Pressed(Keys.Escape) || Pressed(Keys.Space), Pressed(Keys.F5), (Pressed(Keys.Down) ? 1 : 0) - (Pressed(Keys.Up) ? 1 : 0), Pressed(Keys.F9), Pressed(Keys.F6), Pressed(Keys.F10), (Pressed(Keys.Right) ? 1 : 0) - (Pressed(Keys.Left) ? 1 : 0), (Pressed(Keys.PageDown) ? 1 : 0) - (Pressed(Keys.PageUp) ? 1 : 0), Pressed(Keys.R));
        previous = current;
        return input;
    }
}
