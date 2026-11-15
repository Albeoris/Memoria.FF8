namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleTypedDescriptorSubjectTests
{
    [Fact]
    public void Unaliased_model_binding_survives_an_omitted_operand_and_a_raw_write_replaces_it()
    {
        BattleModelOwner owner = new BattleEnemyActorModel(new("addedEnemy"));
        BattleModelRoster roster = new([owner]);
        NativeBattleModelAliases aliases = new(roster, []);
        (NativeBattleDescriptorState state, int slot) = NativeBattleDescriptorState.Clear().EnqueueModel(113, 128, 0x487670, owner, aliases, 17);
        NativeBattleDescriptorPacket packet = state.Packets[slot];
        Assert.Equal(owner, Assert.IsType<NativeBattleDescriptorModelSubject.ExplicitModel>(packet.ModelSubject).Owner);
        Assert.Throws<NotSupportedException>(() => packet.ScalarWord8);
        Assert.Equal((ushort)17, packet.Value);

        (state, int reused) = state.RemoveFirst().Enqueue(113, 128, 0x48AD10);
        Assert.Equal(slot, reused);
        packet = state.Packets[reused];
        Assert.Equal(owner, Assert.IsType<NativeBattleDescriptorModelSubject.ExplicitModel>(packet.ModelSubject).Owner);
        Assert.Throws<NotSupportedException>(() => packet.ScalarWord8);
        Assert.Equal((ushort)17, packet.Value);

        (state, reused) = state.RemoveFirst().Enqueue(113, 128, 0x487670, 6);
        packet = state.Packets[reused];
        Assert.Equal(NativeBattleModelSlot.Decode(6), Assert.IsType<NativeBattleDescriptorModelSubject.AuthoredOriginalSlot>(packet.ModelSubject).Slot);
        Assert.Equal((short)6, packet.ScalarWord8);
        Assert.Equal((ushort)17, packet.Value);
    }

    [Fact]
    public void Action_header_above_original_window_is_typed_without_a_fabricated_address()
    {
        BattleActionHeaderReference header = new(NativeBattleRetainedActionPackets.HeaderCapacity);
        (NativeBattleDescriptorState state, int slot) = NativeBattleDescriptorState.Clear().EnqueueAction(128, header);
        Assert.Equal(header, state.Packets[slot].Header);
        Assert.Throws<NotSupportedException>(() => state.Packets[slot].Argument);
        (state, int task) = state.AppendTask(NativeBattleDescriptorTaskKind.Action104, slot);
        Assert.Equal(header, state.Tasks[task].Header);
        state.Validate();

        (state, slot) = NativeBattleDescriptorState.Clear().EnqueueAction(128, new BattleActionHeaderReference(0));
        Assert.Equal(NativeBattleEffectParameterWindow.HeaderBase, state.Packets[slot].Argument);
    }
}
