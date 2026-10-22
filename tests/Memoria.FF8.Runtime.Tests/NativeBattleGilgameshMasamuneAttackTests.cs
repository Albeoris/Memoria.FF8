using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleGilgameshMasamuneAttackTests
{
    private const uint Parent = 0x21FFD68;

    [Fact]
    public void Initial_flares_observe_anchor_then_projection_before_scalar_stores()
    {
        NativeBattleGilgameshFrame original = Fixture(0);
        List<string> operations = [];
        NativeBattleGilgameshMasamuneAttackStep step = NativeBattleGilgameshMasamuneAttack.Advance(original, Parent, 0, [5, 3], Execute);
        Assert.Equal(new[] { "anchor5", "project", "anchor3", "project" }, operations);
        Assert.Equal(original.Random, step.Frame.Random);
        Assert.Equal((ushort)1, Child(step.Frame, 0).ReadWord(12));
        for (int slot = 1; slot <= 2; slot++)
        {
            NativeBattleModulePoolTask child = Child(step.Frame, slot);
            Assert.Equal(0x590790u, child.Callback);
            Assert.Equal((ushort)0xA00, child.ReadWord(0x18));
            Assert.Equal((ushort)0x1C0, child.ReadWord(0x1C));
            Assert.Equal((ushort)0x1C0, child.ReadWord(0x1E));
            Assert.Equal((ushort)0x300, child.ReadWord(0x20));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(0x0E));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(0x1A));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(0x22));
            Assert.Equal(unchecked((ushort)-360), child.ReadWord(0x10));
            Assert.Equal(unchecked((ushort)-308), child.ReadWord(0x12));
            Assert.Equal(unchecked((ushort)-300), child.ReadWord(0x14));
            Assert.Throws<NotSupportedException>(() => child.ReadWord(0x16));
        }
        Assert.Equal(1, Child(step.Frame, 0).Next);
        Assert.Equal(2, Child(step.Frame, 1).Next);

        NativeBattleGilgameshFrame Execute(NativeBattleGilgameshFrame frame, NativeBattleGilgameshOperation operation)
        {
            uint address = operation switch
            {
                NativeBattleGilgameshMasamuneAnchor anchor => anchor.Address,
                NativeBattleGilgameshMasamuneProjectFlare flare => flare.Address,
                _ => throw new InvalidOperationException()
            };
            NativeBattleModulePoolTask child = Read(frame, address);
            Assert.Equal((ushort)0, child.ReadWord(12));
            Assert.Equal((ushort)0xA5A5, child.ReadWord(0x18));
            if (operation is NativeBattleGilgameshMasamuneAnchor anchorOperation)
            {
                operations.Add($"anchor{anchorOperation.Actor}");
                return Anchor(frame, anchorOperation);
            }
            operations.Add("project");
            Assert.Equal(unchecked((ushort)-200), child.ReadWord(0x12));
            return Write(frame, address, child.WriteWord(0x10, unchecked((ushort)-360)).WriteWord(0x12, unchecked((ushort)-308)));
        }
    }

    [Fact]
    public void Contact_births_copy_packed_anchor_knownness_draw_once_each_and_preserve_raw_links()
    {
        NativeBattleGilgameshFrame original = Fixture(16);
        List<byte> anchors = [];
        NativeBattleGilgameshMasamuneAttackStep step = NativeBattleGilgameshMasamuneAttack.Advance(original, Parent, 0, [5, 3], (frame, operation) =>
        {
            NativeBattleGilgameshMasamuneAnchor anchor = Assert.IsType<NativeBattleGilgameshMasamuneAnchor>(operation);
            anchors.Add(anchor.Actor);
            return Anchor(frame, anchor);
        });
        Assert.Equal(new byte[] { 5, 3 }, anchors);
        NativeCrtRandomState random = original.Random;
        for (int actor = 0; actor < 2; actor++)
        {
            random = random.Draw(out ushort draw);
            ushort size = (ushort)((draw % 2560 + 2560) / 3);
            int first = 1 + actor * 4;
            Assert.Equal(new uint[] { 0x5908D0, 0x590A10, 0x590B60, 0x590F10 }, step.Frame.Effect.Pools[1].Slots.Skip(first).Take(4).Select(task => task.Callback));
            for (int index = 0; index < 4; index++)
            {
                NativeBattleModulePoolTask child = Child(step.Frame, first + index);
                Assert.Equal((ushort)0, child.ReadWord(12));
                Assert.Equal((ushort)((actor == 0 ? 5 : 3) * 100), child.ReadWord(0x10));
                Assert.Equal(unchecked((ushort)-200), child.ReadWord(0x12));
                Assert.Equal(unchecked((ushort)-300), child.ReadWord(0x14));
                Assert.Equal((ushort)(index == 3 ? 3 : 0xA5A5), child.ReadWord(0x0E));
                if (index < 2)
                {
                    Assert.Throws<NotSupportedException>(() => child.ReadWord(0x16));
                    Assert.Equal((ushort)0x200, child.ReadWord(0x18));
                    Assert.Equal((ushort)(size + index * 1536), child.ReadWord(0x1C));
                    Assert.Equal(child.ReadWord(0x1C), child.ReadWord(0x1E));
                }
                else
                {
                    Assert.Equal((ushort)(1 << actor), child.ReadWord(0x16));
                }
            }
        }
        Assert.Equal(random, step.Frame.Random);
        for (int index = 0; index < 8; index++)
        {
            Assert.Equal(index + 1, Child(step.Frame, index).Next);
        }
        Assert.Null(Child(step.Frame, 8).Next);
        Assert.False(step.Contact);
    }

    [Theory]
    [InlineData(0, 1, 0, false)]
    [InlineData(16, 17, 0, false)]
    [InlineData(17, 18, 0, true)]
    [InlineData(49, 50, 2, false)]
    [InlineData(32767, 32768, 0, false)]
    [InlineData(65535, 0, 0, false)]
    public void Counter_contact_and_terminal_tests_use_native_signed_widths(int counter, int next, int result, bool contact)
    {
        NativeBattleGilgameshFrame original = Fixture((ushort)counter);
        NativeBattleGilgameshMasamuneAttackStep step = NativeBattleGilgameshMasamuneAttack.Advance(original, Parent, 0, [], (_, _) => throw new InvalidOperationException());
        Assert.Equal((ushort)next, Child(step.Frame, 0).ReadWord(12));
        Assert.Equal(contact, step.Contact);
        Assert.Equal(result, step.Result);
        Assert.Equal(original.Random, step.Frame.Random);
        Assert.Null(Child(step.Frame, 0).Next);
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(0x200u)]
    [InlineData(0x201u)]
    public void Pause_does_not_read_unknown_counter_or_missing_contact_owner(uint flags)
    {
        NativeBattleGilgameshFrame original = Fixture(0);
        original = Write(original, Parent, Child(original, 0).MarkPayloadUnknown(12, 2));
        NativeBattleGilgameshMasamuneAttackStep step = NativeBattleGilgameshMasamuneAttack.Advance(original, Parent, flags, default, (_, _) => throw new InvalidOperationException());
        Assert.Same(original, step.Frame);
        Assert.False(step.Contact);
        Assert.Equal(0, step.Result);
    }

    [Fact]
    public void Callback_frame_mutations_and_appended_links_survive_producer_publication()
    {
        NativeBattleGilgameshFrame original = Fixture(0);
        NativeBattleGilgameshMasamuneAttackStep step = NativeBattleGilgameshMasamuneAttack.Advance(original, Parent, 0, [3], (frame, operation) =>
        {
            if (operation is NativeBattleGilgameshMasamuneAnchor anchor)
            {
                frame = Anchor(frame, anchor);
                NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
                (pool, _) = pool.Append(0x123456);
                return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool) }, Random = new(123) };
            }
            return frame;
        });
        Assert.Equal(new NativeCrtRandomState(123), step.Frame.Random);
        Assert.Equal(1, Child(step.Frame, 0).Next);
        Assert.Equal(2, Child(step.Frame, 1).Next);
        Assert.Equal(0x123456u, Child(step.Frame, 2).Callback);
        Assert.Equal((ushort)1, Child(step.Frame, 0).ReadWord(12));
    }

    [Fact]
    public void Restored_raw_banks_produce_the_same_next_contact_boundary()
    {
        NativeBattleGilgameshFrame original = Fixture(17);
        NativeBattleGilgameshFrame restored = JsonSerializer.Deserialize<NativeBattleGilgameshFrame>(JsonSerializer.Serialize(original))!;
        NativeBattleGilgameshMasamuneAttackStep first = NativeBattleGilgameshMasamuneAttack.Advance(original, Parent, 0, [], (_, _) => throw new InvalidOperationException());
        NativeBattleGilgameshMasamuneAttackStep second = NativeBattleGilgameshMasamuneAttack.Advance(restored, Parent, 0, [], (_, _) => throw new InvalidOperationException());
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
    }

    private static NativeBattleGilgameshFrame Fixture(ushort counter)
    {
        NativeBattleEffectTaskPool children = NativeBattleEffectTaskPool.Initialize(0x21FFD58, Parent, 36, 100);
        children = children with { Slots = children.Slots.Select(task => task with { Payload = Enumerable.Repeat((byte)0xA5, 24).ToImmutableArray() }).ToImmutableArray() };
        int slot;
        (children, slot) = children.Append(0x590510);
        children = children with { Slots = children.Slots.SetItem(slot, children.Slots[slot].WriteWord(12, counter)) };
        NativeBattleOriginalEffectState effect = new(328, 327, new(328, 0x58D930, 0x58D700), null, null)
        {
            Pools = [NativeBattleEffectTaskPool.Initialize(0x2200D00, 0x2200D10, 100, 2), children, NativeBattleEffectTaskPool.Initialize(0x2201000, 0x2201010, 24, 2)]
        };
        return new(NativeBattleEffectMemory.FromColdProcess(), effect, new(0x12345678));
    }

    private static NativeBattleModulePoolTask Child(NativeBattleGilgameshFrame frame, int index) => frame.Effect.Pools[1].Slots[index];
    private static NativeBattleModulePoolTask Read(NativeBattleGilgameshFrame frame, uint address) => Child(frame, checked((int)((address - Parent) / 36)));

    private static NativeBattleGilgameshFrame Write(NativeBattleGilgameshFrame frame, uint address, NativeBattleModulePoolTask child)
    {
        NativeBattleEffectTaskPool pool = frame.Effect.Pools[1];
        int index = checked((int)((address - Parent) / 36));
        return frame with { Effect = frame.Effect with { Pools = frame.Effect.Pools.SetItem(1, pool with { Slots = pool.Slots.SetItem(index, child) }) } };
    }

    private static NativeBattleGilgameshFrame Anchor(NativeBattleGilgameshFrame frame, NativeBattleGilgameshMasamuneAnchor anchor)
    {
        NativeBattleModulePoolTask child = Read(frame, anchor.Address).WriteWord(0x10, (ushort)(anchor.Actor * 100)).WriteWord(0x12, unchecked((ushort)-200)).WriteWord(0x14, unchecked((ushort)-300)).MarkPayloadUnknown(0x16, 2);
        return Write(frame, anchor.Address, child);
    }
}
