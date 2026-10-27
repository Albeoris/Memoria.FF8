using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRinoa86ControllerTests
{
    [Fact]
    public void Phase2_waits_for_children_without_clearing_or_allocating_auxiliary_storage()
    {
        var fixture = Fixture();
        NativeBattleRinoa86Frame frame = fixture.Frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(41, 2).WriteByte(40, 1));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleRinoa86Controller.Advance(frame, fixture.Inputs);
        Assert.Equal((byte)2, frame.Effect.Pools[0].Slots[0].ReadByte(41));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(40, 0));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleRinoa86Frame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleRinoa86Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleRinoa86Controller.Advance(frame, fixture.Inputs);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleRinoa86Controller.Advance(clone, fixture.Inputs)));
        Assert.Equal(256 + 114688, frame.State.AuxiliaryCursor);
        Assert.All(frame.Memory.Arena!.Read(256, 114688), value => Assert.Equal((byte)0xA5, value));
        Assert.Equal((byte)3, frame.Effect.Pools[0].Slots[0].ReadByte(41));
    }

    [Fact]
    public void Root_runs_the_physical_child_order_before_publishing_empty_survivors()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(41, 6).WriteWord(94, 1));
        for (int pool = 1; pool < 9; pool++)
        {
            hierarchy = hierarchy.Append8DC540(pool, checked((uint)(0x1000 + pool)), 36, root, (_, _, _) => new(0, 3)).Hierarchy;
        }
        List<int> order = [];
        NativeBattleRinoa86Inputs inputs = fixture.Inputs with
        {
            AdvanceLeaf = (current, address) =>
            {
                NativeBattleEffectHierarchy children = new(current.Effect.Pools);
                order.Add(checked((int)children.Read(address).Callback - 0x1000));
                children = children.Complete8DC530(address);
                return new(current with { Effect = current.Effect with { Pools = children.Pools } }, 2);
            }
        };
        NativeBattleRinoa86Frame frame = NativeBattleRinoa86Controller.Advance(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, inputs);
        Assert.Equal(new[] { 1, 2, 8, 7, 4, 5, 3, 6 }, order);
        Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(94));
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(40));
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(41));
        frame = NativeBattleRinoa86Controller.Advance(frame, inputs);
        Assert.Equal((byte)7, frame.Effect.Pools[0].Slots[0].ReadByte(41));
        Assert.Equal(NativeBattleRinoa86Controller.Descriptor, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Target_requires_exact_scene_contact_flag_and_releases_the_original_parent()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(99, 1));
        Assert.Equal((byte)3, fixture.Inputs.Packets.Headers[0].ReadByte(16));
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, 0x77F5B0, 88, root, (_, _, _) => new(0, 3));
        hierarchy = appended.Hierarchy;
        hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteByte(41, 1));
        var poseFixture = NativeBattleRestorationControllerTests.Fixture(24);
        NativeBattleRinoa86Inputs controller = fixture.Inputs with { ReadModel = poseFixture.Inputs.ReadModel, ReadPoseSource = poseFixture.Inputs.ReadPoseSource };
        ushort contactFlag = 2;
        List<int> contacts = [];
        NativeBattleRinoa86TargetInputs inputs = new(controller, (_, _) => throw new InvalidOperationException(), _ => contactFlag, _ => throw new InvalidOperationException("No children means no scene completion read."), (current, index) =>
        {
            contacts.Add(index);
            return current;
        });
        NativeBattleRinoa86Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleRinoa86Controller.AdvanceTarget(frame, appended.Address, inputs).Frame;
        Assert.Empty(contacts);
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(appended.Address).ReadByte(41));
        contactFlag = 1;
        frame = NativeBattleRinoa86Controller.AdvanceTarget(frame, appended.Address, inputs).Frame;
        Assert.Equal(new[] { 0, 1, 2 }, contacts);
        frame = NativeBattleRinoa86Controller.AdvanceTarget(frame, appended.Address, inputs).Frame;
        NativeBattleRinoa86LeafStep released = NativeBattleRinoa86Controller.AdvanceTarget(frame, appended.Address, inputs);
        hierarchy = new(released.Frame.Effect.Pools);
        Assert.Equal(2, released.Result);
        Assert.Equal((byte)0, hierarchy.Read(root).ReadByte(99));
        Assert.Equal((byte)0, hierarchy.Read(root).ReadByte(40));
        Assert.Equal(1, hierarchy.Read(appended.Address).ReadByte(38) & 1);
    }

    internal static (NativeBattleRinoa86Frame Frame, NativeBattleRinoa86Inputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 27, 86, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(5, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(3);
        NativeBattleOriginalEffectState selected = new(86, 85, new(86, 0x77F250, 0x77F230), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x25B4CC8, 256)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(85) with { Rinoa86 = NativeBattleRinoa86Storage.FromColdProcess() };
        memory = memory with { Arena = memory.Arena!.Write(256, Enumerable.Repeat((byte)0xA5, 114688).ToArray()) };
        NativeBattleRinoa86Frame frame = NativeBattleRinoa86Controller.Initialize(memory, selected, packets, null!);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleRinoa86Inputs inputs = new(packets, camera, index => new(index, 0), _ => throw new InvalidOperationException("Inactive model does not read a pose."), (_, _) => throw new NotSupportedException("The selected scene leaf has no test renderer."), (_, _) => throw new NotSupportedException("Texture release has no test completion producer."));
        return (frame, inputs);
    }
}
