using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Memoria.FF8.Runtime.Tests;

internal static class NativeBattleHostedTestFixture
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    internal static BattlePresentationProcess SeedProcess(BattleSession logical, NativeBattleGraphicsState graphics, NativeBattleGraphicsDefinition? graphicsResources = null, BattleDefinition? definition = null)
    {
        // These fixtures enter at a typed packet or descriptor boundary after model binding.
        BattlePresentationProcess process = (BattlePresentationProcess)RuntimeHelpers.GetUninitializedObject(typeof(BattlePresentationProcess));
        BattleDefinition selected = definition ?? logical.ReadDefinition();
        BattleModelRoster roster = graphics.ActorModels.Roster;
        NativeBattleModelAliases aliases = graphics.NativeBindings.CaptureAliases(graphics.ActorModels);
        SetField(process, "resources", new BattlePresentationResources(selected, graphicsResources));
        SetField(process, "services", typeof(BattleSession).GetMethod("CreateBattlePresentationServices", PrivateInstance)!.Invoke(logical, []));
        SetField(process, "hostedEntryRoster", roster);
        SetField(process, "hostedEntryAliases", aliases);
        var entryDescriptors = typeof(BattlePresentationProcess).GetField("hostedEntryDescriptors", PrivateInstance)!;
        entryDescriptors.SetValue(process, Activator.CreateInstance(entryDescriptors.FieldType));
        SetField(process, "nativeActionPackets", NativeBattleActionPacketsState.Create(roster, NativeBattleRetainedActionPackets.Fresh(), aliases));
        SetField(process, "nativeCrtRandom", selected.NativeCrtRandom);
        SetField(process, "nativeGlobalRandom", selected.NativeGlobalRandom);
        SetField(process, "nativeBattleUiRandom", selected.NativeBattleUiRandom);
        SetField(process, "presentationContacts", new List<NativeBattleContactPacket>());
        SetField(process, "presentationSounds", new List<NativeBattleModuleSound>());
        SetField(process, "presentationSequences", new List<NativeBattleForeignSequenceRequest>());
        SetField(process, "presentationActivations", new List<NativeBattleEffectActivation>());
        SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics);
        return process;
    }

    internal static void BindProcess(BattleSession logical, NativeBattleGraphicsState graphics, NativeBattleGraphicsDefinition? graphicsResources = null)
    {
        BattlePresentationProcess process = SeedProcess(logical, graphics, graphicsResources);
        typeof(BattleSession).GetField("expandedPresentationProcess", PrivateInstance)!.SetValue(logical, process);
    }

    internal static void SetField(BattlePresentationProcess process, string name, object? value)
    {
        typeof(BattlePresentationProcess).GetField(name, PrivateInstance)!.SetValue(process, value);
    }

    internal static void SetProperty(BattlePresentationProcess process, string name, object? value)
    {
        typeof(BattlePresentationProcess).GetProperty(name)!.SetValue(process, value);
    }

    internal static NativeBattleModelLifecycleServices ReadModelServices(BattlePresentationProcess process, BattleModelRoster roster)
    {
        BattlePresentationServices services = (BattlePresentationServices)typeof(BattlePresentationProcess).GetField("services", PrivateInstance)!.GetValue(process)!;
        return services.CaptureModels(roster, null);
    }

    internal static NativeBattleGraphicsDefinition CreateGraphicsDefinition(params NativeBattleGraphicsBinding[] bindings)
    {
        NativeBattleGraphicsRules rules = new(NativeFieldRuntimeProfile.FF8EnExecutableHash, [.. Enumerable.Repeat((sbyte)-1, 16)], [.. Enumerable.Repeat((sbyte)-1, 32)]);
        return new(rules, bindings.ToImmutableArray(), []);
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
