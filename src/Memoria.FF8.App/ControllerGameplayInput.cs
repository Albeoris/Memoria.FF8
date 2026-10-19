using Memoria.FF8.Events;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class ControllerGameplayInput
{
    public static PlayerAction CreateWorldAction(InputFrame input, GameState state, ushort buttons)
    {
        if (state.Scene == SceneKind.WorldMap && state.World is { NativeFootRuntime: null } world && (world.NativeCarRuntime is not null || world.NativeGardenRuntime is not null))
        {
            // Pause is consumed by the host before world simulation.
            int vehicleButtons = buttons & ~0x800;
            // Directional throttle is a host convenience; explicit throttle buttons retain priority.
            if ((vehicleButtons & 0x90) == 0)
            {
                vehicleButtons |= (buttons & 0x5000) switch
                {
                    0x1000 => 0x80,
                    0x4000 => 0x10,
                    _ => 0
                };
            }
            float throttle = world.NativeGardenRuntime is not null && input.SecondaryVertical == 0 ? input.Vertical : input.SecondaryVertical;
            return new PlayerAction(PlayerActionKind.WorldButtons, vehicleButtons, Horizontal: input.Horizontal, Vertical: throttle);
        }
        ushort worldButtons = (ushort)(buttons & 0x02);
        return input.Horizontal != 0 || input.Vertical != 0 ? new PlayerAction(PlayerActionKind.Move, Horizontal: input.Horizontal, Vertical: input.Vertical, CameraHorizontal: input.CameraHorizontal, WorldButtons: worldButtons) : new PlayerAction(PlayerActionKind.Wait, CameraHorizontal: input.CameraHorizontal, WorldButtons: worldButtons);
    }

    public static (float Horizontal, float Vertical) ReadVehicleAxes(uint buttons, float steering, float throttle)
    {
        // 5573C0 shares this digital packet with the other ordinary vehicles.
        // Any D-pad direction suppresses analog steering; face buttons override analog throttle.
        NativeRagnarokInputPacket digital = NativeRagnarokWorldInput.ReadDigitalButtons(buttons);
        float horizontal = (buttons & 0xF000) != 0 ? -digital.Steering13 / 127f : steering;
        float vertical = (buttons & 0x90) != 0 ? -digital.Throttle11 / 127f : throttle;
        return (horizontal, vertical);
    }
}
