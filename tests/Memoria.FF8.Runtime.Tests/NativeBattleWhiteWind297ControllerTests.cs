using System.Collections.Immutable;
using System.Buffers.Binary;
using System.Text.Json;
using static Memoria.FF8.Runtime.NativeBattleWhiteWind297Profile;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleWhiteWind297ControllerTests
{
    [Theory]
    [InlineData(0u, SecondPacketOffset, 1u)]
    [InlineData(1u, FirstPacketOffset, 0u)]
    [InlineData(0xFFFFFFFFu, FirstPacketOffset, 0u)]
    public void First_root_scan_selects_native_private_parity_without_initializing_children(uint parity, int offset, uint next)
    {
        NativeBattleWhiteWind297Frame original = Frame(parity);
        NativeBattleWhiteWind297Frame result = NativeBattleWhiteWind297Controller.Advance(original, Resources(), Inputs());
        Assert.Equal(ArenaAddress + (uint)offset, result.State.ReadDword(CursorGlobal));
        Assert.Equal(next, result.Effect.Pools[0].Slots[0].ReadDword(16));
        Assert.Equal((ushort)1, result.Effect.Pools[0].Slots[0].ReadWord(12));
        Assert.Single(result.Effect.Pools);
        Assert.Equal(RootDescriptor, result.Effect.ReturnedDescriptor);
        Assert.Null(result.Effect.DrawArenaOffset);
    }

    [Fact]
    public void Cold_restored_root_has_identical_next_parity_and_pool_bytes()
    {
        NativeBattleWhiteWind297Frame original = Frame(1);
        NativeBattleWhiteWind297Frame restored = JsonSerializer.Deserialize<NativeBattleWhiteWind297Frame>(JsonSerializer.Serialize(original))!;
        NativeBattleWhiteWind297Frame Next(NativeBattleWhiteWind297Frame frame) => NativeBattleWhiteWind297Controller.Advance(frame, Resources(), Inputs());
        Assert.Equal(JsonSerializer.Serialize(Next(original)), JsonSerializer.Serialize(Next(restored)));
    }

    [Fact]
    public void Canonical_globals_reject_duplicate_descriptor_and_root_task_ownership()
    {
        NativeBattleWhiteWind297State cold = NativeBattleWhiteWind297State.FromColdProcess();
        cold.Validate();
        Assert.Throws<InvalidDataException>(() => cold.WriteDword(RootBank, 0));
        Assert.Throws<InvalidDataException>(() => cold.WriteDword(TargetDescriptor, 0));
        Assert.Throws<InvalidDataException>(() => (cold with { Globals = cold.Globals.Write((int)(RootBank - NativeBattleWhiteWind297State.BaseAddress), [1]) }).Validate());
    }

    [Fact]
    public void Real_ACT_workspaces_finish_same_scan_children_and_apply_each_matching_contact()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 4, 0, 0, 297, null));
        for (int target = 3; target <= 5; target++)
        {
            (packets, _) = packets.AppendContact(new(new((byte)target, 1, 0, 0, 0, 7, 0), new(255, 0, 0, 0, 0, 0, 0)));
        }
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(3);
        List<int> contacts = [];
        List<int> anchors = [];
        List<NativeBattleWhiteWind297Operation> operations = [];
        int emits = 0;
        NativeBattleActionMatrix camera = NativeBattleCuragaMatrices.Identity8DD770();
        NativeBattleWhiteWind297Inputs inputs = new(packets, actor => new(actor, 0)
        {
            MinimumY146 = -7,
            Transform = new(new(0, 0, 0), new(0, 0, (short)(actor * 100)), 0, 100, 8192)
        }, _ => NativeBattlePackedMatrix32.FromMatrix(camera, 0x2222), (frame, actor) =>
        {
            anchors.Add(actor);
            return new(frame, new((short)(actor * 10), -123, (short)(actor * 100), 100));
        }, (frame, operation) =>
        {
            operations.Add(operation);
            if (operation is NativeBattleWhiteWind297Contact contact)
            {
                contacts.Add(contact.Index);
            }
            return frame;
        }, (frame, _, context, _) =>
        {
            emits++;
            return frame with { State = frame.State.WriteDword(0x2507914, (uint)emits) };
        });
        NativeBattleWhiteWind297Frame current = Frame(0) with { State = Frame(0).State.WriteDword(CasterGlobal, 0x1D972C0) };
        NativeBattleWhiteWind297Resources resources = new(Source(22), Source(26), Source(26), new("fixture", []), 0x5578A, new("fixture", []));
        int callbacks = 0;
        while (current.Effect.ReturnedDescriptor != 0 && callbacks++ < 64)
        {
            current = NativeBattleWhiteWind297Controller.Advance(current, resources, inputs);
        }
        Assert.True(callbacks < 64);
        Assert.Equal(0u, current.Effect.ReturnedDescriptor);
        Assert.Equal(new[] { 0, 1, 2 }, contacts);
        Assert.Equal(new[] { 0, 3, 4, 5 }, anchors);
        Assert.Single(operations.OfType<NativeBattleWhiteWind297Sound>());
        Assert.True(emits > 0);
        Assert.Equal((uint)emits, current.State.ReadDword(0x2507914));
        Assert.All(current.Effect.Pools, pool => Assert.All(pool.Slots, task => Assert.Equal(0, task.Flags & 1)));
    }

    private static NativeBattleGfAnimationSource Source(int duration)
    {
        byte[] bytes = new byte[80];
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4), 32);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(32), 1);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(36), duration);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(40), 16);
        return new(0x123400, bytes.ToImmutableArray());
    }

    private static NativeBattleWhiteWind297Frame Frame(uint parity)
    {
        NativeBattleEffectTaskPool root = NativeBattleEffectTaskPool.Initialize(RootDescriptor, RootBank, 20, 1);
        (root, int slot) = root.Append(RootCallback);
        root = root with { Slots = root.Slots.SetItem(slot, root.Slots[slot].WriteDword(12, 0).WriteDword(16, parity)) };
        NativeBattleOriginalEffectState effect = new(297, 297, new(297, Entry, Initializer), new(0, 1, 8, 0, 0, 0), RootDescriptor) { Pools = [root] };
        return new(NativeBattleEffectMemory.FromColdProcess().BeginLoad(297), effect, new(1), NativeBattleWhiteWind297State.FromColdProcess().WriteDword(WorkingGlobal, ArenaAddress));
    }

    private static NativeBattleWhiteWind297Resources Resources() => new(new(0, []), new(0, []), new(0, []), new("fixture", []), 0, new("fixture", []));
    private static NativeBattleWhiteWind297Inputs Inputs() => new(NativeBattleActionPacketsState.Fresh(), _ => throw new InvalidOperationException("Root parity must not read actors."), _ => throw new InvalidOperationException("Root parity must not read camera."), (_, _) => throw new InvalidOperationException("Root parity must not read anchors."), (_, _) => throw new InvalidOperationException("Root parity must not invoke services."), (_, _, _, _) => throw new InvalidOperationException("Root parity must not draw."));
}
