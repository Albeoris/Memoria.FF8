namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const ushort HostedEnemyBlockedTextureSlots = 63;

    private (string ActorId, NativeBattlePresentationTextureBank Bank) RequireHostedModelLoadOwner(NativeBattleGraphicsState graphics, BattleModelRoster roster, int actorRow)
    {
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new InvalidDataException("Hosted model loading requires its CPU resource owner.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new InvalidDataException("Hosted model loading requires its texture alias owner.");
        NativeBattlePartyLoadPlan.RequireMatchingRoster(roster, cpu.Cpu.PresentationRoster);
        NativeBattlePartyLoadPlan.RequireMatchingRoster(roster, cache.PresentationRoster);
        cache.Validate();
        if (cpu.Rows.Length != cache.Rows.Length || (uint)actorRow >= graphics.Models.Length || (uint)actorRow >= graphics.ActorIds.Length || (uint)actorRow >= roster.ActorCount || roster.Rows[actorRow] is not { } actor || !StringComparer.Ordinal.Equals(actor.ActorId, graphics.ActorIds[actorRow]))
        {
            throw new InvalidDataException("Hosted model loading requires matching resource aliases and its actual bound actor.");
        }
        NativeBattleEffectTextureState textures = NativeEffectMemory?.Textures ?? throw new InvalidDataException("Hosted model loading requires its texture image owner.");
        return (actor.ActorId, textures.RequirePresentationTexture(actor.ActorId));
    }

    private static void RequireHostedModelLoadAlias(NativeBattleModelCpuResources cpu, NativeBattleModelTextureCache cache, int alias, int actorRow, byte kind, byte modelId)
    {
        if (cache.PresentationRoster is null)
        {
            return;
        }
        if ((uint)alias >= cpu.Rows.Length || (uint)alias >= cache.Rows.Length || cache.Rows[alias].Kind != kind || cache.Rows[alias].ModelId != modelId || cache.PresentationActors is not { } owners || !owners.TryGetValue(alias, out int owner) || owner != actorRow)
        {
            throw new InvalidDataException("Hosted CPU and texture resource aliases disagree with the selected actor.");
        }
    }
}
