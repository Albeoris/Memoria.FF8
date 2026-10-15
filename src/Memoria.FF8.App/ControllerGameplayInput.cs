using Memoria.FF8.Runtime;

namespace Memoria.FF8;

internal static class ControllerGameplayInput
{
    public static InputFrame Resolve(InputFrame input, GameState state)
    {
        if (state.Scene != SceneKind.WorldMap || state.World?.NativeCarRuntime is null)
        {
            return input;
        }
        // This is a host binding to the existing native car motor. It does not
        // add a second movement or camera contract to the vehicle runtime.
        return input with
        {
            Horizontal = input.SecondaryHorizontal != 0 ? input.SecondaryHorizontal : input.Horizontal,
            Vertical = input.SecondaryVertical != 0 ? input.SecondaryVertical : input.Vertical,
            CameraHorizontal = 0,
            SecondaryVertical = 0
        };
    }
}
