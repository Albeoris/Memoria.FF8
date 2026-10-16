namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleGraphicsResource NativeGraphicsPoseResource(NativeBattleGraphicsState graphics, int index)
    {
        if ((uint)index >= 7 || graphics.ActorIds[index] is not string actorId)
        {
            throw new InvalidDataException("Native graphics pose has no allocated resource owner.");
        }
        NativeBattleGraphicsBinding binding = FindNativeGraphicsBinding(actorId) ?? throw new InvalidDataException("Native graphics pose lost its original model resource.");
        return graphics.Models[index].ResourceMagicId is ushort magic ? definition.NativeRenzokukenResources.Single(resource => resource.MagicId == magic).BindGraphicsResource(binding.Resource.PoseSource ?? throw new NotSupportedException("Embedded native pose requires its original skeleton owner.")) : binding.Resource;
    }

    internal NativeBattleVector ReadNativeGraphicsBone(int index, byte selector, short fraction)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native bone projection requires its original skeleton and compressed animation source.");
        return NativeBattlePoseMatrices.ReadBone(graphics.Models[index], source, selector, fraction);
    }

    internal NativeBattleCameraPoint TransformNativeGraphicsCameraPoint(int index, byte lowNibble, NativeBattleCameraPoint local, bool mirror)
    {
        NativeBattleGraphicsState graphics = RequireNativeGraphics();
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native actor-relative camera requires its original skeleton and animation source.");
        NativeBattleVector result = NativeBattlePoseMatrices.TransformCameraPoint(graphics.Models[index], source, lowNibble, new(local.X, local.Y, local.Z), mirror);
        return new(result.X, result.Y, result.Z);
    }

    private NativeBattleModuleAnchor ReadNativeModelAnchor(NativeBattleGraphicsState graphics, int index)
    {
        NativeBattlePoseSource source = NativeGraphicsPoseResource(graphics, index).PoseSource ?? throw new NotSupportedException("Native571400 requires its original skeleton and compressed animation source.");
        return NativeBattlePoseMatrices.ReadAnchor(graphics.Models[index], source);
    }
}
