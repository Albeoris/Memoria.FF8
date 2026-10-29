using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleSigh46Controller;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSigh46Smooth60Tests
{
    [Fact]
    public void Six_authored_angle_material_copies_keep_stable_distinct_task_identities_without_serialized_metadata()
    {
        var fixture = NativeBattleSigh46LowerTests.Fixture(3, 0x8359C0, 2);
        NativeBattleSigh46Frame frame = Write(fixture.Frame, fixture.Address, Read(fixture.Frame, fixture.Address).WriteWord(0x24, 5));
        List<NativeBattleSigh46Operation> operations = [];
        var inputs = NativeBattleSigh46LowerTests.Inputs(operations);
        var first = AdvanceCustom8359C0(frame, fixture.Address, fixture.Resources, inputs);
        NativeBattleSigh46DrawMesh88[] draws = operations.OfType<NativeBattleSigh46DrawMesh88>().ToArray();
        Assert.Equal(6, draws.Length);
        NativeBattlePilotVisualIdentity[] identities = draws.Select(draw => Assert.IsType<NativeBattlePilotVisualIdentity>(draw.PilotIdentity)).ToArray();
        Assert.Equal(Enumerable.Range(0, 6), identities.Select(identity => identity.Primitive));
        Assert.Equal(6, identities.Distinct().Count());
        Assert.Single(identities.Select(identity => (identity.Bank, identity.Slot, identity.Generation)).Distinct());
        for (int index = 0; index < draws.Length; index++)
        {
            Assert.Equal((long)draws[index].SourceAddress, identities[index].ResourceGeneration);
            Assert.DoesNotContain("PilotIdentity", JsonSerializer.Serialize(draws[index]));
        }
        operations.Clear();
        var second = AdvanceCustom8359C0(first.Frame, fixture.Address, fixture.Resources, inputs);
        Assert.Equal(identities, operations.OfType<NativeBattleSigh46DrawMesh88>().Select(draw => draw.PilotIdentity!.Value));
        Assert.Equal(frame.Random, second.Frame.Random);
        Assert.Equal((ushort)7, Read(second.Frame, fixture.Address).ReadWord(0x24));
    }

    [Fact]
    public void Smoke_uses_actual_particle_task_generation_and_selected_resource_without_changing_animation()
    {
        var fixture = NativeBattleSigh46LowerTests.Fixture(4, 0x836090, 1);
        NativeBattleModulePoolTask task = Read(fixture.Frame, fixture.Address).WriteDword(0x4C, 0x15B82A4).WriteWord(0x52, 14).WriteWord(0x20, 2000);
        NativeBattleSigh46Frame frame = Write(fixture.Frame, fixture.Address, task);
        List<NativeBattleSigh46Operation> operations = [];
        var result = AdvanceSprite836090(frame, fixture.Address, fixture.Resources, NativeBattleSigh46LowerTests.Inputs(operations));
        NativeBattleSigh46DrawSprite180 draw = Assert.Single(operations.OfType<NativeBattleSigh46DrawSprite180>());
        NativeBattlePilotVisualIdentity identity = Assert.IsType<NativeBattlePilotVisualIdentity>(draw.PilotIdentity);
        Assert.Equal(NativeBattlePilotVisualLifetimes.Identity(46, task) with { ResourceGeneration = 0x15B82A4 }, identity);
        Assert.Equal((ushort)1, Read(result.Frame, fixture.Address).ReadWord(0x50));
        Assert.Equal(frame.Random, result.Frame.Random);
    }
}
