using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWaterCallbackTests
{
    private const uint Manager = 0x2797690;
    private static readonly NativeBattleWater222Resources Resources = CreateResources();

    [Theory]
    [InlineData(0xFFFF, 0, 0)]
    [InlineData(0, 1, 1)]
    public void Paused_callback_still_draws_consumes_one_CRT_and_publishes_current_view_and_parity(ushort tick, ushort expectedTick, byte parity)
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = WaterSchedulerFixture.CoreWord(frame, Manager + 0x32, tick);
        frame = frame with { Core = frame.Core.Write(0x2797554, [0, 0xFF]) };
        NativeCrtRandomState expectedRandom = frame.Random.Draw(out _);
        int draws = 0;
        short? focal = null;
        NativeBattleWaterCallbackServices services = new(UnexpectedRead, UnexpectedInstruction, UnexpectedUpdate, (current, kind) =>
        {
            draws++;
            Assert.Equal((byte)0, kind);
            Assert.Equal((byte)0xFF, current.Core.Read(Manager + 0x35, 1)[0]);
            Assert.Equal(parity, current.ReadTemporaryByte(0x141));
            Assert.Equal(parity, current.ReadTemporaryByte(0x140));
            return current;
        }, UnexpectedClear, (current, value) =>
        {
            focal = value;
            return current;
        });
        NativeBattleWaterCallbackResult result = NativeBattleWaterCallback.RunA9B870(frame, Input(1, -123), services);
        Assert.Equal(1, draws);
        Assert.Equal(expectedRandom, result.Frame.Random);
        Assert.Equal(expectedTick, Word(result.Frame, Manager + 0x32));
        Assert.Equal((short?)-123, focal);
        Assert.Equal(0x300000u, result.CommonCursor);
        Assert.Equal(0u, result.TaskResult);
        byte[] view = Input(1, -123).ViewMatrix.ToArray();
        view[18] = 0;
        view[19] = 0;
        Assert.Equal(view, result.Frame.ModuleViewBackup);
        Assert.Equal(view, result.Frame.Core.Read(0x27979E8, 32));
        Assert.Equal((byte)0, result.Frame.Core.Read(Manager + 0xD2, 1)[0]);
        Assert.Equal(0x100000u + (uint)parity * 0x1000, WaterSchedulerFixture.ReadCoreDword(result.Frame, Manager + 0x88));
        Assert.Equal(0x600000u + 0x44, result.Frame.ReadTemporaryDword(0x138));
        Assert.Equal((ushort)0, Word(result.Frame, 0x27975D8 + 4));
        Assert.Equal((ushort)0, result.Frame.ReadTemporaryWord(0x146));
    }

    [Fact]
    public void Unpaused_callback_orders_pre_script_physics_post_script_draw_and_focal_restore()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0, 0x80001000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 4, 0x1000);
        frame = frame with { Core = frame.Core.Write(0x2797554, [0, 0xFF]) };
        List<string> calls = [];
        NativeBattleWaterCallbackServices services = new((current, pc) => 9, (current, _) =>
        {
            calls.Add(unchecked((int)WaterSchedulerFixture.ReadCoreDword(current, 0x2797450)) < 0 ? "pre" : "post");
            current = WaterSchedulerFixture.CoreDword(current, 0x2797450, 0);
            return current.WriteTemporaryWord(0x13E, 3);
        }, (current, _) =>
        {
            calls.Add("physics");
            return current;
        }, (current, _) =>
        {
            calls.Add("draw");
            return current;
        }, UnexpectedClear, (current, _) =>
        {
            calls.Add("focal");
            return current;
        });
        NativeBattleWaterCallback.RunA9B870(frame, Input(), services);
        Assert.Equal(new[] { "pre", "physics", "post", "draw", "focal" }, calls);
    }

    [Fact]
    public void Context_and_debug_branches_preserve_exact_WORD_and_DWORD_wrap_and_clear_only_selected_OT()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = WaterSchedulerFixture.CoreWord(frame, Manager, 0x8400);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x40, 0x9005);
        frame = frame with { Core = frame.Core.Write(Manager + 0x4C, [0x50]) };
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797782, 0xFFF0);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797780, 0x10);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797784, 0x10);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797770, 0x80);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797774, 0);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2797778, 0x10);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x27975D8 + 0x2C, 0xDEAD0001);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x27975D8 + 0x24, 0xABCD);
        int clears = 0;
        NativeBattleWaterCallbackServices services = Services() with
        {
            ClearOrderingTable = (current, pointer, count) =>
            {
                clears++;
                Assert.Equal(0x600044u, pointer);
                Assert.Equal(0x1000, count);
                return current;
            }
        };
        NativeBattleWaterExecutionFrame skipped = NativeBattleStorageSnapshots.Restore(NativeBattleStorageSnapshots.Capture(frame));
        skipped = WaterSchedulerFixture.CoreWord(skipped, Manager, 0);
        NativeBattleWaterCallbackResult result = NativeBattleWaterCallback.RunA9B870(frame, Input(1), services);
        Assert.Equal(1, clears);
        Assert.Equal(0x60000Cu, WaterSchedulerFixture.ReadCoreDword(result.Frame, 0x27975D8 + 0x2C));
        Assert.Equal((ushort)0, Word(result.Frame, 0x27975D8 + 0x24));
        Assert.Equal((ushort)0x10, Word(result.Frame, 0x2797782));
        Assert.Equal((ushort)0xFFF0, Word(result.Frame, 0x2797780));
        Assert.Equal((ushort)0xFFF0, Word(result.Frame, 0x2797784));
        Assert.Equal(0xFFFFFF80u, WaterSchedulerFixture.ReadCoreDword(result.Frame, 0x2797770));
        Assert.Equal(0xFFFFFF00u, WaterSchedulerFixture.ReadCoreDword(result.Frame, 0x2797774));
        Assert.Equal(0xFFFFFEE0u, WaterSchedulerFixture.ReadCoreDword(result.Frame, 0x2797778));
        NativeBattleWaterCallbackResult withoutBranch = NativeBattleWaterCallback.RunA9B870(skipped, Input(1), Services());
        Assert.Equal(0x80u, WaterSchedulerFixture.ReadCoreDword(withoutBranch.Frame, 0x2797770));
        Assert.Equal((ushort)0xFFF0, Word(withoutBranch.Frame, 0x2797782));
        Assert.Equal(0xDEAD0001u, WaterSchedulerFixture.ReadCoreDword(withoutBranch.Frame, 0x27975D8 + 0x2C));
        Assert.Equal((ushort)0xABCD, Word(withoutBranch.Frame, 0x27975D8 + 0x24));
    }

    [Fact]
    public void OT_clear_can_clear_current_debug_guard_before_the_separate_fresh_flag_check()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.CoreWord(Create(), Manager, 0x8000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x40, 0x8000);
        frame = frame with { Core = frame.Core.Write(Manager + 0x4C, [0x10]) };
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797782, 0xFFF0);
        int clears = 0;
        NativeBattleWaterCallbackServices services = Services() with
        {
            ClearOrderingTable = (current, pointer, count) =>
            {
                clears++;
                Assert.Equal(0x600044u, pointer);
                Assert.Equal(0x1000, count);
                return WaterSchedulerFixture.CoreWord(current, Manager, 0);
            }
        };
        NativeBattleWaterCallbackResult result = NativeBattleWaterCallback.RunA9B870(frame, Input(1), services);
        Assert.Equal(1, clears);
        Assert.Equal((ushort)0, Word(result.Frame, Manager));
        Assert.Equal((ushort)0xFFF0, Word(result.Frame, 0x2797782));
    }

    [Fact]
    public void Physics_integrates_signed_acceleration_before_positions_and_publishes_arithmetic_WORD_caches()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Arena = frame.Arena.Write(0x2018, [3]).Write(0x201A, [9]) };
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x68, 0xFFFFF000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x6C, uint.MaxValue);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x70, 0x80000000);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x74, 0xFFFFFFF0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x7C, 1);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x50, 0x7FFFFFF0);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x54, uint.MaxValue);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x58, 1);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x5C, 0x30);
        frame = WaterSchedulerFixture.SlotDword(frame, 0, 0x64, 2);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x80, 0xFFFF);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x82, 1);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x86, 2);
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x88, 0xFFFE);
        int updates = 0;
        NativeBattleWaterCallbackServices services = Services() with
        {
            UpdateSlot = (current, kind) =>
            {
                updates++;
                Assert.Equal((byte)3, kind);
                Assert.Equal(0xFFFFE000u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x68));
                Assert.Equal(0x7FFFDFF0u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x50));
                Assert.Equal(0xFFEu, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x54));
                Assert.Equal(0x80000001u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x58));
                Assert.Equal(0x2020u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x5C));
                Assert.Equal(0xFFFFE000u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x60));
                Assert.Equal(3u, WaterSchedulerFixture.ReadSlotDword(current, 0, 0x64));
                Assert.Equal((ushort)0x7FFF, WaterSchedulerFixture.ReadSlotWord(current, 0, 0x8C));
                Assert.Equal((ushort)0, WaterSchedulerFixture.ReadSlotWord(current, 0, 0x8E));
                Assert.Equal((ushort)0x8000, WaterSchedulerFixture.ReadSlotWord(current, 0, 0x90));
                return current;
            }
        };
        NativeBattleWaterCallback.RunA9B870(frame, Input(), services);
        Assert.Equal(1, updates);
    }

    [Fact]
    public void Draw_uses_slot_selected_private_swap_and_rereads_mutated_order_before_next_dispatch()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Core = frame.Core.Write(0x2797554, [0, 1, 0xFF]), Arena = frame.Arena.Write(0x204C, [1]) };
        frame = WaterSchedulerFixture.SlotWord(frame, 0, 0x48, 0xF234);
        List<byte> drawn = [];
        NativeBattleWaterCallbackServices services = Services() with
        {
            DrawSlot = (current, _) =>
            {
                byte slot = current.ReadTemporaryByte(0x142);
                drawn.Add(slot);
                uint cursor = WaterSchedulerFixture.ReadCoreDword(current, Manager + 0x7C);
                Assert.Equal(slot == 0 ? 0x400000u : 0x300000u, cursor);
                if (slot == 0)
                {
                    Assert.Equal(0x600044u + 4 * 0x234, current.ReadTemporaryDword(0x14C));
                    current = current with { Core = current.Core.Write(0x2797555, [2]) };
                }
                return WaterSchedulerFixture.CoreDword(current, Manager + 0x7C, cursor + 5);
            }
        };
        NativeBattleWaterCallbackResult result = NativeBattleWaterCallback.RunA9B870(frame, Input(1), services);
        Assert.Equal(new byte[] { 0, 2 }, drawn);
        Assert.Equal(0x300005u, result.CommonCursor);
        Assert.Equal(0x400005u, WaterSchedulerFixture.ReadCoreDword(result.Frame, Manager + 0xD8));
    }

    [Theory]
    [InlineData(0, 2u)]
    [InlineData(0x7FFF, 2u)]
    [InlineData(0x8000, 0u)]
    [InlineData(0xFFFF, 0u)]
    public void Completion_uses_current_command_bit_and_not_script_or_task_counts(ushort command, uint expected)
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797628 + 0xA, command);
        frame = frame with { Core = frame.Core.Write(0x2797454, [0xFF]).Write(0x2797554, [0xFF]) };
        NativeBattleWaterCallbackResult result = NativeBattleWaterCallback.RunA9B870(frame, Input(), Services());
        Assert.Equal(expected, result.TaskResult);
    }

    [Fact]
    public void Restored_owners_produce_identical_next_callback_and_unsupported_selected_draw_fails_at_service()
    {
        NativeBattleWaterExecutionFrame frame = Create();
        frame = frame with { Core = frame.Core.Write(0x2797554, [0, 0xFF]) };
        NativeBattleWaterExecutionFrame before = NativeBattleStorageSnapshots.Capture(frame);
        NativeBattleWaterExecutionFrame cold = NativeBattleTestSnapshots.RestoreJson<NativeBattleWaterExecutionFrame>(NativeBattleTestSnapshots.Json(frame));
        NativeBattleWaterCallbackResult first = NativeBattleWaterCallback.RunA9B870(frame, Input(1), Services());
        NativeBattleWaterCallbackResult restored = NativeBattleWaterCallback.RunA9B870(cold, Input(1), Services());
        Assert.Equal(NativeBattleTestSnapshots.Json(first), NativeBattleTestSnapshots.Json(restored));
        NativeBattleWaterCallbackResult changedCommand = NativeBattleWaterCallback.RunA9B870(NativeBattleStorageSnapshots.Restore(before), Input(1), Services() with
        {
            DrawSlot = (current, _) => WaterSchedulerFixture.CoreWord(current, 0x2797628 + 0xA, 0)
        });
        Assert.Equal(2u, changedCommand.TaskResult);
        Assert.Throws<NotSupportedException>(() => NativeBattleWaterCallback.RunA9B870(NativeBattleStorageSnapshots.Restore(before), Input(1), Services() with
        {
            DrawSlot = (_, selector) => throw new NotSupportedException($"Unowned selected draw {selector}")
        }));
    }

    private static NativeBattleWaterExecutionFrame Create()
    {
        NativeBattleWaterExecutionFrame frame = WaterSchedulerFixture.Create();
        frame = frame.WriteTemporaryByte(0x145, 0);
        frame = frame with { Core = frame.Core.Write(Manager + 0xD2, [0xFF]) };
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797628 + 0xA, 0x8000);
        frame = WaterSchedulerFixture.CoreWord(frame, 0x2797068 + 0x38, 8);
        frame = WaterSchedulerFixture.CoreDword(frame, 0x2798A6C, NativeBattleEffectNodeMemory.ArenaAddress + 0x1FA8);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x84, 0x100000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0x8C, 0x1000);
        frame = WaterSchedulerFixture.CoreDword(frame, Manager + 0xD4, 0x400000);
        return frame;
    }

    private static NativeBattleWaterCallbackInput Input(ushort flags = 0, short focal = 216) => new(Resources, flags, Enumerable.Range(0, 32).Select(index => (byte)(0x70 + index)).ToImmutableArray(), 0x600000, 0x300000, focal);
    private static NativeBattleWaterCallbackServices Services() => new(UnexpectedRead, UnexpectedInstruction, (frame, _) => frame, (frame, _) => frame, UnexpectedClear, (frame, _) => frame);
    private static ushort UnexpectedRead(NativeBattleWaterExecutionFrame frame, uint pc) => throw new InvalidOperationException("No instruction fetch expected.");
    private static NativeBattleWaterExecutionFrame UnexpectedInstruction(NativeBattleWaterExecutionFrame frame, ushort instruction) => throw new InvalidOperationException("No instruction dispatch expected.");
    private static NativeBattleWaterExecutionFrame UnexpectedUpdate(NativeBattleWaterExecutionFrame frame, byte kind) => throw new InvalidOperationException("No update dispatch expected.");
    private static NativeBattleWaterExecutionFrame UnexpectedClear(NativeBattleWaterExecutionFrame frame, uint pointer, int count) => throw new InvalidOperationException("No OT clear expected.");
    private static ushort Word(NativeBattleWaterExecutionFrame frame, uint address) => BinaryPrimitives.ReadUInt16LittleEndian(frame.Core.Read(address, 2));

    private static NativeBattleWater222Resources CreateResources()
    {
        byte[] owners = new byte[20];
        uint[] values = [0x2797690, 0x803FF000, 0x2797068, 0x27975D8, 0x2797628];
        for (int index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(owners.AsSpan(index * 4), values[index]);
        }
        NativeBattleWater222Profile profile = new(new("tables", new byte[0x70].ToImmutableArray()), new("initialization", new byte[0x1C].ToImmutableArray()), new("owners", owners.ToImmutableArray()));
        return new([new("file0", new byte[0x1799C].ToImmutableArray()), new("file1", new byte[0x26258].ToImmutableArray())], profile);
    }
}
