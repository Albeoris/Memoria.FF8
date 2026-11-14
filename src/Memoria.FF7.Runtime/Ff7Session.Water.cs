using System.Numerics;
using Memoria.FF7.Formats;
using Memoria.FF7.Resources;

namespace Memoria.FF7.Runtime;

public sealed partial class Ff7Session
{
    private void ConfigureNativeWater(Ff7FieldHost host, Ff7FieldResources field, IReadOnlyList<Ff7FieldModelResources> resources, ushort map)
    {
        host.NativeWaterSplashResources = Enumerable.Range(0, 4).Select(index => index < field.WaterSplashTextures.Count).ToArray();
        host.NativeWaterEnvironmentProfile = (actor, camera) => Ff7NativeWaterEffect.EnvironmentProfile(map, actor, camera);
        host.NativeWaterGeometryCapture = (actor, camera, parameters) =>
        {
            PcFieldModelDescriptor descriptor = field.Models.Models[actor.Model];
            Ff7FieldModelResources model = resources[actor.Model];
            PcFieldAnimation animation = model.Animations[Path.ChangeExtension(descriptor.Animations[actor.Animation], ".a")];
            Ff7NativeModelPresentation presentation = (host.NativeModelConfiguration ?? throw new NotSupportedException("Native water sampling requires the selected model presentation."))(actor.Slot);
            Vector3 position = Ff7NativeModelPosition.PresentationPoint(actor, presentation);
            Vector2 cameraOffset = Ff7NativeCamera.PresentationOffset(host.Process, camera);
            return Ff7FieldWaterGeometryMath.Capture(model, descriptor, animation, actor.AnimationFrame16 >> 4, position, checked((byte)actor.Direction), camera, cameraOffset, executable.NativePresentation.NearPlane(map), executable.FieldFarPlane, parameters, actor.Animation != 0);
        };
    }
}
