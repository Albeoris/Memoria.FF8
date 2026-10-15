using System.Runtime.InteropServices;
using System.Text;
using Memoria.FF8.Runtime;
using Microsoft.Xna.Framework.Input;
using SDL3;

namespace Memoria.FF8.App.Tests;

[CollectionDefinition("SDL virtual controllers", DisableParallelization = true)]
public sealed class SdlControllerCollection;

[Collection("SDL virtual controllers")]
public sealed class ControllerDeviceTests
{
    [Fact]
    public void Virtual_controller_preserves_raw_analog_values_and_all_standard_buttons()
    {
        using SdlSession session = new();
        using VirtualGamepad pad = new("XboxOne");
        using ControllerDevice device = new();
        Assert.True(device.Read().IsConnected);
        Assert.False(SDL.GamepadEventsEnabled());
        Assert.False(SDL.JoystickEventsEnabled());
        Assert.Equal(SDL.InitFlags.Gamepad | SDL.InitFlags.Joystick | SDL.InitFlags.Events, SDL.WasInit(0));

        pad.Axis(SDL.GamepadAxis.LeftX, 1024);
        pad.Axis(SDL.GamepadAxis.LeftY, short.MinValue);
        pad.Axis(SDL.GamepadAxis.RightX, short.MaxValue);
        pad.Axis(SDL.GamepadAxis.RightY, 8192);
        pad.Axis(SDL.GamepadAxis.LeftTrigger, 0);
        pad.Axis(SDL.GamepadAxis.RightTrigger, short.MaxValue);
        GamePadState state = device.Read();
        Assert.Equal(1024f / 32767f, state.ThumbSticks.Left.X);
        Assert.Equal(1f, state.ThumbSticks.Left.Y);
        Assert.Equal(1f, state.ThumbSticks.Right.X);
        Assert.Equal(-8192f / 32767f, state.ThumbSticks.Right.Y);
        Assert.InRange(state.Triggers.Left, 0.49f, 0.51f);
        Assert.Equal(1f, state.Triggers.Right);

        (SDL.GamepadButton Source, Buttons Target)[] buttons =
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
        foreach ((SDL.GamepadButton source, Buttons target) in buttons)
        {
            pad.Button(source, true);
            Assert.True(device.Read().IsButtonDown(target));
            pad.Button(source, false);
            Assert.False(device.Read().IsButtonDown(target));
        }

        pad.Axis(SDL.GamepadAxis.LeftX, short.MinValue);
        pad.Axis(SDL.GamepadAxis.LeftTrigger, short.MinValue);
        state = device.Read();
        Assert.Equal(-1f, state.ThumbSticks.Left.X);
        Assert.Equal(0f, state.Triggers.Left);
    }

    [Fact]
    public void Selection_survives_other_hotplugs_and_reselects_after_disconnect_and_reconnect()
    {
        using SdlSession session = new();
        using ControllerDevice device = new();
        Assert.False(device.Read().IsConnected);
        Assert.Equal(0, device.Generation);
        using VirtualGamepad first = new("XboxOne");
        first.Button(SDL.GamepadButton.South, true);
        Assert.True(device.Read().IsButtonDown(Buttons.A));
        int firstGeneration = device.Generation;
        using VirtualGamepad second = new("PS5");
        second.Button(SDL.GamepadButton.East, true);
        Assert.True(device.Read().IsButtonDown(Buttons.A));
        Assert.Equal(ControllerPresentation.Xbox, device.Presentation);
        Assert.Equal(firstGeneration, device.Generation);
        first.Dispose();
        Assert.True(device.Read().IsButtonDown(Buttons.B));
        Assert.Equal(ControllerPresentation.PlayStation, device.Presentation);
        Assert.Equal(firstGeneration + 1, device.Generation);
        second.Dispose();
        Assert.False(device.Read().IsConnected);
        Assert.Equal(ControllerPresentation.Generic, device.Presentation);
        int disconnectedGeneration = device.Generation;
        using VirtualGamepad reconnected = new("XboxOne");
        Assert.True(device.Read().IsConnected);
        Assert.Equal(disconnectedGeneration + 1, device.Generation);
    }

    [Theory]
    [InlineData("Standard", ControllerPresentation.Generic)]
    [InlineData("GameCube", ControllerPresentation.Generic)]
    [InlineData("Xbox360", ControllerPresentation.Xbox)]
    [InlineData("PS5", ControllerPresentation.PlayStation)]
    [InlineData("NintendoSwitchPro", ControllerPresentation.Nintendo)]
    public void Identity_uses_SDL_type_and_face_buttons_keep_physical_positions(string type, ControllerPresentation presentation)
    {
        using SdlSession session = new();
        using VirtualGamepad pad = new(type);
        using ControllerDevice device = new();
        pad.Button(SDL.GamepadButton.South, true);
        GamePadState state = device.Read();
        Assert.Equal(presentation, device.Presentation);
        Assert.True(state.IsButtonDown(Buttons.A));
        Assert.False(state.IsButtonDown(Buttons.B));
        if (presentation == ControllerPresentation.Nintendo)
        {
            Assert.Equal("[B]", InputChord.ButtonName(Buttons.A, device.Presentation));
        }
    }

    [Fact]
    public void Native_feedback_reaches_virtual_rumble_and_focus_settings_and_disposal_stop_motors()
    {
        using SdlSession session = new();
        using VirtualGamepad pad = new("XboxOne");
        using ControllerDevice device = new();
        Assert.True(device.Read().IsConnected);
        NativeVibrationProcessState cold = NativeVibrationProcessState.FromColdProcess();
        NativeVibrationProcessState process = cold with { Ports = cold.Ports.SetItem(0, new NativeVibrationPort(64, 192, true)) };
        ControllerFeedback feedback = new(device.SetVibration);
        Assert.True(feedback.Update(process, true, true));
        Assert.Equal((ushort)(64 * 257), pad.LastRumble.Left);
        Assert.Equal((ushort)(192 * 257), pad.LastRumble.Right);
        Assert.True(feedback.Update(process, false, true));
        Assert.Equal(((ushort)0, (ushort)0), pad.LastRumble);
        Assert.True(feedback.Update(process, true, true));
        Assert.True(feedback.Update(process, true, false));
        Assert.Equal(((ushort)0, (ushort)0), pad.LastRumble);
        Assert.True(feedback.Update(process, true, true));
        device.Dispose();
        Assert.Equal(((ushort)0, (ushort)0), pad.LastRumble);
        Assert.False(device.SetVibration(1, 1));
        Assert.Equal((byte)64, process.Ports[0].RequestedA);
        Assert.Equal((byte)192, process.Ports[0].RequestedB);
    }

    private sealed class SdlSession : IDisposable
    {
        public SdlSession()
        {
            // Isolate these virtual-device tests from any real controllers attached to the test machine.
            SDL.SetHint("SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT", "0x1209/0xf801,0x1209/0xf802,0x1209/0xf803,0x1209/0xf804,0x1209/0xf805");
            Assert.True(SDL.InitSubSystem(SDL.InitFlags.Gamepad), SDL.GetError());
        }

        public void Dispose()
        {
            SDL.QuitSubSystem(SDL.InitFlags.Gamepad);
            SDL.ResetHint("SDL_GAMECONTROLLER_IGNORE_DEVICES_EXCEPT");
        }
    }

    private sealed class VirtualGamepad : IDisposable
    {
        private readonly uint id;
        private readonly nint joystick;
        private readonly nint name;
        private readonly SDL.VirtualJoystickRumbleCallback rumble;
        private bool disposed;

        public VirtualGamepad(string type)
        {
            rumble = (_, left, right) =>
            {
                LastRumble = (left, right);
                return true;
            };
            name = Marshal.StringToCoTaskMemUTF8($"FF8 virtual {type}");
            SDL.VirtualJoystickDesc descriptor = new();
            SDL.InitInterface(ref descriptor);
            descriptor.Type = SDL.JoystickType.Gamepad;
            descriptor.VendorID = 0x1209;
            descriptor.ProductID = type switch
            {
                "XboxOne" => 0xf801,
                "PS5" => 0xf802,
                "NintendoSwitchPro" => 0xf803,
                "Xbox360" => 0xf804,
                _ => 0xf805
            };
            descriptor.Name = name;
            descriptor.NAxes = 6;
            descriptor.NButtons = 15;
            descriptor.AxisMask = (1u << 6) - 1;
            descriptor.ButtonMask = (1u << 15) - 1;
            descriptor.Ramble = rumble;
            id = SDL.AttachVirtualJoystick(in descriptor);
            Assert.NotEqual(0u, id);
            byte[] guidText = new byte[33];
            SDL.GUIDToString(SDL.GetGamepadGUIDForID(id), guidText, guidText.Length);
            string guid = Encoding.UTF8.GetString(guidText, 0, 32);
            string mappingType = SDL.GetGamepadStringForType(Enum.Parse<SDL.GamepadType>(type)) ?? throw new InvalidOperationException($"Unknown SDL gamepad type {type}.");
            Assert.True(SDL.AddGamepadMapping($"{guid},FF8 virtual {type},a:b0,b:b1,x:b2,y:b3,back:b4,guide:b5,start:b6,leftstick:b7,rightstick:b8,leftshoulder:b9,rightshoulder:b10,dpup:b11,dpdown:b12,dpleft:b13,dpright:b14,leftx:a0,lefty:a1,rightx:a2,righty:a3,lefttrigger:a4,righttrigger:a5,type:{mappingType},") >= 0, SDL.GetError());
            joystick = SDL.OpenJoystick(id);
            Assert.NotEqual(0, joystick);
            Axis(SDL.GamepadAxis.LeftTrigger, short.MinValue);
            Axis(SDL.GamepadAxis.RightTrigger, short.MinValue);
        }

        public (ushort Left, ushort Right) LastRumble { get; private set; }

        public void Axis(SDL.GamepadAxis axis, short value)
        {
            Assert.True(SDL.SetJoystickVirtualAxis(joystick, (int)axis, value), SDL.GetError());
        }

        public void Button(SDL.GamepadButton button, bool value)
        {
            Assert.True(SDL.SetJoystickVirtualButton(joystick, (int)button, value), SDL.GetError());
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            SDL.CloseJoystick(joystick);
            Assert.True(SDL.DetachVirtualJoystick(id), SDL.GetError());
            Marshal.FreeCoTaskMem(name);
            GC.KeepAlive(rumble);
            disposed = true;
        }
    }
}
