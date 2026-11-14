namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private NativeBattleModelTextureOwner? SelectNativeModelMaterial(NativeBattleGraphicsState graphics, NativeBattleGraphicsModel model)
    {
        NativeBattleEffectTextureState? textures = NativeEffectMemory?.Textures;
        // Ordinary pose overrides retain the original body and weapon geometry.
        if (graphics.PresentationRoster is not { } roster || textures?.PresentationTextures is null || NativeGraphicsUsesForeignPose(graphics, model.Index))
        {
            return null;
        }
        string actorId = roster[model.Index].ActorId;
        if (!StringComparer.Ordinal.Equals(actorId, graphics.ActorIds[model.Index]))
        {
            throw new InvalidDataException("Native model packets require the selected row's bound material actor.");
        }
        textures.RequirePresentationTexture(actorId);
        return new(actorId);
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
