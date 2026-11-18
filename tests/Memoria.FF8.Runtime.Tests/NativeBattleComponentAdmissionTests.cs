using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleComponentAdmissionTests
{
    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void Casting_component_admits_only_the_loaded_preentry_descriptor_phases(byte phase)
    {
        BattlePresentationProcess process = CastingComponent(phase);
        BattleDiagnosticComponentAdmission admission = process.ReadDiagnosticComponentAdmission(219);
        Assert.True(admission.ModuleLoaded);
        Assert.Equal((byte)8, admission.Command);
        Assert.Equal((ushort)311, admission.Action);
        Assert.Equal((byte)3, admission.OriginalActorIndex);
    }

    [Theory]
    [InlineData("entered")]
    [InlineData("completed")]
    [InlineData("owner")]
    [InlineData("header")]
    [InlineData("effect")]
    [InlineData("pool")]
    public void Casting_component_rejects_entered_or_mismatched_native_owners(string fault)
    {
        BattlePresentationProcess process = CastingComponent(fault is "entered" or "completed" ? (byte)6 : (byte)3);
        NativeBattleGraphicsState graphics = process.Graphics;
        NativeBattleOriginalEffectState effect = graphics.OriginalEffect!;
        effect = fault switch
        {
            "completed" => effect with { ReturnedDescriptor = 0 },
            "owner" => effect with { Parameters = effect.Parameters! with { Source = process.FindOriginalActor(NativeBattleModelSlot.Decode(0))! } },
            "header" => effect with { Parameters = effect.Parameters! with { Header = new(1) } },
            "effect" => effect with { RequestedEffect = 218 },
            "pool" => effect with { Pools = [NativeBattleEffectTaskPool.Initialize(NativeBattleEffect101Profile.RootDescriptor, NativeBattleEffect101Profile.RootBank, 20, 1)] },
            _ => effect
        };
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics with { OriginalEffect = effect });
        Assert.Throws<InvalidDataException>(() => process.ReadDiagnosticComponentAdmission(219));
    }

    private static BattlePresentationProcess CastingComponent(byte phase)
    {
        BattleSession battle = new(new BattleDefinition { Combatants = [new() { Id = "party", Name = "Party", IsPlayer = true }, new() { Id = "enemy", Name = "Enemy" }] }, BattleAtbMode.Normal);
        var (roster, aliases) = battle.ReadBattlePresentationPacketBindings();
        NativeBattleGraphicsState graphics = NativeBattleHostedTestFixture.Graphics(roster, aliases, owner => NativeBattleGraphicsModels.ClearUnloadedModel(null, null, null, null, null) with { ModelId4 = owner is BattlePartyActorModel ? (byte)1 : (byte)16 }) with { EntryReady = true, EntryLifecycle = 4 };
        NativeBattleGraphicsDefinition resources = NativeBattleHostedTestFixture.CreateGraphicsDefinition(new NativeBattleGraphicsBinding("party", 1, new([0, 0])), new NativeBattleGraphicsBinding("enemy", 16, new([0, 0])));
        BattlePresentationProcess process = NativeBattleHostedTestFixture.SeedProcess(battle, graphics, resources);
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeEffectMemory), NativeBattleEffectMemory.FromColdProcess() with { Textures = NativeBattleEffectTextureState.FromColdProcess() });
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeModelCpuResources), NativeBattleModelCpuResources.FromColdProcess().WithPresentationRoster(roster, aliases));
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeModelTextureCache), NativeBattleModelTextureCache.FromColdProcess().WithPresentationRoster(roster));
        BattleModelOwner actor = process.FindOriginalActor(NativeBattleModelSlot.Decode(3))!;
        BattleModelOwner target = process.FindOriginalActor(NativeBattleModelSlot.Decode(0))!;
        NativeBattleActionHeaderPreparation header = new(actor, 8, 3, -1, 311, 219, null);
        process.BeginHostedAction(header, [new(new(target, 0, 1, 0, 0, 0, 0), null)]);
        NativeBattleDescriptorState descriptors = process.Graphics.Descriptors;
        int packet = Assert.Single(descriptors.Queue);
        var (tasks, slot) = descriptors.AppendTask(NativeBattleDescriptorTaskKind.Action104, packet);
        NativeBattleDescriptorTask task = tasks.Tasks[slot];
        tasks = tasks with { Tasks = tasks.Tasks.SetItem(slot, task with { Payload = task.Payload.SetItem(1, phase) }) };
        NativeBattleEffectParameters parameters = new(actor, 1, BattleModelTargets.From([target]), new(0), 0, 0);
        NativeBattleOriginalEffectState effect = new(219, 218, new(219, 1, 1), parameters, null) { Pools = [] };
        graphics = process.Graphics with { Descriptors = tasks, OriginalEffect = effect, ActionGraphics = NativeBattleActionGraphics.Normalize(process.ActionPackets, new(0)) };
        NativeBattleHostedTestFixture.SetProperty(process, nameof(BattlePresentationProcess.NativeGraphics), graphics);
        return process;
    }
}
