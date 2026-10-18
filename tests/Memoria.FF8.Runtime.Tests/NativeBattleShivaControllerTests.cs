using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleShivaControllerTests
{
    [Fact]
    public void Root_publishes_the_draw_bank_before_each_actual_child_pass_and_wraps_its_own_word_counter()
    {
        NativeBattleShivaControllerFrame frame = Fixture(ushort.MaxValue);
        int calls = 0;
        for (int pass = 0; pass < 3; pass++)
        {
            int expectedDraw = pass % 2 == 0 ? 0x10000 : 0x18000;
            ushort expectedCounter = unchecked((ushort)(ushort.MaxValue + pass));
            frame = NativeBattleShivaController.Advance5C7F50(frame, (current, address) =>
            {
                calls++;
                Assert.Equal(0x22BC1A8u, address);
                Assert.Equal(expectedDraw, current.CurrentDrawOffset);
                Assert.Equal(expectedCounter, Root(current).ReadWord(0x0C));
                Assert.Equal(0x5C0F30u, current.Entry.Effect.Pools[1].Slots[0].Callback);
                NativeBattleShivaEntryState state = current.Entry.State;
                return new(current with { Entry = current.Entry with { State = state with { Counter22BD010 = state.Counter22BD010 + 1 } } }, 0);
            });
            Assert.Equal(unchecked((ushort)(expectedCounter + 1)), Root(frame).ReadWord(0x0C));
            Assert.Equal((ushort)0xABCD, Root(frame).ReadWord(0x0E));
            Assert.Equal(0x22BC180u, frame.Entry.Effect.ReturnedDescriptor);
            Assert.Equal(expectedDraw, frame.CurrentDrawOffset);
            Assert.Equal(123, frame.Entry.Memory.Cursor);
        }
        Assert.Equal(3, calls);
        Assert.Equal(34, frame.Entry.State.Counter22BD010);
    }

    [Fact]
    public void Child_callback_result_owns_retirement_and_the_root_finishes_only_when_the_bank_is_empty()
    {
        NativeBattleShivaControllerFrame frame = Fixture();
        foreach (int result in new[] { 0, 1 })
        {
            frame = NativeBattleShivaController.Advance5C7F50(frame, (current, _) => new(current, result));
            Assert.Equal(0x22BC180u, frame.Entry.Effect.ReturnedDescriptor);
            Assert.Equal(0, frame.Entry.Effect.Pools[0].Head);
            Assert.Equal(0, frame.Entry.Effect.Pools[1].Head);
        }
        frame = NativeBattleShivaController.Advance5C7F50(frame, (current, _) => new(current, 2));
        Assert.Equal(0u, frame.Entry.Effect.ReturnedDescriptor);
        Assert.Null(frame.Entry.Effect.Pools[0].Head);
        Assert.Null(frame.Entry.Effect.Pools[0].Tail);
        Assert.Null(frame.Entry.Effect.Pools[1].Head);
        Assert.Equal((ushort)0, Root(frame).Flags);
        Assert.Equal((ushort)0, frame.Entry.Effect.Pools[1].Slots[0].Flags);
        Assert.Equal((ushort)3, Root(frame).ReadWord(0x0C));
        Assert.Same(frame, NativeBattleShivaController.Advance5C7F50(frame, NoChild));
    }

    [Fact]
    public void Child_appended_during_the_callback_runs_in_the_same_pass_and_keeps_the_root_alive()
    {
        NativeBattleShivaControllerFrame frame = Fixture();
        List<uint> visits = [];
        frame = NativeBattleShivaController.Advance5C7F50(frame, (current, address) =>
        {
            visits.Add(address);
            if (address == 0x22BC1A8)
            {
                NativeBattleEffectTaskPool children = current.Entry.Effect.Pools[1];
                (children, _) = children.Append(0x5C0F30);
                current = current with { Entry = current.Entry with { Effect = current.Entry.Effect with { Pools = current.Entry.Effect.Pools.SetItem(1, children) } } };
                return new(current, 2);
            }
            return new(current, 0);
        });
        Assert.Equal(new uint[] { 0x22BC1A8, 0x22BC1CC }, visits);
        Assert.Equal(1, frame.Entry.Effect.Pools[1].Head);
        Assert.Equal(1, frame.Entry.Effect.Pools[1].Tail);
        Assert.Equal(0x22BC180u, frame.Entry.Effect.ReturnedDescriptor);
        Assert.Equal((ushort)1, Root(frame).ReadWord(0x0C));
        frame = NativeBattleShivaController.Advance5C7F50(frame, (current, address) =>
        {
            Assert.Equal(0x22BC1CCu, address);
            Assert.Equal(0x10000, current.CurrentDrawOffset);
            return new(current, 2);
        });
        Assert.Equal(0u, frame.Entry.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Empty_child_bank_retires_the_root_after_publishing_and_incrementing_without_calling_a_child()
    {
        NativeBattleShivaControllerFrame frame = Fixture(6, false);
        frame = NativeBattleShivaController.Advance5C7F50(frame, NoChild);
        Assert.Equal(0x18000, frame.CurrentDrawOffset);
        Assert.Equal((ushort)7, Root(frame).ReadWord(0x0C));
        Assert.Equal(0u, frame.Entry.Effect.ReturnedDescriptor);
        Assert.Null(frame.Entry.Effect.Pools[0].Head);
    }

    [Fact]
    public void Independent_snapshot_restores_keep_next_pass_parity_links_and_caller_owned_state()
    {
        NativeBattleShivaControllerFrame frame = NativeBattleShivaController.Advance5C7F50(Fixture(), (current, _) => new(current, 0));
        NativeBattleShivaControllerFrame restored = JsonSerializer.Deserialize<NativeBattleShivaControllerFrame>(JsonSerializer.Serialize(frame))!;
        for (int pass = 0; pass < 3; pass++)
        {
            frame = NativeBattleShivaController.Advance5C7F50(frame, (current, _) => new(current, 0));
            restored = NativeBattleShivaController.Advance5C7F50(restored, (current, _) => new(current, 0));
            Assert.Equal(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(restored));
            Assert.Equal(pass % 2 == 0 ? 0x10000 : 0x18000, frame.CurrentDrawOffset);
        }
    }

    [Fact]
    public void Scheduler_rejects_replaced_physical_banks_unknown_root_callback_and_invalid_draw_publications()
    {
        NativeBattleShivaControllerFrame frame = Fixture();
        NativeBattleEffectTaskPool main = frame.Entry.Effect.Pools[0];
        NativeBattleEffectTaskPool children = frame.Entry.Effect.Pools[1];
        NativeBattleShivaControllerFrame replaced = frame with { Entry = frame.Entry with { Effect = frame.Entry.Effect with { Pools = [main, children with { BankAddress = 0x22BC1AC }] } } };
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaController.Advance5C7F50(replaced, NoChild));
        replaced = frame with { Entry = frame.Entry with { Effect = frame.Entry.Effect with { Pools = [main with { Slots = main.Slots.SetItem(0, main.Slots[0] with { Callback = 0x5C0F30 }) }, children] } } };
        Assert.Throws<NotSupportedException>(() => NativeBattleShivaController.Advance5C7F50(replaced, NoChild));
        Assert.Throws<InvalidDataException>(() => NativeBattleShivaController.Advance5C7F50(frame with { CurrentDrawOffset = 123 }, NoChild));
        Assert.Throws<ArgumentNullException>(() => NativeBattleShivaController.Advance5C7F50(frame, null!));
    }

    private static NativeBattleModulePoolTask Root(NativeBattleShivaControllerFrame frame)
    {
        return frame.Entry.Effect.Pools[0].Slots[0];
    }

    private static NativeBattleShivaChildStep NoChild(NativeBattleShivaControllerFrame frame, uint address)
    {
        throw new InvalidOperationException("The child bank is empty.");
    }

    private static NativeBattleShivaControllerFrame Fixture(ushort counter = 0, bool withChild = true)
    {
        NativeBattleEffectTaskPool main = NativeBattleEffectTaskPool.Initialize(0x22BC180, 0x22BC160, 0x10, 2);
        (main, int root) = main.Append(0x5C7F50);
        main = main with { Slots = main.Slots.SetItem(root, main.Slots[root].WriteWord(0x0C, counter).WriteWord(0x0E, 0xABCD)) };
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x22BC198, 0x22BC1A8, 0x24, 100);
        if (withChild)
        {
            (children, _) = children.Append(0x5C0F30);
        }
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess() with { Cursor = 123 };
        NativeBattleOriginalEffectState effect = new(185, 184, new(185, 0x5C0D50, 0x5C0D30), new(0, 0, 8, 0, 0, 0), 0x22BC180) { Pools = [main, children] };
        NativeBattleShivaEntryState state = new(0, 3, new(1, 2, 3), new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 0, 4096), 31);
        return new(new(memory, effect, state), null);
    }
}
