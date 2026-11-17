namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private (string ActorId, NativeBattlePresentationTextureBank Bank)? SelectNativeModelTextureBank(BattleModelOwner owner, NativeBattleEffectTextureState textures)
    {
        if (textures.PresentationTextures is null)
        {
            return null;
        }
        if (!RequireNativeGraphics().ActorModels.Contains(owner))
        {
            throw new InvalidDataException("Model texture publication requires a bound model owner.");
        }
        string actorId = ReadGraphicsPresentationId(owner);
        return (actorId, textures.RequirePresentationTexture(actorId));
    }

    private static NativeBattleEffectTextureState PublishNativeModelTextures(NativeBattleEffectTextureState textures, (string ActorId, NativeBattlePresentationTextureBank Bank)? material, NativeBattleEffectTextureState updated)
    {
        return material is { } owner ? textures.WithPresentationTexture(owner.ActorId, owner.Bank with { Textures = updated }) : updated;
    }
}
