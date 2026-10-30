using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SDL3;

namespace Memoria.FF8;

internal sealed class ControllerDevice : IDisposable
{
    private const float TriggerButtonThreshold = 30f / 255f;

    private static readonly (SDL.GamepadButton Source, Buttons Target)[] ButtonMap =
    [
        (SDL.GamepadButton.South, Buttons.A),
        (SDL.GamepadButton.East, Buttons.B),
        (SDL.GamepadButton.West, Buttons.X),
        (SDL.GamepadButton.North, Buttons.Y),
        (SDL.GamepadButton.Back, Buttons.Back),
        (SDL.GamepadButton.Guide, Buttons.BigButton),
        (SDL.GamepadButton.Start, Buttons.Start),
        (SDL.GamepadButton.LeftStick, Buttons.LeftStick),
        (SDL.GamepadButton.RightStick, Buttons.RightStick),
        (SDL.GamepadButton.LeftShoulder, Buttons.LeftShoulder),
        (SDL.GamepadButton.RightShoulder, Buttons.RightShoulder),
        (SDL.GamepadButton.DPadUp, Buttons.DPadUp),
        (SDL.GamepadButton.DPadDown, Buttons.DPadDown),
        (SDL.GamepadButton.DPadLeft, Buttons.DPadLeft),
        (SDL.GamepadButton.DPadRight, Buttons.DPadRight)
    ];

    private nint gamepad;
    private uint instanceId;
    private bool disposed;

    public ControllerDevice()
    {
        // The MonoGame host controls focus and suppresses input and motors when inactive.
        SDL.SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1");
        if (!SDL.InitSubSystem(SDL.InitFlags.Gamepad))
        {
            throw new InvalidOperationException($"Cannot initialize SDL gamepad input: {SDL.GetError()}");
        }

        // There is no SDL event loop: update state explicitly without building an unused event queue.
        SDL.SetGamepadEventsEnabled(false);
        SDL.SetJoystickEventsEnabled(false);
    }

    public ControllerPresentation Presentation { get; private set; } = ControllerPresentation.Generic;

    public int Generation { get; private set; }

    public GamePadState Read()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        SDL.UpdateGamepads();
        uint previousId = instanceId;
        if (gamepad != 0 && !SDL.GamepadConnected(gamepad))
        {
            CloseSelectedDevice();
        }

        if (gamepad == 0)
        {
            uint[] candidates = SDL.GetGamepads(out _) ?? throw new InvalidOperationException($"Cannot enumerate SDL gamepads: {SDL.GetError()}");
            foreach (uint candidateId in candidates)
            {
                nint candidate = SDL.OpenGamepad(candidateId);
                if (candidate == 0)
                {
                    continue;
                }

                gamepad = candidate;
                instanceId = candidateId;
                break;
            }
        }

        if (previousId != instanceId)
        {
            Generation++;
        }

        if (gamepad == 0)
        {
            return default;
        }

        Presentation = Classify(SDL.GetGamepadType(gamepad));
        Buttons buttons = 0;
        foreach ((SDL.GamepadButton source, Buttons target) in ButtonMap)
        {
            if (SDL.GetGamepadButton(gamepad, source))
            {
                buttons |= target;
            }
        }

        Vector2 left = new(Normalize(SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftX)), -Normalize(SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftY)));
        Vector2 right = new(Normalize(SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.RightX)), -Normalize(SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.RightY)));
        float leftTrigger = Math.Max(0f, SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.LeftTrigger) / (float)short.MaxValue);
        float rightTrigger = Math.Max(0f, SDL.GetGamepadAxis(gamepad, SDL.GamepadAxis.RightTrigger) / (float)short.MaxValue);
        // GamePadState preserves the supplied button mask; analog triggers need explicit button flags.
        if (leftTrigger >= TriggerButtonThreshold)
        {
            buttons |= Buttons.LeftTrigger;
        }

        if (rightTrigger >= TriggerButtonThreshold)
        {
            buttons |= Buttons.RightTrigger;
        }

        return new GamePadState(left, right, leftTrigger, rightTrigger, buttons);
    }

    public bool SetVibration(float left, float right)
    {
        if (disposed || gamepad == 0 || !SDL.GamepadConnected(gamepad))
        {
            return false;
        }

        // Refresh while active; a bounded duration also stops motors if the host ceases updating.
        return SDL.RumbleGamepad(gamepad, MotorStrength(left), MotorStrength(right), 250);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        CloseSelectedDevice();
        SDL.QuitSubSystem(SDL.InitFlags.Gamepad);
        disposed = true;
    }

    private void CloseSelectedDevice()
    {
        if (gamepad == 0)
        {
            return;
        }

        SDL.RumbleGamepad(gamepad, 0, 0, 0);
        SDL.CloseGamepad(gamepad);
        gamepad = 0;
        instanceId = 0;
        Presentation = ControllerPresentation.Generic;
    }

    private static float Normalize(short value) => value < 0 ? value / 32768f : value / 32767f;

    private static ushort MotorStrength(float value) => float.IsFinite(value) ? (ushort)MathF.Round(Math.Clamp(value, 0f, 1f) * ushort.MaxValue) : (ushort)0;

    private static ControllerPresentation Classify(SDL.GamepadType type) => type switch
    {
        SDL.GamepadType.Xbox360 or SDL.GamepadType.XboxOne => ControllerPresentation.Xbox,
        SDL.GamepadType.PS3 or SDL.GamepadType.PS4 or SDL.GamepadType.PS5 => ControllerPresentation.PlayStation,
        SDL.GamepadType.NintendoSwitchPro or SDL.GamepadType.NintendoSwitchJoyconLeft or SDL.GamepadType.NintendoSwitchJoyconRight or SDL.GamepadType.NintendoSwitchJoyconPair => ControllerPresentation.Nintendo,
        _ => ControllerPresentation.Generic
    };
}
