using System.Collections.Immutable;
using static Memoria.FF8.Runtime.NativeBattleEffect326Profile;

namespace Memoria.FF8.Runtime;

public static partial class NativeBattleEffect326Controller
{
    private static NativeBattleCactuarRawModelState RequireModel(NativeBattleEffect326Frame frame, uint address) => frame.State.Models.TryGetValue(address, out NativeBattleCactuarRawModelState? model) && model is not null ? model : throw new NotSupportedException("Effect326 requires its actual selected raw model owner.");

    private static NativeBattleEffectOwnedModelResource ModelResource(NativeBattleEffect326Resources resources, uint address) => address switch
    {
        0x24F1268 => resources.MainModel,
        0x24F0EE8 => resources.EmbeddedModel,
        0x24F0D50 => resources.LateModel,
        _ => throw new NotSupportedException("Effect326 selected an unsupported model row.")
    };

    private static NativeBattleRawModelBinding ModelBinding(uint address) => new(ModelBindings.Single(binding => binding.Model == address).Control, 4);

    private static NativeBattleEffect326Frame SetModel(NativeBattleEffect326Frame frame, uint address, NativeBattleCactuarRawModelState model) => frame with { State = frame.State with { Models = frame.State.Models.SetItem(address, model) } };

    public static NativeBattleEffect326Frame InitializeModel62C790(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, uint modelAddress, uint sourceAddress, ushort argument)
    {
        NativeBattleEffectOwnedModelResource resource = ModelResource(resources, modelAddress);
        if (sourceAddress != resource.Address || !ReadBytes(frame, sourceAddress, 20).Read(0, 20).AsSpan().SequenceEqual(resource.Bytes.AsSpan().Slice(0, 20)))
        {
            throw new InvalidDataException("Effect326 model initialization requires its actual loaded section directory.");
        }
        NativeBattleCactuarRawModelState model = NativeBattleCactuarRawModel.Initialize5A8F40(RequireModel(frame, modelAddress), resource, sourceAddress, argument, ModelBinding(modelAddress));
        return SetModel(frame, modelAddress, model);
    }

    public static NativeBattleEffect326Frame SelectModel62C800(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, uint modelAddress, short animation)
    {
        NativeBattleCactuarRawModelState model = NativeBattleCactuarRawModel.Select5A8FB0(RequireModel(frame, modelAddress), ModelResource(resources, modelAddress), animation, ModelBinding(modelAddress));
        return PublishModelPose(SetModel(frame, modelAddress, model), resources, modelAddress);
    }

    public static NativeBattleEffect326Frame AdvanceModel62CBC0(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, uint modelAddress)
    {
        NativeBattleCactuarRawModelStep step = NativeBattleCactuarRawModel.Advance5A8FD0(RequireModel(frame, modelAddress), ModelResource(resources, modelAddress), ModelBinding(modelAddress));
        return PublishModelPose(SetModel(frame, modelAddress, step.State), resources, modelAddress);
    }

    private static NativeBattleEffect326Frame PublishModelPose(NativeBattleEffect326Frame frame, NativeBattleEffect326Resources resources, uint modelAddress)
    {
        NativeBattleCactuarRawModelState model = RequireModel(frame, modelAddress);
        NativeBattleEffectOwnedModelResource resource = ModelResource(resources, modelAddress);
        NativeBattleDecodedPose pose = model.DecodedPose ?? throw new NotSupportedException("Effect326 pose publication requires its actual decoded compressed pose.");
        uint skeleton = checked((model.WorkingBase ?? throw new NotSupportedException("Effect326 pose publication requires its actual source pointer.")) + (uint)resource.Section(0));
        int length = resource.Section(1) - resource.Section(0);
        NativeBattleEffectArena source = ReadBytes(frame, skeleton, length);
        source = NativeBattleArenaPose.WritePose(source, 0, pose);
        source = NativeBattleArenaPose.RestoreBoneMatrices(source, 0, pose);
        frame = WriteBytes(frame, skeleton, source, length);
        (frame, uint temporary) = Allocate(frame, 88);
        NativeBattleEffectArena context = NativeBattleBodyBoneTemporary.Prepare508C90(resource.PoseSource, pose, NativeBattlePoseMatrices.StageBoneMatrices(resource.PoseSource, pose), Context(frame, temporary, 88));
        return Release(PublishContext(frame, temporary, context, 88), 88);
    }
}
