using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private static void ValidatePresentationBindings(NativeBattleEffectPresentationInputs inputs, BattleModelRoster roster, BattleModelOwner source)
    {
        if (!roster.Contains(source) || inputs.NativeTargets.IsDefault || inputs.ForeignActors.IsDefault)
        {
            throw new InvalidDataException("The effect presentation requires a bound source and explicit model bindings.");
        }
        HashSet<BattleModelOwner> occupied = [source];
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
            : NativeBattleWaterHeaderCapture.SeedRetainedActorFields(core.ReadHeader(), graphics.ActorModels, graphics.NativeBindings);
        NativeEffectMemory = NativeEffectMemory with { ScriptedEffects = core with { PresentationActors = context } };
    }
}
