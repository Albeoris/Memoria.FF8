using System.Buffers.Binary;
using System.Collections.Immutable;
using Memoria.FF8.Runtime;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleSequenceSelectionsTests
{
    [Theory]
    [InlineData(0, 2, 1)]
    [InlineData(6, 2, 64)]
    [InlineData(7, 5, 0x4010)]
    [InlineData(255, 5, 0x4010)]
    public void Opcode92_preserves_native_actor_and_mask_argument_ownership(byte selector, byte expectedActor, ushort expectedMask)
    {
        var arguments = NativeBattleSequenceSelections.GeometryArguments92(5, selector, new(2, 0x4010));
        Assert.Equal(expectedActor, arguments.Actor);
        Assert.Equal(expectedMask, arguments.TargetMask);
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.GeometryArguments92(5, selector, null));
    }

    [Fact]
    public void Full_integer_directory_predicate_and_byte_header_store_keep_sequence0_directory_ownership()
    {
        NativeBattleGraphicsResource resource = new(ImmutableArray.Create<byte>(1, 0, 4, 0, 0xA1));
        NativeBattleGraphicsModel model = new(0, 3) { ModelId4 = 0, Flags8 = 0, ResourceFlags44 = 0xAB01, Sequence = 1, SequenceState = new() };
        Assert.False(resource.ContainsNativeIndex505850(0x0201));
        NativeBattleGraphicsModel completed = NativeBattleSequenceSelections.SelectResource505BC0(model, resource, 0x0201);
        Assert.Equal((ushort)0xAB41, completed.ResourceFlags44);
        Assert.Equal(model.Sequence, completed.Sequence);
        Assert.True(resource.ContainsNativeIndex505850(0));
        NativeBattleGraphicsModel selectedZero = NativeBattleSequenceSelections.SelectResource505BC0(model, resource, 0);
        Assert.Equal((byte)0, selectedZero.Sequence);
        Assert.Equal(1, resource.SequenceStart(0));
        Assert.Equal((ushort)1, selectedZero.ResourceFlags44);
        NativeBattleSequenceStep zeroStep = NativeBattleSequenceMachine.Advance(selectedZero, resource with { AnimationFrames = [1] }, new(null, null));
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
        NativeBattleGraphicsModel[] models = NativeBattleGraphicsModels.ClearBank();
        models[0] = new(0, 2) { ModelId4 = 77 };
        models[3] = new(3, 2) { ModelId4 = 77 };
        models[4] = new(4, 0) { ModelId4 = 77 };
        models[5] = new(5, 2) { ModelId4 = 78 };
        models[6] = new(6, 2) { ModelId4 = 77 };
        Assert.Equal(new[] { 0, 6 }, NativeBattleSequenceSelections.SelectModels93(models, 3, 77));
        Assert.Equal(new[] { 3 }, NativeBattleSequenceSelections.SelectModels93(models, 3, 3));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.SelectModels93(models, 3, 7));
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
        NativeBattleGraphicsModel model = new(index, 2) { Flags8 = status, ResourceFlags44 = resourceFlags };
        Assert.Equal(expected, NativeBattleSequenceSelections.CanSelectTargetBF(model));
    }

    [Fact]
    public void BF_protected_party_short_circuits_unknown_resource_flags()
    {
        Assert.False(NativeBattleSequenceSelections.CanSelectTargetBF(new(0, 2) { Flags8 = 0x100000 }));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.CanSelectTargetBF(new(0, 2)));
        Assert.Throws<NotSupportedException>(() => NativeBattleSequenceSelections.CanSelectTargetBF(new(3, 2)));
    }
}
