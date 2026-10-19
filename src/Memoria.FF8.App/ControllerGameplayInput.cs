using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class ControllerGameplayInput
{
    public static PlayerAction CreateWorldAction(InputFrame input, GameState state, ushort buttons)
    {
        if (state.Scene == SceneKind.WorldMap && state.World?.NativeCarRuntime is not null)
        {
            // Pause is consumed by the host before world simulation.
            return new PlayerAction(PlayerActionKind.WorldButtons, buttons & ~0x800, Horizontal: input.Horizontal, Vertical: input.SecondaryVertical);
        }
        ushort worldButtons = (ushort)(buttons & 0x02);
        return input.Horizontal != 0 || input.Vertical != 0 ? new PlayerAction(PlayerActionKind.Move, Horizontal: input.Horizontal, Vertical: input.Vertical, CameraHorizontal: input.CameraHorizontal, WorldButtons: worldButtons) : new PlayerAction(PlayerActionKind.Wait, CameraHorizontal: input.CameraHorizontal, WorldButtons: worldButtons);
    }

    public static (float Horizontal, float Vertical) ReadCarAxes(uint buttons, float steering, float throttle)
    {
        // 5573C0 shares this digital packet with the other ordinary vehicles.
        // Any D-pad direction suppresses analog steering; face buttons override right-stick throttle.
        NativeRagnarokInputPacket digital = NativeRagnarokWorldInput.ReadDigitalButtons(buttons);
        float horizontal = (buttons & 0xF000) != 0 ? -digital.Steering13 / 127f : steering;
        float vertical = (buttons & 0x90) != 0 ? -digital.Throttle11 / 127f : throttle;
        return (horizontal, vertical);
    }
}
