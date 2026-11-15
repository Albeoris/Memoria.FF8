using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed record ExpandedBattleSceneFrame(NativeBattleCameraPose Camera, NativeBattleCameraProjection Projection, ImmutableDictionary<string, NativeBattleVector> Positions);

public interface IExpandedBattlePresentationHost
{
    ExpandedBattleSceneFrame? ReadSceneFrame(BattleSession owner);

    NativeBattleGraphicsBinding PrepareModelBinding(BattleSession owner, BattleModelOwner modelOwner)
    {
        throw new NotSupportedException("The expanded battle host does not provide installed model bindings.");
    }

    NativeBattleEffectPresentation PrepareEffect(BattleSession owner, BattleCombatant caster, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts, GuardianFormInvocationMode? mode);
}

public sealed partial class BattleSession
{
    private IExpandedBattlePresentationHost? expandedPresentationHost;

    public IExpandedBattlePresentationHost? ExpandedPresentationHost => expandedPresentationHost;

    public void AttachExpandedPresentationHost(IExpandedBattlePresentationHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!UsesExpandedRoster || expandedPresentationHost is not null)
        {
            throw new InvalidOperationException("The expanded battle presentation owner is unavailable or already attached.");
        }
        expandedPresentationHost = host;
    }

    public void AttachExpandedEffectFactory(Func<BattleSession, BattleCombatant, BattleNativeAction, byte, ImmutableArray<ExpandedBattleContact>, GuardianFormInvocationMode?, NativeBattleEffectPresentation> factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        AttachExpandedPresentationHost(new ExpandedEffectFactoryHost(factory));
    }

    private sealed class ExpandedEffectFactoryHost(Func<BattleSession, BattleCombatant, BattleNativeAction, byte, ImmutableArray<ExpandedBattleContact>, GuardianFormInvocationMode?, NativeBattleEffectPresentation> factory) : IExpandedBattlePresentationHost
    {
        public ExpandedBattleSceneFrame? ReadSceneFrame(BattleSession owner) => null;

        public NativeBattleEffectPresentation PrepareEffect(BattleSession owner, BattleCombatant caster, BattleNativeAction action, byte sequence, ImmutableArray<ExpandedBattleContact> contacts, GuardianFormInvocationMode? mode)
        {
            return factory(owner, caster, action, sequence, contacts, mode);
        }
    }
}
