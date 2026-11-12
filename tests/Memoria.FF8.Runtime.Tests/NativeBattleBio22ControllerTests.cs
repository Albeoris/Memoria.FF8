using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleBio22ControllerTests
{
    [Fact]
    public void Root_tests_prior_child_survivors_and_publishes_zero_descriptor_only_after_actual_retirement()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 3).WriteWord(0x5E, 1));
        NativeBattleBio22Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleBio22ChildStep wait = NativeBattleBio22Controller.Advance89A7B0(frame, fixture.Resources, fixture.Inputs, new(1));
        Assert.Equal(0x2705250u, wait.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(0xC000, wait.Frame.State.DrawCursor);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(wait.Frame.Effect.Pools).Read(root);
        Assert.Equal((byte)3, task.ReadByte(0x29));
        Assert.Equal((ushort)0, task.ReadWord(0x5E));
        NativeBattleBio22Frame restored = NativeBattleTestSnapshots.RestoreJson<NativeBattleBio22Frame>(NativeBattleTestSnapshots.Json(wait.Frame))!;
        NativeBattleBio22ChildStep done = NativeBattleBio22Controller.Advance89A7B0(restored, fixture.Resources, fixture.Inputs, wait.Random);
        Assert.Equal(0u, done.Frame.Effect.ReturnedDescriptor);
        Assert.Equal(0, done.Frame.State.DrawCursor);
        Assert.All(done.Frame.Effect.Pools, pool => Assert.Null(pool.Head));
        Assert.Equal(new NativeCrtRandomState(1), done.Random);
    }

    [Fact]
    public void Entry_keeps_distinct_native_pool_layout_and_does_not_reset_process_geometry_or_palette()
    {
        var fixture = Fixture();
        Assert.Equal(new uint[] { 0x2705250, 0x2714960, 0x2714BA8, 0x270E540, 0x2705150, 0x27044E0 }, fixture.Frame.Effect.Pools.Select(pool => pool.DescriptorAddress));
        Assert.Equal(new ushort[] { 100, 140, 56, 112, 152, 60 }, fixture.Frame.Effect.Pools.Select(pool => pool.Stride));
        Assert.Equal(new[] { 2, 4, 10, 300, 20, 2 }, fixture.Frame.Effect.Pools.Select(pool => pool.Slots.Length));
        Assert.Same(fixture.Memory.Bio22Geometry, fixture.Frame.Memory.Bio22Geometry);
        Assert.Same(fixture.Memory.Bio22Palette, fixture.Frame.Memory.Bio22Palette);
        Assert.Null(fixture.Frame.State.DrawCursor);
    }

    [Fact]
    public void Foreign_target_uses_host_anchors_and_bounds_without_reading_a_native_pose()
    {
        var fixture = Fixture();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        uint root = hierarchy.Address(0, 0);
        hierarchy = hierarchy.Write(root, hierarchy.Read(root).WriteByte(0x29, 2).WriteByte(0x63, 1));
        NativeBattleEffectHierarchyAppend appended = hierarchy.Append8DC540(1, 0x89A8D0, 140, root, (_, _, _) => new(0, 3));
        hierarchy = appended.Hierarchy.Write(appended.Address, appended.Hierarchy.Read(appended.Address).WriteByte(0x29, 10).WriteWord(0x24, 1));
        NativeBattleEffectActorGeometry target = new(new(10, -600, 30), new(20, -200, 40), 50, new(new(-100, -600, -80), new(140, 0, 90)));
        int reads = 0;
        NativeBattleBio22Inputs inputs = fixture.Inputs with
        {
            ReadForeignTarget = index =>
            {
                Assert.Equal(3, index);
                reads++;
                return target;
            }
        };
        NativeBattleBio22Frame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        NativeBattleBio22ChildStep step = NativeBattleBio22Controller.Advance89A7B0(frame, fixture.Resources, inputs, new(1));
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(appended.Address);
        Assert.Equal(1, reads);
        Assert.Equal((short)10, unchecked((short)task.ReadWord(0x40)));
        Assert.Equal((short)-600, unchecked((short)task.ReadWord(0x42)));
        Assert.Equal((short)50, unchecked((short)task.ReadWord(0x32)));
        Assert.Equal((short)-100, unchecked((short)task.ReadWord(0x48)));
        Assert.Equal((short)140, unchecked((short)task.ReadWord(0x50)));
    }

    private static (NativeBattleBio22Frame Frame, NativeBattleBio22Resources Resources, NativeBattleBio22Inputs Inputs, NativeBattleEffectMemory Memory) Fixture()
    {
        var data = NativeBattleBio22GeometryTaskTests.Fixture();
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(0, 2, 0, -1, 12, 22, null));
        (packets, _) = packets.AppendContact(new(new(0, 1, 0, 0, 0, 0, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(1);
        NativeBattleOriginalEffectState effect = new(22, 21, new(22, 0x89A6B0, 0x89A690), new(0, 1, 1, 0, 0, 0), null);
        NativeBattleEffectMemory memory = data.Memory.BeginLoad(21);
        NativeBattleBio22Frame frame = NativeBattleBio22Controller.Initialize89A6B0(memory, effect, packets, data.Resources);
        NativeBattleBio22Inputs inputs = new(packets, new(NativeBattleCuragaMatrices.Identity8DD770(), 256, 160, 108), 0, 341, index => new(index, 0), _ => throw new InvalidOperationException("This root-only case consumes no pose."), _ => throw new InvalidOperationException("This root-only case consumes no stage."), (_, _) => throw new InvalidOperationException("This root-only case consumes no proxy."), (_, _) => throw new InvalidOperationException("This root-only case emits no operation."), (_, _, _) => throw new InvalidOperationException("This root-only case reaches no actor renderer."));
        return (frame, data.Resources, inputs, memory);
    }
}
