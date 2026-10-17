using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleRestorationContextTests
{
    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    [InlineData((ushort)57)]
    [InlineData((ushort)67)]
    public void Initial_context_uses_actual_model_WORD36_bounds_WORD60_and_Q16_anchors_without_consuming_random_state(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleRestorationFrame initialized = NativeBattleRestorationContext.Initialize(fixture.Frame, fixture.Address, Resources(effect), fixture.Inputs);
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(initialized.Effect.Pools).Read(fixture.Address);
        Assert.Equal((ushort)2, task.ReadWord(0x34 + 0x1C));
        Assert.Equal((ushort)2, task.ReadWord(0x34 + 0x1E));
        Assert.Equal((ushort)45, task.ReadWord(0x34 + 0x1A));
        Assert.Equal(3u, task.ReadDword(0x34 + 4));
        Assert.Equal(4u, task.ReadDword(0x34 + 8));
        AssertVector(task, 0x1C4, 0, -3, 0);
        AssertVector(task, 0x1E4, 0, 100, 0);
        AssertVector(task, 0x204, 0, 800, 0);
        AssertVector(task, 0x54, -10, 10, 30);
        AssertVector(task, 0xD4, -10, -200, 30);
        AssertVector(task, 0x154, -10, 900, 30);
        AssertVector(task, 0x194, 5, 0, -10);
        Assert.Equal(301u, task.ReadDword(0x34));
        Assert.Equal(NativeBattleRestorationProfile.ForEffect(effect).ExecutableAddress + 4, task.ReadDword(0x34 + 0x224));
        Assert.Equal(1u, Bank(initialized.Memory, effect)!.InitializationFlag);
        Assert.Equal(0u, Bank(fixture.Frame.Memory, effect)!.InitializationFlag);
        NativeBattleRestorationFrame independent = JsonSerializer.Deserialize<NativeBattleRestorationFrame>(JsonSerializer.Serialize(fixture.Frame))!;
        Assert.Equal(JsonSerializer.Serialize(initialized), JsonSerializer.Serialize(NativeBattleRestorationContext.Initialize(independent, fixture.Address, Resources(effect), fixture.Inputs)));
    }

    [Theory]
    [InlineData((ushort)24, 1)]
    [InlineData((ushort)24, 3)]
    [InlineData((ushort)24, 4)]
    [InlineData((ushort)27, 1)]
    [InlineData((ushort)57, 1)]
    [InlineData((ushort)67, 1)]
    [InlineData((ushort)27, 3)]
    [InlineData((ushort)57, 3)]
    [InlineData((ushort)67, 3)]
    [InlineData((ushort)27, 4)]
    [InlineData((ushort)57, 4)]
    [InlineData((ushort)67, 4)]
    public void Original_context_mode_selectors_copy_their_actual_four_DWORD_vectors(ushort effect, ushort mode)
    {
        var fixture = Fixture(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(fixture.Address).WriteWord(0x29C, mode).WriteWord(0x288, 100).WriteWord(0x28A, unchecked((ushort)-200)).WriteWord(0x28C, 300).WriteWord(0x290, 7).WriteWord(0x292, 8).WriteWord(0x294, 9);
        hierarchy = hierarchy.Write(fixture.Address, task);
        NativeBattleRestorationFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleRestorationContext.Initialize(frame, fixture.Address, Resources(effect), fixture.Inputs);
        task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(fixture.Address);
        if (mode == 4)
        {
            foreach (int offset in new[] { 0x1C4, 0x1D4, 0x1E4, 0x1F4, 0x204 })
            {
                AssertVector(task, offset, 7, 8, 9);
            }
            AssertVector(task, 0x54, -10, 10, 30);
        }
        else
        {
            foreach (int offset in new[] { 0x54, 0x94, 0xD4, 0x114, 0x154 })
            {
                AssertVector(task, offset, mode == 1 ? 5 : 100, mode == 1 ? 0 : -200, mode == 1 ? -10 : 300);
                AssertVector(task, offset + 16, mode == 1 ? 5 : 100, mode == 1 ? 0 : -200, mode == 1 ? -10 : 300);
            }
        }
    }

    [Theory]
    [InlineData((ushort)24)]
    [InlineData((ushort)27)]
    [InlineData((ushort)57)]
    [InlineData((ushort)67)]
    public void Stack_VECTOR_mode_is_unknown_and_a_missing_retained_executable_owner_is_not_seeded(ushort effect)
    {
        var fixture = Fixture(effect);
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        hierarchy = hierarchy.Write(fixture.Address, hierarchy.Read(fixture.Address).WriteWord(0x29C, 2));
        NativeBattleRestorationFrame frame = fixture.Frame with { Effect = fixture.Frame.Effect with { Pools = hierarchy.Pools } };
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationContext.Initialize(frame, fixture.Address, Resources(effect), fixture.Inputs));
        Assert.Throws<NotSupportedException>(() => NativeBattleRestorationContext.Initialize(fixture.Frame with { Memory = fixture.Frame.Memory with { EsunaExecutable = null, FullLifeExecutable = null, Effect57Executable = null, RemedyExecutable = null } }, fixture.Address, Resources(effect), fixture.Inputs));
    }

    private static void AssertVector(NativeBattleModulePoolTask task, int offset, int x, int y, int z)
    {
        Assert.Equal(unchecked((uint)(x << 16)), task.ReadDword(0x34 + offset));
        Assert.Equal(unchecked((uint)(y << 16)), task.ReadDword(0x34 + offset + 4));
        Assert.Equal(unchecked((uint)(z << 16)), task.ReadDword(0x34 + offset + 8));
    }

    private static NativeBattleRestorationExecutableState? Bank(NativeBattleEffectMemory memory, ushort effect) => memory.GetRestorationExecutable(effect);
    private static NativeBattleRestorationResources Resources(ushort effect) => new(effect, new("skipped-registration", []), effect == 27 ? 0x49445u : 0x49442u) { Executable = new(effect, new byte[NativeBattleRestorationExecutableState.Length(effect)].ToImmutableArray()) };

    private static (NativeBattleRestorationFrame Frame, uint Address, NativeBattleRestorationControllerInputs Inputs) Fixture(ushort effect)
    {
        NativeBattleRestorationProfile profile = NativeBattleRestorationProfile.ForEffect(effect);
        NativeBattleActionPacketsState packets = NativeBattleActionPacketsState.Fresh();
        (packets, _) = packets.Prepare(new(2, 2, 11, 0, effect == 27 ? (ushort)25 : (ushort)27, effect, null));
        (packets, _) = packets.AppendContact(new(new(3, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        (packets, _) = packets.AppendContact(new(new(4, 1, 0, 0, 0, 100, 0), new(255, 0, 0, 0, 0, 0, 0)));
        packets = packets.WritePlayerRepeatMode(0, 1);
        (packets, _) = packets.Publish(2);
        NativeBattleOriginalEffectState selected = new(effect, effect - 1, new(effect, profile.Entry, profile.Initializer), new(2, 1, 24, 0, 0, 0), null) { ResourcePointers = [new(profile.InitializerGlobal, 0)] };
        NativeBattleRestorationFrame frame = NativeBattleRestorationController.Initialize(NativeBattleEffectMemory.FromColdProcess().WithRestorationExecutable(NativeBattleRestorationExecutableState.FromColdProcess(effect)).BeginLoad(effect - 1), selected, packets, Resources(effect));
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend first = hierarchy.Append8DC540(1, profile.TargetCallback, 88, hierarchy.Address(0, 0), (_, _, _) => new(2, 3));
        NativeBattleEffectHierarchyAppend child = first.Hierarchy.Append8DC540(2, profile.GenericCallback, 676, first.Address, (_, _, _) => new(2, 3));
        hierarchy = child.Hierarchy;
        hierarchy = hierarchy.Write(child.Address, hierarchy.Read(child.Address).WriteDword(0x30, NativeBattleRestorationProfile.ForEffect(effect).ExecutableAddress).WriteWord(0x29E, 45));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        byte[] skeleton = new byte[64];
        skeleton[0] = 1;
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(2), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(skeleton.AsSpan(16), -1);
        NativeBattlePoseSource source = new(skeleton.ToImmutableArray(), [1, 0, 0, 0, 8, 0, 0, 0, 1, 0, 0, 0]);
        NativeBattleGraphicsModel Model(int index)
        {
            NativeBattleVector position = index == 2 ? new(0, 100, 0) : index == 3 ? new(-10, -200, 30) : new(20, -400, -50);
            short angle36 = index == 2 ? (short)-3 : index == 3 ? (short)10 : (short)-20;
            return new(index, 0) { Transform = new(new(0, 0, 0), position, 0, angle36, 4096), GeometryBounds52 = new(new(-100, -200, -300), new((short)(600 + index * 100), 200, 300)) };
        }
        NativeBattleActionMatrix camera = new([4096, 0, 0, 0, 4096, 0, 0, 0, 4096], 0, 0, 30000, 4096);
        return (frame, child.Address, new(packets, camera, Model, _ => source));
    }
}
