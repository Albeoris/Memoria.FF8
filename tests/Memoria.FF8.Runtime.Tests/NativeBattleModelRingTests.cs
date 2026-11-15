using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleModelRingTests
{
    [Fact]
    public void World_matrix_uses_the_closed_ring_owner_without_replacing_independent_animation_or_transform_state()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        NativeBattleActionMatrix ownerMatrix = new([0, 0, -4096, 0, 2048, 0, 4096, 0, 0], 123456, -654321, 4200, 4096);
        NativeBattleActionMatrix separateMatrix = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], -400, 200, -3600, 4096);
        models = models.SetItem(3, (models[3] with { GeometryWorldMatrix = ownerMatrix }).WriteLinkedModelIndex140(4));
        NativeBattleGraphicsModel sword = (models[4] with { GeometryWorldMatrix = separateMatrix, Sequence = 32, SequenceState = new() { Pose = 22, PoseFrame114 = 3, PoseLimit115 = 9 }, Transform = new(new(0, 0, -3600), new(0, 0, -3600), 0, 0, 4096) }).WriteLinkedModelIndex140(3);
        models = models.SetItem(4, sword);
        NativeBattleGraphicsModel result = NativeBattleModelRings.BindWorldMatrix502AB0(models, sword);
        Assert.Equal(sword with { GeometryWorldMatrix = ownerMatrix }, result);
        Assert.Equal(3, NativeBattleScanModelSelection.Minimum501FF0(models, 4));
        Assert.Equal(NativeBattleScanModelSelection.ModelBase + 3u * NativeBattleScanModelSelection.ModelStride, NativeBattleScanModelSelection.Minimum501FF0(NativeBattleScanPhysicalModels.Capture(models), NativeBattleScanModelSelection.ModelBase + 4u * NativeBattleScanModelSelection.ModelStride));
        ImmutableArray<NativeBattleGraphicsModel> restored = JsonSerializer.Deserialize<ImmutableArray<NativeBattleGraphicsModel>>(JsonSerializer.Serialize(models));
        NativeBattleGraphicsModel restoredSword = NativeBattleModelRings.BindWorldMatrix502AB0(restored, restored[4]);
        Assert.Equal(JsonSerializer.Serialize(result), JsonSerializer.Serialize(restoredSword));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void World_matrix_keeps_the_sources_own_matrix_for_null_links_and_open_chains(bool openChain)
    {
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        NativeBattleGraphicsModel source = models[4].WriteLinkedModelIndex140(openChain ? 3 : null);
        models = models.SetItem(3, models[3].WriteLinkedModelIndex140(null)).SetItem(4, source);
        Assert.Same(source, NativeBattleModelRings.BindWorldMatrix502AB0(models, source));
        Assert.Equal(4, NativeBattleScanModelSelection.Minimum501FF0(models, 4));
    }

    [Fact]
    public void World_matrix_requires_the_ring_owners_real_publication()
    {
        ImmutableArray<NativeBattleGraphicsModel> models = NativeBattleGraphicsModels.ClearBank().ToImmutableArray();
        models = models.SetItem(3, models[3].WriteLinkedModelIndex140(4)).SetItem(4, models[4].WriteLinkedModelIndex140(3));
        Assert.Throws<NotSupportedException>(() => NativeBattleModelRings.BindWorldMatrix502AB0(models, models[4]));
    }

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
