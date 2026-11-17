namespace Memoria.FF8.Runtime;

public sealed partial class BattlePresentationProcess
{
    private BattleModelRoster? sourceBindingsRoster;
    private NativeBattleModelBoundary? sourceBindingsOriginalBoundary;
    private readonly Dictionary<BattleSourceGroupId, BattleSourceBindings> sourceBindings = [];

    internal BattleSourceBindings? ReadSourceBindings(NativeBattleGraphicsState graphics, BattleModelOwner owner)
    {
        BattleSourceGrouping grouping = resources.SourceGrouping;
        if (grouping.FindGroup(owner) is not { } group)
        {
            return null;
        }
        if (!ReferenceEquals(sourceBindingsRoster, graphics.ActorModels.Roster) || !ReferenceEquals(sourceBindingsOriginalBoundary, graphics.NativeBindings))
        {
            sourceBindings.Clear();
            sourceBindingsRoster = graphics.ActorModels.Roster;
            sourceBindingsOriginalBoundary = graphics.NativeBindings;
        }
        if (!sourceBindings.TryGetValue(group, out BattleSourceBindings? bindings))
        {
            bindings = grouping.Bind(group, graphics.ActorModels, graphics.NativeBindings);
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
}
