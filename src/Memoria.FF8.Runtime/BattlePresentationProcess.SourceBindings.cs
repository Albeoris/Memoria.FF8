using System.Collections.Immutable;

namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private BattleModelRoster? sourceBindingsRoster;
    private NativeBattleModelBoundary? sourceBindingsOriginalBoundary;
    private (BattleModelOwner? First, BattleModelOwner? Second, BattleModelOwner? Third) sourceBindingPartyAliases;
    private readonly Dictionary<BattleSourceGroupId, BattleSourceBindings> sourceBindings = [];

    internal BattleSourceBindings? ReadSourceBindings(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        BattleSourceGrouping grouping = resources.SourceGrouping;
        ImmutableDictionary<BattleModelOwner, HostedModelLoadAdmission>? loads = hostedModelRegistry?.Loads;
        BattleSourceGroupId? selected = grouping.FindGroup(owner);
        if (selected is null && loads is not null && loads.TryGetValue(owner, out HostedModelLoadAdmission? load))
        {
            selected = load.Source?.Group;
        }
        if (selected is not { } group)
        {
            return null;
        }
        BattleModelRoster roster = graphics.ActorModels.Roster;
        if (!ReferenceEquals(sourceBindingsRoster, roster) || !ReferenceEquals(sourceBindingsOriginalBoundary, graphics.NativeBindings))
        {
            var partyAliases = (graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(0), graphics.ActorModels),
                graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(1), graphics.ActorModels),
                graphics.NativeBindings.FindOriginalOwner(NativeBattleModelSlot.Decode(2), graphics.ActorModels));
            if (!ReferenceEquals(sourceBindingsRoster, roster) || partyAliases != sourceBindingPartyAliases)
            {
                sourceBindings.Clear();
                sourceBindingsRoster = roster;
                sourceBindingPartyAliases = partyAliases;
            }
            sourceBindingsOriginalBoundary = graphics.NativeBindings;
        }
        if (!sourceBindings.TryGetValue(group, out BattleSourceBindings? bindings))
        {
            List<BattleSourceMember> added = [];
            if (loads is not null)
            {
                foreach (HostedModelLoadAdmission candidate in loads.Values)
                {
                    if (candidate.Source is { } source && source.Group == group &&
                        candidate.Load is HostedDispatchControl.LoadEnemy enemy && roster.Contains(enemy.Owner))
                    {
                        NativeBattleModelSlot? alias = source.OriginalEnemyOrdinal is int ordinal ? NativeBattleModelSlot.Decode(checked((byte)(NativeBattleSlots.AllyCount + ordinal))) : null;
                        added.Add(new(enemy.Owner, source.SceneSlot, checked((byte)(enemy.ModelId + 16)), alias));
                    }
                }
            }
            bindings = grouping.Bind(group, graphics.ActorModels, graphics.NativeBindings, added);
            sourceBindings.Add(group, bindings);
        }
        return bindings;
    }

    private BattleModelOwner ResolveSourceModelOperand(NativeBattleGraphicsState graphics, BattleModelOwner caller, byte operand)
    {
        return ReadSourceBindings(graphics, caller)?.DecodeLiteral(operand) ?? graphics.NativeBindings.DecodeLiteral(operand, graphics.ActorModels);
    }

    private BattleModelOwner ResolveSourceRelativeModel(NativeBattleGraphicsState graphics, BattleModelOwner caller, int offset)
    {
        if (ReadSourceBindings(graphics, caller) is { } bindings)
        {
            return bindings.DecodeRelative(caller, offset);
        }
        if (!graphics.NativeBindings.TryEncodeExactActor(caller, graphics.ActorModels, out byte operand))
        {
            throw new InvalidDataException("The action source has no original model operand for its relative reference.");
        }
        return graphics.NativeBindings.DecodeLiteral(checked((byte)(operand + offset)), graphics.ActorModels);
    }

    private NativeBattleModelAliases CaptureScriptAuthoredOperands(NativeBattleGraphicsState graphics, BattleModelOwner? caller)
    {
        NativeBattleModelAliases physical = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        return caller is not null && ReadSourceBindings(graphics, caller) is { } bindings ? bindings.ApplyAuthoredOperands(physical) : physical;
    }
}
