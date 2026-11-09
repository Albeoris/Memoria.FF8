using Memoria.FF7.Formats;
using Memoria.FF7.Resources;
using Memoria.FF7.Runtime;
using Memoria.FF8.Rendering;
using Matrix4x4N = System.Numerics.Matrix4x4;

namespace Memoria.FF8;

internal sealed partial class Ff7Scene
{
    private Ff7ModelRenderOverrides? ModelEffects(Ff7NativeModelEffectState? state, PcFieldCamera camera)
    {
        if (state is null || state.Restored)
        {
            return null;
        }
        state.Validate();
        Func<System.Numerics.Vector3, System.Numerics.Vector3, uint, uint>? color = null;
        if (state.Applied)
        {
            audioNative ??= Ff7PcExecutableData.Load(installation);
            IReadOnlyList<short> directions = audioNative.NativeSinCos;
            Matrix4x4N packed = new(
                camera.AxisX.X / 4096, camera.AxisX.Y / 4096, camera.AxisX.Z / 4096, 0,
                camera.AxisY.X / 4096, camera.AxisY.Y / 4096, camera.AxisY.Z / 4096, 0,
                camera.AxisZ.X / 4096, camera.AxisZ.Y / 4096, camera.AxisZ.Z / 4096, 0,
                camera.Translation.X, camera.Translation.Y, camera.Translation.Z, 1);
            color = (position, normal, original) => Ff7NativeModelEffect.ShadeVertex(state, state.SampleFrame, directions, position, normal, original, packed);
        }
        return new(part => Ff7NativeModelEffect.PartVisible(state, part), color, state.SuppressesNativeLighting);
    }
}
