using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleCommandBanksTests
{
    [Fact]
    public void Physical_admission_scans_only_first_actor_of_banks_one_and_two_and_retains_command_bytes()
    {
        NativeBattleCommandBanks state = NativeBattleCommandBanks.Initialize();
        (state, _) = state.EnqueueCallback(0, 0, 3);
        (state, NativeBattleCommandLocation ordinary) = state.Allocate(2);
        state = state.WriteCommand(ordinary, 0, 1, 2, 0, 0, 7, [8, 16, 32]);
        state = state.WriteCommand(ordinary, 1, 0, 4, 0, 0, 12, [4]);
        Assert.Equal(0, state.CountActor(0));
        Assert.Equal(1, state.CountActor(1));
        Assert.Equal((ushort)32, state.Banks[2].Packets[0].ReadWord(10));
        state.Validate();
        NativeBattleCommandBanks restored = JsonSerializer.Deserialize<NativeBattleCommandBanks>(JsonSerializer.Serialize(state))!;
        Assert.Equal(JsonSerializer.Serialize(state), JsonSerializer.Serialize(restored));
        state = state.Release(ordinary);
        Assert.Equal(0, state.CountActor(1));
        Assert.Equal((byte)255, state.Banks[2].Packets[0].ReadByte(0));
        Assert.Equal((byte)2, state.Banks[2].Packets[0].ReadByte(1));
        Assert.Equal((ushort)7, state.Banks[2].Packets[0].ReadWord(4));
        Assert.Equal((ushort)0, state.Banks[2].Packets[0].ReadWord(10));
        state.Validate();
    }

    [Fact]
    public void Three_priorities_and_linked_insertion_order_survive_removal_and_reuse()
    {
        NativeBattleCommandBanks state = NativeBattleCommandBanks.Initialize();
        (state, NativeBattleCommandLocation ordinary) = state.EnqueueCallback(2, 2, 7);
        (state, NativeBattleCommandLocation limit) = state.EnqueueCallback(1, 1, 8);
        (state, NativeBattleCommandLocation initial) = state.EnqueueCallback(0, 0, 0);
        Assert.Equal(initial, state.FirstEligible(_ => true));
        state = state.Release(initial);
        Assert.Null(state.FirstEligible(_ => true));
        Assert.Equal(limit, state.FirstEligible(_ => false));
        state = state.RemoveActor(1);
        Assert.Equal(ordinary, state.FirstEligible(_ => false));
        state.Validate();
        state = state.Release(ordinary);
        Assert.Null(state.FirstEligible(_ => false));
        Assert.Equal((byte)255, state.Banks[2].Tail);
        state.Validate();
    }

    [Fact]
    public void Eleven_entries_preserve_native_capacity_without_emulating_overwrite_of_slot_zero()
    {
        NativeBattleCommandBanks state = NativeBattleCommandBanks.Initialize();
        for (int index = 0; index < 11; index++)
        {
            (state, _) = state.EnqueueCallback(0, (byte)(index % 7), 0);
        }
        state.Validate();
        Assert.Throws<InvalidOperationException>(() => state.EnqueueCallback(0, 0, 0));
        Assert.Throws<NotSupportedException>(() => state.Banks[1].Packets[0].ReadByte(1));
        Assert.Throws<NotSupportedException>(() => state.Banks[0].Packets[0].ReadByte(3));
    }
}
