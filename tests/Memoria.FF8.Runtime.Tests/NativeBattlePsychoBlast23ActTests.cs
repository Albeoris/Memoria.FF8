using System.Buffers.Binary;
using System.Collections.Immutable;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattlePsychoBlast23ActTests
{
    [Fact]
    public void Primary_waits_signed_age35_and_advances_workspace_once_on_initial_draw()
    {
        NativeBattlePsychoBlast23ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattlePsychoBlast23Frame frame = NativeBattlePsychoBlast23Act.Append(fixture.Frame, hierarchy.Address(0, 0), NativeBattlePsychoBlast23Profile.ActPrimaryCallback, 0x17EE254, 0xD8, 0, (_, _, _) => new(0, 3), out uint address);
        hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x24, 34));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools }, State = frame.State with { Basis56 = Identity().MarkUnknown(18, 2) } };
        NativeBattlePsychoBlast23LeafStep step = NativeBattlePsychoBlast23Act.Advance(frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3));
        Assert.Empty(fixture.Operations);
        Assert.Equal((byte)0, new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(address).ReadByte(0x29));
        step = NativeBattlePsychoBlast23Act.Advance(step.Frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(step.Frame.Effect.Pools).Read(address);
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal(1u, task.ReadDword(0x98));
        Assert.Equal(4096u, task.ReadDword(0x58));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x42));
        Assert.Single(fixture.Operations.OfType<NativeBattlePsychoBlast23ActDraw>());
    }

    [Fact]
    public void Delayed_clamps_after_third_1366_step_then_births_followup_and_debris_before_drawing()
    {
        NativeBattlePsychoBlast23ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattlePsychoBlast23Frame frame = NativeBattlePsychoBlast23Act.Append(fixture.Frame, hierarchy.Address(0, 0), NativeBattlePsychoBlast23Profile.ActDelayedCallback, 0x17F01B0, 0x58, 1, (_, _, _) => new(0, 3), out uint address);
        hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address).WriteWord(0x24, 35);
        hierarchy = hierarchy.Write(address, task);
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools }, State = frame.State with { Basis56 = Identity() } };
        frame = NativeBattlePsychoBlast23Act.Advance(frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        for (int index = 0; index < 2; index++)
        {
            frame = NativeBattlePsychoBlast23Act.Advance(frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
            Assert.DoesNotContain(frame.Effect.Pools[4].Slots, slot => slot.Callback == NativeBattlePsychoBlast23Profile.ActFollowupCallback);
        }
        frame = NativeBattlePsychoBlast23Act.Advance(frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        Assert.Equal((ushort)4096, task.ReadWord(0x17C));
        Assert.Equal((byte)2, task.ReadByte(0x29));
        Assert.Single(frame.Effect.Pools[4].Slots, slot => slot.Callback == NativeBattlePsychoBlast23Profile.ActFollowupCallback);
        Assert.Single(frame.Effect.Pools[5].Slots, slot => slot.Callback == NativeBattlePsychoBlast23Profile.DebrisEmitterCallback);
        Assert.Equal(4, fixture.Operations.OfType<NativeBattlePsychoBlast23ActDraw>().Count());
        uint child = new NativeBattleEffectHierarchy(frame.Effect.Pools).Address(4, Array.FindIndex(frame.Effect.Pools[4].Slots.ToArray(), slot => slot.Callback == NativeBattlePsychoBlast23Profile.ActFollowupCallback));
        frame = NativeBattlePsychoBlast23Act.Advance(frame, child, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        Assert.Equal((short)1, frame.State.Word(0x48));
    }

    [Fact]
    public void CPU_endpoint_uses_scaled_rotation_low_WORD_wrap_and_preserves_destination_W()
    {
        NativeBattlePsychoBlast23ControllerTests.Fixture fixture = new();
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        NativeBattleEffectHierarchyAppend append = hierarchy.Append8DC540(4, NativeBattlePsychoBlast23Profile.ActDelayedCallback, 384, hierarchy.Address(0, 0), (_, _, _) => new(0, 3));
        NativeBattleModulePoolTask task = append.Hierarchy.Read(append.Address);
        NativeBattleActionMatrix matrix = NativeBattleCuragaMatrices.Identity8DD770();
        for (int index = 0; index < 9; index++)
        {
            task = task.WriteWord(0x30 + index * 2, unchecked((ushort)matrix.Rotation[index]));
        }
        task = task.MarkPayloadUnknown(0x42, 2).WriteDword(0x50, 4096).WriteDword(0x54, 4096).WriteDword(0x58, 2048).WriteWord(0x17A, 0xABCD);
        byte[] anchor = new byte[6];
        BinaryPrimitives.WriteInt16LittleEndian(anchor.AsSpan(4), short.MinValue);
        task = NativeBattlePsychoBlast23Act.ComputeEnd(task, Identity().Write(0x30, anchor), 0x174);
        Assert.Equal(unchecked((ushort)(short.MinValue - 256)), task.ReadWord(0x178));
        Assert.Equal((ushort)0xABCD, task.ReadWord(0x17A));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x42));
    }

    private static NativeBattleEffectArena Identity()
    {
        byte[] bytes = new byte[56];
        BinaryPrimitives.WriteInt16LittleEndian(bytes, 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(8), 4096);
        BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(16), 4096);
        return NativeBattleEffectArena.Clear().Write(0, bytes);
    }
}
