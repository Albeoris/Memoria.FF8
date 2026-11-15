namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private const ushort HostedEnemyBlockedTextureSlots = 63;

    private (string ActorId, NativeBattlePresentationTextureBank Bank) RequireHostedModelLoadOwner(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        NativeBattleModelCpuResources cpu = NativeModelCpuResources ?? throw new InvalidDataException("Hosted model loading requires its CPU resource owner.");
        NativeBattleModelTextureCache cache = NativeModelTextureCache ?? throw new InvalidDataException("Hosted model loading requires its texture alias owner.");
        NativeBattlePartyLoadPlan.RequireMatchingRoster(cpu.Cpu.PresentationRoster, cache.PresentationRoster);
        cache.Validate();
        if (cpu.Rows.Length != cache.Rows.Length || !graphics.ActorModels.Contains(owner) || cache.PresentationRoster?.Contains(owner) != true)
        {
            throw new InvalidDataException("Hosted model loading requires matching resource aliases and a bound model owner.");
        }
        string id = ReadGraphicsPresentationId(owner);
        NativeBattleEffectTextureState textures = NativeEffectMemory?.Textures ?? throw new InvalidDataException("Hosted model loading requires its texture image owner.");
        return (id, textures.RequirePresentationTexture(id));
    }

    private static void RequireHostedModelLoadAlias(NativeBattleModelCpuResources cpu, NativeBattleModelTextureCache cache, int alias, BattleModelOwner model, byte kind, byte modelId)
    {
        if (cache.PresentationRoster is null)
        {
            return;
        }
        if ((uint)alias >= cpu.Rows.Length || (uint)alias >= cache.Rows.Length || cache.Rows[alias].Kind != kind || cache.Rows[alias].ModelId != modelId || cache.PresentationActors is not { } owners || !owners.TryGetValue(alias, out BattleModelOwner? owner) || owner != model)
        {
            throw new InvalidDataException("Hosted CPU and texture resource aliases disagree with the selected actor.");
        }
    }
}
