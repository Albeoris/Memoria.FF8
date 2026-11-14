namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private (string ActorId, NativeBattlePresentationTextureBank Bank)? SelectNativeModelTextureBank(int actorRow, NativeBattleEffectTextureState textures)
    {
        if (NativeGraphics is not { PresentationRoster: { } roster } graphics || textures.PresentationTextures is null)
        {
            return null;
        }
        if ((uint)actorRow >= graphics.ActorIds.Length || (uint)actorRow >= roster.ActorCount || roster.Rows[actorRow] is not { } actor || !StringComparer.Ordinal.Equals(actor.ActorId, graphics.ActorIds[actorRow]))
        {
            throw new InvalidDataException("Model texture publication requires the actual bound presentation actor.");
        }
        return (actor.ActorId, textures.RequirePresentationTexture(actor.ActorId));
    }

    private static NativeBattleEffectTextureState PublishNativeModelTextures(NativeBattleEffectTextureState textures, (string ActorId, NativeBattlePresentationTextureBank Bank)? material, NativeBattleEffectTextureState updated)
    {
        return material is { } owner ? textures.WithPresentationTexture(owner.ActorId, owner.Bank with { Textures = updated }) : updated;
    }
}
