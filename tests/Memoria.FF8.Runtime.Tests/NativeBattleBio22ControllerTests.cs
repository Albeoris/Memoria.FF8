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
