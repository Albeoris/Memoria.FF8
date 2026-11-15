using System.Collections.Immutable;
using System.Reflection;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleHostedTestFixture
{
    internal static void InstallModelBindings(BattleSession session, params NativeBattleGraphicsBinding[] bindings)
    {
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [.. Enumerable.Repeat((sbyte)-1, 16)], [.. Enumerable.Repeat((sbyte)-1, 32)]);
        var definition = session.ReadDefinition() with { NativeGraphics = new(rules, bindings.ToImmutableArray(), []) };
        typeof(BattleSession).GetField("definition", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, definition);
    }

    internal static NativeBattleGraphicsState Graphics(BattleModelRoster roster, NativeBattleModelAliases aliases, Func<BattleModelOwner, NativeBattleGraphicsModel> modelFactory, BattleContactTaskStorageContract storage = BattleContactTaskStorageContract.ExpandableArena)
    {
        BattleActorModels models = new([]);
        NativeBattleModelBoundary boundary = NativeBattleModelBoundary.FromRetainedMemory(null);
        foreach (BattleModelOwner owner in roster.Owners)
        {
            NativeBattleGraphicsModel model = modelFactory(owner);
            if (aliases.TryEncodeExact(owner, out byte operand))
            {
                (boundary, models) = boundary.BindOriginalSlot(NativeBattleModelSlot.Decode(operand), owner, models);
                models = models.Set(owner, model);
            }
            else
            {
                models = models.Add(owner, model);
            }
        }
        return new(models, boundary, 0, 0, NativeBattleEffectsState.Clear(storage));
    }
}
