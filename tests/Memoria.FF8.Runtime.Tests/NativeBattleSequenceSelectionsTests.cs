using static Memoria.FF8.Tests.NativeBattleTypedFixture;
using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceSelectionsTests
{
    [Theory]
    [InlineData(0, 2, 0, false)]
    [InlineData(6, 2, 6, false)]
    [InlineData(7, 5, 4, true)]
    [InlineData(255, 5, 4, true)]
    public void Opcode92_preserves_literal_alias_or_retained_target_arguments(byte selector, byte expectedSource, byte expectedTarget, bool nonActorSelectors)
    {
        var (models, boundary) = OriginalModels(Enumerable.Range(0, 7).Select(slot => (slot, new NativeBattleGraphicsModel(2))));
        BattleModelOwner current = OriginalOwner(5);
        NativeBattleActionGeometry retained = new(new BattleRetainedModelReference.OriginalSlot(NativeBattleModelSlot.Decode(2)), BattleRetainedModelSelection.From([new BattleRetainedModelReference.OriginalSlot(NativeBattleModelSlot.Decode(4))]), true);

        var arguments = NativeBattleSequenceSelections.GeometryArguments92(models, current, selector, retained, boundary);

        Assert.Equal(OriginalOwner(expectedSource), arguments.Source);
        Assert.Equal(OriginalOwner(expectedTarget), Assert.Single(arguments.Targets.Enumerate()));
        Assert.Equal(nonActorSelectors, arguments.SourceHasNonActorSelectors);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.GeometryArguments92(models, current, selector, null, boundary));
    }

    [Fact]
    public void Full_integer_directory_predicate_and_byte_header_store_keep_sequence0_directory_ownership()
    {
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(1, 0, 4, 0, 0xA1));
        NativeBattleGraphicsModel model = new(3) { ModelId4 = 0, Flags8 = 0, ResourceFlags44 = 0xAB01, Sequence = 1, SequenceState = new() };
        Assert.False(resource.ContainsNativeIndex505850(0x0201));
        NativeBattleGraphicsModel completed = NativeBattleSequenceSelections.SelectResource505BC0(model, resource, 0x0201);
        Assert.Equal((ushort)0xAB41, completed.ResourceFlags44);
        Assert.Equal(model.Sequence, completed.Sequence);
        Assert.True(resource.ContainsNativeIndex505850(0));
        NativeBattleGraphicsModel selectedZero = NativeBattleSequenceSelections.SelectResource505BC0(model, resource, 0);
        Assert.Equal((byte)0, selectedZero.Sequence);
        Assert.Equal(1, resource.SequenceStart(0));
        Assert.Equal((ushort)1, selectedZero.ResourceFlags44);
        NativeBattleSequenceStep zeroStep = NativeBattleSequenceMachine.Advance(OriginalOwner(0), selectedZero, resource with { AnimationFrames = [1] }, new(null, null));
        Assert.Equal((byte)0, zeroStep.Model.SequenceState!.Pose);
        Assert.Equal((ushort)1, zeroStep.Model.SequenceState.Offset46);
        Assert.Throws<NotSupportedException>(() => resource.ContainsNativeIndex505850(-1));
        byte[] wide = new byte[1029];
        BinaryPrimitives.WriteInt16LittleEndian(wide, 513);
        BinaryPrimitives.WriteUInt16LittleEndian(wide.AsSpan(1026), 1028);
        NativeBattleGraphicsResource wideResource = new(wide.ToImmutableArray());
        Assert.True(wideResource.ContainsNativeIndex505850(513));
        Assert.Equal((byte)1, NativeBattleSequenceSelections.SelectResource505BC0(model, wideResource, 513).Sequence);
        BinaryPrimitives.WriteUInt16LittleEndian(wide.AsSpan(512), 1028);
        wideResource = new(wide.ToImmutableArray());
        Assert.Equal((byte)0, NativeBattleSequenceSelections.SelectResource505BC0(model, wideResource, 256).Sequence);
        Assert.Equal(513, wideResource.SequenceStart(0));
    }

    [Fact]
    public void Model_selector93_matches_all_other_loaded_models_in_physical_order()
    {
        var (models, boundary) = OriginalModels([
            (0, new(2) { ModelId4 = 77 }),
            (3, new(2) { ModelId4 = 77 }),
            (4, new(0) { ModelId4 = 77 }),
            (5, new(2) { ModelId4 = 78 }),
            (6, new(2) { ModelId4 = 77 })
        ]);
        BattleModelOwner current = OriginalOwner(3);

        Assert.Equal(new[] { OriginalOwner(0), OriginalOwner(6) }, NativeBattleSequenceSelections.SelectModels93(models, current, 77, boundary).Enumerate());
        Assert.Equal(current, Assert.Single(NativeBattleSequenceSelections.SelectModels93(models, current, 3, boundary).Enumerate()));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.SelectModels93(models, current, 7, boundary));
    }

    [Theory]
    [InlineData(0, 0x100000, 0x40, false)]
    [InlineData(2, 0x200000, 0x140, false)]
    [InlineData(0, 0, 0x40, true)]
    [InlineData(3, 0x300000, 0x40, true)]
    [InlineData(3, 0, 0x100, true)]
    [InlineData(3, 0, 0x20, false)]
    public void Target_sequenceBF_checks_party_status_and_both_505CE0_resource_bits(int index, uint status, ushort resourceFlags, bool expected)
    {
        NativeBattleGraphicsModel model = new(2) { Flags8 = status, ResourceFlags44 = resourceFlags };
        BattleModelOwner owner = OriginalOwner(index);
        Assert.Equal(expected, NativeBattleSequenceSelections.CanSelectTargetBF(new BattleActorModels([new(owner, model)]), owner));
    }

    [Fact]
    public void BF_protected_party_short_circuits_unknown_resource_flags()
    {
        bool Select(int slot, NativeBattleGraphicsModel model)
        {
            BattleModelOwner owner = OriginalOwner(slot);
            return NativeBattleSequenceSelections.CanSelectTargetBF(new BattleActorModels([new(owner, model)]), owner);
        }

        Assert.False(Select(0, new(2) { Flags8 = 0x100000 }));
        Assert.Throws<NotSupportedException>(() => Select(0, new(2)));
        Assert.Throws<NotSupportedException>(() => Select(3, new(2)));
    }
}
