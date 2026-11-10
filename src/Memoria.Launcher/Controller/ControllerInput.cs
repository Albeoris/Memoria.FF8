using System.Diagnostics;
using SDL3;

namespace Memoria.Launcher;

internal enum ControllerAction
{
    Left,
    Right,
    Up,
    Down,
    Confirm,
    Cancel,
    Previous,
    Next,
    ScrollUp,
    ScrollDown
}

internal sealed class ControllerInput : IDisposable
{
    private const int DiscoveryInterval = 500;
    private const int InitialRepeatDelay = 350;
    private const int RepeatInterval = 110;
    private const int StickPressThreshold = 16500;
    private const int StickReleaseThreshold = 11000;
    private static readonly ControllerAction[] Actions = Enum.GetValues<ControllerAction>();

    private readonly Dictionary<uint, Gamepad> gamepads = new();
    private long nextDiscovery;
    private bool initializationAttempted;
    private bool initialized;
    private bool suppressOnNextPoll = true;
    private bool disposed;

    public void Suppress()
    {
        suppressOnNextPoll = true;
    }

    public void Poll(bool active, Action<ControllerAction> dispatch)
    {
        if (!active)
        {
            Suppress();
            return;
        }

        if (disposed)
        {
            return;
        }

        if (!initializationAttempted)
        {
            Initialize();
        }
        if (!initialized)
        {
            return;
        }

        long now = Environment.TickCount64;
        SDL.UpdateGamepads();
        foreach ((uint id, Gamepad gamepad) in gamepads)
        {
            if (!SDL.GamepadConnected(gamepad.Handle))
            {
                SDL.CloseGamepad(gamepad.Handle);
                gamepads.Remove(id);
            }
        }

        if (suppressOnNextPoll || now >= nextDiscovery)
        {
            DiscoverGamepads();
            nextDiscovery = now + DiscoveryInterval;
        }

        Span<bool> pressed = stackalloc bool[Actions.Length];
        pressed.Clear();
        foreach (Gamepad gamepad in gamepads.Values)
        {
            gamepad.Read(now, suppressOnNextPoll, pressed);
        }
        suppressOnNextPoll = false;

        foreach (ControllerAction action in Actions)
        {
            // Dispatch can synchronously move input to a different window or close the launcher.
            if (suppressOnNextPoll || disposed)
            {
                break;
            }
            if (pressed[(int)action])
            {
                dispatch(action);
            }
        }
    }

    private void Initialize()
    {
        initializationAttempted = true;
        try
        {
            // Avalonia owns the windows. Launcher activation gates input at the point of dispatch.
            SDL.SetHint(SDL.Hints.JoystickAllowBackgroundEvents, "1");
            initialized = SDL.InitSubSystem(SDL.InitFlags.Gamepad);
            if (!initialized)
            {
                Trace.TraceWarning($"Cannot initialize launcher controller input: {SDL.GetError()}");
                return;
            }

            SDL.SetGamepadEventsEnabled(false);
            SDL.SetJoystickEventsEnabled(false);
        }
        catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            if (initialized)
            {
                SDL.QuitSubSystem(SDL.InitFlags.Gamepad);
                initialized = false;
            }
            Trace.TraceWarning($"Cannot load launcher controller input: {exception.Message}");
        }
    }

    private void DiscoverGamepads()
    {
        if (SDL.GetGamepads(out _) is not uint[] ids)
        {
            return;
        }

        foreach (uint id in ids)
        {
            if (gamepads.ContainsKey(id))
            {
                continue;
            }

            nint handle = SDL.OpenGamepad(id);
            if (handle != 0)
            {
                gamepads.Add(id, new Gamepad(handle));
            }
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (!initialized)
        {
            return;
        }

        foreach (Gamepad gamepad in gamepads.Values)
        {
            SDL.CloseGamepad(gamepad.Handle);
        }
        gamepads.Clear();
        SDL.QuitSubSystem(SDL.InitFlags.Gamepad);
    }

    private sealed class Gamepad(nint handle)
    {
        private readonly ButtonState[] buttons = new ButtonState[Actions.Length];
        private bool sampled;
        private bool stickBlocked;
        private bool scrollBlocked;

        public nint Handle { get; } = handle;

        public void Read(long now, bool suppress, Span<bool> pressed)
        {
            suppress |= !sampled;
            sampled = true;
            int x = SDL.GetGamepadAxis(Handle, SDL.GamepadAxis.LeftX);
            int y = SDL.GetGamepadAxis(Handle, SDL.GamepadAxis.LeftY);
            int scroll = SDL.GetGamepadAxis(Handle, SDL.GamepadAxis.RightY);
            // Require a held stick to return to center even if it changes direction.
            stickBlocked = (suppress || stickBlocked) && Math.Max(Math.Abs(x), Math.Abs(y)) > StickReleaseThreshold;
            scrollBlocked = (suppress || scrollBlocked) && Math.Abs(scroll) > StickReleaseThreshold;
            if (stickBlocked)
            {
                x = 0;
                y = 0;
            }
            if (scrollBlocked)
            {
                scroll = 0;
            }
            bool horizontal = Math.Abs(x) > Math.Abs(y);

            foreach (ControllerAction action in Actions)
            {
                ref ButtonState state = ref buttons[(int)action];
                int threshold = state.Held ? StickReleaseThreshold : StickPressThreshold;
                bool held = action switch
                {
                    ControllerAction.Left => Button(SDL.GamepadButton.DPadLeft) || (horizontal && x <= -threshold),
                    ControllerAction.Right => Button(SDL.GamepadButton.DPadRight) || (horizontal && x >= threshold),
                    ControllerAction.Up => Button(SDL.GamepadButton.DPadUp) || (!horizontal && y <= -threshold),
                    ControllerAction.Down => Button(SDL.GamepadButton.DPadDown) || (!horizontal && y >= threshold),
                    ControllerAction.Confirm => Button(SDL.GamepadButton.South),
                    ControllerAction.Cancel => Button(SDL.GamepadButton.East),
                    ControllerAction.Previous => Button(SDL.GamepadButton.LeftShoulder),
                    ControllerAction.Next => Button(SDL.GamepadButton.RightShoulder),
                    ControllerAction.ScrollUp => scroll <= -threshold,
                    ControllerAction.ScrollDown => scroll >= threshold,
                    _ => false
                };
                bool repeats = action is ControllerAction.Left or ControllerAction.Right or ControllerAction.Up or ControllerAction.Down or ControllerAction.ScrollUp or ControllerAction.ScrollDown;
                pressed[(int)action] |= state.Update(now, held, suppress, repeats);
            }
        }

        private bool Button(SDL.GamepadButton button) => SDL.GetGamepadButton(Handle, button);
    }

    private struct ButtonState
    {
        private bool blocked;
        private long repeatAt;

        public bool Held { get; private set; }

        public bool Update(long now, bool held, bool suppress, bool repeats)
        {
            bool wasHeld = Held;
            Held = held;
            if (!held || suppress)
            {
                // A control held across activation or connection must be released before it can fire.
                blocked = held;
                return false;
            }
            if (blocked)
            {
                return false;
            }
            if (!wasHeld)
            {
                repeatAt = now + InitialRepeatDelay;
                return true;
            }
            if (repeats && now >= repeatAt)
            {
                repeatAt = now + RepeatInterval;
                return true;
            }
            return false;
        }
    }
}
