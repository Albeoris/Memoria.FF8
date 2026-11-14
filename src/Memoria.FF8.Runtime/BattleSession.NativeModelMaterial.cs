using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleModelTextureOwner? SelectNativeModelMaterial(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model)
    {
        return SelectNativeModelMaterial(graphics, model, NativeEffectMemory?.Textures);
    }

    private NativeBattleModelTextureOwner? SelectNativeModelMaterial(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model, NativeBattleEffectTextureState? textures)
    {
        // Ordinary pose overrides retain the original body and weapon geometry.
        if (graphics.PresentationRoster is not { } roster || textures?.PresentationTextures is null || NativeGraphicsUsesForeignPose(graphics, model.Index))
        {
            return null;
        }
        return RequireNativeModelMaterial(graphics, model, roster, textures);
    }

    private static NativeBattleModelTextureOwner? SelectNativeModelMaterial(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model, NativeBattleResourcePointer geometry, NativeBattleEffectTextureState? textures)
    {
        if (graphics.PresentationRoster is not { } roster || textures?.PresentationTextures is null)
        {
            return null;
        }
        if (geometry.Cpu is null)
        {
            throw new NotSupportedException("Hosted model geometry selected by a numeric pointer requires an explicit texture owner.");
        }
        return RequireNativeModelMaterial(graphics, model, roster, textures);
    }

    private static NativeBattleModelTextureOwner RequireNativeModelMaterial(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model, NativeBattleGraphicsRoster roster, NativeBattleEffectTextureState textures)
    {
        string actorId = roster[model.Index].ActorId;
        if (!StringComparer.Ordinal.Equals(actorId, graphics.ActorIds[model.Index]))
        {
            throw new InvalidDataException("Native model packets require the selected row's bound material actor.");
        }
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
