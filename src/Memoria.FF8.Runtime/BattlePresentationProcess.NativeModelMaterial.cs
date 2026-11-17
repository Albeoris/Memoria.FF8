using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private NativeBattleModelTextureOwner? SelectNativeModelMaterial(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        return SelectNativeModelMaterial(graphics, owner, NativeEffectMemory?.Textures);
    }

    private NativeBattleModelTextureOwner? SelectNativeModelMaterial(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleEffectTextureState? textures)
    {
        if (textures?.PresentationTextures is null || NativeGraphicsUsesForeignPose(graphics, owner))
        {
            return null;
        }
        return RequireNativeModelMaterial(graphics, owner, textures);
    }

    private static NativeBattleModelTextureOwner? SelectNativeModelMaterial(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleResourcePointer geometry, NativeBattleEffectTextureState? textures)
    {
        if (textures?.PresentationTextures is null)
        {
            return null;
        }
        if (geometry.Cpu is null)
        {
            throw new NotSupportedException("Hosted model geometry selected by a numeric pointer requires an explicit texture owner.");
        }
        return RequireNativeModelMaterial(graphics, owner, textures);
    }

    private static NativeBattleModelTextureOwner RequireNativeModelMaterial(NativeBattleGraphicsState graphics, BattleModelOwner owner, NativeBattleEffectTextureState textures)
    {
        if (!graphics.ActorModels.Contains(owner))
        {
            throw new InvalidDataException("Native model packets require a bound material actor.");
        }
        string actorId = ReadGraphicsPresentationId(owner);
        textures.RequirePresentationTexture(actorId);
        return new(actorId);
    }

    private static ImmutableArray<NativeBattleGeometryPrimitive> BindNativeModelMaterial(ImmutableArray<NativeBattleGeometryPrimitive> primitives, NativeBattleModelTextureOwner? material)
    {
        return material is null
            ? primitives
            : primitives.Select(primitive => BindNativeModelMaterial(primitive, primitive.PresentationActorId, material)).ToImmutableArray();
    }

    private static NativeBattleGfGeometryBatch BindNativeModelMaterial(NativeBattleGfGeometryBatch batch, NativeBattleModelTextureOwner? material)
    {
        if (material is null)
        {
            return batch;
        }
        return batch with { Attempts = batch.Attempts.Select(attempt => attempt.Primitive is { } primitive ? attempt with { Primitive = BindNativeModelMaterial(primitive, primitive.PresentationActorId, material) } : attempt).ToImmutableArray() };
    }

    private static NativeBattleGeometryPrimitive BindNativeModelMaterial(NativeBattleGeometryPrimitive primitive, string? actorId, NativeBattleModelTextureOwner? material)
    {
        if (primitive.ModelTextureOwner is null && primitive.PilotGeometry?.ForeignSurface is null && primitive.SceneCapture is null)
        {
            primitive = primitive with { ModelTextureOwner = material };
        }
        return primitive with { PresentationActorId = actorId };
    }
}
