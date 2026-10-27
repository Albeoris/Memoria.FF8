using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleAngelWing84ControllerTests
{
    [Fact]
    public void Arena_clear_waits_for_native_children_and_preserves_both_draw_banks()
    {
        var fixture = Fixture();
        NativeBattleAngelWing84Frame frame = fixture.Frame;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(41, 2).WriteByte(40, 1));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleAngelWing84Controller.Advance(frame, fixture.Inputs);
        Assert.Null(frame.State.Emitters);
        Assert.Equal((byte)2, frame.Effect.Pools[0].Slots[0].ReadByte(41));
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(40, 0));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleAngelWing84Frame clone = NativeBattleTestSnapshots.RestoreJson<NativeBattleAngelWing84Frame>(NativeBattleTestSnapshots.Json(frame))!;
        frame = NativeBattleAngelWing84Controller.Advance(frame, fixture.Inputs);
        Assert.Equal(NativeBattleTestSnapshots.Json(frame), NativeBattleTestSnapshots.Json(NativeBattleAngelWing84Controller.Advance(clone, fixture.Inputs)));
        Assert.Equal(256 + 98304, frame.State.Emitters);
        Assert.Equal(frame.State.Emitters + 432, frame.State.Geometry);
        Assert.Equal(frame.State.Emitters + 27312, frame.State.AuxiliaryCursor);
        Assert.All(frame.Memory.Arena!.Read(frame.State.Emitters!.Value, 27312), value => Assert.Equal((byte)0, value));
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(256, 1)[0]);
        Assert.Equal((byte)0xA5, frame.Memory.Arena.Read(256 + 98304 - 1, 1)[0]);
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
        NativeBattleAngelWing84Inputs inputs = fixture.Inputs with
        {
            AdvanceLeaf = (current, address) =>
            {
                NativeBattleEffectHierarchy children = new(current.Effect.Pools);
                order.Add(checked((int)children.Read(address).Callback - 0x1000));
                children = children.Complete8DC530(address);
                return new(current with { Effect = current.Effect with { Pools = children.Pools } }, 2);
            }
        };
        NativeBattleAngelWing84Frame frame = NativeBattleAngelWing84Controller.Advance(fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } }, inputs);
        Assert.Equal(new[] { 1, 2, 8, 3, 5, 6, 4, 7 }, order);
        Assert.Equal((ushort)0, frame.Effect.Pools[0].Slots[0].ReadWord(94));
        Assert.Equal((byte)0, frame.Effect.Pools[0].Slots[0].ReadByte(40));
        Assert.Equal((byte)6, frame.Effect.Pools[0].Slots[0].ReadByte(41));
        frame = NativeBattleAngelWing84Controller.Advance(frame, inputs);
        Assert.Equal((byte)7, frame.Effect.Pools[0].Slots[0].ReadByte(41));
        Assert.Equal(NativeBattleAngelWing84Controller.Descriptor, frame.Effect.ReturnedDescriptor);
    }

    [Fact]
    public void Target_requires_exact_scene_contact_flag_and_releases_the_original_parent()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(99, 1));
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, 0x78DBA0, 88, root, (_, _, _) => new(0, 3));
        hierarchy = appended.Hierarchy;
        hierarchy = hierarchy.Write(appended.Address, hierarchy.Read(appended.Address).WriteByte(41, 1));
        var poseFixture = NativeBattleRestorationControllerTests.Fixture(24);
        NativeBattleAngelWing84Inputs controller = fixture.Inputs with { ReadModel = poseFixture.Inputs.ReadModel, ReadPoseSource = poseFixture.Inputs.ReadPoseSource };
        ushort contactFlag = 2;
        List<int> contacts = [];
        NativeBattleAngelWing84TargetInputs inputs = new(controller, (_, _) => throw new InvalidOperationException(), _ => contactFlag, _ => throw new InvalidOperationException("No children means no scene completion read."), (current, index) =>
        {
            contacts.Add(index);
            return current;
        });
        NativeBattleAngelWing84Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleAngelWing84Controller.AdvanceTarget(frame, appended.Address, inputs).Frame;
        Assert.Empty(contacts);
        Assert.Equal((byte)1, new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(appended.Address).ReadByte(41));
        contactFlag = 1;
        frame = NativeBattleAngelWing84Controller.AdvanceTarget(frame, appended.Address, inputs).Frame;
        Assert.Equal(new[] { 0 }, contacts);
        frame = NativeBattleAngelWing84Controller.AdvanceTarget(frame, appended.Address, inputs).Frame;
        NativeBattleAngelWing84LeafStep released = NativeBattleAngelWing84Controller.AdvanceTarget(frame, appended.Address, inputs);
        hierarchy = new(released.Frame.Effect.Pools);
        Assert.Equal(2, released.Result);
        Assert.Equal((byte)0, hierarchy.Read(root).ReadByte(99));
        Assert.Equal((byte)0, hierarchy.Read(root).ReadByte(40));
        Assert.Equal(1, hierarchy.Read(appended.Address).ReadByte(38) & 1);
    }

    internal static (NativeBattleAngelWing84Frame Frame, NativeBattleAngelWing84Inputs Inputs) Fixture()
    {
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, 27, 84, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState selected = new(84, 83, new(84, 0x78D7B0, 0x78D790), new(0, 1, 8, 0, 0, 0), null) { ResourcePointers = [new(0x25C2350, 256)] };
        NativeBattleEffectMemory memory = NativeBattleEffectMemory.FromColdProcess().BeginLoad(83);
        memory = memory with { Arena = memory.Arena!.Write(256, Enumerable.Repeat((byte)0xA5, 98304 + 27312).ToArray()) };
        NativeBattleAngelWing84Frame frame = NativeBattleAngelWing84Controller.Initialize(memory, selected, packets);
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        NativeBattleAngelWing84Inputs inputs = new(packets, camera, index => new(index, 0), _ => throw new InvalidOperationException("Inactive model does not read a pose."), (_, _) => throw new NotSupportedException("The selected scene leaf has no test renderer."), (_, _) => throw new NotSupportedException("Texture release has no test completion producer."));
        return (frame, inputs);
    }
}
