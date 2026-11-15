using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattleSession
{
    private static void ValidatePresentationBindings(NativeBattleEffectPresentationInputs inputs, BattleModelRoster roster)
    {
        if (!roster.Contains(inputs.Parameters.Source) || inputs.NativeTargets.IsDefault || inputs.ForeignActors.IsDefault)
        {
            throw new InvalidDataException("The effect presentation requires a bound source and explicit model bindings.");
        }
        HashSet<BattleModelOwner> occupied = [inputs.Parameters.Source];
        foreach (NativeBattleEffectModelBinding target in inputs.NativeTargets)
        {
            if (target?.Model is null || target.Resource is null || target.Owner is not (BattlePartyActorModel or BattleEnemyActorModel) ||
                !roster.Contains(target.Owner) || !occupied.Add(target.Owner))
            {
                throw new InvalidDataException("A native effect target requires a unique canonical model binding.");
            }
        }
        foreach (NativeBattleForeignActor foreign in inputs.ForeignActors)
        {
            if (foreign is null || !roster.Contains(foreign.Owner) || !occupied.Add(foreign.Owner) ||
                (foreign.NativeModel is null) != (foreign.NativeResource is null))
            {
                throw new InvalidDataException("A foreign effect participant requires a unique model owner and consistent native resources.");
            }
        }
        if (occupied.Count != roster.Count)
        {
            throw new InvalidDataException("The effect presentation lacks a model binding for a supplied owner.");
        }
    }

    private void BindHostedCinematicActors()
    {
        if (NativeGraphics is not { } graphics || NativeEffectMemory?.ScriptedEffects is not { } core)
        {
            return;
        }
        NativeBattleCinematicActorContext context = core.PresentationActors is { } retained
            ? retained.RefreshCache(graphics.ActorModels)
            : SeedHostedCinematicActors(core.ReadHeader(), NativeBattleCinematicActorContext.FromGraphics(graphics.ActorModels), graphics);
        NativeEffectMemory = NativeEffectMemory with { ScriptedEffects = core with { PresentationActors = context } };
    }

    private static NativeBattleCinematicActorContext SeedHostedCinematicActors(NativeBattleWaterHeader header, NativeBattleCinematicActorContext context, NativeBattleGraphicsState graphics)
    {
        ImmutableDictionary<BattleModelOwner, int?>.Builder ordinals = context.ActorOrdinals.ToBuilder();
        Dictionary<byte, BattleModelOwner> originalOwners = [];
        foreach (BattleModelOwner owner in graphics.ActorModels.Owners)
        {
            if (graphics.NativeBindings.TryEncodeExactActor(owner, graphics.ActorModels, out byte slot))
            {
                originalOwners.Add(slot, owner);
                if (header.Known[0xCC + slot] && header.Bytes[0xCC + slot] < graphics.ActorModels.Roster.Count)
                {
                    ordinals[owner] = header.Bytes[0xCC + slot];
                }
            }
        }
        context = context with { ActorOrdinals = ordinals.ToImmutable() };

        if (!header.Known[0x17A] || header.Bytes[0x17A] > 6)
        {
            return context;
        }
        int savedCount = header.Bytes[0x17A];
        ImmutableArray<BattleModelOwner>.Builder saved = ImmutableArray.CreateBuilder<BattleModelOwner>(savedCount);
        for (int index = 0; index < savedCount; index++)
        {
            byte slot = header.Bytes[0x174 + index];
            if (!header.Known[0x174 + index] || slot >= NativeBattleModelBoundary.CellCount ||
                !originalOwners.TryGetValue(slot, out BattleModelOwner? owner))
            {
                return context;
            }
            saved.Add(owner);
        }
        return context.WithSavedActors(saved.MoveToImmutable());
    }
}
