using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelRingTests
{
    [Fact]
    public void Opcode90MatchesUnloadedPhysicalEnemyIdsAndUnlinksTheWholeClosedRing()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        models = models.SetItem(3, models[3] with { ModelId4 = 17 });
        models = NativeBattleModelRings.Bind90(models, 0, 17);
        Assert.Equal((int?)3, models[0].ReadLinkedModelIndex140());
        Assert.Equal((ushort)0, models[3].Flags0);
        models = NativeBattleModelRings.Bind90(models, 3, 3);
        models = models.SetItem(3, models[3].WriteLinkedModelIndex140(0));
        models = JsonSerializer.Deserialize<ImmutableArray<NativeBattleGraphicsModel>>(JsonSerializer.Serialize(models));
        Assert.Equal(new int[] { 0, 3 }, NativeBattleModelRings.Walk(models, 0));
        models = NativeBattleModelRings.Bind90(models, 0, 0);
        Assert.Null(models[0].ReadLinkedModelIndex140());
        Assert.Null(models[3].ReadLinkedModelIndex140());
        Assert.Throws<InvalidDataException>(() => NativeBattleModelRings.Bind90(models, 0, 0));
    }

    [Fact]
    public void SharedRingCallbacksRetainPhysicalPointerAndDistinctOperandsAcrossJsonAndReleaseOnce()
    {
        NativeBattleAuxiliaryState state = NativeBattleAuxiliaryState.Clear();
        state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelSequence505C70, 3, sequence: 254).State;
        state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelStatus50A000, 6, 0xFFFFFFFF).State;
        state = state.AppendModelRing(NativeBattleAuxiliaryKind.ModelResourceFlags50B080, 0, 0x200).State;
        state = JsonSerializer.Deserialize<NativeBattleAuxiliaryState>(JsonSerializer.Serialize(state))!;
        List<(NativeBattleAuxiliaryKind Kind, int Model, uint Argument)> calls = [];
        NativeBattleAuxiliaryInputs inputs = new() { ApplyModelRing = (kind, model, argument) => calls.Add((kind, model, argument)) };
        state = state.Advance(inputs).State;
        state = state.Advance(inputs).State;
        Assert.Equal(new[] { (NativeBattleAuxiliaryKind.ModelSequence505C70, 3, 254u), (NativeBattleAuxiliaryKind.ModelStatus50A000, 6, uint.MaxValue), (NativeBattleAuxiliaryKind.ModelResourceFlags50B080, 0, 0x200u) }, calls);
        Assert.Null(state.Head);
        Assert.Null(state.Tail);
        Assert.Equal(0x831E0u, NativeBattleModelRings.PreservedStatusMask50A000(0x40));
        Assert.Equal(0x21E35F0u, NativeBattleModelRings.PreservedStatusMask50A000(0));
    }
}
