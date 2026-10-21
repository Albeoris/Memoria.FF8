using System.Buffers.Binary;

namespace Memoria.FF8.Runtime.Tests;

public sealed class NativeBattleEffect60ActTests
{
    [Fact]
    public void Target_shift_waits_age6_then_saves_rotation_and_packed_position_without_resolving_W()
    {
        NativeBattleEffect60ControllerTests.Fixture fixture = new();
        fixture.Positions[3] = fixture.Positions[3].MarkUnknown(6, 2);
        NativeBattleEffect60Frame frame = Append(fixture, out uint address);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteWord(0x24, 5));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleEffect60Act.Advance(frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        Assert.Empty(fixture.Operations);
        frame = NativeBattleEffect60Act.Advance(frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        Assert.Equal((byte)1, task.ReadByte(0x29));
        Assert.Equal((short)900, unchecked((short)task.ReadWord(0x184)));
        Assert.Equal((short)1412, unchecked((short)task.ReadWord(0x17C)));
        Assert.Equal((short)1412, NativeBattleEffect60ControllerTests.Fixture.Word(fixture.Positions[3], 4));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x186));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x17E));
        Assert.Throws<NotSupportedException>(() => frame.State.SavedRotation8.Read(6, 2));
        Assert.Equal((short)22, NativeBattleEffect60ControllerTests.Fixture.Word(frame.State.SavedRotation8, 2));
        Assert.IsType<NativeBattleEffect60SetYaw>(fixture.Operations[0]);
        Assert.IsType<NativeBattleEffect60Rotate>(fixture.Operations[1]);
        Assert.IsType<NativeBattleEffect60SetPackedPosition>(fixture.Operations[2]);
    }

    [Fact]
    public void Primary_init_waits_age36_and_births_distinct36byte_secondary_after_first_draw()
    {
        NativeBattleEffect60ControllerTests.Fixture fixture = new();
        NativeBattleEffect60Frame frame = Append(fixture, out uint address);
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        hierarchy = hierarchy.Write(address, hierarchy.Read(address).WriteByte(0x29, 1).WriteWord(0x24, 35));
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools } };
        frame = NativeBattleEffect60Act.Advance(frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        Assert.Empty(fixture.Operations);
        frame = NativeBattleEffect60Act.Advance(frame, address, fixture.Resources, fixture.Inputs, (_, _, _) => new(0, 3)).Frame;
        NativeBattleModulePoolTask task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        Assert.Equal((byte)2, task.ReadByte(0x29));
        Assert.Equal(1u, task.ReadDword(0x98));
        Assert.Equal((ushort)0, task.ReadWord(0x84));
        Assert.Throws<NotSupportedException>(() => task.ReadWord(0x42));
        NativeBattleModulePoolTask child = Assert.Single(frame.Effect.Pools[4].Slots, slot => slot.Callback == NativeBattleEffect60Profile.ActSecondaryCallback);
        Assert.Equal(0x17A3150u, child.ReadDword(0x74));
        Assert.Equal(36u, child.ReadDword(0x78));
        Assert.Equal((ushort)1, child.ReadWord(0x80));
        NativeBattleEffect60TargetAnchor anchor = Assert.Single(fixture.Operations.OfType<NativeBattleEffect60TargetAnchor>());
        Assert.Equal((byte)0x19, anchor.Selector);
        Assert.Equal((short)1024, anchor.Distance);
    }

    [Theory]
    [InlineData(3, 3706)]
    [InlineData(7, 4096)]
    public void Ratio_motion_restores_position_at_endpoint_before_draw_and_rotation_only_after_completion(ushort index, short ratio)
    {
        NativeBattleEffect60ControllerTests.Fixture fixture = new();
        NativeBattleEffect60Frame frame = Append(fixture, out uint address);
        NativeBattleEffectArena original = NativeBattleEffect60ControllerTests.Fixture.Packed(300, 10, 900, 0xABCD).MarkUnknown(6, 2);
        NativeBattleEffectArena shifted = NativeBattleEffect60ControllerTests.Fixture.Packed(100, -10, 1400, 0x4321);
        fixture.Positions[3] = shifted;
        NativeBattleEffectHierarchy hierarchy = new(frame.Effect.Pools);
        NativeBattleModulePoolTask task = hierarchy.Read(address).WriteByte(0x29, 2).WriteWord(0x84, index).WriteDword(0x98, 31).WriteWord(0x1C, 50).WriteWord(0x1E, 60).WriteWord(0x20, 70);
        task = Copy(original, task, 0x180);
        task = Copy(shifted, task, 0x178);
        hierarchy = hierarchy.Write(address, task);
        NativeBattleEffectArena saved = fixture.Rotations[3];
        frame = frame with { Effect = frame.Effect with { Pools = hierarchy.Pools }, State = frame.State with { SavedRotation8 = saved } };
        NativeBattleEffect60Inputs inputs = fixture.Inputs with { Execute = (current, operation) =>
        {
            if (operation is NativeBattleEffect60ActDraw)
            {
                Assert.Empty(fixture.Operations.OfType<NativeBattleEffect60RestoreRotation>());
                int expectedZ = index == 7 ? 900 : 1400 + unchecked((900 - 1400) * ratio) / 4096;
                Assert.Equal((short)expectedZ, NativeBattleEffect60ControllerTests.Fixture.Word(fixture.Positions[3], 4));
                if (index == 7)
                {
                    Assert.Throws<NotSupportedException>(() => fixture.Positions[3].Read(6, 2));
                }
            }
            return fixture.Inputs.Execute(current, operation);
        } };
        frame = NativeBattleEffect60Act.MoveTarget(frame, address, fixture.Resources, inputs);
        task = new NativeBattleEffectHierarchy(frame.Effect.Pools).Read(address);
        Assert.Equal(unchecked((ushort)ratio), task.ReadWord(0x188));
        Assert.Equal(index == 7 ? index : (ushort)(index + 1), task.ReadWord(0x84));
        Assert.Equal((byte)3, task.ReadByte(0x29));
        Assert.Equal(1, task.ReadByte(0x26) & 1);
        Assert.Single(fixture.Operations.OfType<NativeBattleEffect60RestoreRotation>());
        Assert.Same(saved, fixture.Rotations[3]);
        Assert.IsType<NativeBattleEffect60SetPackedPosition>(fixture.Operations[0]);
        Assert.IsType<NativeBattleEffect60ActDraw>(fixture.Operations[1]);
        Assert.IsType<NativeBattleEffect60RestoreRotation>(fixture.Operations[2]);
    }

    private static NativeBattleEffect60Frame Append(NativeBattleEffect60ControllerTests.Fixture fixture, out uint address)
    {
        NativeBattleEffectHierarchy hierarchy = new(fixture.Frame.Effect.Pools);
        return NativeBattleEffect60Act.Append(fixture.Frame, hierarchy.Address(0, 0), NativeBattleEffect60Profile.ActPrimaryCallback, 0x17A0D04, 228, 0, (_, _, _) => new(0, 3), out address);
    }
    private static NativeBattleModulePoolTask Copy(NativeBattleEffectArena source, NativeBattleModulePoolTask task, int offset)
    {
        (byte[] bytes, bool[] known) = source.ReadSnapshot(0, 8);
        for (int index = 0; index < 8; index++)
        {
            task = known[index] ? task.WriteByte(offset + index, bytes[index]) : task.MarkPayloadUnknown(offset + index, 1);
        }
        return task;
    }
}
