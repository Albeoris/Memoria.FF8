using Memoria.FF8.Events;
using Memoria.FF8.Resources;
using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class ControllerGameplayInput
{
    public static bool WorldOwnsMenu(GameState state) => state.Scene == SceneKind.WorldMap && (state.World is { NativeFootRuntime: null } world && (world.NativeGardenRuntime is not null || world.NativeRagnarokRuntime is not null) || state.World is null && state.WorldArrival?.Mode is 48 or 50);

    public static PlayerAction CreateWorldAction(InputFrame input, GameState state, ushort buttons)
    {
        // The dialogue UI handles Confirm separately. Keep advancing text while the tutorial owns world input.
        if (state.Scene == SceneKind.WorldMap && state.World?.EventUi is { ScriptMode: 14 } tutorial && tutorial.Windows.Any(window => window.WindowId is 9 or 11))
        {
            return new PlayerAction(PlayerActionKind.Wait);
        }
        if (state.Scene == SceneKind.WorldMap && state.World is { NativeFootRuntime: null } world && (world.NativeCarRuntime is not null || world.NativeGardenRuntime is not null || world.NativeRagnarokRuntime is not null))
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
            return new PlayerAction(PlayerActionKind.WorldButtons, vehicleButtons, Horizontal: input.Horizontal, Vertical: throttle, CameraHorizontal: input.CameraHorizontal);
        }
        bool parkedVehicle = state.World?.NativeFootRuntime is not null && (state.World.NativeFootRuntime.ParkedCar is not null || state.World.NativeGardenRuntime is not null || state.World.NativeRagnarokRuntime is not null);
        bool worldConfirm = parkedVehicle || state.World?.NativeChocoboRuntime is not null;
        ushort worldButtons = (ushort)(buttons & (state.Scene == SceneKind.WorldMap ? worldConfirm ? 0x142 : 0x102 : 0x02));
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

    internal static ushort WorldTutorialButton(GameDataEdition edition, int messageId, ushort button)
    {
        if (edition != GameDataEdition.Remastered)
        {
            return button;
        }

        // Remastered's world text uses its own glyph slots for reverse and cockpit.
        return (messageId, button) switch
        {
            (49 or 50 or 52, 0x20) => 0x10,
            (50 or 52, 0x10) => 0x20,
            _ => button
        };
    }
}
